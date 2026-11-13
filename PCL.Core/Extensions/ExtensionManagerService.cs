using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.Logging;
using PCL.Core.Plugins;
using PCL.Plugin.SDK;

namespace PCL.Core.Extensions;

/// <summary>An index entry together with the source it came from and what is installed locally.</summary>
public sealed record ExtensionStoreItem(ExtensionIndexEntry Entry, ExtensionSource Source,
    InstalledPlugin? Installed, PluginMarketUpdateKind Kind);

/// <summary>Result of reading every software source. Failing sources do not hide the others.</summary>
public sealed record ExtensionStoreSnapshot(IReadOnlyList<ExtensionStoreItem> Items,
    IReadOnlyDictionary<string, string> SourceErrors);

/// <summary>
///     Starts installed extensions and exposes the store operations (sources, index, install).
///     Extensions live in <c>Data/Extensions</c> and share the plugin package pipeline through a
///     second <see cref="PluginRuntimeManager" /> with the extension profile.
/// </summary>
[LifecycleService(LifecycleState.Loaded)]
[LifecycleScope("extensions", "扩展系统")]
public sealed partial class ExtensionManagerService
{
    /// <summary>Install source type recorded for packages from a software source.</summary>
    public const string SourceInstallType = "source";

    private static readonly HttpClient IndexHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static PluginRuntimeManager? Manager { get; private set; }
    public static ExtensionSourceStore? Sources { get; private set; }

    /// <summary>Official signer thumbprints compiled into this build.</summary>
    public static IReadOnlyList<string> OfficialSigners { get; } =
        ExtensionTrustPolicy.ParseThumbprints(Secrets.ExtensionOfficialSigners);

    public static string ExtensionsDirectory => Path.Combine(Paths.Data, "Extensions");

    [LifecycleStart]
    private static async Task _StartAsync()
    {
        Sources = new ExtensionSourceStore(Path.Combine(ExtensionsDirectory, ".sources", "sources.json"),
            ProjectLinks.ExtensionCatalog);
        Manager = new PluginRuntimeManager(ExtensionsDirectory, profile: ExtensionRuntimeProfile.Create());
        await Manager.PrepareEnabledAsync().ConfigureAwait(false);
        LogWrapper.Info("Extension", $"扩展已预载，当前扩展数：{Manager.GetInstalled().Count}");
        Lifecycle.When(LifecycleState.WindowCreated, () => _ = _StartAtWindowCreatedAsync());
    }

    private static async Task _StartAtWindowCreatedAsync()
    {
        if (Manager is null) return;
        try
        {
            await Manager.StartPreparedAsync().ConfigureAwait(false);
            LogWrapper.Info("Extension", "扩展生命周期启动完成");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Extension", "启动扩展时发生错误");
        }
    }

    [LifecycleStop]
    private static async Task _StopAsync()
    {
        if (Manager is null) return;
        await Manager.DisposeAsync().ConfigureAwait(false);
        Manager = null;
    }

    /// <summary>Reads every source and pairs each entry with the local install state.</summary>
    public static async Task<ExtensionStoreSnapshot> LoadStoreAsync(CancellationToken cancellationToken = default)
    {
        var sources = Sources ?? throw new InvalidOperationException("The extension system is not running.");
        var manager = Manager ?? throw new InvalidOperationException("The extension system is not running.");
        var installed = manager.GetInstalled().ToDictionary(item => item.Manifest.Id, StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ExtensionStoreItem>();

        var results = await Task.WhenAll(sources.GetAll().Select(async source =>
        {
            try
            {
                var index = await ExtensionCatalog.FetchAsync(IndexHttpClient, new Uri(source.Url), source.IsOfficial,
                    cancellationToken).ConfigureAwait(false);
                return (source, index, error: (string?)null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogWrapper.Warn(ex, "Extension", $"软件源读取失败：{source.Url}");
                return (source, index: (ExtensionIndex?)null, error: ex.Message);
            }
        })).ConfigureAwait(false);

        foreach (var (source, index, error) in results)
        {
            if (index is null)
            {
                errors[source.Url] = error ?? "Unknown error";
                continue;
            }
            if (!source.IsOfficial && !string.IsNullOrWhiteSpace(index.Name)) sources.SetName(source.Url, index.Name);
            foreach (var entry in index.Extensions)
            {
                installed.TryGetValue(entry.Id, out var current);
                items.Add(new ExtensionStoreItem(entry, source, current, _Compare(entry, current)));
            }
        }

        return new ExtensionStoreSnapshot(
            items.OrderByDescending(item => item.Source.IsOfficial)
                .ThenBy(item => item.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            errors);
    }

    /// <summary>Fetches and validates a source's index before the user adds it, so typos fail early.</summary>
    public static async Task<ExtensionIndex> PreviewSourceAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            throw new ArgumentException("The software source URL is not a valid absolute URL.", nameof(url));
        return await ExtensionCatalog.FetchAsync(IndexHttpClient, uri, Sources?.IsOfficial(uri.AbsoluteUri) ?? false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Downloads and installs an entry. The trust policy runs before <paramref name="confirm" />;
    ///     a denied package throws <see cref="InvalidDataException" /> and is never installed.
    /// </summary>
    public static Task<PluginPackageCheck> InstallFromSourceAsync(ExtensionStoreItem item,
        Func<PluginPackageCheck, ExtensionTrustDecision, CancellationToken, Task<bool>> confirm,
        CancellationToken cancellationToken = default)
    {
        var manager = Manager ?? throw new InvalidOperationException("The extension system is not running.");
        if (!item.Entry.IsCompatible)
            throw new InvalidDataException(
                $"This extension requires extension API {item.Entry.ExtensionApi}; please update the launcher.");
        var origin = item.Source.IsOfficial ? ExtensionPackageOrigin.Official : ExtensionPackageOrigin.ThirdParty;
        return manager.InstallFromCatalogAsync(ExtensionCatalog.ToCatalogEntry(item.Entry) with { SourceUrl = item.Source.Url },
            SourceInstallType, _WithPolicy(origin, confirm), cancellationToken);
    }

    /// <summary>Installs a local <c>.pclext</c> file (third-party trust rules).</summary>
    public static Task<PluginPackageCheck> InstallLocalAsync(string packagePath,
        Func<PluginPackageCheck, ExtensionTrustDecision, CancellationToken, Task<bool>> confirm,
        CancellationToken cancellationToken = default)
    {
        var manager = Manager ?? throw new InvalidOperationException("The extension system is not running.");
        return manager.InstallAsync(packagePath, _WithPolicy(ExtensionPackageOrigin.LocalFile, confirm), cancellationToken);
    }

    public static bool IsExtensionPackagePath(string path) =>
        path.EndsWith(PluginPackage.ExtensionPackageExtension, StringComparison.OrdinalIgnoreCase);

    private static Func<PluginPackageCheck, CancellationToken, Task<bool>> _WithPolicy(ExtensionPackageOrigin origin,
        Func<PluginPackageCheck, ExtensionTrustDecision, CancellationToken, Task<bool>> confirm) =>
        (check, token) =>
        {
            var decision = ExtensionTrustPolicy.Evaluate(check, origin, OfficialSigners);
            if (!decision.Allowed) throw new InvalidDataException(decision.Reason);
            return confirm(check, decision, token);
        };

    private static PluginMarketUpdateKind _Compare(ExtensionIndexEntry entry, InstalledPlugin? installed)
    {
        if (installed is null) return PluginMarketUpdateKind.NotInstalled;
        if (!PluginVersionRange.TryCompareVersions(entry.Version, installed.Manifest.Version, out var comparison))
            return PluginMarketUpdateKind.UpdateAvailable;
        return comparison switch
        {
            > 0 => PluginMarketUpdateKind.UpdateAvailable,
            < 0 => PluginMarketUpdateKind.DowngradeAvailable,
            _ => PluginMarketUpdateKind.Current
        };
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Plugins;
using PCL.Extension.SDK;
using PCL.Plugin.SDK;

namespace PCL.Core.Extensions;

/// <summary>Launcher-specific callbacks for the extension host, supplied by the WPF host at startup.</summary>
public static class ExtensionHostBridge
{
    public static Func<ExtensionMessage, CancellationToken, Task<int>>? ShowMessageAsync { get; set; }
    public static Func<ExtensionInput, CancellationToken, Task<string?>>? InputAsync { get; set; }

    /// <summary>Arguments: extension id, section id, page id (optional).</summary>
    public static Action<string, string, string?>? NavigateTo { get; set; }

    public static Func<bool>? IsElevated { get; set; }
}

/// <summary>One registered section, tagged with the extension that owns it.</summary>
public sealed record RegisteredExtensionSection(string ExtensionId, string ExtensionName, ExtensionSection Section)
{
    public string Key => ExtensionId + "/" + Section.Id;
}

/// <summary>Sections registered by running extensions, shown on the Extensions page.</summary>
public static class ExtensionSectionRegistry
{
    private static readonly ConcurrentDictionary<string, RegisteredExtensionSection> Sections = new(StringComparer.Ordinal);

    public static event Action? Changed;

    public static IReadOnlyList<RegisteredExtensionSection> GetAll() =>
        Sections.Values.OrderBy(item => item.Section.Title, StringComparer.CurrentCulture).ToArray();

    public static RegisteredExtensionSection? Find(string key) => Sections.GetValueOrDefault(key);

    internal static RegisteredExtensionSection Register(string extensionId, string extensionName, ExtensionSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (!ExtensionCatalog.IsValidId(section.Id))
            throw new ArgumentException("Section id must contain lowercase letters, digits, dots, and hyphens.", nameof(section));
        if (string.IsNullOrWhiteSpace(section.Title) || section.Title.Length > 64)
            throw new ArgumentException("Section title is missing or too long.", nameof(section));
        if (section.Pages is null || section.Pages.Count is 0 or > 16)
            throw new ArgumentException("A section needs between 1 and 16 pages.", nameof(section));
        var pageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in section.Pages)
        {
            if (page is null || !ExtensionCatalog.IsValidId(page.Id) || !pageIds.Add(page.Id))
                throw new ArgumentException("Page ids must be unique and contain lowercase letters, digits, dots, and hyphens.", nameof(section));
            if (string.IsNullOrWhiteSpace(page.Title) || page.CreateView is null)
                throw new ArgumentException($"Page {page.Id} needs a title and a view factory.", nameof(section));
        }

        var registered = new RegisteredExtensionSection(extensionId, extensionName, section);
        if (!Sections.TryAdd(registered.Key, registered))
            throw new InvalidOperationException($"Extension section is already registered: {registered.Key}");
        Changed?.Invoke();
        return registered;
    }

    internal static void Unregister(string key)
    {
        if (Sections.TryRemove(key, out _)) Changed?.Invoke();
    }

    internal static void RemoveExtension(string extensionId)
    {
        var removed = false;
        foreach (var key in Sections.Keys.Where(key => key.StartsWith(extensionId + "/", StringComparison.Ordinal)))
            removed |= Sections.TryRemove(key, out _);
        if (removed) Changed?.Invoke();
    }
}

/// <summary>
///     Strings from an extension package's <c>lang/&lt;code&gt;.json</c> files. They are also merged into
///     the application resources so <c>{DynamicResource Key}</c> works in extension XAML. Keys that the
///     launcher (or an earlier extension) already defines are skipped, so an extension cannot change
///     launcher text.
/// </summary>
internal sealed class ExtensionLocalization : IExtensionLocalization, IDisposable
{
    private const long MaximumFileBytes = 1024 * 1024;
    private const int MaximumKeys = 5000;

    private readonly string _extensionId;
    private readonly Dictionary<string, Dictionary<string, string>> _languages = new(StringComparer.OrdinalIgnoreCase);
    private readonly ResourceDictionary _resources = new();
    private Dictionary<string, string> _active = new(StringComparer.Ordinal);
    private bool _merged;

    public ExtensionLocalization(string extensionId, string installDirectory)
    {
        _extensionId = extensionId;
        var langDirectory = Path.Combine(installDirectory, "lang");
        if (Directory.Exists(langDirectory))
            foreach (var file in Directory.EnumerateFiles(langDirectory, "*.json").Take(64))
                _LoadFile(file);
        LocalizationService.LanguageChanged += _OnLanguageChanged;
        _Apply();
    }

    public event Action? LanguageChanged;

    public string LanguageCode => LocalizationService.CurrentLanguage.Code;

    public string Text(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (_active.TryGetValue(key, out var text)) return text;
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    public string Text(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Text(key), args);

    public void Dispose()
    {
        LocalizationService.LanguageChanged -= _OnLanguageChanged;
        _RunOnUi(() =>
        {
            if (_merged) Application.Current?.Resources.MergedDictionaries.Remove(_resources);
            _merged = false;
        });
    }

    private void _OnLanguageChanged()
    {
        _Apply();
        try { LanguageChanged?.Invoke(); }
        catch (Exception ex) { LogWrapper.Warn(ex, "Extension", $"扩展语言切换回调失败：{_extensionId}"); }
    }

    /// <summary>Default language first, then the current language on top.</summary>
    private void _Apply()
    {
        var active = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_languages.TryGetValue(LocalizationService.DefaultLanguageCode, out var fallback))
            foreach (var pair in fallback) active[pair.Key] = pair.Value;
        if (_languages.TryGetValue(LocalizationService.CurrentLanguage.Code, out var current))
            foreach (var pair in current) active[pair.Key] = pair.Value;
        _active = active;

        _RunOnUi(() =>
        {
            var app = Application.Current;
            if (app is null) return;
            if (_merged) app.Resources.MergedDictionaries.Remove(_resources);
            _resources.Clear();
            foreach (var pair in active)
            {
                if (app.TryFindResource(pair.Key) is not null)
                {
                    LogWrapper.Warn("Extension", $"扩展 {_extensionId} 的文案键与启动器已有键冲突，已忽略：{pair.Key}");
                    continue;
                }
                _resources[pair.Key] = pair.Value;
            }
            app.Resources.MergedDictionaries.Add(_resources);
            _merged = true;
        });
    }

    private void _LoadFile(string path)
    {
        var code = Path.GetFileNameWithoutExtension(path);
        if (!LocalizationService.IsLanguageSupported(code)) return;
        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaximumFileBytes) throw new InvalidDataException("Language file exceeds 1 MiB.");
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(path),
                          new JsonSerializerOptions { MaxDepth = 4 })
                      ?? new Dictionary<string, string>();
            var valid = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in map.Take(MaximumKeys))
                if (pair.Key.Length is > 0 and <= 128 && pair.Value is not null &&
                    pair.Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
                    valid[pair.Key] = pair.Value;
            _languages[code] = valid;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Extension", $"扩展语言文件读取失败：{_extensionId}/{Path.GetFileName(path)}");
        }
    }

    private static void _RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }
}

/// <summary>Per-extension implementation of <see cref="IExtensionHost" />.</summary>
internal sealed class ExtensionHost(PluginManifest manifest) : IExtensionHost, IDisposable
{
    private readonly ConcurrentDictionary<string, byte> _sectionKeys = new(StringComparer.Ordinal);

    public string ApiVersion => ExtensionApiVersion.Current;

    public bool IsElevated => ExtensionHostBridge.IsElevated?.Invoke() ?? false;

    public IExtensionSectionHandle RegisterSection(ExtensionSection section)
    {
        var registered = ExtensionSectionRegistry.Register(manifest.Id, manifest.Name, section);
        _sectionKeys[registered.Key] = 0;
        return new SectionHandle(this, registered);
    }

    public Task<int> ShowMessageAsync(ExtensionMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Buttons is null || message.Buttons.Count is 0 or > 3)
            throw new ArgumentException("A message needs between 1 and 3 buttons.", nameof(message));
        return ExtensionHostBridge.ShowMessageAsync?.Invoke(message, cancellationToken) ?? Task.FromResult(0);
    }

    public Task<string?> InputAsync(ExtensionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ExtensionHostBridge.InputAsync?.Invoke(input, cancellationToken) ?? Task.FromResult<string?>(null);
    }

    public void NavigateTo(string sectionId, string? pageId = null) =>
        ExtensionHostBridge.NavigateTo?.Invoke(manifest.Id, sectionId, pageId);

    public Task RunOnUiAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return dispatcher.InvokeAsync(action).Task;
    }

    public void Dispose()
    {
        foreach (var key in _sectionKeys.Keys) ExtensionSectionRegistry.Unregister(key);
        _sectionKeys.Clear();
    }

    private sealed class SectionHandle(ExtensionHost owner, RegisteredExtensionSection registered) : IExtensionSectionHandle
    {
        public ExtensionSection Section => registered.Section;

        public void Dispose()
        {
            if (owner._sectionKeys.TryRemove(registered.Key, out _))
                ExtensionSectionRegistry.Unregister(registered.Key);
        }
    }
}

/// <summary>The plugin API v1 context plus the extension host.</summary>
internal sealed class ExtensionContext(IPluginContext inner, IExtensionHost host, IExtensionLocalization localization)
    : IExtensionContext
{
    public IPluginMetadata Plugin => inner.Plugin;
    public string DataDirectory => inner.DataDirectory;
    public IPluginLogger Logger => inner.Logger;
    public IPluginStorage Storage => inner.Storage;
    public IPluginSettings Settings => inner.Settings;
    public IPluginUi Ui => inner.Ui;
    public IPluginEvents Events => inner.Events;
    public IPluginNetwork Network => inner.Network;
    public IPluginMinecraft Minecraft => inner.Minecraft;
    public IPluginLauncher Launcher => inner.Launcher;
    public IPluginServices Services => inner.Services;
    public IExtensionHost Host => host;
    public IExtensionLocalization Localization => localization;
}

/// <summary>Runs an <see cref="IPclExtension" /> inside the shared plugin lifecycle.</summary>
internal sealed class ExtensionPluginAdapter(IPclExtension extension, PluginManifest manifest, string installDirectory)
    : IPclPlugin
{
    private ExtensionHost? _host;
    private ExtensionLocalization? _localization;

    public async Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        _host = new ExtensionHost(manifest);
        _localization = new ExtensionLocalization(manifest.Id, installDirectory);
        try
        {
            await extension.StartAsync(new ExtensionContext(context, _host, _localization), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            _Release();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try { await extension.StopAsync(cancellationToken).ConfigureAwait(false); }
        finally { _Release(); }
    }

    private void _Release()
    {
        _host?.Dispose();
        _localization?.Dispose();
        ExtensionSectionRegistry.RemoveExtension(manifest.Id);
        _host = null;
        _localization = null;
    }
}

/// <summary>Builds the runtime profile for <c>.pclext</c> packages.</summary>
public static class ExtensionRuntimeProfile
{
    public static PluginRuntimeProfile Create() => new()
    {
        Name = "Extension",
        PackageExtension = PluginPackage.ExtensionPackageExtension,
        ManifestKind = PluginManifestKind.Extension,
        EntryInterface = typeof(IPclExtension),
        AllowMixins = false,
        SharedAssemblies = [typeof(IPclPlugin).Assembly, typeof(IPclExtension).Assembly],
        CreatePlugin = (entry, manifest, directory) => entry is IPclExtension extension
            ? new ExtensionPluginAdapter(extension, manifest, directory)
            : throw new InvalidOperationException("Extension entry type must implement IPclExtension."),
        ValidateManifest = manifest => ExtensionApiVersion.IsSupported(manifest.ExtensionApi)
            ? null
            : $"Extension requires extension API {manifest.ExtensionApi}; this launcher supports {ExtensionApiVersion.Current}."
    };
}

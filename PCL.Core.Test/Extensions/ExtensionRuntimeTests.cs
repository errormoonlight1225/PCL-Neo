using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Extensions;
using PCL.Core.Plugins;
using PCL.Extension.SDK;
using PCL.Plugin.SDK;

namespace PCL.Core.Test.Extensions;

/// <summary>
///     Started by the extension runtime in <see cref="ExtensionRuntimeTests" />. It runs in its own
///     load context (a separate copy of this assembly), so it reports back only through host APIs.
/// </summary>
public sealed class ProbeExtension : IPclExtension
{
    private IExtensionContext? _context;

    public Task StartAsync(IExtensionContext context, CancellationToken cancellationToken)
    {
        _context = context;
        context.Ui.ShowNotification("probe-started|" + context.Host.ApiVersion);
        context.Host.RegisterSection(new ExtensionSection
        {
            Id = "probe",
            Title = "Probe",
            Launcher = new ExtensionLauncherCard { OnLaunch = _ => Task.CompletedTask },
            Pages = [new ExtensionPage { Id = "main", Title = "Main", CreateView = () => new System.Windows.FrameworkElement() }]
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _context?.Ui.ShowNotification("probe-stopped");
        return Task.CompletedTask;
    }
}

[TestClass]
[DoNotParallelize]
public sealed class ExtensionRuntimeTests
{
    private static readonly string TestAssemblyPath = typeof(ProbeExtension).Assembly.Location;
    private static readonly string TestAssemblyName = Path.GetFileName(TestAssemblyPath);

    private static PluginManifest ExtensionManifest(string api = "1.0") => new()
    {
        Id = "runtime.extension.probe",
        Name = "Extension probe",
        Version = "1.0.0",
        Author = "PCL.Core.Test",
        EntryAssembly = TestAssemblyName,
        EntryType = typeof(ProbeExtension).FullName!,
        Kind = PluginManifestKind.Extension,
        ExtensionApi = api
    };

    [STATestMethod]
    public async Task SignedExtensionInstallsStartsRegistersItsSectionAndStops()
    {
        using var certificate = CreateCertificate();
        var package = TempPath(".pclext");
        var directory = TempPath("");
        var notices = new List<string>();
        var previousNotice = PluginHostBridge.ShowNotification;
        PluginHostBridge.ShowNotification = (message, _) => { lock (notices) notices.Add(message); };
        try
        {
            await WritePackageAsync(package, ExtensionManifest(), certificate);
            var profile = ExtensionRuntimeProfile.Create();
            await using (var installer = new PluginRuntimeManager(directory, profile: profile))
                await installer.InstallAsync(package, (_, _) => Task.FromResult(true));

            await using (var runtime = new PluginRuntimeManager(directory, profile: profile))
            {
                await runtime.LoadEnabledAsync();
                CollectionAssert.Contains(notices, "probe-started|" + ExtensionApiVersion.Current);
                var section = ExtensionSectionRegistry.Find("runtime.extension.probe/probe");
                Assert.IsNotNull(section, "the extension's section is visible to the host");
                Assert.AreEqual("Extension probe", section.ExtensionName);
            }

            CollectionAssert.Contains(notices, "probe-stopped");
            Assert.IsNull(ExtensionSectionRegistry.Find("runtime.extension.probe/probe"), "sections are removed on stop");
        }
        finally
        {
            PluginHostBridge.ShowNotification = previousNotice;
            DeleteFile(package);
            DeleteDirectory(directory);
        }
    }

    [STATestMethod]
    public async Task UnsignedExtensionsInstallLocallyButAreFlagged()
    {
        var package = TempPath(".pclext");
        var directory = TempPath("");
        try
        {
            await WritePackageAsync(package, ExtensionManifest(), certificate: null);
            ExtensionTrustDecision? seen = null;
            await using var installer = new PluginRuntimeManager(directory, profile: ExtensionRuntimeProfile.Create());
            await installer.InstallAsync(package, (check, _) =>
            {
                seen = ExtensionTrustPolicy.Evaluate(check, ExtensionPackageOrigin.LocalFile, []);
                return Task.FromResult(seen.Allowed);
            });
            Assert.IsNotNull(seen);
            Assert.IsTrue(seen.IsUnsigned);
            Assert.AreEqual(1, installer.GetInstalled().Count(item => item.PendingRestart || item.Enabled));
        }
        finally
        {
            DeleteFile(package);
            DeleteDirectory(directory);
        }
    }

    [STATestMethod]
    public async Task RuntimesRejectTheOtherPackageKind()
    {
        using var certificate = CreateCertificate();
        var extensionPackage = TempPath(".pclext");
        var pluginPackage = TempPath(".pclplugin");
        var directory = TempPath("");
        try
        {
            await WritePackageAsync(extensionPackage, ExtensionManifest(), certificate);
            await WritePackageAsync(pluginPackage, ExtensionManifest() with { Kind = null, ExtensionApi = null }, certificate);

            await using var plugins = new PluginRuntimeManager(directory);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                plugins.InstallAsync(extensionPackage, (_, _) => Task.FromResult(true)));

            await using var extensions = new PluginRuntimeManager(directory + "-ext", profile: ExtensionRuntimeProfile.Create());
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                extensions.InstallAsync(pluginPackage, (_, _) => Task.FromResult(true)));
        }
        finally
        {
            DeleteFile(extensionPackage);
            DeleteFile(pluginPackage);
            DeleteDirectory(directory);
            DeleteDirectory(directory + "-ext");
        }
    }

    [TestMethod]
    public void ExtensionProfileRejectsMixinsAndNewerApiVersions()
    {
        var profile = ExtensionRuntimeProfile.Create();

        Assert.IsNull(profile.Validate(ExtensionManifest()));
        Assert.IsNotNull(profile.Validate(ExtensionManifest() with { MixinConfig = "mixins.json" }));
        Assert.IsNotNull(profile.Validate(ExtensionManifest(api: "1.99")));
        Assert.IsNotNull(profile.Validate(ExtensionManifest(api: "2.0")));
        Assert.IsNotNull(PluginRuntimeProfile.Plugins.Validate(ExtensionManifest()));
    }

    [TestMethod]
    public void ExtensionServiceIsRegisteredWithTheLifecycle()
    {
        // The service is discovered by a source generator; without registration extensions never start.
        CollectionAssert.Contains(PCL.Core.App.IoC.LifecycleServiceTypes.Loaded, typeof(ExtensionManagerService));
        CollectionAssert.Contains(PCL.Core.App.IoC.LifecycleServiceTypes.Loaded, typeof(PluginManagerService));
    }

    [TestMethod]
    public void ManifestsWithoutNewFieldsKeepTheirCanonicalForm()
    {
        // Signatures cover the re-serialized manifest; absent kind/extensionApi must not appear in it.
        var json = JsonSerializer.Serialize(ExtensionManifest() with { Kind = null, ExtensionApi = null },
            PluginPackage.SerializerOptions);

        Assert.IsFalse(json.Contains("\"kind\"", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("\"extensionApi\"", StringComparison.Ordinal));
    }

    private static async Task WritePackageAsync(string path, PluginManifest manifest, X509Certificate2? certificate)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [PluginPackage.ManifestFileName] = JsonSerializer.SerializeToUtf8Bytes(manifest, PluginPackage.SerializerOptions),
            [TestAssemblyName] = await File.ReadAllBytesAsync(TestAssemblyPath)
        };
        await using var output = File.Create(path);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var (name, contents) in files)
        {
            await using var entryStream = archive.CreateEntry(name).Open();
            await entryStream.WriteAsync(contents);
        }
        if (certificate is null) return;
        var signature = PluginPackage.SignFiles(files, certificate);
        await using var signatureStream = archive.CreateEntry(PluginPackage.SignatureFileName).Open();
        await JsonSerializer.SerializeAsync(signatureStream, signature, PluginPackage.SerializerOptions);
    }

    private static X509Certificate2 CreateCertificate()
    {
        const string password = "extension-runtime-test-only";
        var pfx = PluginAuthoring.CreateSigningCertificate("CN=PCL Extension Runtime Test", password);
        return X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.EphemeralKeySet);
    }

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), "pcl-extension-test-" + Guid.NewGuid().ToString("N") + extension);

    private static void DeleteFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void DeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 10 && Directory.Exists(path); attempt++)
        {
            try { Directory.Delete(path, recursive: true); }
            catch (IOException) { GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(40); }
            catch (UnauthorizedAccessException) { GC.Collect(); GC.WaitForPendingFinalizers(); Thread.Sleep(40); }
        }
    }
}

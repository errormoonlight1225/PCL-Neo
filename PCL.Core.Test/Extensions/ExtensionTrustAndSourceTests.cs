using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Extensions;
using PCL.Plugin.SDK;

namespace PCL.Core.Test.Extensions;

[TestClass]
public sealed class ExtensionTrustAndSourceTests
{
    private const string OfficialThumbprint = "AABBCCDDEEFF00112233445566778899AABBCCDD";

    private static readonly PluginManifest Manifest = new()
    {
        Id = "example.tool", Name = "Tool", Version = "1.0.0", Author = "Someone",
        EntryAssembly = "Tool.dll", EntryType = "Tool.Extension",
        Kind = PluginManifestKind.Extension, ExtensionApi = "1.0"
    };

    private static PluginPackageCheck Unsigned() => new(Manifest, false, false, null, null, null, "hash", null);

    private static PluginPackageCheck Signed(string thumbprint) =>
        new(Manifest, true, true, thumbprint, "CN=Someone", DateTimeOffset.UtcNow, "hash", null);

    [TestMethod]
    public void OfficialSourceAcceptsOnlyTheOfficialSigner()
    {
        string[] official = [OfficialThumbprint];

        Assert.IsTrue(ExtensionTrustPolicy.Evaluate(Signed(OfficialThumbprint), ExtensionPackageOrigin.Official, official).Allowed);
        Assert.IsTrue(ExtensionTrustPolicy.Evaluate(Signed("aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd"),
            ExtensionPackageOrigin.Official, official).Allowed, "thumbprints compare without separators or case");
        Assert.IsFalse(ExtensionTrustPolicy.Evaluate(Unsigned(), ExtensionPackageOrigin.Official, official).Allowed);
        Assert.IsFalse(ExtensionTrustPolicy.Evaluate(Signed("1111111111111111111111111111111111111111"),
            ExtensionPackageOrigin.Official, official).Allowed);
        Assert.IsFalse(ExtensionTrustPolicy.Evaluate(Signed(OfficialThumbprint), ExtensionPackageOrigin.Official, []).Allowed,
            "a build without official signers must not accept official packages");
    }

    [TestMethod]
    public void ThirdPartySourcesAllowUnsignedPackagesButFlagThem()
    {
        var unsigned = ExtensionTrustPolicy.Evaluate(Unsigned(), ExtensionPackageOrigin.ThirdParty, []);
        Assert.IsTrue(unsigned.Allowed);
        Assert.IsTrue(unsigned.IsUnsigned);

        var signed = ExtensionTrustPolicy.Evaluate(Signed("1111111111111111111111111111111111111111"),
            ExtensionPackageOrigin.LocalFile, []);
        Assert.IsTrue(signed.Allowed);
        Assert.IsFalse(signed.IsUnsigned);
    }

    [TestMethod]
    public void InvalidSignaturesAndPackageErrorsAreAlwaysRejected()
    {
        var tampered = new PluginPackageCheck(Manifest, true, false, OfficialThumbprint, null, null, "hash", null);
        var broken = new PluginPackageCheck(Manifest, false, false, null, null, null, "hash", "broken");

        Assert.IsFalse(ExtensionTrustPolicy.Evaluate(tampered, ExtensionPackageOrigin.ThirdParty, []).Allowed);
        Assert.IsFalse(ExtensionTrustPolicy.Evaluate(broken, ExtensionPackageOrigin.LocalFile, []).Allowed);
    }

    [TestMethod]
    public void ParsesThumbprintLists()
    {
        var parsed = ExtensionTrustPolicy.ParseThumbprints($"{OfficialThumbprint.ToLowerInvariant()}, junk ; {OfficialThumbprint}");

        CollectionAssert.AreEqual(new[] { OfficialThumbprint }, parsed.ToArray());
    }

    [TestMethod]
    public void SourceStoreKeepsOfficialFirstAndPersistsThirdPartySources()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pcl-extension-sources-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(directory, "sources.json");
        const string official = "https://raw.example.com/official/extensions/index.json";
        try
        {
            var store = new ExtensionSourceStore(file, official);
            Assert.AreEqual(1, store.GetAll().Count);
            Assert.IsTrue(store.GetAll()[0].IsOfficial);

            store.Add("https://mirror.example.org/index.json", "Mirror");
            Assert.ThrowsExactly<ArgumentException>(() => store.Add("https://mirror.example.org/index.json"), "duplicate");
            Assert.ThrowsExactly<ArgumentException>(() => store.Add(official), "official cannot be added twice");
            Assert.ThrowsExactly<ArgumentException>(() => store.Add("http://insecure.example.org/index.json"));
            Assert.ThrowsExactly<ArgumentException>(() => store.Add("https://user:secret@example.org/index.json"));
            Assert.ThrowsExactly<ArgumentException>(() => store.Add("not a url"));
            Assert.IsFalse(store.Remove(official), "official source cannot be removed");

            var reloaded = new ExtensionSourceStore(file, official);
            var sources = reloaded.GetAll();
            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual("Mirror", sources[1].Name);
            Assert.IsFalse(sources[1].IsOfficial);

            Assert.IsTrue(reloaded.Remove("https://mirror.example.org/index.json"));
            Assert.AreEqual(1, new ExtensionSourceStore(file, official).GetAll().Count);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void CorruptSourceFileFallsBackToOfficialOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pcl-extension-sources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "sources.json");
        try
        {
            File.WriteAllText(file, "{ not json");
            var store = new ExtensionSourceStore(file, "https://raw.example.com/official/index.json");
            Assert.AreEqual(1, store.GetAll().Count);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}

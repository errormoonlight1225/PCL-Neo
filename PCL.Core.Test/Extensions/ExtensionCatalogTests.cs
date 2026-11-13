using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Extensions;

namespace PCL.Core.Test.Extensions;

[TestClass]
public sealed class ExtensionCatalogTests
{
    private static readonly Uri ThirdPartySource = new("https://example.com/pcl/extensions/index.json?cache=1");
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static byte[] Index(string entries, string name = "\"Example source\"") =>
        Encoding.UTF8.GetBytes($$"""{ "schemaVersion": 1, "name": {{name}}, "extensions": [ {{entries}} ] }""");

    private static string Entry(string id = "example.tool", string packageUrl = "https://cdn.example.com/tool.pclext",
        string sha = Sha, string api = "1.0", string version = "1.2.0") =>
        $$"""{ "id": "{{id}}", "name": "Tool", "version": "{{version}}", "author": "Someone", "description": "Does things", "packageUrl": "{{packageUrl}}", "sha256": "{{sha}}", "extensionApi": "{{api}}" }""";

    [TestMethod]
    public void ThirdPartySourceAcceptsAnyHttpsHostAndRecordsTheSource()
    {
        var index = ExtensionCatalog.Parse(Index(Entry()), ThirdPartySource, isOfficial: false);

        Assert.AreEqual("Example source", index.Name);
        var entry = index.Extensions.Single();
        Assert.AreEqual("example.tool", entry.Id);
        Assert.AreEqual("https://example.com/pcl/extensions/index.json", entry.SourceUrl);
        Assert.IsTrue(entry.IsCompatible);
    }

    [TestMethod]
    public void OfficialSourceRequiresGitHubReleaseAssets()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry()), ThirdPartySource, isOfficial: true));

        var github = Entry(packageUrl: "https://github.com/owner/repo/releases/download/v1.0.0/tool.pclext");
        var index = ExtensionCatalog.Parse(Index(github), ThirdPartySource, isOfficial: true);
        Assert.AreEqual(1, index.Extensions.Count);
    }

    [TestMethod]
    public void EntriesMustUseHttpsAndCarryADigest()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry(packageUrl: "http://cdn.example.com/tool.pclext")), ThirdPartySource, false));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry(sha: "abc")), ThirdPartySource, false));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry(packageUrl: "https://user:pass@cdn.example.com/tool.pclext")), ThirdPartySource, false));
    }

    [TestMethod]
    public void RejectsDuplicateIdsInvalidIdsAndBadVersions()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry() + "," + Entry()), ThirdPartySource, false));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry(id: "Bad Id")), ThirdPartySource, false));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry(version: "latest")), ThirdPartySource, false));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Index(Entry(api: "one")), ThirdPartySource, false));
    }

    [TestMethod]
    public void NewerApiVersionIsListedButMarkedIncompatible()
    {
        var index = ExtensionCatalog.Parse(Index(Entry(api: "1.99")), ThirdPartySource, false);

        Assert.IsFalse(index.Extensions.Single().IsCompatible);
    }

    [TestMethod]
    public void RejectsUnknownSchemaAndOversizedIndexes()
    {
        var schemaTwo = Encoding.UTF8.GetBytes("""{ "schemaVersion": 2, "extensions": [] }""");
        Assert.ThrowsExactly<InvalidDataException>(() => ExtensionCatalog.Parse(schemaTwo, ThirdPartySource, false));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(new byte[ExtensionCatalog.MaximumIndexBytes + 1], ThirdPartySource, false));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ExtensionCatalog.Parse(Encoding.UTF8.GetBytes("not json"), ThirdPartySource, false));
    }
}

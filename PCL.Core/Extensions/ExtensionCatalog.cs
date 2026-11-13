using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Plugins;
using PCL.Extension.SDK;
using PCL.Plugin.SDK;

namespace PCL.Core.Extensions;

/// <summary>One extension listed by a software source's <c>index.json</c>.</summary>
public sealed record ExtensionIndexEntry(string Id, string Name, string Version, string Author, string Description,
    string PackageUrl, string Sha256, string ExtensionApi)
{
    public string? Icon { get; init; }
    public string? Website { get; init; }
    public Dictionary<string, string> LocalizedNames { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LocalizedDescriptions { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Index URL this entry was read from (after redirects). Not part of the JSON.</summary>
    [JsonIgnore]
    public string? SourceUrl { get; init; }

    /// <summary>Whether this launcher can run the entry's declared API version.</summary>
    [JsonIgnore]
    public bool IsCompatible => ExtensionApiVersion.IsSupported(ExtensionApi);
}

/// <summary>A software source's <c>index.json</c> (schema version 1).</summary>
public sealed record ExtensionIndex(int SchemaVersion, string? Name, IReadOnlyList<ExtensionIndexEntry> Extensions);

/// <summary>Fetches and validates software source indexes.</summary>
public static class ExtensionCatalog
{
    public const int MaximumIndexBytes = 2 * 1024 * 1024;
    public const int MaximumEntries = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 32
    };

    /// <summary>Downloads and validates an index. The final URL after redirects must still be HTTPS.</summary>
    public static async Task<ExtensionIndex> FetchAsync(HttpClient httpClient, Uri indexUri, bool isOfficial,
        CancellationToken cancellationToken = default)
    {
        ExtensionSourceStore.ValidateSourceUri(indexUri);
        using var response = await httpClient.GetAsync(indexUri, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var finalUri = response.RequestMessage?.RequestUri ?? indexUri;
        if (finalUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The software source redirected away from HTTPS.");
        if (response.Content.Headers.ContentLength > MaximumIndexBytes)
            throw new InvalidDataException("The software source index exceeds 2 MiB.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaximumIndexBytes)
                throw new InvalidDataException("The software source index exceeds 2 MiB.");
            buffer.Write(chunk, 0, read);
        }
        return Parse(buffer.ToArray(), finalUri, isOfficial);
    }

    /// <summary>
    ///     Parses and validates an index. Official sources additionally require every package to be a
    ///     GitHub release asset, matching the plugin market; third-party sources may host packages on
    ///     any HTTPS server.
    /// </summary>
    public static ExtensionIndex Parse(byte[] json, Uri sourceUri, bool isOfficial)
    {
        if (json.Length > MaximumIndexBytes) throw new InvalidDataException("The software source index exceeds 2 MiB.");
        ExtensionIndex? index;
        try { index = JsonSerializer.Deserialize<ExtensionIndex>(json, JsonOptions); }
        catch (JsonException ex) { throw new InvalidDataException("The software source index is not valid JSON.", ex); }
        if (index is null) throw new InvalidDataException("The software source index is empty.");
        if (index.SchemaVersion != 1) throw new InvalidDataException("The software source index schema is not supported.");
        if (index.Name is { Length: > 128 }) throw new InvalidDataException("The software source name is too long.");
        if (index.Extensions is null || index.Extensions.Count > MaximumEntries)
            throw new InvalidDataException($"A software source may list at most {MaximumEntries} extensions.");

        var normalizedSource = new UriBuilder(sourceUri) { Query = string.Empty, Fragment = string.Empty }.Uri.AbsoluteUri;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<ExtensionIndexEntry>(index.Extensions.Count);
        foreach (var entry in index.Extensions)
        {
            if (entry is null) throw new InvalidDataException("The software source contains an empty entry.");
            ValidateEntry(entry, isOfficial);
            if (!ids.Add(entry.Id)) throw new InvalidDataException($"Duplicate extension id in software source: {entry.Id}");
            entries.Add(entry with
            {
                Sha256 = entry.Sha256.ToLowerInvariant(),
                SourceUrl = normalizedSource,
                LocalizedNames = entry.LocalizedNames ?? new(StringComparer.OrdinalIgnoreCase),
                LocalizedDescriptions = entry.LocalizedDescriptions ?? new(StringComparer.OrdinalIgnoreCase)
            });
        }
        return index with { Extensions = entries };
    }

    private static void ValidateEntry(ExtensionIndexEntry entry, bool isOfficial)
    {
        if (!IsValidId(entry.Id)) throw new InvalidDataException("The software source contains an invalid extension id.");
        if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 128 ||
            string.IsNullOrWhiteSpace(entry.Author) || entry.Author.Length > 128 ||
            string.IsNullOrWhiteSpace(entry.Version) || entry.Version.Length > 64 ||
            entry.Description is null || entry.Description.Length > 4096 ||
            string.IsNullOrWhiteSpace(entry.PackageUrl) || entry.PackageUrl.Length > 2048 ||
            (entry.Website?.Length ?? 0) > 2048 || (entry.Icon?.Length ?? 0) > 1024 ||
            (entry.LocalizedNames?.Count ?? 0) > 32 || (entry.LocalizedDescriptions?.Count ?? 0) > 32)
            throw new InvalidDataException($"Extension entry has invalid or oversized metadata: {entry.Id}");
        if (!PluginVersionRange.IsValidVersion(entry.Version))
            throw new InvalidDataException($"Extension entry has an invalid SemVer version: {entry.Id}");
        if (!ExtensionApiVersion.TryParse(entry.ExtensionApi, out _, out _))
            throw new InvalidDataException($"Extension entry has an invalid extensionApi: {entry.Id}");
        if (string.IsNullOrWhiteSpace(entry.Sha256) || entry.Sha256.Length != 64 || !entry.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Extension entry must include a SHA-256 digest: {entry.Id}");
        if (!IsSafeHttps(entry.PackageUrl))
            throw new InvalidDataException($"Extension package URL must use HTTPS: {entry.Id}");
        if (entry.Website is not null && !IsSafeHttps(entry.Website))
            throw new InvalidDataException($"Extension website URL must use HTTPS: {entry.Id}");
        if (entry.Icon is not null && !IsSafeHttps(entry.Icon))
            throw new InvalidDataException($"Extension icon URL must use HTTPS: {entry.Id}");
        if (isOfficial && !PluginRuntimeManager.IsGitHubReleaseAsset(entry.PackageUrl))
            throw new InvalidDataException($"Official extension packages must be GitHub release assets: {entry.Id}");
    }

    /// <summary>Same id rules as plugin manifests: lowercase ASCII letters, digits, dots and hyphens.</summary>
    public static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && char.IsAsciiLetterOrDigit(id[0]) &&
        id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '-');

    private static bool IsSafeHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo);

    /// <summary>Adapts an index entry to the shared download-and-install pipeline.</summary>
    public static PluginMarketEntry ToCatalogEntry(ExtensionIndexEntry entry) =>
        new(entry.Id, entry.Name, entry.Version, entry.Author, entry.Description, entry.PackageUrl,
            entry.Website, entry.Sha256)
        {
            SourceUrl = entry.SourceUrl,
            Icon = entry.Icon,
            LocalizedNames = entry.LocalizedNames,
            LocalizedDescriptions = entry.LocalizedDescriptions
        };
}

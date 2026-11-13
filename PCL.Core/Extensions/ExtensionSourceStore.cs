using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PCL.Core.Logging;

namespace PCL.Core.Extensions;

/// <summary>A software source the user can install extensions from.</summary>
public sealed record ExtensionSource(string Url, string? Name, bool IsOfficial, DateTimeOffset AddedAtUtc);

/// <summary>
///     The user's list of software sources, persisted as <c>sources.json</c>. The official source is
///     always present first and cannot be removed; third-party sources must use HTTPS without
///     embedded credentials.
/// </summary>
public sealed class ExtensionSourceStore
{
    public const int MaximumSources = 64;
    public const int MaximumUrlLength = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _filePath;
    private readonly ExtensionSource _official;
    private readonly List<ExtensionSource> _sources = [];
    private readonly object _sync = new();

    public ExtensionSourceStore(string filePath, string officialUrl)
    {
        _filePath = Path.GetFullPath(filePath);
        _official = new ExtensionSource(Normalize(new Uri(officialUrl)), null, true, DateTimeOffset.MinValue);
        _Load();
    }

    public event Action? Changed;

    public ExtensionSource Official => _official;

    /// <summary>Official source first, then third-party sources in the order they were added.</summary>
    public IReadOnlyList<ExtensionSource> GetAll()
    {
        lock (_sync) return [_official, .. _sources];
    }

    /// <summary>Adds a third-party source. Throws <see cref="ArgumentException" /> for an unusable URL.</summary>
    public ExtensionSource Add(string url, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaximumUrlLength)
            throw new ArgumentException("The software source URL is empty or too long.", nameof(url));
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            throw new ArgumentException("The software source URL is not a valid absolute URL.", nameof(url));
        ValidateSourceUri(uri);
        var normalized = Normalize(uri);
        var source = new ExtensionSource(normalized, string.IsNullOrWhiteSpace(name) ? null : name.Trim()[..Math.Min(name.Trim().Length, 128)],
            false, DateTimeOffset.UtcNow);
        lock (_sync)
        {
            if (string.Equals(normalized, _official.Url, StringComparison.OrdinalIgnoreCase) ||
                _sources.Any(item => string.Equals(item.Url, normalized, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("This software source has already been added.", nameof(url));
            if (_sources.Count >= MaximumSources)
                throw new ArgumentException($"At most {MaximumSources} software sources can be added.", nameof(url));
            _sources.Add(source);
            _Save();
        }
        Changed?.Invoke();
        return source;
    }

    /// <summary>Updates the display name learned from the source's index.</summary>
    public void SetName(string url, string? name)
    {
        lock (_sync)
        {
            var index = _sources.FindIndex(item => string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || string.Equals(_sources[index].Name, name, StringComparison.Ordinal)) return;
            _sources[index] = _sources[index] with { Name = name };
            _Save();
        }
        Changed?.Invoke();
    }

    /// <summary>Removes a third-party source. The official source cannot be removed.</summary>
    public bool Remove(string url)
    {
        bool removed;
        lock (_sync)
        {
            removed = _sources.RemoveAll(item => string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) _Save();
        }
        if (removed) Changed?.Invoke();
        return removed;
    }

    public bool IsOfficial(string? url) =>
        url is not null && string.Equals(url, _official.Url, StringComparison.OrdinalIgnoreCase);

    /// <summary>HTTPS only, no user name or password, no fragment.</summary>
    public static void ValidateSourceUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Software sources must use HTTPS.", nameof(uri));
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Software source URLs must not contain a user name or password.", nameof(uri));
        if (!string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Software source URLs must not contain a fragment.", nameof(uri));
    }

    private static string Normalize(Uri uri) => new UriBuilder(uri) { Fragment = string.Empty }.Uri.AbsoluteUri;

    private void _Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var file = JsonSerializer.Deserialize<SourceFile>(File.ReadAllBytes(_filePath), JsonOptions);
            if (file?.SchemaVersion != 1 || file.Sources is null) return;
            foreach (var item in file.Sources.Take(MaximumSources))
            {
                if (item?.Url is null || !Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)) continue;
                try { ValidateSourceUri(uri); }
                catch (ArgumentException) { continue; }
                var normalized = Normalize(uri);
                if (IsOfficial(normalized) || _sources.Any(source => string.Equals(source.Url, normalized, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _sources.Add(new ExtensionSource(normalized, item.Name, false, item.AddedAtUtc));
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Extension", "软件源列表读取失败，只保留官方源");
            _sources.Clear();
        }
    }

    private void _Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var file = new SourceFile(1, _sources.Select(item => new SourceItem(item.Url, item.Name, item.AddedAtUtc)).ToArray());
        var temp = _filePath + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(file, JsonOptions));
        File.Move(temp, _filePath, overwrite: true);
    }

    private sealed record SourceFile(int SchemaVersion, IReadOnlyList<SourceItem>? Sources);

    private sealed record SourceItem(string? Url, string? Name, DateTimeOffset AddedAtUtc);
}

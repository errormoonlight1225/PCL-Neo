using System;
using System.Collections.Generic;
using System.Reflection;
using PCL.Plugin.SDK;

namespace PCL.Core.Plugins;

/// <summary>
///     What kind of package a <see cref="PluginRuntimeManager" /> instance manages. Plugins use
///     <see cref="Plugins" />; extensions use the profile built in <c>PCL.Core.Extensions</c>.
///     The package format, signing, installation transaction and state files are shared.
/// </summary>
public sealed class PluginRuntimeProfile
{
    /// <summary>Default profile: ordinary <c>.pclplugin</c> packages implementing <see cref="IPclPlugin" />.</summary>
    public static PluginRuntimeProfile Plugins { get; } = new()
    {
        Name = "Plugin",
        PackageExtension = PluginPackage.Extension,
        ManifestKind = PluginManifestKind.Plugin,
        EntryInterface = typeof(IPclPlugin),
        AllowMixins = true,
        SharedAssemblies = [typeof(IPclPlugin).Assembly],
        CreatePlugin = (entry, _, _) => entry as IPclPlugin
            ?? throw new InvalidOperationException("Plugin entry type must implement IPclPlugin.")
    };

    /// <summary>Log and error prefix, e.g. <c>Plugin</c> or <c>Extension</c>.</summary>
    public required string Name { get; init; }

    /// <summary>File extension including the dot, e.g. <c>.pclplugin</c>.</summary>
    public required string PackageExtension { get; init; }

    /// <summary>The manifest <c>kind</c> this runtime accepts.</summary>
    public required string ManifestKind { get; init; }

    /// <summary>The interface the manifest's <c>entryType</c> must implement.</summary>
    public required Type EntryInterface { get; init; }

    public bool AllowMixins { get; init; }

    /// <summary>Host assemblies every package must bind to instead of loading its own copy.</summary>
    public required IReadOnlyList<Assembly> SharedAssemblies { get; init; }

    /// <summary>
    ///     Adapts a freshly constructed entry object to the plugin lifecycle. Arguments: entry instance,
    ///     manifest, install directory.
    /// </summary>
    public required Func<object, PluginManifest, string, IPclPlugin> CreatePlugin { get; init; }

    /// <summary>Extra manifest checks for this kind. Returns an error message, or null when valid.</summary>
    public Func<PluginManifest, string?>? ValidateManifest { get; init; }

    /// <summary>Runs the shared and kind-specific manifest checks.</summary>
    public string? Validate(PluginManifest manifest)
    {
        if (!string.Equals(manifest.Kind ?? PluginManifestKind.Plugin, ManifestKind, StringComparison.Ordinal))
            return $"Package kind '{manifest.Kind ?? PluginManifestKind.Plugin}' cannot be installed as {Name.ToLowerInvariant()}.";
        if (!AllowMixins && (!string.IsNullOrWhiteSpace(manifest.MixinConfig) ||
                             (manifest.MixinConfigs?.Count ?? 0) > 0 ||
                             (manifest.ExperimentalFeatures?.Count ?? 0) > 0))
            return $"{Name} packages may not contain Mixin configurations.";
        return ValidateManifest?.Invoke(manifest);
    }
}

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Markup;
using PCL.Plugin.SDK;

[assembly: XmlnsDefinition(PCL.Extension.SDK.ExtensionApiVersion.XamlNamespace, "PCL.Extension.SDK.Controls")]
[assembly: XmlnsPrefix(PCL.Extension.SDK.ExtensionApiVersion.XamlNamespace, "pcl")]

namespace PCL.Extension.SDK;

/// <summary>
///     Version of the stable extension API. Minor versions only add members; a host that supports
///     <c>1.N</c> runs every extension that declares <c>1.0</c> … <c>1.N</c>.
/// </summary>
public static class ExtensionApiVersion
{
    public const int Major = 1;
    public const int Minor = 0;
    public const string Current = "1.0";

    /// <summary>XAML namespace for the PCL control proxies: <c>xmlns:pcl="urn:pcl-lite:extension-sdk"</c>.</summary>
    public const string XamlNamespace = "urn:pcl-lite:extension-sdk";

    public static bool TryParse(string? value, out int major, out int minor)
    {
        major = minor = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 16) return false;
        var parts = value.Split('.');
        return parts.Length == 2 &&
               int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major) &&
               int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor);
    }

    /// <summary>Whether this host can run an extension that requires <paramref name="required" />.</summary>
    public static bool IsSupported(string? required) =>
        TryParse(required, out var major, out var minor) && major == Major && minor <= Minor;
}

/// <summary>Entry point implemented by a PCL Lite extension (<c>.pclext</c> package).</summary>
public interface IPclExtension
{
    Task StartAsync(IExtensionContext context, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Host API supplied to each extension. Extends the stable plugin API v1 (logging, storage,
///     settings, network, downloads, notifications, dialogs, events) with the extension host.
/// </summary>
public interface IExtensionContext : IPluginContext
{
    IExtensionHost Host { get; }
    IExtensionLocalization Localization { get; }
}

public interface IExtensionHost
{
    /// <summary>Extension API version implemented by the running launcher, e.g. <c>1.0</c>.</summary>
    string ApiVersion { get; }

    /// <summary>Whether the launcher process runs as administrator.</summary>
    bool IsElevated { get; }

    /// <summary>Adds this extension's entry to the launcher's Extensions page. Call from any thread.</summary>
    IExtensionSectionHandle RegisterSection(ExtensionSection section);

    /// <summary>Shows a launcher dialog and returns the 1-based index of the clicked button.</summary>
    Task<int> ShowMessageAsync(ExtensionMessage message, CancellationToken cancellationToken = default);

    /// <summary>Shows a text input dialog. Returns null when cancelled.</summary>
    Task<string?> InputAsync(ExtensionInput input, CancellationToken cancellationToken = default);

    /// <summary>Opens one of this extension's sections (and optionally a page inside it).</summary>
    void NavigateTo(string sectionId, string? pageId = null);

    /// <summary>Runs <paramref name="action" /> on the launcher UI thread.</summary>
    Task RunOnUiAsync(Action action);
}

public sealed record ExtensionMessage(string Title, string Text, IReadOnlyList<string> Buttons)
{
    /// <summary>Red warning styling.</summary>
    public bool IsWarning { get; init; }
}

public sealed record ExtensionInput(string Title, string Text)
{
    public string DefaultValue { get; init; } = "";
    public string HintText { get; init; } = "";
    public string? ConfirmText { get; init; }
    public string? CancelText { get; init; }
}

/// <summary>An extension's entry on the Extensions page: its sub-pages and an optional launcher card.</summary>
public sealed class ExtensionSection
{
    public required string Id { get; init; }
    public required string Title { get; init; }

    /// <summary>
    ///     Optional resource key (from the package's <c>lang</c> files) for the title. When set, the host
    ///     follows language changes; <see cref="Title" /> is the fallback.
    /// </summary>
    public string? TitleResourceKey { get; init; }

    /// <summary>Optional launcher icon name, e.g. <c>lucide/boxes</c>.</summary>
    public string? SvgIcon { get; init; }

    public required IReadOnlyList<ExtensionPage> Pages { get; init; }

    /// <summary>Optional status card and launch button rendered by the host in the left column.</summary>
    public ExtensionLauncherCard? Launcher { get; init; }
}

public sealed class ExtensionPage
{
    public required string Id { get; init; }
    public required string Title { get; init; }

    /// <summary>Optional resource key for the title; see <see cref="ExtensionSection.TitleResourceKey" />.</summary>
    public string? TitleResourceKey { get; init; }

    public string? SvgIcon { get; init; }

    /// <summary>Creates the right-hand page. Called once on the UI thread; the view is reused.</summary>
    public required Func<FrameworkElement> CreateView { get; init; }

    public PluginPageLifecycle? Lifecycle { get; init; }
}

public interface IExtensionSectionHandle : IDisposable
{
    ExtensionSection Section { get; }
}

public enum ExtensionStatusLevel
{
    Idle,
    Ready,
    Busy,
    Error
}

/// <summary>
///     Status card plus launch/stop button. Property changes may be made from any thread;
///     the host marshals them to the UI. The host always checks administrator permission
///     before invoking <see cref="OnLaunch" />.
/// </summary>
public sealed class ExtensionLauncherCard : INotifyPropertyChanged
{
    private string _statusTitle = "";
    private string _statusDetail = "";
    private ExtensionStatusLevel _level;
    private string _launchText = "";
    private string _stopText = "";
    private string _subtitle = "";
    private bool _canLaunch = true;
    private bool _isRunning;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusTitle { get => _statusTitle; set => Set(ref _statusTitle, value ?? ""); }
    public string StatusDetail { get => _statusDetail; set => Set(ref _statusDetail, value ?? ""); }
    public ExtensionStatusLevel Level { get => _level; set => Set(ref _level, value); }
    public string LaunchText { get => _launchText; set => Set(ref _launchText, value ?? ""); }
    public string StopText { get => _stopText; set => Set(ref _stopText, value ?? ""); }

    /// <summary>Small line under the launch button, e.g. the selected version.</summary>
    public string Subtitle { get => _subtitle; set => Set(ref _subtitle, value ?? ""); }

    public bool CanLaunch { get => _canLaunch; set => Set(ref _canLaunch, value); }

    /// <summary>While true the button shows <see cref="StopText" /> and invokes <see cref="OnStop" />.</summary>
    public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }

    public required Func<CancellationToken, Task> OnLaunch { get; init; }
    public Func<CancellationToken, Task>? OnStop { get; init; }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
///     Strings from the package's <c>lang/&lt;language&gt;.json</c> files (flat key → text maps).
///     The host also exposes them as application resources, so <c>{DynamicResource Key}</c> works in
///     extension XAML. Keys that already exist in the launcher are ignored; prefix keys with your
///     extension name.
/// </summary>
public interface IExtensionLocalization
{
    string LanguageCode { get; }
    string Text(string key);
    string Text(string key, params object?[] args);
    event Action? LanguageChanged;
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace PCL.Extension.SDK.Controls;

/// <summary>
///     Creates the launcher's real control for a proxy. Implemented by the launcher; extensions never
///     implement or call this.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IPclControlFactory
{
    /// <summary>
    ///     Builds the real control, applies the proxy's current values and keeps it in sync through
    ///     <see cref="PclControl.ProxyPropertyChanged" />. Returns null for an unknown proxy type.
    /// </summary>
    FrameworkElement? Create(PclControl proxy);
}

/// <summary>Registration point for the launcher's control factory.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PclControlHost
{
    private static IPclControlFactory? _factory;

    internal static IPclControlFactory? Factory => Volatile.Read(ref _factory);

    /// <summary>Called once by the launcher during startup.</summary>
    public static void SetFactory(IPclControlFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (Interlocked.CompareExchange(ref _factory, factory, null) is not null)
            throw new InvalidOperationException("The PCL control factory is already registered.");
    }
}

/// <summary>
///     Base class of every PCL control proxy. The proxy only declares stable properties and events;
///     the launcher supplies the real control as <see cref="ContentControl.Content" />. Extensions
///     must not set <c>Content</c> on a proxy — use the proxy's own content property instead.
/// </summary>
public abstract class PclControl : ContentControl
{
    protected PclControl()
    {
        Focusable = false;
        IsTabStop = false;
        // Size set on the proxy (Height, Width, alignment) must apply to the real control.
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>Raised when one of the proxy's own properties changes. Used by the launcher to forward values.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public event EventHandler<DependencyPropertyChangedEventArgs>? ProxyPropertyChanged;

    /// <summary>The launcher's real control, or a placeholder when running outside the launcher.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public FrameworkElement? Inner { get; private set; }

    /// <summary>Concrete proxies call this as the last statement of their constructor.</summary>
    protected void CreateInner()
    {
        Inner = PclControlHost.Factory?.Create(this) ?? new TextBlock
        {
            Text = $"[{GetType().Name}]",
            Opacity = 0.6
        };
        Content = Inner;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (Inner is not null && typeof(PclControl).IsAssignableFrom(e.Property.OwnerType))
            ProxyPropertyChanged?.Invoke(this, e);
    }
}

public enum PclButtonColor
{
    Normal,
    Highlight,
    Red
}

public enum PclIconTheme
{
    Color,
    White,
    Black,
    Red
}

public enum PclHintTheme
{
    Blue,
    Red,
    Yellow
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

namespace PCL.Extension.SDK.Controls;

// Every proxy follows the same shape: stable dependency properties, CLR events, and a
// [EditorBrowsable(Never)] Raise* method the launcher calls when the real control fires.
// Add new members only; never rename or remove one (see docs/EXTENSIONS.md).

public sealed class Button : PclControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(Button), new PropertyMetadata(""));

    public static readonly DependencyProperty ColorTypeProperty =
        DependencyProperty.Register(nameof(ColorType), typeof(PclButtonColor), typeof(Button),
            new PropertyMetadata(PclButtonColor.Normal));

    public Button() => CreateInner();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public PclButtonColor ColorType { get => (PclButtonColor)GetValue(ColorTypeProperty); set => SetValue(ColorTypeProperty, value); }

    public event RoutedEventHandler? Click;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RaiseClick() => Click?.Invoke(this, new RoutedEventArgs());
}

public sealed class TextButton : PclControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextButton), new PropertyMetadata(""));

    public TextButton() => CreateInner();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public event RoutedEventHandler? Click;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RaiseClick() => Click?.Invoke(this, new RoutedEventArgs());
}

public sealed class IconButton : PclControl
{
    public static readonly DependencyProperty SvgIconProperty =
        DependencyProperty.Register(nameof(SvgIcon), typeof(string), typeof(IconButton), new PropertyMetadata(""));

    public static readonly DependencyProperty LogoScaleProperty =
        DependencyProperty.Register(nameof(LogoScale), typeof(double), typeof(IconButton), new PropertyMetadata(1d));

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(PclIconTheme), typeof(IconButton),
            new PropertyMetadata(PclIconTheme.Color));

    public IconButton() => CreateInner();

    /// <summary>Launcher icon name, e.g. <c>lucide/refresh-cw</c>.</summary>
    public string SvgIcon { get => (string)GetValue(SvgIconProperty); set => SetValue(SvgIconProperty, value); }
    public double LogoScale { get => (double)GetValue(LogoScaleProperty); set => SetValue(LogoScaleProperty, value); }
    public PclIconTheme Theme { get => (PclIconTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }

    public event RoutedEventHandler? Click;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RaiseClick() => Click?.Invoke(this, new RoutedEventArgs());
}

public sealed class IconTextButton : PclControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(IconTextButton), new PropertyMetadata(""));

    public static readonly DependencyProperty SvgIconProperty =
        DependencyProperty.Register(nameof(SvgIcon), typeof(string), typeof(IconTextButton), new PropertyMetadata(""));

    public static readonly DependencyProperty LogoScaleProperty =
        DependencyProperty.Register(nameof(LogoScale), typeof(double), typeof(IconTextButton), new PropertyMetadata(1d));

    public IconTextButton() => CreateInner();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string SvgIcon { get => (string)GetValue(SvgIconProperty); set => SetValue(SvgIconProperty, value); }
    public double LogoScale { get => (double)GetValue(LogoScaleProperty); set => SetValue(LogoScaleProperty, value); }

    public event RoutedEventHandler? Click;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RaiseClick() => Click?.Invoke(this, new RoutedEventArgs());
}

[ContentProperty(nameof(Child))]
public sealed class Card : PclControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(""));

    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.Register(nameof(Child), typeof(UIElement), typeof(Card), new PropertyMetadata(null));

    public Card() => CreateInner();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public UIElement? Child { get => (UIElement?)GetValue(ChildProperty); set => SetValue(ChildProperty, value); }
}

public sealed class Hint : PclControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(Hint), new PropertyMetadata(""));

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(PclHintTheme), typeof(Hint),
            new PropertyMetadata(PclHintTheme.Blue));

    public Hint() => CreateInner();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public PclHintTheme Theme { get => (PclHintTheme)GetValue(ThemeProperty); set => SetValue(ThemeProperty, value); }
}

public sealed class CheckBox : PclControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(CheckBox), new PropertyMetadata(""));

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(CheckBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public CheckBox() => CreateInner();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>Raised only when the user toggles the box, not when code sets <see cref="IsChecked" />.</summary>
    public event RoutedEventHandler? Change;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RaiseChange() => Change?.Invoke(this, new RoutedEventArgs());
}

public sealed class ListItem : PclControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ListItem), new PropertyMetadata(""));

    public static readonly DependencyProperty InfoProperty =
        DependencyProperty.Register(nameof(Info), typeof(string), typeof(ListItem), new PropertyMetadata(""));

    public static readonly DependencyProperty SvgIconProperty =
        DependencyProperty.Register(nameof(SvgIcon), typeof(string), typeof(ListItem), new PropertyMetadata(""));

    public static readonly DependencyProperty LogoScaleProperty =
        DependencyProperty.Register(nameof(LogoScale), typeof(double), typeof(ListItem), new PropertyMetadata(1d));

    public ListItem() => CreateInner();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Info { get => (string)GetValue(InfoProperty); set => SetValue(InfoProperty, value); }
    public string SvgIcon { get => (string)GetValue(SvgIconProperty); set => SetValue(SvgIconProperty, value); }
    public double LogoScale { get => (double)GetValue(LogoScaleProperty); set => SetValue(LogoScaleProperty, value); }

    public event RoutedEventHandler? Click;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void RaiseClick() => Click?.Invoke(this, new RoutedEventArgs());
}

/// <summary>
///     PCL text box. Routed events of the real box (<c>KeyDown</c>, <c>TextBoxBase.TextChanged</c>, …)
///     bubble through the proxy, so ordinary WPF event attributes work.
/// </summary>
public sealed class TextBox : PclControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextBox),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty HintTextProperty =
        DependencyProperty.Register(nameof(HintText), typeof(string), typeof(TextBox), new PropertyMetadata(""));

    public static readonly DependencyProperty MaxLengthProperty =
        DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(TextBox), new PropertyMetadata(0));

    public static readonly DependencyProperty AcceptsReturnProperty =
        DependencyProperty.Register(nameof(AcceptsReturn), typeof(bool), typeof(TextBox), new PropertyMetadata(false));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TextBox), new PropertyMetadata(false));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(TextBox),
            new PropertyMetadata(TextWrapping.NoWrap));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(TextBox),
            new PropertyMetadata(ScrollBarVisibility.Hidden));

    public TextBox() => CreateInner();

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string HintText { get => (string)GetValue(HintTextProperty); set => SetValue(HintTextProperty, value); }
    public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }
    public bool AcceptsReturn { get => (bool)GetValue(AcceptsReturnProperty); set => SetValue(AcceptsReturnProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public TextWrapping TextWrapping { get => (TextWrapping)GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>Raised after <see cref="Text" /> changes, whether by typing or by code.</summary>
    public event TextChangedEventHandler TextChanged
    {
        add => AddHandler(TextBoxBase.TextChangedEvent, value);
        remove => RemoveHandler(TextBoxBase.TextChangedEvent, value);
    }

    /// <summary>Moves keyboard focus into the real text box.</summary>
    public new bool Focus() => Inner?.Focus() ?? false;
}

/// <summary>An option of a <see cref="ComboBox" />. Plain data: the launcher creates the visual item.</summary>
public sealed class ComboBoxItem : DependencyObject
{
    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(ComboBoxItem), new PropertyMetadata(null));

    public static readonly DependencyProperty TagProperty =
        DependencyProperty.Register(nameof(Tag), typeof(object), typeof(ComboBoxItem), new PropertyMetadata(null));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ComboBoxItem), new PropertyMetadata(false));

    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>Only read when the combo box is created, to pick the initial selection from XAML.</summary>
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
}

[ContentProperty(nameof(Items))]
public sealed class ComboBox : PclControl
{
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(ComboBox),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(ComboBox), new PropertyMetadata(false));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(ComboBox),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public ComboBox()
    {
        // Items must exist before the launcher builds the real control.
        Items = [];
        CreateInner();
    }

    /// <summary>Options. Changes to the collection are reflected immediately.</summary>
    public ObservableCollection<ComboBoxItem> Items { get; }

    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public bool IsEditable { get => (bool)GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <summary>Typed text when <see cref="IsEditable" /> is true.</summary>
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public ComboBoxItem? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    /// <summary>Raised whenever <see cref="SelectedIndex" /> changes (by the user or by code).</summary>
    public event EventHandler? SelectionChanged;

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ComboBox)d).SelectionChanged?.Invoke(d, EventArgs.Empty);
}

[ContentProperty(nameof(Child))]
public sealed class ScrollViewer : PclControl
{
    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.Register(nameof(Child), typeof(UIElement), typeof(ScrollViewer), new PropertyMetadata(null));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer),
            new PropertyMetadata(ScrollBarVisibility.Auto));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer),
            new PropertyMetadata(ScrollBarVisibility.Disabled));

    public ScrollViewer() => CreateInner();

    public UIElement? Child { get => (UIElement?)GetValue(ChildProperty); set => SetValue(ChildProperty, value); }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>Scrolls the real viewer to the top.</summary>
    public void ScrollToTop() => (Inner as System.Windows.Controls.ScrollViewer)?.ScrollToTop();
}

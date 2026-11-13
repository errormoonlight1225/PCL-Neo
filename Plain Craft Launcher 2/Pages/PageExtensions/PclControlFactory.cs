using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.Logging;
using Sdk = PCL.Extension.SDK.Controls;

namespace PCL;

/// <summary>
///     Builds the launcher's real controls for the extension SDK's control proxies and keeps them in
///     sync. This is the only place that knows both sides: when an internal control changes, only the
///     mapping here needs updating, and extensions keep working unchanged.
/// </summary>
internal sealed class PclControlFactory : Sdk.IPclControlFactory
{
    public FrameworkElement? Create(Sdk.PclControl proxy)
    {
        try
        {
            return proxy switch
            {
                Sdk.Button button => CreateButton(button),
                Sdk.TextButton textButton => CreateTextButton(textButton),
                Sdk.IconButton iconButton => CreateIconButton(iconButton),
                Sdk.IconTextButton iconTextButton => CreateIconTextButton(iconTextButton),
                Sdk.Card card => CreateCard(card),
                Sdk.Hint hint => CreateHint(hint),
                Sdk.CheckBox checkBox => CreateCheckBox(checkBox),
                Sdk.ListItem listItem => CreateListItem(listItem),
                Sdk.TextBox textBox => CreateTextBox(textBox),
                Sdk.ComboBox comboBox => CreateComboBox(comboBox),
                Sdk.ScrollViewer scrollViewer => CreateScrollViewer(scrollViewer),
                _ => null
            };
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Extension", $"无法创建扩展控件：{proxy.GetType().Name}");
            return null;
        }
    }

    /// <summary>Applies every mapped property now and again whenever the proxy's value changes.</summary>
    private static void Bind(Sdk.PclControl proxy, params (DependencyProperty Property, Action Apply)[] mappings)
    {
        foreach (var mapping in mappings) mapping.Apply();
        proxy.ProxyPropertyChanged += (_, e) =>
        {
            foreach (var mapping in mappings)
                if (mapping.Property == e.Property) mapping.Apply();
        };
    }

    private static MyButton CreateButton(Sdk.Button proxy)
    {
        var real = new MyButton();
        Bind(proxy,
            (Sdk.Button.TextProperty, () => real.Text = proxy.Text),
            (Sdk.Button.ColorTypeProperty, () => real.ColorType = proxy.ColorType switch
            {
                Sdk.PclButtonColor.Highlight => MyButton.ColorState.Highlight,
                Sdk.PclButtonColor.Red => MyButton.ColorState.Red,
                _ => MyButton.ColorState.Normal
            }));
        real.Click += (_, _) => proxy.RaiseClick();
        return real;
    }

    private static MyTextButton CreateTextButton(Sdk.TextButton proxy)
    {
        var real = new MyTextButton();
        Bind(proxy, (Sdk.TextButton.TextProperty, () => real.Text = proxy.Text));
        real.Click += (_, _) => proxy.RaiseClick();
        return real;
    }

    private static MyIconButton CreateIconButton(Sdk.IconButton proxy)
    {
        var real = new MyIconButton();
        Bind(proxy,
            (Sdk.IconButton.SvgIconProperty, () => real.SvgIcon = proxy.SvgIcon),
            (Sdk.IconButton.LogoScaleProperty, () => real.LogoScale = proxy.LogoScale),
            (Sdk.IconButton.ThemeProperty, () => real.Theme = proxy.Theme switch
            {
                Sdk.PclIconTheme.White => MyIconButton.Themes.White,
                Sdk.PclIconTheme.Black => MyIconButton.Themes.Black,
                Sdk.PclIconTheme.Red => MyIconButton.Themes.Red,
                _ => MyIconButton.Themes.Color
            }));
        real.Click += (_, _) => proxy.RaiseClick();
        return real;
    }

    private static MyIconTextButton CreateIconTextButton(Sdk.IconTextButton proxy)
    {
        var real = new MyIconTextButton();
        Bind(proxy,
            (Sdk.IconTextButton.TextProperty, () => real.Text = proxy.Text),
            (Sdk.IconTextButton.SvgIconProperty, () => real.SvgIcon = proxy.SvgIcon),
            (Sdk.IconTextButton.LogoScaleProperty, () => real.LogoScale = proxy.LogoScale));
        real.Click += (_, _) => proxy.RaiseClick();
        return real;
    }

    /// <summary>The card supplies the standard inner padding, so extensions never hard-code it.</summary>
    private static MyCard CreateCard(Sdk.Card proxy)
    {
        var real = new MyCard();
        var content = new Border();
        real.Children.Add(content);
        Bind(proxy,
            (Sdk.Card.TitleProperty, () =>
            {
                real.Title = proxy.Title;
                content.Margin = string.IsNullOrWhiteSpace(proxy.Title)
                    ? new Thickness(20, 17, 20, 17)
                    : new Thickness(20, 40, 20, 18);
            }),
            (Sdk.Card.ChildProperty, () => content.Child = proxy.Child));
        return real;
    }

    private static MyHint CreateHint(Sdk.Hint proxy)
    {
        var real = new MyHint();
        Bind(proxy,
            (Sdk.Hint.TextProperty, () => real.Text = proxy.Text),
            (Sdk.Hint.ThemeProperty, () => real.Theme = proxy.Theme switch
            {
                Sdk.PclHintTheme.Red => MyHint.Themes.Red,
                Sdk.PclHintTheme.Yellow => MyHint.Themes.Yellow,
                _ => MyHint.Themes.Blue
            }));
        return real;
    }

    private static MyCheckBox CreateCheckBox(Sdk.CheckBox proxy)
    {
        var real = new MyCheckBox();
        Bind(proxy,
            (Sdk.CheckBox.TextProperty, () => real.Text = proxy.Text),
            (Sdk.CheckBox.IsCheckedProperty, () =>
            {
                if (real.Checked != proxy.IsChecked) real.Checked = proxy.IsChecked;
            }));
        real.Change += (_, user) =>
        {
            if (!user) return;
            proxy.IsChecked = real.Checked == true;
            proxy.RaiseChange();
        };
        return real;
    }

    private static MyListItem CreateListItem(Sdk.ListItem proxy)
    {
        var real = new MyListItem { Type = MyListItem.CheckType.Clickable, MinPaddingRight = 35 };
        Bind(proxy,
            (Sdk.ListItem.TitleProperty, () => real.Title = proxy.Title),
            (Sdk.ListItem.InfoProperty, () => real.Info = proxy.Info),
            (Sdk.ListItem.SvgIconProperty, () => real.SvgIcon = proxy.SvgIcon),
            (Sdk.ListItem.LogoScaleProperty, () => real.LogoScale = proxy.LogoScale));
        real.Click += (_, _) => proxy.RaiseClick();
        return real;
    }

    private static MyTextBox CreateTextBox(Sdk.TextBox proxy)
    {
        var real = new MyTextBox();
        var syncing = false;
        Bind(proxy,
            (Sdk.TextBox.TextProperty, () =>
            {
                if (syncing || real.Text == proxy.Text) return;
                real.Text = proxy.Text ?? "";
            }),
            (Sdk.TextBox.HintTextProperty, () => real.HintText = proxy.HintText),
            (Sdk.TextBox.MaxLengthProperty, () => real.MaxLength = proxy.MaxLength),
            (Sdk.TextBox.AcceptsReturnProperty, () => real.AcceptsReturn = proxy.AcceptsReturn),
            (Sdk.TextBox.IsReadOnlyProperty, () => real.IsReadOnly = proxy.IsReadOnly),
            (Sdk.TextBox.TextWrappingProperty, () => real.TextWrapping = proxy.TextWrapping),
            (Sdk.TextBox.VerticalScrollBarVisibilityProperty,
                () => real.VerticalScrollBarVisibility = proxy.VerticalScrollBarVisibility));
        // Runs before the routed event bubbles to handlers on the proxy, so they read the new text.
        real.TextChanged += (_, _) =>
        {
            if (proxy.Text == real.Text) return;
            syncing = true;
            try { proxy.Text = real.Text; }
            finally { syncing = false; }
        };
        return real;
    }

    private static MyComboBox CreateComboBox(Sdk.ComboBox proxy)
    {
        var real = new MyComboBox();
        var syncing = false;

        void RebuildItems()
        {
            syncing = true;
            try
            {
                real.Items.Clear();
                foreach (var item in proxy.Items)
                    real.Items.Add(new MyComboBoxItem { Content = item.Content, Tag = item.Tag });
                if (proxy.SelectedIndex < 0)
                {
                    var initial = proxy.Items.ToList().FindIndex(item => item.IsSelected);
                    if (initial >= 0) proxy.SelectedIndex = initial;
                }
                real.SelectedIndex = proxy.SelectedIndex < real.Items.Count ? proxy.SelectedIndex : -1;
            }
            finally { syncing = false; }
        }

        RebuildItems();
        proxy.Items.CollectionChanged += (_, _) => RebuildItems();
        Bind(proxy,
            (Sdk.ComboBox.SelectedIndexProperty, () =>
            {
                if (syncing || real.SelectedIndex == proxy.SelectedIndex) return;
                real.SelectedIndex = proxy.SelectedIndex < real.Items.Count ? proxy.SelectedIndex : -1;
            }),
            (Sdk.ComboBox.IsEditableProperty, () => real.IsEditable = proxy.IsEditable),
            (Sdk.ComboBox.TextProperty, () =>
            {
                if (syncing || real.Text == proxy.Text) return;
                real.Text = proxy.Text ?? "";
            }));
        real.SelectionChanged += (_, e) =>
        {
            // The real SelectionChanged bubbles; stop it so extensions only see the proxy event.
            e.Handled = true;
            if (syncing || proxy.SelectedIndex == real.SelectedIndex) return;
            syncing = true;
            try { proxy.SelectedIndex = real.SelectedIndex; }
            finally { syncing = false; }
        };
        real.TextChanged += (_, _) =>
        {
            if (syncing || proxy.Text == real.Text) return;
            syncing = true;
            try { proxy.Text = real.Text; }
            finally { syncing = false; }
        };
        return real;
    }

    private static MyScrollViewer CreateScrollViewer(Sdk.ScrollViewer proxy)
    {
        var real = new MyScrollViewer();
        Bind(proxy,
            (Sdk.ScrollViewer.ChildProperty, () => real.Content = proxy.Child),
            (Sdk.ScrollViewer.VerticalScrollBarVisibilityProperty,
                () => real.VerticalScrollBarVisibility = proxy.VerticalScrollBarVisibility),
            (Sdk.ScrollViewer.HorizontalScrollBarVisibilityProperty,
                () => real.HorizontalScrollBarVisibility = proxy.HorizontalScrollBarVisibility));
        return real;
    }
}

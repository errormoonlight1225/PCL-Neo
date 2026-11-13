using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App.Localization;
using PCL.Core.Extensions;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Extension.SDK;

namespace PCL;

/// <summary>
///     Left column of the Extensions page, drawn by the launcher for every extension:
///     store + installed extensions, the selected extension's pages, its status card and the
///     launch button. Extensions describe this through <see cref="ExtensionSection" /> instead of
///     writing their own left column.
/// </summary>
internal sealed class PageExtensionsLeft : MyPageLeft
{
    public const string StoreKey = "store";

    private readonly StackPanel _sectionList = new();
    private readonly StackPanel _builtinList = new();
    private readonly TextBlock _pageHeading = new() { Margin = new Thickness(13, 12, 5, 3), Opacity = 0.6, FontSize = 12 };
    private readonly StackPanel _pageList = new();
    private readonly TextBlock _statusHeading = new();
    private readonly Border _statusCard = new();
    private readonly Ellipse _statusDot = new() { Width = 8, Height = 8, Margin = new Thickness(0, 4, 8, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _statusTitle = new() { FontSize = 13, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxHeight = 36 };
    private readonly TextBlock _statusDetail = new() { Margin = new Thickness(16, 4, 0, 0), MaxHeight = 60, LineHeight = 14 };
    private readonly Grid _launchArea = new() { Margin = new Thickness(0, 10, 0, 0) };
    private readonly MyButton _launchButton;
    private readonly TextBlock _launchSubtitle = new();
    private readonly Dictionary<string, MyPageRight> _pages = new(StringComparer.Ordinal);

    private ExtensionLauncherCard? _boundCard;
    private CancellationTokenSource? _launchCancellation;
    private bool _isUpdatingSelection;

    public PageExtensionsLeft()
    {
        Width = 260;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

        var panItem = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var heading = new TextBlock { Margin = new Thickness(13, 5, 5, 3), Opacity = 0.6, FontSize = 12 };
        heading.SetResourceReference(TextBlock.TextProperty, "Extensions.Left.Category");
        panItem.Children.Add(heading);
        panItem.Children.Add(_sectionList);
        panItem.Children.Add(_builtinList);
        panItem.Children.Add(_pageHeading);
        panItem.Children.Add(_pageList);

        _statusHeading.SetResourceReference(StyleProperty, "TahoeSidebarHeadingStyle");
        _statusHeading.SetResourceReference(TextBlock.TextProperty, "Extensions.Left.Status");
        _statusCard.SetResourceReference(StyleProperty, "TahoeSidebarSectionStyle");
        _statusDetail.SetResourceReference(StyleProperty, "TahoeSidebarPathStyle");
        _statusDetail.Margin = new Thickness(16, 4, 0, 0);
        _statusDetail.SetBinding(ToolTipProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = _statusDetail });
        _statusTitle.SetBinding(ToolTipProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = _statusTitle });
        var statusRow = new Grid();
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusRow.ColumnDefinitions.Add(new ColumnDefinition());
        statusRow.Children.Add(_statusDot);
        Grid.SetColumn(_statusTitle, 1);
        statusRow.Children.Add(_statusTitle);
        var statusStack = new StackPanel();
        statusStack.Children.Add(statusRow);
        statusStack.Children.Add(_statusDetail);
        _statusCard.Child = statusStack;
        panItem.Children.Add(_statusHeading);
        panItem.Children.Add(_statusCard);

        var scroll = new MyScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panItem
        };
        Children.Add(scroll);

        _launchButton = new MyButton
        {
            Height = 54,
            Margin = new Thickness(20, 0, 20, 0),
            ColorType = MyButton.ColorState.Highlight,
            Padding = new Thickness(30, 0, 30, 15)
        };
        _launchButton.Click += async (_, _) => await OnLaunchClickAsync();
        _launchSubtitle.Margin = new Thickness(35, 0, 35, 10);
        _launchSubtitle.IsHitTestVisible = false;
        _launchSubtitle.VerticalAlignment = VerticalAlignment.Bottom;
        _launchSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _launchSubtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _launchSubtitle.FontSize = 11;
        _launchSubtitle.SetResourceReference(StyleProperty, "TahoeLaunchSubtitleStyle");
        _launchArea.Children.Add(_launchButton);
        _launchArea.Children.Add(_launchSubtitle);
        SetRow(_launchArea, 1);
        Children.Add(_launchArea);

        AnimatedControl = panItem;
        Loaded += (_, _) =>
        {
            ExtensionSectionRegistry.Changed += OnRegistryChanged;
            RebuildSections();
        };
        Unloaded += (_, _) =>
        {
            ExtensionSectionRegistry.Changed -= OnRegistryChanged;
            BindCard(null);
        };
        RebuildSections();
    }

    /// <summary>The store, or <c>extensionId/sectionId</c>.</summary>
    public static string SelectedKey { get; private set; } = StoreKey;

    private static readonly Dictionary<string, string> SelectedPages = new(StringComparer.Ordinal);

    /// <summary>The right page for the current selection.</summary>
    public MyPageRight PageGet()
    {
        if (SelectedKey == StoreKey || ExtensionSectionRegistry.Find(SelectedKey) is not { } section)
            return ModMain.frmExtensionStore ??= new PageExtensionStore();
        var page = CurrentPage(section);
        var key = section.Key + "/" + page.Id;
        if (!_pages.TryGetValue(key, out var host)) _pages[key] = host = new ExtensionPageHost(section, page);
        return host;
    }

    /// <summary>Selects a section (and optionally one of its pages), switching the right column if visible.</summary>
    public void Select(string key, string? pageId = null)
    {
        if (key != StoreKey && ExtensionSectionRegistry.Find(key) is null) key = StoreKey;
        if (pageId is not null) SelectedPages[key] = pageId;
        var changed = SelectedKey != key || pageId is not null;
        SelectedKey = key;
        RefreshSelection();
        if (changed && ModMain.frmMain.pageCurrent.page == FormMain.PageType.Extensions && IsLoaded)
            SwitchRightPage(PageGet());
    }

    private static ExtensionPage CurrentPage(RegisteredExtensionSection section)
    {
        var pages = section.Section.Pages;
        return SelectedPages.TryGetValue(section.Key, out var id) && pages.FirstOrDefault(page => page.Id == id) is { } selected
            ? selected
            : pages[0];
    }

    private void OnRegistryChanged() => Dispatcher.BeginInvoke(() =>
    {
        RebuildSections();
        if (SelectedKey != StoreKey && ExtensionSectionRegistry.Find(SelectedKey) is null) Select(StoreKey);
    });

    private void RebuildSections()
    {
        _isUpdatingSelection = true;
        try
        {
            _sectionList.Children.Clear();
            _sectionList.Children.Add(CreateRadio(StoreKey, Lang.Text("Extensions.Left.Store"), "lucide/package-plus"));
            foreach (var section in ExtensionSectionRegistry.GetAll())
            {
                var item = CreateRadio(section.Key, section.Section.Title,
                    string.IsNullOrWhiteSpace(section.Section.SvgIcon) ? "lucide/boxes" : section.Section.SvgIcon);
                BindTitle(item, section.Section.Title, section.Section.TitleResourceKey);
                _sectionList.Children.Add(item);
            }

            // 第一阶段：基岩版与 Lunar 仍内置，入口直接跳到原来的页面。拆成扩展后删掉这两项。
            _builtinList.Children.Clear();
            _builtinList.Children.Add(CreateLink(Lang.Text("Main.Tab.Bedrock"), "lucide/boxes", FormMain.PageType.Bedrock));
            _builtinList.Children.Add(CreateLink("Lunar", "brand/lunar", FormMain.PageType.Lunar));
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        RefreshSelection();
    }

    private MyListItem CreateRadio(string key, string title, string icon)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox, Tag = key, Title = title, SvgIcon = icon, LogoScale = 0.85,
            Height = 36, MinPaddingRight = 35, IsScaleAnimationEnabled = false, VerticalAlignment = VerticalAlignment.Top
        };
        item.Check += (_, _) =>
        {
            if (!_isUpdatingSelection) Select(key);
        };
        return item;
    }

    /// <summary>Follows language changes when the extension supplied a resource key for the title.</summary>
    private static void BindTitle(MyListItem item, string fallback, string? resourceKey)
    {
        if (!string.IsNullOrWhiteSpace(resourceKey) && Application.Current?.TryFindResource(resourceKey) is string)
            item.SetResourceReference(MyListItem.TitleProperty, resourceKey);
        else
            item.Title = fallback;
    }

    private static MyListItem CreateLink(string title, string icon, FormMain.PageType target)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable, Title = title, SvgIcon = icon, LogoScale = 0.85,
            Height = 36, MinPaddingRight = 35, IsScaleAnimationEnabled = false, VerticalAlignment = VerticalAlignment.Top
        };
        item.Click += (_, _) => ModMain.frmMain.PageChange(target);
        return item;
    }

    private void RefreshSelection()
    {
        _isUpdatingSelection = true;
        try
        {
            foreach (var item in _sectionList.Children.OfType<MyListItem>())
                if (Equals(item.Tag, SelectedKey) && !item.Checked) item.SetChecked(true, false, false);

            var section = SelectedKey == StoreKey ? null : ExtensionSectionRegistry.Find(SelectedKey);
            _pageList.Children.Clear();
            var showPages = section is not null && section.Section.Pages.Count > 1;
            _pageHeading.Visibility = showPages ? Visibility.Visible : Visibility.Collapsed;
            if (section is not null && showPages)
            {
                if (!string.IsNullOrWhiteSpace(section.Section.TitleResourceKey) &&
                    Application.Current?.TryFindResource(section.Section.TitleResourceKey) is string)
                    _pageHeading.SetResourceReference(TextBlock.TextProperty, section.Section.TitleResourceKey);
                else
                    _pageHeading.Text = section.Section.Title;
                var current = CurrentPage(section);
                foreach (var page in section.Section.Pages)
                {
                    var item = new MyListItem
                    {
                        Type = MyListItem.CheckType.RadioBox, Title = page.Title,
                        SvgIcon = string.IsNullOrWhiteSpace(page.SvgIcon) ? "lucide/file" : page.SvgIcon,
                        LogoScale = 0.85, Height = 36, MinPaddingRight = 35, IsScaleAnimationEnabled = false
                    };
                    BindTitle(item, page.Title, page.TitleResourceKey);
                    var pageId = page.Id;
                    item.Check += (_, _) =>
                    {
                        if (!_isUpdatingSelection) Select(section.Key, pageId);
                    };
                    _pageList.Children.Add(item);
                    if (page.Id == current.Id) item.SetChecked(true, false, false);
                }
            }
            BindCard(section?.Section.Launcher);
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void BindCard(ExtensionLauncherCard? card)
    {
        if (!ReferenceEquals(_boundCard, card))
        {
            if (_boundCard is not null) _boundCard.PropertyChanged -= OnCardChanged;
            _boundCard = card;
            if (card is not null) card.PropertyChanged += OnCardChanged;
        }
        RefreshCard();
    }

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.BeginInvoke(RefreshCard);

    private void RefreshCard()
    {
        var card = _boundCard;
        var visible = card is null ? Visibility.Collapsed : Visibility.Visible;
        _statusHeading.Visibility = visible;
        _statusCard.Visibility = visible;
        _launchArea.Visibility = visible;
        if (card is null) return;

        _statusTitle.Text = card.StatusTitle;
        _statusDetail.Text = card.StatusDetail;
        _statusDetail.Visibility = string.IsNullOrEmpty(card.StatusDetail) ? Visibility.Collapsed : Visibility.Visible;
        _statusDot.SetResourceReference(Shape.FillProperty, card.Level switch
        {
            ExtensionStatusLevel.Ready => "ColorBrush3",
            ExtensionStatusLevel.Busy => "ColorBrush4",
            ExtensionStatusLevel.Error => "ColorBrushRedLight",
            _ => "ColorBrushGray4"
        });
        _launchSubtitle.Text = card.Subtitle;
        if (card.IsRunning && card.OnStop is not null)
        {
            _launchButton.Text = string.IsNullOrEmpty(card.StopText) ? Lang.Text("Extensions.Left.Stop") : card.StopText;
            _launchButton.ColorType = MyButton.ColorState.Red;
            _launchButton.IsEnabled = true;
        }
        else
        {
            _launchButton.Text = string.IsNullOrEmpty(card.LaunchText) ? Lang.Text("Extensions.Left.Launch") : card.LaunchText;
            _launchButton.ColorType = MyButton.ColorState.Highlight;
            _launchButton.IsEnabled = card.CanLaunch && !card.IsRunning;
        }
    }

    private async Task OnLaunchClickAsync()
    {
        var card = _boundCard;
        if (card is null) return;
        var stopping = card.IsRunning && card.OnStop is not null;
        // 扩展权限很高：启动前一律检查管理员身份，未提权时询问是否以管理员身份重启
        if (!stopping && !ModExtensions.EnsureElevatedForLaunch()) return;
        _launchCancellation?.Cancel();
        _launchCancellation = new CancellationTokenSource();
        try
        {
            if (stopping) await card.OnStop!(_launchCancellation.Token);
            else await card.OnLaunch(_launchCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Extension", $"扩展{(stopping ? "停止" : "启动")}回调失败：{SelectedKey}");
            HintService.Hint(Lang.Text("Extensions.Left.LaunchFailed", ex.Message), HintType.Error);
        }
    }

    /// <summary>Same transition as the built-in left pages use when switching sub-pages.</summary>
    private static void SwitchRightPage(MyPageRight target)
    {
        if (ModMain.frmMain.pageCurrent.page != FormMain.PageType.Extensions) return;
        if (ReferenceEquals(ModMain.frmMain.PanMainRight.Child, target)) return;
        ModAnimation.AniStop("FrmMain PageChangeRight");
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }
}

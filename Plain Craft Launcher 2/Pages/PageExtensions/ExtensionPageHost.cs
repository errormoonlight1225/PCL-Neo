using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Extensions;
using PCL.Core.Logging;
using PCL.Extension.SDK;

namespace PCL;

/// <summary>
///     Right-hand page that hosts one extension page: standard PCL page chrome and scrolling, lazy view
///     creation, and the page's shown/hidden callbacks.
/// </summary>
internal sealed class ExtensionPageHost : MyPageRight
{
    private readonly RegisteredExtensionSection _section;
    private readonly ExtensionPage _page;
    private readonly Border _container;
    private bool _created;

    public ExtensionPageHost(RegisteredExtensionSection section, ExtensionPage page)
    {
        _section = section;
        _page = page;
        _container = new Border { Margin = new Thickness(25, 10, 25, 24) };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _container
        };
        Child = scroll;
        PanScroll = scroll;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string Key => _section.Key + "/" + _page.Id;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_created)
        {
            _created = true;
            try
            {
                _container.Child = _page.CreateView();
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Extension", $"扩展页面创建失败：{Key}");
                _container.Child = PluginPageSupport.Text(Lang.Text("Extensions.Page.CreateFailed", ex.Message));
            }
        }
        _ = InvokeLifecycleAsync(_page.Lifecycle?.OnShown, "shown");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        _ = InvokeLifecycleAsync(_page.Lifecycle?.OnHidden, "hidden");

    private async Task InvokeLifecycleAsync(Func<CancellationToken, Task>? callback, string name)
    {
        if (callback is null) return;
        try { await callback(CancellationToken.None); }
        catch (Exception ex) { LogWrapper.Warn(ex, "Extension", $"扩展页面 {name} 回调失败：{Key}"); }
    }
}

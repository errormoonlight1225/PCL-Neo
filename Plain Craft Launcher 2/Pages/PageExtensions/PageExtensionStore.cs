using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PCL.Core.App.Localization;
using PCL.Core.Extensions;
using PCL.Core.Logging;
using PCL.Core.Plugins;
using PCL.Plugin.SDK;

namespace PCL;

/// <summary>Extension store: available extensions from every software source, installed extensions, and the source list.</summary>
internal sealed class PageExtensionStore : MyPageRight
{
    private readonly TextBlock _status;
    private readonly StackPanel _content;
    private bool _refreshing;

    public PageExtensionStore()
    {
        var root = PluginPageSupport.CreateRoot(Lang.Text("Extensions.Store.Title"), out _status, out _content);
        root.Children.Insert(1, PluginPageSupport.CreateCard(new TextBlock
        {
            Text = Lang.Text("Extensions.Store.Description"), TextWrapping = TextWrapping.Wrap, LineHeight = 20, Opacity = 0.9
        }));
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
        actions.Children.Add(PluginPageSupport.Button(Lang.Text("Extensions.Store.Refresh"), async (_, _) => await RefreshAsync()));
        actions.Children.Add(PluginPageSupport.Button(Lang.Text("Extensions.Store.InstallFile"), async (_, _) => await InstallFileAsync()));
        actions.Children.Add(PluginPageSupport.Button(Lang.Text("Extensions.Source.Add"), async (_, _) => await AddSourceAsync(), true));
        root.Children.Insert(2, actions);
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root
        };
        Child = scroll;
        PanScroll = scroll;
        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (_refreshing || PluginPageSupport.IsInstallationInProgress) return;
        var manager = ExtensionManagerService.Manager;
        var sources = ExtensionManagerService.Sources;
        if (manager is null || sources is null)
        {
            _status.Text = Lang.Text("Extensions.Store.NotRunning");
            return;
        }
        _refreshing = true;
        _status.Text = Lang.Text("Extensions.Store.Loading");
        try
        {
            var snapshot = await ExtensionManagerService.LoadStoreAsync();
            _content.Children.Clear();
            AddAvailable(snapshot);
            AddInstalled(manager, snapshot);
            AddSources(sources, snapshot);
            _status.Text = PluginRuntimeManager.IsSafeMode ? Lang.Text("Extensions.Store.SafeMode") : "";
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Extension", "扩展商店加载失败");
            _status.Text = Lang.Text("Extensions.Store.LoadFailed", ex.Message);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void AddAvailable(ExtensionStoreSnapshot snapshot)
    {
        _content.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Store.Available"), 16, true));
        if (snapshot.Items.Count == 0)
        {
            _content.Children.Add(PluginPageSupport.CreateCard(PluginPageSupport.Text(Lang.Text("Extensions.Store.Empty"))));
            return;
        }
        foreach (var item in snapshot.Items)
        {
            var stack = new StackPanel();
            stack.Children.Add(PluginPageSupport.Text(
                $"{DisplayName(item.Entry)}  ·  {item.Entry.Author}  ·  {item.Entry.Version}", 12));
            stack.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Store.FromSource",
                ExtensionStoreSupport.DescribeSource(item.Source)), 12));
            stack.Children.Add(PluginPageSupport.Text(Description(item.Entry)));
            var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            if (!item.Entry.IsCompatible)
                row.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Store.Incompatible", item.Entry.ExtensionApi), 12));
            else if (item.Kind == PluginMarketUpdateKind.Current)
                row.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Store.Current"), 12));
            else
            {
                var label = item.Kind switch
                {
                    PluginMarketUpdateKind.UpdateAvailable => Lang.Text("Extensions.Store.Update"),
                    PluginMarketUpdateKind.DowngradeAvailable => Lang.Text("Extensions.Store.Downgrade"),
                    _ => Lang.Text("Extensions.Store.Install")
                };
                MyButton button = null!;
                button = PluginPageSupport.Button(label, async (_, _) =>
                {
                    button.IsEnabled = false;
                    var done = await ExtensionStoreSupport.InstallFromStoreAsync(item, text => _status.Text = text);
                    if (done) button.Text = Lang.Text("Extensions.Store.PendingRestart");
                    else button.IsEnabled = true;
                }, true);
                row.Children.Add(button);
            }
            stack.Children.Add(row);
            _content.Children.Add(PluginPageSupport.CreateCard(stack, DisplayName(item.Entry)));
        }
    }

    private void AddInstalled(PluginRuntimeManager manager, ExtensionStoreSnapshot snapshot)
    {
        var installed = manager.GetInstalled();
        _content.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Store.Installed"), 16, true));
        if (installed.Count == 0)
        {
            _content.Children.Add(PluginPageSupport.CreateCard(PluginPageSupport.Text(Lang.Text("Extensions.Store.NoneInstalled"))));
            return;
        }
        foreach (var plugin in installed)
        {
            var stack = new StackPanel();
            stack.Children.Add(PluginPageSupport.Text(
                $"{plugin.Manifest.Name}  ·  {plugin.Manifest.Author}  ·  {plugin.Manifest.Version}", 12));
            stack.Children.Add(PluginPageSupport.Text(InstalledSource(plugin, snapshot), 12));
            var state = plugin.Error is not null
                ? Lang.Text("Extensions.Store.StateError", plugin.Error)
                : plugin.PendingRestart
                    ? Lang.Text("Extensions.Store.PendingRestart")
                    : plugin.Running
                        ? Lang.Text("Extensions.Store.StateRunning")
                        : plugin.Enabled
                            ? Lang.Text("Extensions.Store.StateEnabled")
                            : Lang.Text("Extensions.Store.StateDisabled");
            stack.Children.Add(PluginPageSupport.Text(state, 12));
            var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var id = plugin.Manifest.Id;
            row.Children.Add(PluginPageSupport.Button(
                plugin.Enabled ? Lang.Text("Extensions.Store.Disable") : Lang.Text("Extensions.Store.Enable"),
                async (_, _) => await ChangeAsync(() => manager.SetEnabledAsync(id, !plugin.Enabled))));
            var uninstall = PluginPageSupport.Button(Lang.Text("Extensions.Store.Uninstall"), async (_, _) =>
            {
                if (ModMain.MyMsgBox(Lang.Text("Extensions.Store.UninstallConfirm", plugin.Manifest.Name),
                        Lang.Text("Extensions.Store.Uninstall"), Lang.Text("Extensions.Store.Uninstall"),
                        Lang.Text("Common.Action.Cancel"), isWarn: true) != 1)
                    return;
                await ChangeAsync(() => manager.UninstallAsync(id));
            });
            uninstall.ColorType = MyButton.ColorState.Red;
            row.Children.Add(uninstall);
            stack.Children.Add(row);
            _content.Children.Add(PluginPageSupport.CreateCard(stack, plugin.Manifest.Name));
        }
    }

    private void AddSources(ExtensionSourceStore sources, ExtensionStoreSnapshot snapshot)
    {
        _content.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Source.Title"), 16, true));
        var stack = new StackPanel();
        foreach (var source in sources.GetAll())
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = new StackPanel();
            info.Children.Add(PluginPageSupport.Text(ExtensionStoreSupport.DescribeSource(source), 13, true));
            if (snapshot.SourceErrors.TryGetValue(source.Url, out var error))
                info.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Source.Error", error), 12));
            row.Children.Add(info);
            if (!source.IsOfficial)
            {
                var url = source.Url;
                var remove = PluginPageSupport.Button(Lang.Text("Extensions.Source.Remove"), async (_, _) =>
                {
                    if (ModMain.MyMsgBox(Lang.Text("Extensions.Source.RemoveConfirm", url), Lang.Text("Extensions.Source.Remove"),
                            Lang.Text("Extensions.Source.Remove"), Lang.Text("Common.Action.Cancel")) != 1)
                        return;
                    sources.Remove(url);
                    await RefreshAsync();
                });
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
            }
            stack.Children.Add(row);
        }
        stack.Children.Add(PluginPageSupport.Text(Lang.Text("Extensions.Source.SelfHostHint"), 12));
        _content.Children.Add(PluginPageSupport.CreateCard(stack));
    }

    private async Task AddSourceAsync()
    {
        var sources = ExtensionManagerService.Sources;
        if (sources is null) return;
        if (ModMain.MyMsgBox(Lang.Text("Extensions.Source.AddWarning"), Lang.Text("Extensions.Source.AddTitle"),
                Lang.Text("Extensions.Source.AddContinue"), Lang.Text("Common.Action.Cancel"), isWarn: true) != 1)
            return;
        var url = ModMain.MyMsgBoxInput(Lang.Text("Extensions.Source.AddTitle"), Lang.Text("Extensions.Source.AddPrompt"),
            hintText: "https://example.com/pcl/extensions/index.json");
        if (string.IsNullOrWhiteSpace(url)) return;
        _status.Text = Lang.Text("Extensions.Source.Checking");
        try
        {
            var index = await ExtensionManagerService.PreviewSourceAsync(url);
            sources.Add(url, index.Name);
            PluginPageSupport.ShowPluginNotice(Lang.Text("Extensions.Source.Added", index.Name ?? url, index.Extensions.Count),
                PluginNotificationKind.Success);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Extension", $"添加软件源失败：{url}");
            ModMain.MyMsgBox(Lang.Text("Extensions.Source.AddFailed", ex.Message), Lang.Text("Extensions.Source.AddTitle"));
        }
        finally
        {
            _status.Text = "";
        }
        await RefreshAsync();
    }

    private async Task InstallFileAsync()
    {
        if (PluginPageSupport.IsInstallationInProgress) return;
        var extension = PluginPackage.ExtensionPackageExtension;
        var dialog = new OpenFileDialog
        {
            Filter = $"PCL extension package (*{extension})|*{extension}",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;
        await ExtensionStoreSupport.InstallLocalAsync(dialog.FileNames, text => _status.Text = text);
        await RefreshAsync();
    }

    private async Task ChangeAsync(Func<Task> change)
    {
        try
        {
            await change();
            PluginPageSupport.PromptRestartForPluginChanges(Lang.Text("Extensions.Store.Changed"));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Extension", "更改扩展状态失败");
            PluginPageSupport.ShowPluginNotice(Lang.Text("Extensions.Store.ChangeFailed", ex.Message), PluginNotificationKind.Error);
        }
        await RefreshAsync();
    }

    private static string InstalledSource(InstalledPlugin plugin, ExtensionStoreSnapshot snapshot)
    {
        if (plugin.InstallSourceType != ExtensionManagerService.SourceInstallType || plugin.InstallSourceUrl is null)
            return Lang.Text("Extensions.Store.FromSource", Lang.Text("Extensions.Store.Source.LocalFile"));
        var source = ExtensionManagerService.Sources?.GetAll()
            .FirstOrDefault(item => string.Equals(item.Url, plugin.InstallSourceUrl, StringComparison.OrdinalIgnoreCase));
        return Lang.Text("Extensions.Store.FromSource", source is null
            ? Lang.Text("Extensions.Store.Source.Removed", plugin.InstallSourceUrl)
            : ExtensionStoreSupport.DescribeSource(source));
    }

    private static string DisplayName(ExtensionIndexEntry entry) =>
        entry.LocalizedNames.TryGetValue(LocalizationService.CurrentLanguage.Code, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : entry.Name;

    private static string Description(ExtensionIndexEntry entry) =>
        entry.LocalizedDescriptions.TryGetValue(LocalizationService.CurrentLanguage.Code, out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : entry.Description;
}

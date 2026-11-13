using System.Windows;
using System.Windows.Controls;
using PCL.Extension.SDK;
using Pcl = PCL.Extension.SDK.Controls;

namespace PCL.Extension.Sample;

/// <summary>Minimal extension: one section with two pages and a launcher card that simulates a launch.</summary>
public sealed class SampleExtension : IPclExtension
{
    private IExtensionContext? _context;
    private IExtensionSectionHandle? _section;
    private ExtensionLauncherCard? _card;
    private CancellationTokenSource? _running;

    public Task StartAsync(IExtensionContext context, CancellationToken cancellationToken)
    {
        _context = context;
        var text = context.Localization;
        _card = new ExtensionLauncherCard
        {
            OnLaunch = LaunchAsync,
            OnStop = _ =>
            {
                _running?.Cancel();
                return Task.CompletedTask;
            }
        };
        ApplyIdleState();
        // Text read from the card is not a DynamicResource, so refresh it when the language changes.
        text.LanguageChanged += () =>
        {
            if (_card.IsRunning) ApplyRunningState();
            else ApplyIdleState();
        };

        _section = context.Host.RegisterSection(new ExtensionSection
        {
            Id = "sample",
            Title = text.Text("Sample.Section"),
            TitleResourceKey = "Sample.Section",
            SvgIcon = "lucide/sparkles",
            Launcher = _card,
            Pages =
            [
                new ExtensionPage
                {
                    Id = "controls",
                    Title = text.Text("Sample.Page.Controls"),
                    TitleResourceKey = "Sample.Page.Controls",
                    SvgIcon = "lucide/puzzle",
                    CreateView = () => new ControlsPage(context)
                },
                new ExtensionPage
                {
                    Id = "about",
                    Title = text.Text("Sample.Page.About"),
                    TitleResourceKey = "Sample.Page.About",
                    SvgIcon = "lucide/sparkles",
                    CreateView = CreateAboutPage,
                    Lifecycle = new PCL.Plugin.SDK.PluginPageLifecycle(
                        OnShown: _ =>
                        {
                            context.Logger.Info("About page shown");
                            return Task.CompletedTask;
                        })
                }
            ]
        });
        context.Logger.Info($"Sample extension started (host API {context.Host.ApiVersion}, elevated: {context.Host.IsElevated})");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running?.Cancel();
        _section?.Dispose();
        return Task.CompletedTask;
    }

    private static FrameworkElement CreateAboutPage()
    {
        var body = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
        body.SetResourceReference(TextBlock.TextProperty, "Sample.About");
        var card = new Pcl.Card { Child = body };
        card.SetResourceReference(Pcl.Card.TitleProperty, "Sample.Card.About");
        return card;
    }

    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        if (_card is null || _context is null) return;
        _running = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _card.IsRunning = true;
        ApplyRunningState();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), _running.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _card.IsRunning = false;
            _card.StatusDetail = _context.Localization.Text("Sample.Status.Detail", DateTime.Now.ToString("T"));
            ApplyIdleState(keepDetail: true);
        }
    }

    private void ApplyIdleState(bool keepDetail = false)
    {
        if (_card is null || _context is null) return;
        var text = _context.Localization;
        _card.StatusTitle = text.Text("Sample.Status.Idle");
        if (!keepDetail) _card.StatusDetail = "";
        _card.Level = ExtensionStatusLevel.Ready;
        _card.LaunchText = text.Text("Sample.Launch");
        _card.StopText = text.Text("Sample.Stop");
        _card.Subtitle = "1.0.0";
    }

    private void ApplyRunningState()
    {
        if (_card is null || _context is null) return;
        _card.StatusTitle = _context.Localization.Text("Sample.Status.Running");
        _card.Level = ExtensionStatusLevel.Busy;
    }
}

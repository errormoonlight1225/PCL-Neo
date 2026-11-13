using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.Extensions;
using PCL.Core.Logging;
using PCL.Plugin.SDK;

namespace PCL;

/// <summary>Install flows shared by the store page and window drag-and-drop.</summary>
internal static class ExtensionStoreSupport
{
    /// <summary>Installs one entry from a software source. Must be called on the UI thread.</summary>
    public static async Task<bool> InstallFromStoreAsync(ExtensionStoreItem item, Action<string>? setStatus = null)
    {
        if (!ModExtensions.ConfirmPrivilegeNotice()) return false;
        if (!PluginPageSupport.TryBeginInstallation())
        {
            PluginPageSupport.ShowPluginNotice(Lang.Text("Plugin.Install.Busy"), PluginNotificationKind.Warning);
            return false;
        }
        setStatus?.Invoke(Lang.Text("Extensions.Install.Working"));
        PluginPackageCheck check;
        try
        {
            var sourceLabel = DescribeSource(item.Source);
            check = await Task.Run(() => ExtensionManagerService.InstallFromSourceAsync(item,
                (current, decision, _) => ConfirmAsync(current, decision, sourceLabel, item.Source.IsOfficial)));
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Extension", $"从软件源安装扩展失败：{item.Entry.Id}");
            PluginPageSupport.ShowPluginNotice(Lang.Text("Extensions.Install.Failed", ex.Message), PluginNotificationKind.Error);
            return false;
        }
        finally
        {
            PluginPageSupport.EndInstallation();
            setStatus?.Invoke("");
        }
        PluginPageSupport.PromptRestartForPluginChanges(
            (check.IsSigned ? Lang.Text("Plugin.Install.SignedNote") + " " : "") + Lang.Text("Extensions.Install.Done"));
        return true;
    }

    /// <summary>Installs local <c>.pclext</c> files one after another. Must be called on the UI thread.</summary>
    public static async Task InstallLocalAsync(IReadOnlyList<string> paths, Action<string>? setStatus = null)
    {
        if (paths.Count == 0 || !ModExtensions.ConfirmPrivilegeNotice()) return;
        if (!PluginPageSupport.TryBeginInstallation())
        {
            PluginPageSupport.ShowPluginNotice(Lang.Text("Plugin.Install.Busy"), PluginNotificationKind.Warning);
            return;
        }
        var installed = 0;
        var allSigned = true;
        setStatus?.Invoke(Lang.Text("Extensions.Install.Working"));
        try
        {
            foreach (var path in paths)
            {
                try
                {
                    var label = Lang.Text("Extensions.Install.SourceLocal", Path.GetFileName(path));
                    var check = await Task.Run(() => ExtensionManagerService.InstallLocalAsync(path,
                        (current, decision, _) => ConfirmAsync(current, decision, label, false)));
                    installed++;
                    allSigned &= check.IsSigned;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "Extension", $"安装本地扩展包失败：{path}");
                    var detail = paths.Count > 1 ? $"{Path.GetFileName(path)}: {ex.Message}" : ex.Message;
                    PluginPageSupport.ShowPluginNotice(Lang.Text("Extensions.Install.Failed", detail), PluginNotificationKind.Error);
                }
            }
        }
        finally
        {
            PluginPageSupport.EndInstallation();
            setStatus?.Invoke("");
        }
        if (installed == 0) return;
        PluginPageSupport.PromptRestartForPluginChanges(
            (allSigned ? Lang.Text("Plugin.Install.SignedNote") + " " : "") + Lang.Text("Extensions.Install.Done"));
    }

    public static string DescribeSource(ExtensionSource source) => source.IsOfficial
        ? Lang.Text("Extensions.Store.Source.Official")
        : $"{source.Name ?? Lang.Text("Extensions.Store.Source.Unnamed")} ({source.Url})";

    /// <summary>
    ///     One dialog: where the package comes from, that extensions run with the launcher's full
    ///     permissions, a red warning for unsigned packages, then the usual package/signer details.
    ///     Anything not from the official source is shown as a warning dialog.
    /// </summary>
    private static Task<bool> ConfirmAsync(PluginPackageCheck check, ExtensionTrustDecision decision,
        string sourceLabel, bool isOfficial)
    {
        var preface = Lang.Text("Extensions.Install.Preface", sourceLabel);
        if (decision.IsUnsigned) preface += "\n\n" + Lang.Text("Extensions.Install.UnsignedWarning");
        return PluginPageSupport.ConfirmAsync(PluginPageSupport.DescribeInstallation(check), preface,
            forceWarning: !isOfficial || decision.IsUnsigned);
    }
}

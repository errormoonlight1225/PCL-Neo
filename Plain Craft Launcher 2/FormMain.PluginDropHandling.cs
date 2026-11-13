using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public partial class FormMain
{
    /// <summary>
    /// Handles a drop that consists only of <c>.pclplugin</c> packages by routing them through the same
    /// verification, confirmation and restart flow as the plugin page's local install button.
    /// Mixed drops are left to the regular file-drop handler.
    /// </summary>
    private static bool TryInstallDroppedPlugins(IReadOnlyList<string> files)
    {
        if (files.Count == 0 || !files.All(PluginPageSupport.IsPluginPackagePath)) return false;
        var missing = files.FirstOrDefault(file => !File.Exists(file));
        if (missing is not null)
        {
            HintService.Hint(Lang.Text("Main.FileDrag.FileNotFound", missing), HintType.Error);
            return true;
        }

        ModBase.Log($"[System] 文件为插件包，尝试安装 {files.Count} 个插件");
        var packages = files.ToArray();
        ModBase.RunInUi(async () => await PluginPageSupport.InstallLocalPackagesAsync(packages));
        return true;
    }
}

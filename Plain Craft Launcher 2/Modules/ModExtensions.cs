using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;

namespace PCL;

/// <summary>
///     「扩展」板块：基岩版与 Lunar Client 共用顶栏上的一个按钮。
///     <para>
///         两个页面本身（<see cref="PageBedrockLeft" />、<see cref="PageLunarLeft" />）和
///         <see cref="FormMain.PageType.Bedrock" /> / <see cref="FormMain.PageType.Lunar" /> 都保持原样，
///         这里只负责：记住上次打开的是哪个、首次进入时的高权限提示、启动前的管理员权限检查。
///     </para>
///     <para>
///         扩展会下载并运行启动器之外的程序（基岩版安装包、Lunar Client 及其 Java 运行时），
///         所以它不能被隐藏或折叠，进入前要用户确认，启动时要求启动器以管理员身份运行。
///         Lunar 需要重定向游戏输出、GDK 版基岩版用 <c>UseShellExecute = false</c> 启动，
///         都没法单独对子进程走 <c>runas</c>，所以提权的方式是把整个启动器以管理员身份重启，
///         之后启动的游戏进程自然继承管理员权限。
///     </para>
/// </summary>
internal static class ModExtensions
{
    /// <summary>以管理员身份重启后，启动器读到这个参数就直接回到「扩展」。</summary>
    public const string OpenExtensionsArgument = "--open-extensions";

    /// <summary>ERROR_CANCELLED：用户在 UAC 弹窗里点了「否」。</summary>
    private const int ErrorCancelled = 1223;

    private static readonly Lazy<bool> _isElevated = new(() =>
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Extensions", "无法确定启动器是否以管理员身份运行，按未提权处理");
            return false;
        }
    });

    /// <summary>启动器当前是否以管理员身份运行。进程的权限在运行期间不会变，算一次即可。</summary>
    public static bool IsElevated => _isElevated.Value;

    public static bool IsExtensionPage(FormMain.PageType page) =>
        page is FormMain.PageType.Extensions or FormMain.PageType.Bedrock or FormMain.PageType.Lunar;

    /// <summary>「扩展」按钮要打开的页面：上次停留的页面，默认扩展商店。</summary>
    public static FormMain.PageType LastPage
    {
        get => Config.Preference.ExtensionsLastPage switch
        {
            (int)FormMain.PageType.Lunar => FormMain.PageType.Lunar,
            (int)FormMain.PageType.Bedrock => FormMain.PageType.Bedrock,
            _ => FormMain.PageType.Extensions
        };
        set
        {
            if (IsExtensionPage(value) && Config.Preference.ExtensionsLastPage != (int)value)
                Config.Preference.ExtensionsLastPage = (int)value;
        }
    }

    /// <summary>
    ///     首次进入扩展前的高权限提示。确认过一次就记住；取消则这次不进入，下次进入还会再问。
    ///     必须在 UI 线程调用。
    /// </summary>
    public static bool ConfirmPrivilegeNotice()
    {
        if (Config.Preference.ExtensionsPrivilegeAccepted) return true;
        var accepted = ModMain.MyMsgBox(Lang.Text("Extensions.Privilege.Message"),
            Lang.Text("Extensions.Privilege.Title"), Lang.Text("Extensions.Privilege.Accept"),
            Lang.Text("Common.Action.Cancel"), isWarn: true, forceWait: true) == 1;
        if (accepted) Config.Preference.ExtensionsPrivilegeAccepted = true;
        return accepted;
    }

    /// <summary>
    ///     扩展启动游戏前调用。已是管理员时返回 true；否则询问是否以管理员身份重启启动器，
    ///     并返回 false（这次启动取消，重启后由用户再点一次）。必须在 UI 线程调用。
    /// </summary>
    public static bool EnsureElevatedForLaunch()
    {
        if (IsElevated) return true;
        if (ModMain.MyMsgBox(Lang.Text("Extensions.Elevation.Message"), Lang.Text("Extensions.Elevation.Title"),
                Lang.Text("Extensions.Elevation.Restart"), Lang.Text("Common.Action.Cancel"),
                isWarn: true, forceWait: true) != 1)
            return false;

        try
        {
            var process = Process.Start(new ProcessStartInfo(Basics.ExecutablePath, OpenExtensionsArgument)
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            if (process is null) throw new InvalidOperationException("The launcher process could not be started.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            HintService.Hint(Lang.Text("Extensions.Elevation.Declined"), HintType.Info);
            return false;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Extensions", "无法以管理员身份重启启动器");
            ModMain.MyMsgBox(Lang.Text("Extensions.Elevation.Failed", ex.Message), Lang.Text("Extensions.Elevation.Title"),
                Lang.Text("Common.Action.Confirm"), forceWait: true);
            return false;
        }

        FormMain.EndProgramForce();
        return false;
    }

    /// <summary>扩展左栏顶部的「基岩版 / Lunar」切换。</summary>
    public static void SwitchTo(FormMain.PageType page)
    {
        if (!IsExtensionPage(page) || ModMain.frmMain.pageCurrent.page == page) return;
        ModMain.frmMain.PageChange(page);
    }

    /// <summary>扩展通过 SDK 请求打开自己的某个页面。</summary>
    public static void NavigateToSection(string extensionId, string sectionId, string? pageId) =>
        ModBase.RunInUi(() =>
        {
            ModMain.frmExtensionsLeft ??= new PageExtensionsLeft();
            ModMain.frmExtensionsLeft.Select(extensionId + "/" + sectionId, pageId);
            if (ModMain.frmMain.pageCurrent.page != FormMain.PageType.Extensions)
                ModMain.frmMain.PageChange(FormMain.PageType.Extensions);
        });

    /// <summary>基岩版 / Lunar 左栏顶部的「全部扩展」。</summary>
    public static void OpenHub() => ModMain.frmMain.PageChange(FormMain.PageType.Extensions);

    /// <summary>窗口加载完成后调用：以管理员身份重启而来时直接打开「扩展」。</summary>
    public static void OpenFromStartupArguments()
    {
        if (!Basics.CommandLineArguments.Any(argument =>
                string.Equals(argument, OpenExtensionsArgument, StringComparison.OrdinalIgnoreCase)))
            return;
        ModMain.frmMain.PageChange(LastPage);
    }
}

# 扩展与软件源

扩展是启动器「扩展」页里的功能模块，从扩展商店安装。目标是启动器本体只保留 Java 版的核心功能，基岩版、Lunar Client 这类功能都做成扩展，按需安装。

> **当前进度（第一阶段）**：扩展商店、软件源、扩展 SDK 和运行时已经可用；基岩版和 Lunar 仍内置在本体里，会在第二、三阶段拆成官方扩展。

## 一、扩展和插件有什么不同

|  | 插件（`.pclplugin`） | 扩展（`.pclext`） |
| --- | --- | --- |
| 入口 | 「插件」页 | 顶栏「扩展」 |
| 接口 | 插件 API v1 | 插件 API v1 **加上** 扩展 SDK（页面、启动卡片、PCL 控件、多语言） |
| Mixin | 允许 | **不允许**，只能调用稳定 SDK |
| 启动时 | — | 启动器必须以管理员身份运行；未提权时会询问是否以管理员身份重启 |
| 来源 | 插件市场 / 本地文件 | 官方源 / 你添加的第三方软件源 / 本地文件 |

两者的包格式完全相同（ZIP + `plugin.json` + `signature.json`），签名、安装事务、待重启生效、安全模式（`--safe-mode`）都共用一套实现。

**安全边界**：扩展运行在启动器进程里，拥有启动器的全部权限（启动游戏时还是管理员权限）。签名只能证明包没被篡改，不能证明作者可信，也不是沙箱。

## 二、写一个扩展

完整示例见 [`PCL.Extension.Sample`](../PCL.Extension.Sample)，下面是要点。

### 1. 工程

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <!-- SDK 由启动器提供，不要复制进包里 -->
    <ProjectReference Include="..\PCL.Plugin.SDK\PCL.Plugin.SDK.csproj" Private="false" />
    <ProjectReference Include="..\PCL.Extension.SDK\PCL.Extension.SDK.csproj" Private="false" />
  </ItemGroup>
  <ItemGroup>
    <Content Include="plugin.json" CopyToOutputDirectory="PreserveNewest" />
    <Content Include="lang\*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

**只引用两个 SDK 程序集**。扩展在独立的加载上下文里运行，即使引用了 `PCL.Core` 或启动器本体，拿到的也只是另一份互不相通的副本，而且启动器一升级就会失效。

### 2. 清单 `plugin.json`

```json
{
  "schemaVersion": 1,
  "kind": "extension",
  "extensionApi": "1.0",
  "id": "example.tool",
  "name": "Example Tool",
  "version": "1.0.0",
  "author": "You",
  "description": "What it does.",
  "apiVersion": 1,
  "entryAssembly": "Example.Tool.dll",
  "entryType": "Example.Tool.ToolExtension"
}
```

- `kind` 必须是 `extension`。插件运行时不接受扩展包，扩展运行时也不接受插件包。
- `extensionApi` 写你用到的最低 SDK 版本。启动器支持同一主版本内不高于自己的次版本，例如 1.2 的启动器可以运行声明 `1.0`、`1.1` 的扩展。
- 不能出现 `mixinConfig`、`mixinConfigs` 和 `experimentalFeatures`。

### 3. 入口

```csharp
public sealed class ToolExtension : IPclExtension
{
    private IExtensionSectionHandle? _section;

    public Task StartAsync(IExtensionContext context, CancellationToken cancellationToken)
    {
        var card = new ExtensionLauncherCard
        {
            StatusTitle = "就绪",
            Level = ExtensionStatusLevel.Ready,
            LaunchText = "启动",
            OnLaunch = async token => { /* 启动你的程序 */ }
        };
        _section = context.Host.RegisterSection(new ExtensionSection
        {
            Id = "tool",
            Title = "Example Tool",
            TitleResourceKey = "Tool.Title",   // 可选：跟随语言切换
            SvgIcon = "lucide/boxes",
            Launcher = card,                   // 可选：左栏的状态卡片和启动按钮
            Pages = [new ExtensionPage { Id = "main", Title = "主页", CreateView = () => new MainPage(context) }]
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _section?.Dispose();
        return Task.CompletedTask;
    }
}
```

- **左栏由启动器绘制**：扩展只描述有哪些子页面、状态卡片显示什么；扩展入口、子页面切换、状态圆点和启动按钮都由启动器画，样式跟随主题。
- **启动前的权限检查由启动器负责**：点「启动」时，启动器先确认自己在以管理员身份运行，再调用 `OnLaunch`。扩展不需要也不应该自己提权。
- `ExtensionLauncherCard` 的属性可以在任意线程修改，启动器会自动切回 UI 线程。
- `IExtensionContext` 继承了插件 API v1 的全部能力：日志、存储、设置、网络、下载、通知、事件、实例查询。用法见 [插件文档](../plugin_docs.md)。

### 4. 界面：PCL 控件代理

在 XAML 里引用 `xmlns:pcl="urn:pcl-lite:extension-sdk"`：

```xml
<pcl:Card Title="{DynamicResource Tool.Card}">
    <StackPanel>
        <pcl:Button Text="{DynamicResource Tool.Run}" ColorType="Highlight" Height="35" Click="Run_Click" />
        <pcl:TextBox HintText="{DynamicResource Tool.Input}" Height="30" TextChanged="Input_TextChanged" />
        <pcl:ComboBox SelectionChanged="Mode_SelectionChanged">
            <pcl:ComboBoxItem Content="A" Tag="a" IsSelected="True" />
            <pcl:ComboBoxItem Content="B" Tag="b" />
        </pcl:ComboBox>
    </StackPanel>
</pcl:Card>
```

可用控件：`Button`、`TextButton`、`IconButton`、`IconTextButton`、`Card`、`Hint`、`CheckBox`、`ListItem`、`TextBox`、`ComboBox`（+ `ComboBoxItem`）、`ScrollViewer`。

这些是**代理**：它们只暴露稳定的属性和事件，真正的 PCL 控件由启动器在运行时创建。启动器内部控件怎么重写，扩展都不受影响。注意：

- 不要设置代理的 `Content`，用各自的内容属性（`Card.Child`、`ScrollViewer.Child`、`ComboBox.Items`）。
- `Card` 会自动加上标准内边距，内容不用再写 `Margin="20,40,20,18"`。
- `TextBox` 的 `KeyDown`、`TextChanged` 等路由事件会从真实控件冒泡上来，照常用即可。
- 子页面会被包进带滚动条和页面动画的 PCL 页面，扩展只需要提供页面内容。

图标名和启动器内部一致（如 `lucide/boxes`），见 `PCL.Core/UI/Assets/IconPacks`。

### 5. 多语言

在包里放 `lang/<语言代码>.json`，内容是扁平的「键 → 文本」：

```json
{ "Tool.Title": "示例工具", "Tool.Run": "运行" }
```

- 支持的语言代码与启动器相同（`zh-CN`、`zh-TW`、`en-US` …），`zh-CN` 是兜底语言。
- XAML 里用 `{DynamicResource Tool.Run}`，代码里用 `context.Localization.Text("Tool.Run")`。
- **键请带上扩展自己的前缀**。和启动器已有键（或更早加载的扩展）冲突的键会被忽略并写入日志，扩展不能改写启动器自己的文案。

### 6. 打包与签名

```powershell
dotnet build Example.Tool -c Release
# 创建签名证书（只需一次，PFX 和密码妥善保管）
dotnet run --project PCL.Plugin.Tools -- cert "CN=Your Name" my-signing.pfx
# 打包并签名；输出文件名必须以 .pclext 结尾
dotnet run --project PCL.Plugin.Tools -- pack Example.Tool/bin/Release/net10.0-windows my-signing.pfx Example.Tool.pclext
```

`pack` 会检查清单类型与输出扩展名是否一致，并拒绝带 Mixin 的扩展。

本地测试：把 `.pclext` 拖到启动器窗口上，或者在「扩展 → 商店」里点「从文件安装」。安装后重启启动器生效。

## 三、自建软件源

软件源就是一个放在 HTTPS 地址上的 `index.json`，扩展包可以放在任意 HTTPS 地址上。GitHub Pages、对象存储、自己的服务器都可以。

### 1. `index.json` 格式

```json
{
  "schemaVersion": 1,
  "name": "我的软件源",
  "extensions": [
    {
      "id": "example.tool",
      "name": "Example Tool",
      "version": "1.0.0",
      "author": "You",
      "description": "What it does.",
      "extensionApi": "1.0",
      "packageUrl": "https://example.github.io/pcl-source/Example.Tool-1.0.0.pclext",
      "sha256": "<包文件的 SHA-256>",
      "website": "https://example.github.io/pcl-source/",
      "icon": "https://example.github.io/pcl-source/tool.png",
      "localizedNames": { "en-US": "Example Tool" },
      "localizedDescriptions": { "en-US": "What it does." }
    }
  ]
}
```

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `id` | ✓ | 与包里 `plugin.json` 的 `id` 一致：小写字母、数字、`.`、`-` |
| `version` | ✓ | 与包里的版本一致，SemVer 2.0 |
| `extensionApi` | ✓ | 与包里一致；高于启动器支持的版本时会列出但不能安装 |
| `packageUrl` | ✓ | HTTPS，不能带用户名密码；下载后的重定向也必须保持 HTTPS |
| `sha256` | ✓ | 包文件的 SHA-256（64 位十六进制），下载后逐字节校验 |
| 其余 | | 可选；`website`、`icon` 也必须是 HTTPS |

限制：`index.json` 最大 2 MiB、最多 1000 个扩展，同一个源里 `id` 不能重复。

### 2. 生成条目

不用手算 SHA-256，打包工具可以直接生成一条条目：

```powershell
dotnet run --project PCL.Plugin.Tools -- entry Example.Tool.pclext https://example.github.io/pcl-source/Example.Tool-1.0.0.pclext
```

把输出的 JSON 粘进 `extensions` 数组，再补上 `icon`、`localizedNames` 等可选字段。

**更新扩展时**：上传新版本的包（建议文件名带版本号，旧链接保持不变），重新生成条目并替换 `index.json` 里那一条。用户在商店里会看到「更新」。

### 3. 在启动器里添加

「扩展 → 商店 → 添加软件源」，输入 `index.json` 的地址。启动器会先下载并校验索引，失败就不会添加。

### 4. 签名建议

第三方源**允许**未签名的包，但用户安装时会看到红色警告：无法确认包有没有被篡改。建议始终用自己的证书签名：

- 包被调包时，签名校验会直接失败。
- 同一扩展换了签名人时，启动器会醒目提示（防止有人接管了你的源）。

## 四、官方源

官方源内置在启动器里，不能删除。地址是服务仓库中的 `extensions/index.json`（见 `ProjectLinks.ExtensionCatalog`），规则比第三方源更严：

- 包必须是 GitHub Release 附件。
- 包必须签名，且签名证书必须是本次构建认可的官方证书。

官方证书指纹在**编译时**注入（指纹不是机密，这样做是为了让分叉项目换成自己的证书）：

```powershell
$env:PCL_WRITE_SECRET = "1"
$env:PCL_EXTENSION_OFFICIAL_SIGNERS = "<证书 SHA-1 或 SHA-256 指纹，多个用逗号分隔>"
dotnet build-server shutdown
dotnet publish "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" -c Release ... -t:Rebuild
```

没有注入时，官方源的扩展一律拒绝安装（第三方源和本地文件不受影响）。注入细节见 [BUILD.md](BUILD.md)。

## 五、兼容性约定（给改 SDK 的人）

`PCL.Extension.SDK` 是稳定接口，扩展编译一次就要能在后续启动器版本上运行。

- **1.x 只做增量**：可以加新类型、新成员、新控件属性；不能改名、删除，也不能改变已有成员的含义。
- 加了成员就把 `ExtensionApiVersion.Minor` 加一，用到新成员的扩展要把 `extensionApi` 写成新版本。
- 控件代理与真实控件的对应关系只在 `Plain Craft Launcher 2/Pages/PageExtensions/PclControlFactory.cs` 里。启动器内部控件改了，就改这里，不要改 SDK。
- 真的需要破坏性修改时升主版本（`2.0`）。旧主版本的扩展在新启动器上会被拒绝，并提示需要更新扩展。

## 六、相关代码

| 位置 | 内容 |
| --- | --- |
| `PCL.Extension.SDK/` | 扩展接口与控件代理 |
| `PCL.Core/Extensions/` | 软件源、索引校验、信任策略、扩展运行时与生命周期 |
| `PCL.Core/Plugins/PluginRuntimeProfile.cs` | 插件 / 扩展共用安装管线的差异点 |
| `Plain Craft Launcher 2/Pages/PageExtensions/` | 扩展页左栏、商店、控件工厂、安装流程 |
| `Plain Craft Launcher 2/Modules/ModExtensions.cs` | 高权限提示、管理员重启、导航 |
| `PCL.Extension.Sample/` | 示例扩展 |
| `PCL.Core.Test/Extensions/` | 测试 |

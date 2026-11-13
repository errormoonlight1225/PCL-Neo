  本readme.md来自PCL-Lite,做了些许改动
**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)


前言：pcl和它的护卫队天天说动了蛋糕，顺手扣帽子，我想知道pcl这个帽子工厂和烘焙店是有仇吗 怎么帽子工厂天天动蛋糕？
[神秘文章来自cubk](https://github.com/askbbtsk/pcl-lite/blob/main/readmedlc.md)




</div>

> **非 MINECRAFT 官方产品。** 未经 MOJANG 或 MICROSOFT 批准，也不与 MOJANG 或 MICROSOFT 关联。
> NOT AN OFFICIAL MINECRAFT PRODUCT. NOT APPROVED BY OR ASSOCIATED WITH MOJANG OR MICROSOFT.

PCL Neo 是一个独立的 Minecraft 启动器项目，在开源代码Pcl-Lite基础上加入了些许改动。

本项目以 **MIT 许可证** 发布，并附带传染性（copyleft）附加条款，详见[许可证](#-许可证)。


## 📦 下载

前往 [Releases](https://github.com/errormoonlight1225/PCL-Neo/releases) 下载最新版本。



- 需要已安装 [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0)


## 💻 支持平台

| 操作系统 | 支持情况 | 环境要求 |
|---|---|---|
| Windows 10 1809 (17763) 或更高 | ✅ 完整支持 | [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ 理论能跑，酌情提供支持 | [.NET 10 桌面运行时](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Windows 7 或更低版本 | ❌ 不支持 | / |
| macOS / Linux / 其他操作系统 | ⚠️ 仅跨平台开发支持（交叉编译） | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) |

**✅ 完整支持**：尽可能提供一切相关支持，但必须确保启动器为最新版本。

**⚠️ 理论能跑，酌情提供支持**：PCL Lite 应该可以在这些平台上运行，但不保证功能完全可用。你可能需要升级到完整支持的系统版本以获得进一步支持。

**❌ 不支持**：PCL Lite 在这些平台的可用性较低，甚至压根打不开。请升级操作系统以使用 PCL Lite。

**⚠️ 仅跨平台开发支持（交叉编译）**：PCL Lite 的源代码可以在 macOS 与 Linux 平台编译，但无法直接运行。作为开发者，你可以在这些平台上进行开发，然后将编译产物转移到 Windows 系统测试。

**注**：
仅对最新版本的启动器提供支持。
取决于部分问题的特殊性（如系统不完整），有时你仍然必须升级操作系统以继续获得支持。
PCL Lite 始终建议使用最新版本的操作系统以获得最佳体验。
你仍然可以尝试在不受支持的系统上使用 PCL Lite，但可能会遇到很多额外问题。

## 📖 文档

| 文档 | 讲什么 |
| --- | --- |
| [docs/README.md](docs/README.md) | 索引、项目结构、版本号在哪 |
| [主题与配色](docs/THEME.md) | 八级色阶怎么算、14 个预设、自定义主题、怎么加新主题 |
| [配置与数据目录](docs/CONFIG.md) | 配置文件位置、手动改法、迁移逻辑 |
| [Discord 状态显示](docs/DISCORD.md) | 显示内容、隐私边界、换自己的应用 |
| [构建与发布](docs/BUILD.md) | 编译、密钥注入、签名、发版 |
| [Azure 登录](docs/AZURE-LOGIN.md) | 从零注册应用，用自己的 client_id 走正版登录 |
| [MCP](docs/MCP.md) | 让 AI 客户端直接操作启动器 |
| [开源项目清单](docs/CREDITS.md) | 代码来源、第三方库、各自的许可证 |

## 🔒 许可证

**第三方移植部分另有许可证**：基岩版（下载 + 安装 + 启动）移植自
[PCL-Symbio-Edition](https://github.com/Hydrogen-Peroxide/PCL-Symbio-Edition)，该部分仍受
**Apache-2.0** 约束（依据其第 4 条），全文见 [`LICENSE-APACHE`](LICENSE-APACHE)。
它只覆盖移植来的那部分代码，不改变本项目整体的许可证。
完整的代码来源与第三方库清单见[开源项目清单](docs/CREDITS.md)。



## ❤️ 致谢
<img width="403" height="310" alt="赞助相关聊天记录" src="docs/assets/thanks/pcl2-sponsor-chat.png" />
<img width="832" height="573" alt="升级模块分析" src="docs/assets/thanks/pcl2-backdoor-analysis.png" />

参考文献:https://zhuanlan.zhihu.com/p/347828430



## Star History

<a href="https://www.star-history.com/?repos=askbbtsk%2Fpcl-lite&amp;type=date&amp;legend=top-left">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=askbbtsk/pcl-lite&amp;type=date&amp;theme=dark&amp;legend=top-left" />
    <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=askbbtsk/pcl-lite&amp;type=date&amp;legend=top-left" />
    <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=askbbtsk/pcl-lite&amp;type=date&amp;legend=top-left" />
  </picture>
</a>

# Axure RP 8.0–8.1 兼容性与能力范围

## 目标范围与已验证版本

| 项目 | 目标 / 已验证信息 |
|---|---|
| 项目目标系列 | Axure RP 8.0–8.1 |
| 当前已验证文件集 | Windows build 3400，以本页 SHA-256 为准 |
| 官方发布历史名称 | 8.1.0.3400，2022-05-11 |
| 本机六个文件的 FileVersion / ProductVersion | 8.0.0.3400 |
| 操作系统 | Windows 桌面版 |
| 宿主架构 | x86 |
| UI 技术 | WPF / .NET Framework 4.x |
| 命令客户端 | Python 3 |
| RP 文件 | 由已安装 Axure RP 原生读写的 .rp |

**注意：** 8.0–8.1 是项目目标范围；当前适配器只验证了下表中的本机 build 3400 文件集，并在启动时校验固定 SHA-256。其他文件集和构建尚未验证；哈希不同即拒绝启动。此处不代表所有 8.0–8.1 安装均可运行。

### 发布名称与文件版本的区别

[Axure 官方发布历史](https://www.axure.com/release-history/rp8)将 build 3400 标为 **8.1.0.3400（2022-05-11）**。2026-11-03 对下表六个本机文件的只读核查显示：它们的 `FileVersion` 与 `ProductVersion` 均为 **8.0.0.3400**；`Get-AuthenticodeSignature` 对六个文件均返回 `Valid`，签名者为 **Axure Software Solutions, Inc**；SHA-256 均与下表相同。

此前文档把 `8.0.0.3400` 文件版本直接写成官方发布名称，表述不准确。有效签名和哈希记录支持对这组具体文件的识别，不能仅凭版本字段的差异推断它们被改过版本号。此次没有重新下载、提取官方安装包并逐字节比较，因此也不把本机文件集无条件等同于所有标为 `8.1.0.3400` 的分发包。

可以在自己的安装目录复查（将路径替换为实际目录；其余五个文件同样检查）：

```powershell
$Binary = 'D:\Apps\Axure RP 8\AxureRP8.exe'
(Get-Item -LiteralPath $Binary).VersionInfo | Select-Object FileVersion, ProductVersion
Get-AuthenticodeSignature -LiteralPath $Binary | Select-Object Status, SignerCertificate
Get-FileHash -LiteralPath $Binary -Algorithm SHA256
```

宿主校验以下 Axure 文件：

| 文件 | SHA-256 |
|---|---|
| AxureRP8.exe | DDDE5C4EC3281F4C0522A38DD38E7498B0D70FFDF126E7F8916C212A109FD279 |
| Client.dll | 4AD07217D548C6F9E5E17AAAA1C55E627B0F4F1855E203A946D88881B0CCF109 |
| Client.Win32.dll | 9D7F4FAA0B8C3C02CFE125A32F4A123B2CF2F91EDC080074C715EE63A7215074 |
| Model.dll | 58B45B074F502EBE4B345346BDF8AC8680F7A9A4A8C53693922CE4AA2D4B3C65 |
| Platform.dll | D53B20B6658932BEF477E592FAAC5A028EF3B8BF482B77F0E7E8C9D1E2A83A87 |
| Platform.Win32.dll | 3C59611656496F8BF00DB641C4D92E5BC172461288BE2BF570440363486F12D6 |

任何哈希不同都应视为不兼容。RP 9、RP 10、RP 11、macOS、Linux、Axure Cloud 和其他 RP8 构建均未验证。

## 已实现命令

- 会话：status、capture、close、undo、redo。
- 页面：pages、page.open、page.reveal、page.create、page.rename、page.content、page.polish。
- 元件：widgets、widget.create、widget.update、widget.group、widget.select、widget.style、widget.path。
- 交互：interactions、interaction.add、interaction.replaceActions、interaction.repairText。
- 输出：save、prototype.export。

page.removeEmpty 和 frame.removeOld 有强限制的专用清理条件；请先阅读源码中的保护条件，不要把它们当作通用删除接口。methods 用于诊断反射到的编辑器方法，不是稳定 API。

## 元件类型与样式

widget.create 支持：button、input、checkbox、image、rectangle、paragraph、heading、ellipse、vector、hline、vline。图片输入仅接受本机 PNG/JPEG。

widget.style 的白名单包含：fontFamily、fontSize、fontWeight、color、fill、borderColor、borderWidth、radius、align、valign、四边 padding、rotation、opacity、visible、shadow。

## 交互范围

已支持的动作包括：设置/复制文字、复选框状态、受限原生表达式、显示/隐藏/切换、启用/禁用、移动、改变尺寸、页面跳转、触发另一个事件和短等待。事件名称由 RP8 原生枚举提供，并且会按目标元件实际支持的事件做检查。

未实现或尚未作为通用功能验证的内容包括：动态面板状态切换、中继器、任意页面嵌套、自适应视图、所有 Axure 表达式、所有交互动作、普遍适用的元件删除，以及文字/样式/交互的独立撤销语义。新增元件的原生撤销/重做已验证；其它操作应及时另存工作副本。

## 已有验证记录

2026-10-24 的本机记录包含 25 项可见窗口、非前台编辑、保存/另存、元件 ID 读回和焦点保护检查；交互闭环还记录了 15 项编辑/持久化检查和 8 项浏览器预览检查。这些结果只代表上表哈希对应的文件集和当时的本机样本，不构成其他 RP8 文件集或所有操作的保证。2026-11-03 的版本、签名与哈希核查属于文件身份复核，没有重新执行这些功能检查。

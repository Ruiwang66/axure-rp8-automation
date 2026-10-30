# Axure RP 8.0–8.1 Live Automation / 实时自动化

**[简体中文](#简体中文) | [English](#english)**

> **Axure RP 8.0–8.1 automation workflow and detailed tutorial**  
> **Axure RP 8.0–8.1 自动化工作流与详细教程**

## 简体中文

### 项目范围

本项目面向 **Axure RP 8.0–8.1** 系列。当前公开适配器已在 Windows x86 上针对 **Axure RP 8.0.0.3400** 验证，并按该构建的程序与程序集 SHA-256 锁定。其他 8.0 或 8.1 构建尚未逐一验证；哈希不匹配时会拒绝启动。因此，项目目标范围是 8.0–8.1，但当前实证兼容性仅限 8.0.0.3400。

适配器使用独立 RP8 进程和本地 JSON 命令，在 RP8 文档模型中创建或修改可编辑页面、元件和交互。RP8 窗口保持可见；程序不模拟鼠标、键盘或剪贴板输入。原生保存和 HTML 导出由 Axure 完成。

### 能力概览

- 查询实例状态、页面 GUID、元件树和交互。
- 打开、创建、重命名页面，并在页面树中定位页面。
- 创建标题、段落、矩形、椭圆、矢量、按钮、输入框、复选框和图片。
- 修改元件名称、文字、位置、尺寸、提示文字和白名单样式。
- 添加受支持的 Axure 原生事件、条件和动作；读取现有交互。
- 保存或另存为 .rp，撤销/重做受支持的操作，捕获 RP8 原生窗口，并调用 Axure HTML 生成器。
- 保持 RP8 窗口可见，同时避免将其他应用的前台焦点抢走。

### 快速开始

1. 准备一份有权使用的 Axure RP 8 Windows 安装。当前实证构建为 **8.0.0.3400**。
2. 把本仓库克隆到 Axure 安装目录下的 rp8-automation 文件夹。
3. 在仓库内运行 live\build.cmd。
4. 从 Axure 安装目录运行 打开独立RP8.exe，并传入要编辑的 .rp 路径。
5. 使用 Python 3 执行 live\live_cli.py 发送 JSON 请求。

```powershell
$AxureRoot = 'D:\Apps\Axure RP 8' # 替换为实际安装目录
git clone https://github.com/Ruiwang66/axure-rp8-automation.git (Join-Path $AxureRoot 'rp8-automation')
Set-Location (Join-Path $AxureRoot 'rp8-automation')
.\live\build.cmd

# 传入自己的 .rp 文件；启动器会先复制一份到独立工作目录。
& (Join-Path $AxureRoot '打开独立RP8.exe') 'D:\Prototypes\source.rp'

$Cli = Join-Path $AxureRoot 'rp8-automation\live\live_cli.py'
py -3 $Cli '{"operation":"status"}'
```

### 文档

- [详细使用教程（中文）](docs/TUTORIAL.md) · [Detailed tutorial (English)](docs/TUTORIAL_EN.md)
- [架构说明（中文）](docs/ARCHITECTURE.md) · [Architecture (English)](docs/ARCHITECTURE_EN.md)
- [兼容性与能力范围（中文）](docs/COMPATIBILITY.md) · [Compatibility and feature scope (English)](docs/COMPATIBILITY_EN.md)
- [安全与隐私（中文）](docs/SECURITY.md) · [Security and privacy (English)](docs/SECURITY_EN.md)
- [RP8 设计工作流 Skill（中文）](skills/axure-rp8-design/SKILL_CN.md) · [Skill (English)](skills/axure-rp8-design/SKILL.md)

### 仓库内容与许可

```text
live/                         RP8 宿主、编辑适配器、启动器、JSON CLI
examples/requests/            可复制的 JSON 命令示例
docs/                         中英文教程、架构、兼容性和安全文档
skills/axure-rp8-design/       中英文 Agent 工作流说明
```

本项目与 Axure Software Solutions, Inc. 无隶属关系。仓库不分发 Axure 安装程序、DLL、图标、用户 .rp 工程或编译二进制。MIT 许可证仅适用于仓库原创代码，不授予 Axure 软件或其组件的使用权；详见 [NOTICE.md](NOTICE.md) 与 [LICENSE](LICENSE)。

## English

### Scope and verified compatibility

This project targets the **Axure RP 8.0–8.1** product line. The published adapter has been verified on Windows x86 against **Axure RP 8.0.0.3400** and is SHA-256-pinned to that build's executable and assemblies. Other 8.0 or 8.1 builds have not been verified individually, and the adapter refuses to start when the hashes differ. The project target is 8.0–8.1; the currently verified compatibility is limited to 8.0.0.3400.

The adapter runs a dedicated RP8 process and sends local JSON commands to create or edit native, editable pages, widgets, and interactions in the RP8 document model. The RP8 window stays visible. The program does not simulate mouse, keyboard, or clipboard input. Axure performs native saves and HTML exports.

### Features

- Read instance status, page GUIDs, widget trees, and interactions.
- Open, create, and rename pages; reveal pages in the page tree.
- Create headings, paragraphs, rectangles, ellipses, vectors, buttons, inputs, checkboxes, and images.
- Update widget names, text, position, dimensions, placeholder text, and allowlisted styles.
- Add supported Axure native events, conditions, and actions; inspect existing interactions.
- Save or Save As .rp files, undo/redo supported operations, capture the native RP8 window, and invoke Axure's HTML generator.
- Keep the RP8 window visible without taking foreground focus from other applications.

### Quick start

1. Prepare an authorized Windows installation of Axure RP 8. The currently verified build is **8.0.0.3400**.
2. Clone this repository into an rp8-automation directory under the Axure installation root.
3. Run live\build.cmd from the repository directory.
4. Launch 打开独立RP8.exe from the Axure installation root and pass the .rp file to edit.
5. Use Python 3 to send JSON requests through live\live_cli.py.

```powershell
$AxureRoot = 'D:\Apps\Axure RP 8' # Change this to your installation directory
git clone https://github.com/Ruiwang66/axure-rp8-automation.git (Join-Path $AxureRoot 'rp8-automation')
Set-Location (Join-Path $AxureRoot 'rp8-automation')
.\live\build.cmd

# Pass your own .rp file; the launcher first copies it into an isolated work directory.
& (Join-Path $AxureRoot '打开独立RP8.exe') 'D:\Prototypes\source.rp'

$Cli = Join-Path $AxureRoot 'rp8-automation\live\live_cli.py'
py -3 $Cli '{"operation":"status"}'
```

### Documentation

- [Detailed tutorial (English)](docs/TUTORIAL_EN.md) · [详细使用教程（中文）](docs/TUTORIAL.md)
- [Architecture (English)](docs/ARCHITECTURE_EN.md) · [架构说明（中文）](docs/ARCHITECTURE.md)
- [Compatibility and feature scope (English)](docs/COMPATIBILITY_EN.md) · [兼容性与能力范围（中文）](docs/COMPATIBILITY.md)
- [Security and privacy (English)](docs/SECURITY_EN.md) · [安全与隐私（中文）](docs/SECURITY.md)
- [RP8 design workflow skill (English)](skills/axure-rp8-design/SKILL.md) · [设计工作流 Skill（中文）](skills/axure-rp8-design/SKILL_CN.md)

### Repository and license

```text
live/                         RP8 host, editing adapters, launcher, JSON CLI
examples/requests/            Copyable JSON request examples
docs/                         Chinese and English tutorials and technical notes
skills/axure-rp8-design/       Chinese and English agent workflow guides
```

This is an independent community project and is not affiliated with Axure Software Solutions, Inc. The repository does not distribute Axure installers, DLLs, icons, user .rp projects, or compiled binaries. The MIT license applies only to original code in this repository; it does not grant rights to Axure software or its components. See [NOTICE.md](NOTICE.md) and [LICENSE](LICENSE).

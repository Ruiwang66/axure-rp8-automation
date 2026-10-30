# Axure RP 8.0–8.1 使用教程

本教程面向 **Axure RP 8.0–8.1** 系列，当前适配器已在 **Windows + Axure RP 8.0.0.3400** 上验证，并按此构建的文件哈希锁定。其他 8.0/8.1 构建尚未验证。建议第一次先用可丢弃的测试工程熟悉流程。

## 1. 准备环境

需要：

- 已安装并获授权使用的 Axure RP 8.0.0.3400。
- Windows 桌面环境。
- Windows 自带的 .NET Framework 4.x C# 编译器，路径为 `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe`。
- Python 3，并能从 PowerShell 用 `py -3` 启动。
- 对 Axure 安装目录有写入权限。构建会在该目录生成 `RP8Live.next.exe` 和 `打开独立RP8.exe`。

仓库不包含 Axure 的 EXE、DLL、配置、图标或示例 `.rp`。编译还会读取 Axure 安装目录中的 `AxureRP8.exe.config` 与 `App.ico`。

## 2. 克隆并构建

将仓库放到 Axure 安装目录中的 `rp8-automation` 子目录。构建脚本按这个目录关系定位 Axure 程序文件：

```powershell
$AxureRoot = 'D:\Apps\Axure RP 8' # 替换为实际路径
$Repo = Join-Path $AxureRoot 'rp8-automation'
git clone https://github.com/Ruiwang66/axure-rp8-automation.git $Repo
Set-Location $Repo
.\live\build.cmd
```

成功后，以下文件会出现在 `$AxureRoot`：

- `RP8Live.next.exe`：承载 Axure RP 8 和原生编辑适配器的独立进程。
- `RP8Live.next.exe.config`：由原版 `AxureRP8.exe.config` 复制而来。
- `打开独立RP8.exe`：把输入工程复制到独立工作目录并启动专用宿主。

如果编译器、`App.ico` 或 Axure 配置文件缺失，构建会失败。请先核对安装目录和版本，不要用其他版本的 DLL 替代。

## 3. 启动一个隔离的工作副本

把已有 `.rp` 的路径作为参数传给启动器：

```powershell
$Source = 'D:\Prototypes\源工程.rp'
& (Join-Path $AxureRoot '打开独立RP8.exe') $Source
```

启动器会把工程复制到 `rp8-automation\live\projects\<会话目录>\AI实时设计.rp`，再打开副本。源工程不作为本次自动化编辑目标。

如果专用 RP8 实例已经运行，启动器会复用它，不会自动切换到新传入的 `.rp`。要切换工程，先确认当前工程已保存，再通过命令安全关闭专用实例，然后重新启动。启动器无参数时会尝试读取 `live\实时设计演示.rp`；该演示工程不随公开仓库分发，所以一般应显式传入自己的文件。

## 4. 发送第一个命令

CLI 接受一条 JSON 字符串，也可以接受 UTF-8 JSON 文件。文件方式更适合中文和较长命令：

```powershell
$Cli = Join-Path $AxureRoot 'rp8-automation\live\live_cli.py'
py -3 $Cli '{"operation":"status"}'
py -3 $Cli --file '.\examples\requests\pages.json'
```

响应为 JSON，`ok` 表示命令结果；失败时含 `error`。默认等待 RP8 返回最多 20 秒。超时后先检查会话和请求结果再重发，避免重复创建元件。

常用只读请求：

```powershell
py -3 $Cli --file '.\examples\requests\status.json'
py -3 $Cli --file '.\examples\requests\pages.json'
py -3 $Cli --file '.\examples\requests\widgets.json'
```

- `status` 返回宿主就绪状态、当前文件、当前页面、未保存状态和窗口焦点审计信息。
- `pages` 返回页面 GUID。调用 `page.open` 后再查 `status`，可以确认当前页名称。
- `widgets` 返回当前页顶层元件及其子元件；每个元件包含稳定 GUID、名称、类型、文字和边界。

## 5. 切换页面并新建元件

将 `pages` 返回的 GUID 填入 `page.open`：

```json
{"operation":"page.open","id":"替换成页面GUID"}
```

创建原生按钮：

```json
{
  "operation": "widget.create",
  "kind": "button",
  "name": "btnContinue",
  "text": "继续",
  "x": 120,
  "y": 180,
  "width": 160,
  "height": 48
}
```

保存响应中的 `id`，后续编辑和交互都用这个 GUID 定位元件。也可创建 `paragraph`、`heading`、`rectangle`、`ellipse`、`vector`、`hline`、`vline`、`input`、`checkbox` 和 `image`。图片需要本机 PNG/JPEG 文件路径；文件会嵌入目标 RP 工程。

修改元件文字和位置：

```json
{
  "operation": "widget.update",
  "id": "替换成元件GUID",
  "text": "新的按钮文字",
  "x": 140,
  "y": 200,
  "width": 180,
  "height": 48
}
```

省略的属性保持原值。`widget.style` 只接受明确列出的样式属性，详见 [COMPATIBILITY.md](COMPATIBILITY.md)。

## 6. 添加一个原生交互

先用 `widgets` 取得按钮与目标文字元件的 GUID，再向按钮添加 OnClick 用例：

```json
{
  "operation": "interaction.add",
  "id": "按钮GUID",
  "event": "OnClick",
  "name": "显示欢迎文字",
  "actions": [
    {
      "kind": "setText",
      "target": "文字元件GUID",
      "text": "欢迎使用 Axure RP 8"
    }
  ]
}
```

操作种类包括 `setText`、`setChecked`、`copyText`、`expressionText`、`visibility`、`enable`、`move`、`resize`、`openPage`、`fire` 和 `wait`。动作和事件必须由目标控件支持；表达式须使用 Axure 原生格式，不能把普通 JavaScript 当作 Axure 表达式。

`interaction.add` 会新增用例；`interaction.replaceActions` 会替换指定用例的动作列表，使用前务必读取当前交互并确认 `caseId`。用 `interactions` 查看某控件交互：

```json
{"operation":"interactions","id":"元件GUID"}
```

## 7. 保存、截图和导出预览

先另存为一个尚不存在的新路径：

```json
{"operation":"save","path":"D:\\Prototypes\\login-edited.rp"}
```

`save` 会调用 Axure 原生保存；目标路径已存在时拒绝覆盖。完成保存后检查响应中的 `status.hasUnsavedChanges` 为 `false`。

捕获 RP8 当前窗口：

```json
{"operation":"capture","path":"D:\\Prototypes\\evidence\\rp8-window.png"}
```

调用 RP8 原生 HTML 生成器：

```json
{"operation":"prototype.export","path":"D:\\Prototypes\\login-preview"}
```

导出目录必须是新的空路径。导出完成后用浏览器实际检查导航、控件和布局；文件能导出不代表交互已经验证。

## 8. 一个推荐的 Agent 工作提示

```text
使用 axure-rp8-design 工作流操作我刚启动的 Axure RP 8 专用窗口。
先读取 status、pages 和当前页 widgets，确认目标文件与页面。
只编辑启动器建立的工作副本。按我提供的页面要求创建原生元件和已支持的交互，
每批修改后读取结果并检查页面，最后另存为一个不存在的新 .rp 路径，
确认 hasUnsavedChanges=false，并列出未实现的交互。
```

## 9. 安全和常见故障

- **版本校验失败**：当前适配器虽以 RP 8.0–8.1 为项目目标，但只验证过 8.0.0.3400，并只接受兼容性页列出的该构建程序哈希。其他 8.0/8.1 构建尚未验证；不要移除或绕开校验。
- **`RP8 client not ready`**：等待专用窗口完成启动后再发 `status`。不要把命令发送给通过普通快捷方式启动的 RP8。
- **启动器没有切换文件**：已有实例被设计为复用且不切换文档。保存并安全关闭专用实例，再对新文件启动。
- **CLI 超时**：先读 `live\session.json` 和对应 `requests` 结果；对可能已经执行的写入请求不要盲目重试。
- **保存或切页后结果不一致**：修改当前页面后先执行 `save`，再切换页面或退出。
- **不支持的交互**：适配器会拒绝未知事件、动作和样式属性。动态面板状态、中继器和自适应视图当前不在实现范围。

运行期的 `session.json`、请求/响应 JSON、截图、导出目录和 `.rp` 文件可能包含用户设计信息，不要把它们加入 Git 提交。


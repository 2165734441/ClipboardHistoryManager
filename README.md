# 剪贴板历史管理器

这是一个 Windows 10/11 桌面剪贴板工具，使用 .NET 8 WinForms 和 SQLite 构建。

面向普通 Windows 用户的安装、首次使用和故障排查说明，请阅读 [使用教程.md](使用教程.md)。

## 已实现功能

- 使用 Windows 剪贴板事件监听，不使用高频轮询。
- 只把文字内容写入 SQLite 历史数据库；图片和未知类型只更新悬浮显示。
- 过滤空字符串、纯空格、纯换行内容，并跳过连续相同文本。
- 支持中文、英文、数字、符号和多行文本。
- 数据库、设置和日志保存到 `%LOCALAPPDATA%\ClipboardHistoryManager`。
- 主界面支持实时搜索、全部/收藏筛选、收藏、删除、详情查看、重新复制和清空普通历史。
- 默认全局快捷键 `Ctrl + Shift + V` 打开历史窗口。
- 默认 `Ctrl + 1` 到 `Ctrl + 9` 快速切换更早的历史内容，可在设置中分别修改。
- 默认 `Ctrl + 0` 切换悬浮窗口锁状态：锁开启时允许移动，锁关闭时固定位置。
- 支持系统托盘、暂停/继续记录、后台运行、开机启动、启动时直接后台运行和单实例。
- 支持透明歌词式悬浮显示，可置顶、调整字号和文字颜色；文字使用自动协调的柔和轮廓，背景保持透明。
- 已配置原创应用图标，并用于 exe、主窗口、任务栏和系统托盘。

## 运行

```powershell
dotnet run
```

## 自测

```powershell
dotnet run -- --self-test
dotnet run -- --clipboard-self-test
```

## 打包 Windows exe

```powershell
.\build-release.ps1
```

如果当前系统禁止运行 PowerShell 脚本，也可以双击或执行：

```cmd
build-release.cmd
```

打包产物位置：

```text
dist\ClipboardHistoryManager-win-x64\ClipboardHistoryManager.exe
```

正式程序为 Windows 图形界面程序，不会弹出黑色控制台窗口。

## 发布包

GitHub Release 中提供免安装的 Windows x64 压缩包。解压后直接运行
`ClipboardHistoryManager.exe` 即可；程序会把数据库、设置和日志保存到当前用户的
`%LOCALAPPDATA%\ClipboardHistoryManager`，不会把用户数据写入程序目录。

项目包含 `.github/workflows/release.yml`，推送 `v*` 标签后会在 GitHub Actions 中重新构建
Windows x64 单文件程序并上传到对应 Release。

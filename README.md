# 每日待办

一个把待办清单、日历和像素小猫放在一起的 Windows 桌面工具。

## 使用

从 [Releases](../../releases/latest) 下载 `DailyTodo.exe`，放到常用的文件夹后双击运行。

- 新增时可直接选日期、填提醒时间，也可设为每天、工作日或每周指定日期重复。
- 「今天」显示到期和逾期的待办。「日历」按实际完成日期统计记录，点日期可查看明细。
- 提醒弹出后，可标记完成或延后 10 分钟。有提醒时，关闭主窗口会转到系统托盘。托盘菜单可以退出程序，或开启登录 Windows 后在托盘运行。
- 点进度卡上的小猫可以拍爪。连续 3 天每天至少完成一项待办，会随机解锁一只新猫。点「猫窝」查看和切换。

待办和猫窝记录保存在本机的 `%LOCALAPPDATA%\DailyTodo`。其中 `tasks.json` 是待办和完成记录，`cats.json` 是小猫收藏。

## 从源码构建

在装有 .NET Framework 4.8 的 Windows 上打开 PowerShell，在仓库目录运行：

```powershell
.\build.ps1
```

构建完成后，双击生成的 `DailyTodo.exe`。项目使用 C# 和 WPF，依赖 Windows 自带的 .NET Framework 组件。

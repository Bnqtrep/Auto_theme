Auto Theme Tray
=================

What it is
- A small Windows Forms tray application that automatically switches Windows light/dark theme at sunrise/sunset.
- Calculates sunrise/sunset using a NOAA-style algorithm (see SunriseCalculator.cs).
- Uses only Win32 APIs and Windows Registry — no external network APIs or WinRT complications.
- The app computes the next sunrise/sunset and schedules a timer to fire exactly at that moment.
- After the timer fires, it computes the next event and reschedules.
- No polling loop.

Run at startup
The app has a "Run at startup" menu option:
1. Right-click the tray icon
2. Click "Run at startup" to toggle on/off

Dependicies of this project :
- WIXtoolkit
- .net 10 sdk +

功能介绍

- 一个小型 Windows Forms 托盘应用程序，可在日出/日落时自动切换 Windows 浅色/深色主题。

- 使用类似 NOAA 的算法计算日出/日落时间（参见 SunriseCalculator.cs）。

- 仅使用 Win32 API 和 Windows 注册表，无需外部网络 API 或 WinRT 相关组件。

- 该应用程序计算下一次日出/日落时间，并设置一个定时器，在该时刻触发。

- 定时器触发后，应用程序会计算下一个事件并重新设置定时器。

- 无轮询循环。


开机启动

该应用有一个“开机启动”菜单选项：

1. 右键单击​​托盘图标

2. 单击“开机启动”以启用/禁用

这个项目的依赖：
- WIXtoolkit
- .net 10 sdk +
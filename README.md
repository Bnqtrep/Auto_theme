Auto Theme Tray 
=================
# EN_US
What it is (it still cannot locate you automatically, please manually configure the coordinates😅)
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

## Setting location
By default, the app uses a fallback of 6:00 AM (light) to 6:00 PM (dark) local time.
To enable sunrise/sunset calculation, edit Program.cs InitializeAndSchedule() and set:
```csharp
latitude = 37.7749;   // your latitude
longitude = -122.4194; // your longitude
```

Dependicies of this project :
- WIXtoolkit
- .net 10 sdk +

# ZH_CN

功能介绍 (现在暂时还没有自动读取位置的功能，但是我会加的😁)

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

## 设置位置

默认情况下，应用使用当地时间上午 6:00（白天）至下午 6:00（晚上）作为备用时间。

要启用日出/日落计算，请编辑 Program.cs 文件中的 InitializeAndSchedule() 函数并设置：

```csharp

latitude = 37.7749; // 您的纬度

longitude = -122.4194; // 您的经度

```

这个项目的依赖：
- WIXtoolkit
- .net 10 sdk +
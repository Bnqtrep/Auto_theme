using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoThemeTray
{
    static class Program
    {
        // P/Invoke to broadcast setting change
        private const int HWND_BROADCAST = 0xffff;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        private static NotifyIcon? trayIcon;
        private static System.Threading.Timer? scheduleTimer;
        private static double latitude = double.NaN, longitude = double.NaN;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Load custom icon or use default
            Icon? customIcon = null;
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                {
                    customIcon = new Icon(iconPath);
                }
            }
            catch { }

            trayIcon = new NotifyIcon
            {
                Text = "Auto Theme Tray",
                Icon = customIcon ?? SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = BuildContextMenu()
            };

            // Start: apply current theme and schedule next transition
            InitializeAndSchedule();

            Application.Run();

            // cleanup
            scheduleTimer?.Dispose();
            trayIcon.Visible = false;
            customIcon?.Dispose();
            trayIcon.Dispose();
        }

        private static ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();
            var startupItem = new ToolStripMenuItem("Run at startup");
            startupItem.Checked = IsRunAtStartupEnabled();
            startupItem.Click += (s, e) =>
            {
                if (startupItem.Checked)
                    DisableRunAtStartup();
                else
                    EnableRunAtStartup();
                startupItem.Checked = !startupItem.Checked;
                trayIcon?.ShowBalloonTip(3000, "Startup", startupItem.Checked ? "Will run at startup" : "Won't run at startup", ToolTipIcon.Info);
            };
            var settingsItem = new ToolStripMenuItem("Settings");
            settingsItem.Click += (s, e) => MessageBox.Show("Location Services: Not available in desktop app mode.\nFallback using 6:00 AM - 6:00 PM (local time).\nTo enable: add coordinates in code or use a location API.", "Auto Theme Tray");
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => Application.Exit();
            menu.Items.Add(startupItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);
            return menu;
        }

        private static bool IsRunAtStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                {
                    return key?.GetValue("AutoThemeTray") != null;
                }
            }
            catch { return false; }
        }

        private static void EnableRunAtStartup()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                {
                    key.SetValue("AutoThemeTray", exePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to enable startup: " + ex.Message, "Error");
            }
        }

        private static void DisableRunAtStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    key?.DeleteValue("AutoThemeTray", false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to disable startup: " + ex.Message, "Error");
            }
        }

        private static void InitializeAndSchedule()
        {
            // Note: Windows.Devices.Geolocation is not directly accessible from desktop .NET apps without complex WinRT interop.
            // Using fallback: hardcoded or stored location, or time-based heuristic.
            // If you have a location, uncomment and set:
            // latitude = 37.7749;   // San Francisco example
            // longitude = -122.4194;

            ScheduleNextChange();
        }

        private static void ScheduleNextChange()
        {
            DateTime now = DateTime.Now;
            DateTime nextEvent;

            if (double.IsNaN(latitude) || double.IsNaN(longitude))
            {
                // Fallback: sunrise 6:00, sunset 18:00 local time
                DateTime todaySunrise = new DateTime(now.Year, now.Month, now.Day, 6, 0, 0);
                DateTime todaySunset = new DateTime(now.Year, now.Month, now.Day, 18, 0, 0);
                if (now < todaySunrise) { nextEvent = todaySunrise; }
                else if (now < todaySunset) { nextEvent = todaySunset; }
                else { nextEvent = todaySunrise.AddDays(1); }
            }
            else
            {
                // Use sunrise/sunset calculation with lat/lon
                var sun = SunriseCalculator.GetSunriseSunsetTimes(now.Date, latitude, longitude);
                DateTime sunrise = sun.sunrise.ToLocalTime();
                DateTime sunset = sun.sunset.ToLocalTime();

                if (now < sunrise) { nextEvent = sunrise; }
                else if (now < sunset) { nextEvent = sunset; }
                else { nextEvent = sunrise.AddDays(1); }
            }

            // Apply current theme immediately
            ApplyThemeBasedOnTime(now);

            TimeSpan due = nextEvent - now;
            if (due < TimeSpan.Zero) due = TimeSpan.Zero;

            // Dispose previous timer and create a new one to fire once at nextEvent
            scheduleTimer?.Dispose();
            scheduleTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    ApplyThemeBasedOnTime(DateTime.Now);
                    ScheduleNextChange();
                }
                catch { }
            }, null, due, Timeout.InfiniteTimeSpan);

            trayIcon?.ShowBalloonTip(3000, "Scheduled", $"Next theme change: {nextEvent:g}", ToolTipIcon.Info);
        }

        private static void ApplyThemeBasedOnTime(DateTime now)
        {
            bool shouldUseLight = true;
            if (double.IsNaN(latitude) || double.IsNaN(longitude))
            {
                shouldUseLight = now.Hour >= 6 && now.Hour < 18;
            }
            else
            {
                var sun = SunriseCalculator.GetSunriseSunsetTimes(now.Date, latitude, longitude);
                DateTime sunrise = sun.sunrise.ToLocalTime();
                DateTime sunset = sun.sunset.ToLocalTime();
                shouldUseLight = now >= sunrise && now < sunset;
            }

            SetWindowsTheme(shouldUseLight);
            trayIcon?.ShowBalloonTip(3000, "Theme", shouldUseLight ? "Light theme applied" : "Dark theme applied", ToolTipIcon.Info);
        }

        private static void SetWindowsTheme(bool light)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
                {
                    if (key != null)
                    {
                        key.SetValue("AppsUseLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("SystemUsesLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
                    }
                }

                UIntPtr result;
                SendMessageTimeout(new IntPtr(HWND_BROADCAST), WM_SETTINGCHANGE, UIntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 1000, out result);
            }
            catch (Exception ex)
            {
                trayIcon?.ShowBalloonTip(5000, "Error", "Failed to change theme: " + ex.Message, ToolTipIcon.Error);
            }
        }
    }
}
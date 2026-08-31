using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Autotheme.Services
{
    public class ThemeScheduler : IDisposable
    {
        private System.Threading.Timer? _timer;
        private double _lat;
        private double _lon;
        private DateTime _sunrise;
        private DateTime _sunset;

        public async Task InitializeAsync()
        {
            var loc = await GeoLocationService.GetLocationAsync();
            _lat = loc.latitude;
            _lon = loc.longitude;
            ComputeSunTimes(DateTime.Now.Date);
        }

        public void Start()
        {
            ScheduleNext();
        }

        private void ComputeSunTimes(DateTime day)
        {
            SunCalculator.GetSunriseSunset(day, _lat, _lon, out _sunrise, out _sunset);
        }

        private void ScheduleNext()
        {
            var now = DateTime.Now;
            DateTime nextEvent;

            // If either sunrise/sunset are invalid (polar day/night), fallback to simple hours
            if (_sunrise == DateTime.MinValue || _sunset == DateTime.MinValue)
            {
                // Fallback: day 7:00 to 19:00
                var dayStart = now.Date.AddHours(7);
                var dayEnd = now.Date.AddHours(19);
                nextEvent = now < dayStart ? dayStart : (now < dayEnd ? dayEnd : dayStart.AddDays(1));
            }
            else
            {
                // If we've passed today's sunset, compute for tomorrow
                if (now >= _sunset)
                {
                    ComputeSunTimes(now.Date.AddDays(1));
                    nextEvent = _sunrise; // next is tomorrow's sunrise
                }
                else if (now < _sunrise)
                {
                    nextEvent = _sunrise;
                }
                else
                {
                    nextEvent = _sunset;
                }
            }

            var dueTime = nextEvent - now;
            if (dueTime < TimeSpan.FromSeconds(1)) dueTime = TimeSpan.FromSeconds(1);

            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => OnTimerFired(), null, dueTime, Timeout.InfiniteTimeSpan);

            // Apply theme now
            ApplyThemeBasedOnNow();
        }

        private void OnTimerFired()
        {
            try
            {
                ApplyThemeBasedOnNow();
                // Recompute and schedule next
                ComputeSunTimes(DateTime.Now.Date);
                ScheduleNext();
            }
            catch { }
        }

        private void ApplyThemeBasedOnNow()
        {
            var now = DateTime.Now;
            bool isLight;
            if (_sunrise == DateTime.MinValue || _sunset == DateTime.MinValue)
            {
                isLight = now.Hour >= 7 && now.Hour < 19;
            }
            else
            {
                isLight = now >= _sunrise && now < _sunset;
            }

            SetWindowsTheme(isLight);
        }

        private void SetWindowsTheme(bool light)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        key.SetValue("AppsUseLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("SystemUsesLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
                    }
                }

                // Notify system
                const uint SMTO_ABORTIFHUNG = 0x0002;
                const int HWND_BROADCAST = 0xffff;
                SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 100, out _);
            }
            catch { }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        private const int WM_SETTINGCHANGE = 0x001A;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
    }
}

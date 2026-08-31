using System;
using System.Threading.Tasks;
using System.Windows;
using WinForms = System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Autotheme.Services;

namespace Autotheme
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private WinForms.NotifyIcon? _trayIcon;
        private ThemeScheduler? _scheduler;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Hide main window - app runs in tray
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Create tray icon
            _trayIcon = new WinForms.NotifyIcon();
            _trayIcon.Icon = SystemIcons.Application;
            _trayIcon.Text = "Autotheme";
            _trayIcon.Visible = true;

            var menu = new WinForms.ContextMenuStrip();
            var openItem = new WinForms.ToolStripMenuItem("Open") { Enabled = true };
            openItem.Click += (s, a) => ShowMainWindow();
            var exitItem = new WinForms.ToolStripMenuItem("Exit");
            exitItem.Click += (s, a) => ExitApplication();
            menu.Items.Add(openItem);
            menu.Items.Add(exitItem);
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, a) => ShowMainWindow();

            // Start scheduler
            _scheduler = new ThemeScheduler();
            try
            {
                await _scheduler.InitializeAsync();
                _scheduler.Start();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Autotheme failed to initialize: " + ex.Message);
            }
        }

        private void ShowMainWindow()
        {
            var wnd = new MainWindow();
            wnd.Show();
            wnd.Activate();
        }

        private void ExitApplication()
        {
            _trayIcon?.Dispose();
            _scheduler?.Dispose();
            Shutdown();
        }
    }

}

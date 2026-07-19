using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace BililiveRecorder.WPF.Controls
{
    public partial class TaskbarIconControl : UserControl
    {
        private UIElement _originalToolTip;
        private bool _toolTipResetting;
        private DateTime _lastTrayMouseMove = DateTime.MinValue;
        private DispatcherTimer _watchdogTimer;

        private const double WatchdogTimeoutSeconds = 1.0;
        private const int WatchdogIntervalMs = 500;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int SW_HIDE = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public TaskbarIconControl()
        {
            this.InitializeComponent();

            using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/BililiveRecorder.WPF;component/ico.ico")).Stream;
            this.TaskbarIcon.Icon = new System.Drawing.Icon(iconStream);

            this._originalToolTip = this.TaskbarIcon.TrayToolTip;

            if (Application.Current.MainWindow is NewMainWindow nmw)
            {
                nmw.ShowBalloonTipCallback = (title, msg, sym) =>
                {
                    this.TaskbarIcon.ShowBalloonTip(title, msg, sym);
                };
            }

            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var window = Application.Current.MainWindow;
            if (window != null)
            {
                window.Activated += OnMainWindowStateChanged;
                window.Deactivated += OnMainWindowStateChanged;
                window.LocationChanged += OnMainWindowStateChanged;
            }
        }

        private void OnMainWindowStateChanged(object sender, EventArgs e)
        {
            ForceCloseToolTip();
        }

        private void TaskbarIcon_TrayMouseMove(object sender, RoutedEventArgs e)
        {
            _lastTrayMouseMove = DateTime.UtcNow;
        }

        private void TaskbarIcon_PreviewTrayToolTipOpen(object sender, RoutedEventArgs e)
        {
            if (this.TaskbarIcon.TrayToolTip == null)
                return;

            _lastTrayMouseMove = DateTime.UtcNow;

            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(FixToolTipPosition));

            StartWatchdog();
        }

        private void TaskbarIcon_TrayToolTipClose(object sender, RoutedEventArgs e)
        {
            StopWatchdog();
        }

        private void StartWatchdog()
        {
            StopWatchdog();
            _watchdogTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(WatchdogIntervalMs),
                DispatcherPriority.Background,
                OnWatchdogTick,
                this.Dispatcher);
            _watchdogTimer.Start();
        }

        private void StopWatchdog()
        {
            if (_watchdogTimer != null)
            {
                _watchdogTimer.Stop();
                _watchdogTimer = null;
            }
        }

        private void OnWatchdogTick(object sender, EventArgs e)
        {
            var tooltip = this.TaskbarIcon.TrayToolTip;
            if (tooltip == null)
            {
                StopWatchdog();
                return;
            }

            if (!GetCursorPos(out POINT cursorPos))
            {
                StopWatchdog();
                ForceCloseToolTip();
                return;
            }

            if (CursorInsidePopup(cursorPos, tooltip))
            {
                _lastTrayMouseMove = DateTime.UtcNow;
                return;
            }

            var elapsed = (DateTime.UtcNow - _lastTrayMouseMove).TotalSeconds;
            if (elapsed >= WatchdogTimeoutSeconds)
            {
                StopWatchdog();
                ForceCloseToolTip();
            }
        }

        private bool CursorInsidePopup(POINT cursorPos, UIElement tooltip)
        {
            try
            {
                var source = PresentationSource.FromVisual(tooltip) as HwndSource;
                if (source == null || source.Handle == IntPtr.Zero)
                    return false;

                if (!GetWindowRect(source.Handle, out RECT rect))
                    return false;

                return cursorPos.X >= rect.Left && cursorPos.X <= rect.Right
                    && cursorPos.Y >= rect.Top && cursorPos.Y <= rect.Bottom;
            }
            catch
            {
                return false;
            }
        }

        private void FixToolTipPosition()
        {
            var tooltip = this.TaskbarIcon.TrayToolTip;
            if (tooltip == null)
                return;

            try
            {
                var source = PresentationSource.FromVisual(tooltip) as HwndSource;
                if (source == null || source.Handle == IntPtr.Zero)
                    return;

                if (!GetWindowRect(source.Handle, out RECT rect))
                    return;

                if (rect.Left > 5 || rect.Top > 5)
                    return;

                if (!GetCursorPos(out POINT cursorPos))
                    return;

                SetWindowPos(source.Handle, IntPtr.Zero,
                    cursorPos.X + 10, cursorPos.Y + 20,
                    0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
            catch
            {
            }
        }

        private void TaskbarIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            ForceCloseToolTip();
            (Application.Current.MainWindow as NewMainWindow)?.SuperActivateAction();
        }

        private void MenuItem_OpenMainWindow_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as NewMainWindow)?.SuperActivateAction();
        }

        private void MenuItem_Quit_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as NewMainWindow)?.CloseWithoutConfirmAction();
        }

        internal void ForceCloseToolTip()
        {
            if (_toolTipResetting || this.TaskbarIcon.TrayToolTip == null)
                return;

            ForceCloseToolTipUnsafe();
        }

        private void ForceCloseToolTipUnsafe()
        {
            StopWatchdog();

            if (this.TaskbarIcon.TrayToolTip == null)
                return;

            _toolTipResetting = true;

            try
            {
                var source = PresentationSource.FromVisual(this.TaskbarIcon.TrayToolTip) as HwndSource;
                if (source != null && source.Handle != IntPtr.Zero)
                    ShowWindow(source.Handle, SW_HIDE);
            }
            catch
            {
            }

            _toolTipResetting = false;
        }
    }
}

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

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

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

        private void TaskbarIcon_PreviewTrayToolTipOpen(object sender, RoutedEventArgs e)
        {
            if (this.TaskbarIcon.TrayToolTip == null)
                return;

            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(FixToolTipPosition));
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

            _toolTipResetting = true;
            this.TaskbarIcon.TrayToolTip = null;

            this.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    this.TaskbarIcon.TrayToolTip = this._originalToolTip;
                    _toolTipResetting = false;
                }));
        }
    }
}

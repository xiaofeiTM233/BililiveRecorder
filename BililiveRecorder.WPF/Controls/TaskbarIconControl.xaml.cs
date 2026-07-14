using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BililiveRecorder.WPF.Controls
{
    /// <summary>
    /// Interaction logic for TaskbarIconControl.xaml
    /// </summary>
    public partial class TaskbarIconControl : UserControl
    {
        private UIElement _originalToolTip;
        private bool _toolTipResetting;

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

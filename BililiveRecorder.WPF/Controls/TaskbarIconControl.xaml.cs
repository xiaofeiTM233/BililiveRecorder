using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.WPF.Models;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using NotifyIconBalloon = System.Windows.Forms.ToolTipIcon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace BililiveRecorder.WPF.Controls
{
    /// <summary>
    /// 替代 Hardcodet 的 BalloonIcon，供 ShowBalloonTipCallback 使用。
    /// </summary>
    internal enum BalloonIcon
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// 托盘图标控件。使用官方 System.Windows.Forms.NotifyIcon 实现，
    /// 避免 Hardcodet.NotifyIcon.Wpf 把气泡/tooltip 弹到屏幕左上角的已知 bug。
    /// </summary>
    public partial class TaskbarIconControl : UserControl, IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Popup _trayToolTipPopup;
        private readonly FormsContextMenuStrip _contextMenuStrip;
        private readonly DispatcherTimer _toolTipWatchdog;
        private DateTime _lastTrayMouseMove = DateTime.MinValue;
        private bool _disposed;

        public TaskbarIconControl()
        {
            this.InitializeComponent();

            // 承载富 tooltip 的资源
            this._trayToolTipPopup = (Popup)this.FindResource("TrayToolTipPopup");
            this._trayToolTipPopup.PlacementTarget = this;

            this._contextMenuStrip = this.BuildContextMenuStrip();

            this._notifyIcon = new NotifyIcon
            {
                // 从当前 exe 提取图标（WinForms NotifyIcon 需要 System.Drawing.Icon）
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Text = "BililiveRecorder",
                Visible = true,
                ContextMenuStrip = this._contextMenuStrip,
            };

            this._notifyIcon.MouseMove += this.NotifyIcon_MouseMove;
            this._notifyIcon.MouseClick += this.NotifyIcon_MouseClick;
            this._notifyIcon.MouseDoubleClick += this.NotifyIcon_MouseDoubleClick;

            // Watchdog：WinForms NotifyIcon 的 MouseLeave 不可靠，用轮询判断鼠标是否离开托盘图标
            this._toolTipWatchdog = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(300),
            };
            this._toolTipWatchdog.Tick += this.ToolTipWatchdog_Tick;

            this.Loaded += this.TaskbarIconControl_Loaded;
            this.Unloaded += this.TaskbarIconControl_Unloaded;

            if (Application.Current.MainWindow is NewMainWindow nmw)
            {
                nmw.ShowBalloonTipCallback = (title, msg, sym) =>
                {
                    // 当关闭托盘提示开关打开时，同时抑制气球提示（开播通知、最小化提示等）
                    if (this.IsToolTipDisabled())
                        return;

                    this._notifyIcon.ShowBalloonTip(5000, title, msg, MapBalloonIcon(sym));
                };
            }
        }

        private void TaskbarIconControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is RootModel model)
            {
                model.PropertyChanged += this.RootModel_PropertyChanged;
                if (model.Recorder?.Config?.Global != null)
                {
                    model.Recorder.Config.Global.PropertyChanged += this.Global_PropertyChanged;
                }
            }
        }

        private void TaskbarIconControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is RootModel model)
            {
                model.PropertyChanged -= this.RootModel_PropertyChanged;
                if (model.Recorder?.Config?.Global != null)
                {
                    model.Recorder.Config.Global.PropertyChanged -= this.Global_PropertyChanged;
                }
            }
        }

        private void Global_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 实时响应“关闭托盘图标悬浮提示”开关：开启时立即隐藏并阻止再次显示
            if (e.PropertyName == nameof(GlobalConfig.WpfDisableTrayToolTip))
            {
                if (this.IsToolTipDisabled())
                {
                    this.HideTrayToolTip();
                }
            }
        }

        private void RootModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RootModel.Recorder) && sender is RootModel model && model.Recorder?.Config?.Global != null)
            {
                model.Recorder.Config.Global.PropertyChanged += this.Global_PropertyChanged;
            }
        }

        #region 托盘事件
        private void NotifyIcon_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            this._lastTrayMouseMove = DateTime.Now;

            if (this.IsToolTipDisabled())
                return;

            if (!this._trayToolTipPopup.IsOpen)
            {
                this.ShowTrayToolTipNearCursor();
            }

            if (!this._toolTipWatchdog.IsEnabled)
            {
                this._toolTipWatchdog.Start();
            }
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // 右键菜单由 NotifyIcon.ContextMenuStrip 自动弹出并自动关闭，无需手动处理
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                this.HideTrayToolTip();
            }
        }

        private void NotifyIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                this.HideTrayToolTip();
                this.ShowHideMainWindow();
            }
        }
        #endregion

        #region tooltip 自动关闭（watchdog 轮询）
        private void ToolTipWatchdog_Tick(object sender, EventArgs e)
        {
            // 鼠标仍在 Popup 内部时不关闭
            if (this._trayToolTipPopup.IsMouseOver)
                return;

            // 鼠标离开托盘图标超过 500ms 则关闭 tooltip
            if (DateTime.Now - this._lastTrayMouseMove > TimeSpan.FromMilliseconds(500))
            {
                this.HideTrayToolTip();
            }
        }
        #endregion

        #region 富 tooltip 显示/隐藏（自绘 Popup，位置跟随鼠标，绝不会跑到左上角）
        private void ShowTrayToolTipNearCursor()
        {
            if (!GetCursorPos(out POINT cursorPos))
                return;

            // 屏幕坐标直接用于 Placement=Absolute 的 Popup
            this._trayToolTipPopup.HorizontalOffset = cursorPos.X + 12;
            this._trayToolTipPopup.VerticalOffset = cursorPos.Y + 12;
            this._trayToolTipPopup.IsOpen = true;
        }

        private void HideTrayToolTip()
        {
            this._trayToolTipPopup.IsOpen = false;
            this._toolTipWatchdog.Stop();
        }
        #endregion

        #region 右键菜单构建（ContextMenuStrip，WinForms 自动关闭）
        private FormsContextMenuStrip BuildContextMenuStrip()
        {
            var menu = new FormsContextMenuStrip();
            menu.Items.Add(new FormsToolStripMenuItem("显示/隐藏", (s, e) => this.ShowHideMainWindow()));
            menu.Items.Add(new FormsToolStripMenuItem("开始全部录制", (s, e) => this.MenuItemStartAll_Click()));
            menu.Items.Add(new FormsToolStripMenuItem("停止全部录制", (s, e) => this.MenuItemStopAll_Click()));
            menu.Items.Add(new FormsToolStripMenuItem("打开工作目录", (s, e) => this.MenuItemOpenWorkDir_Click()));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(new FormsToolStripMenuItem("退出", (s, e) => this.MenuItemExit_Click()));
            return menu;
        }
        #endregion

        #region 菜单动作
        private void ShowHideMainWindow()
        {
            if (!(Application.Current.MainWindow is NewMainWindow nmw))
                return;

            if (nmw.Visibility == Visibility.Visible && nmw.WindowState != WindowState.Minimized)
            {
                nmw.Hide();
            }
            else
            {
                nmw.SuperActivateAction();
            }
        }

        private void MenuItemStartAll_Click()
        {
            if (this.DataContext is RootModel model && model.Recorder != null)
            {
                foreach (var room in model.Recorder.Rooms)
                {
                    room.StartRecord();
                }
            }
        }

        private void MenuItemStopAll_Click()
        {
            if (this.DataContext is RootModel model && model.Recorder != null)
            {
                foreach (var room in model.Recorder.Rooms)
                {
                    room.StopRecord();
                }
            }
        }

        private void MenuItemOpenWorkDir_Click()
        {
            if (this.DataContext is RootModel model && model.Recorder?.Config?.Global != null)
            {
                Process.Start("explorer.exe", model.Recorder.Config.Global.WorkDirectory);
            }
        }

        private void MenuItemExit_Click()
        {
            // 触发主窗口关闭流程（含退出确认对话框）
            Application.Current.MainWindow?.Close();
        }
        #endregion

        private bool IsToolTipDisabled()
        {
            return this.DataContext is RootModel model
                && model.Recorder?.Config?.Global?.WpfDisableTrayToolTip == true;
        }

        private static NotifyIconBalloon MapBalloonIcon(BalloonIcon sym)
        {
            return sym switch
            {
                BalloonIcon.Warning => NotifyIconBalloon.Warning,
                BalloonIcon.Error => NotifyIconBalloon.Error,
                _ => NotifyIconBalloon.Info,
            };
        }

        #region Win32 辅助
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        #endregion

        #region 释放
        public void Dispose()
        {
            if (this._disposed)
                return;
            this._disposed = true;

            this._toolTipWatchdog.Stop();
            this.HideTrayToolTip();

            try
            {
                this._notifyIcon.Visible = false;
                this._notifyIcon.Dispose();
                this._contextMenuStrip.Dispose();
            }
            catch
            {
                // ignored
            }
        }
        #endregion
    }
}

using Eyeshade.Log;
using Eyeshade.FuncModule;
using Eyeshade.TrayIcon;
using Microsoft.Graphics.Display;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.Foundation;
using System.Text;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Eyeshade
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        #region fields
        private readonly TrayIcon.TrayIcon _trayIcon;
        private readonly IntPtr _hWnd;
        private ILogWrapper? _logger;
        private EyeshadeModule? _eyeshadeModule;
        private double _eyeshadePreProgress = 1;
        private int _eyeshadePreRemainingMilliseconds = 0;
        private MediaPlayer _mediaPlayer;
        private readonly Windows.Win32.UI.Shell.SUBCLASSPROC _wndProc;
        #endregion

        public MainWindow()
        {
            this.InitializeComponent();

            // 设置窗口大小
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dpi = PInvoke.GetDpiForWindow(new HWND(_hWnd));
            var dipRateVirtualToPhysical = dpi / 96d;
            AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(300 * dipRateVirtualToPhysical), (int)(400 * dipRateVirtualToPhysical)));

            // 设置自定义窗口标题栏
            this.ExtendsContentIntoTitleBar = true;  // enable custom titlebar
            this.SetTitleBar(AppTitleBar);      // set user ui element as titlebar
            AppWindow.SetIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico"));

            // 设置托盘图标
            _trayIcon = new TrayIcon.TrayIcon(_hWnd, 0);
            _trayIcon.AddMenuItem(0, "关闭菜单"); // 这个选项触发时什么也不做
            _trayIcon.AddSubMenu(10, "推迟");
            _trayIcon.AddSubMenuItem(10, 11, "推迟8分钟");
            _trayIcon.AddSubMenuItem(10, 12, "推迟15分钟");
            _trayIcon.AddSubMenuItem(10, 13, "推迟30分钟");
            _trayIcon.AddMenuItem(1, "暂停"); // 或恢复
            _trayIcon.AddMenuItem(2, "马上休息");
            _trayIcon.AddMenuItem(3, "退出");
            _trayIcon.Select += TrayIcon_Select;
            _trayIcon.MenuItemExecute += TrayIcon_MenuItemExecute;

            // 用户点击关闭按钮时执行隐藏窗口
            AppWindow.Closing += AppWindow_Closing;

            // 设置音效播放器
            _mediaPlayer = new MediaPlayer();

            // 设置窗口消息处理函数
            _wndProc = new Windows.Win32.UI.Shell.SUBCLASSPROC(WndProc);
            if (!PInvoke.SetWindowSubclass(new HWND(_hWnd), _wndProc, 0, 0))
                throw new Win32Exception();

            // 注册以在系统暂停或恢复时接收通知
            var hPowerNotify = PInvoke.RegisterSuspendResumeNotification(new HANDLE(_hWnd), Windows.Win32.UI.WindowsAndMessaging.REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
            if (hPowerNotify.IsNull)
                throw new Win32Exception();
        }

        public MainWindow(ILogWrapper logger) : this()
        {
            _logger = logger;
            _eyeshadeModule = new EyeshadeModule(logger);
            _eyeshadeModule.StateChanged += EyeshadeModule_StateChanged;
            _eyeshadeModule.IsPausedChanged += EyeshadeModule_IsPausedChanged;
            _eyeshadeModule.ProgressChanged += EyeshadeModule_ProgressChanged;
        }

        public void ShowHide()
        {
            // 显示托盘图标
            _trayIcon.Show(GetCurrentStateTrayIcon(), Title, useCustomPopup: false);
            PInvoke.ShowWindow(new HWND(_hWnd), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
        }

        public void ShowNormal()
        {
            // 显示托盘图标
            _trayIcon.Show(GetCurrentStateTrayIcon(), Title, useCustomPopup: false);
            Thread.Sleep(200); // 等待托盘图标显示完成ShowNearToTrayIcon才能获取到正确的显示位置
            ShowNearToTrayIcon();
        }

        private LRESULT WndProc(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam,
            nuint uIdSubclass, nuint dwRefData)
        {
            if (uMsg == PInvoke.WM_POWERBROADCAST)
            {
                if (PInvoke.PBT_APMSUSPEND == wParam)
                {
                    _eyeshadeModule?.Pause();
                }
                else if (PInvoke.PBT_APMRESUMESUSPEND == wParam || // 仅当应用程序在计算机挂起之前收到 PBT_APMSUSPEND 事件时，应用程序才能接收此事件。
                    PInvoke.PBT_APMRESUMECRITICAL == wParam) // 通知应用程序系统已恢复操作。 此事件可以指示部分或所有应用程序未收到 PBT_APMSUSPEND 事件
                {
                    _eyeshadeModule?.Resume();
                }
            }

            _trayIcon.ProcessWindowMessage(hWnd, uMsg, wParam, lParam);
            return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        #region EyeshadeModule
        private void EyeshadeModule_StateChanged(object? sender, EventArgs e)
        {
            _trayIcon.SetIcon(GetCurrentStateTrayIcon());

            DispatcherQueue?.TryEnqueue(() =>
            {
                var module = _eyeshadeModule;
                if (module == null) return;

                if (module.State == EyeshadeStates.Resting)
                {
                    ShowRestingWindow();
                }
                else
                {
                    CloseRestingWindow();
                }

                double volume = Math.Min(1, Math.Max(0, module.RingerVolume / 100d));
                _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Medias\school-chime.mp3"), UriKind.Absolute));
                _mediaPlayer.Volume = volume;
                _mediaPlayer.Play();
            });
        }

        private void EyeshadeModule_ProgressChanged(object? sender, EventArgs e)
        {
            var module = _eyeshadeModule;
            if (module == null) return;

            var currentProgress = module.Progress;
            var remainningMilliseconds = module.RemainingMilliseconds;
            if (module.State == EyeshadeStates.Work)
            {
                if ((int)(_eyeshadePreProgress / 0.25) != (int)(currentProgress / 0.25))
                {
                    // 进度相差了1/4，需要更新托盘图标
                    _trayIcon.SetIcon(GetCurrentStateTrayIcon());
                }

                if (_eyeshadePreRemainingMilliseconds > 10000 && remainningMilliseconds <= 10000)
                {
                    // 最后工作倒计时最后十秒显示主窗口
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        ShowNearToTrayIcon();
                    });
                }
            }

            // 更新托盘tooltip提示
            if (remainningMilliseconds > 60000)
            {
                // 大于1分钟则每分钟更新一次
                if (_eyeshadePreRemainingMilliseconds / 60000 != remainningMilliseconds / 60000)
                {
                    var timespan = TimeSpan.FromMilliseconds(remainningMilliseconds);
                    StringBuilder tooltip = new StringBuilder();
                    if (timespan.Days > 0) tooltip.Append($"{timespan.Days}天");
                    if (timespan.Hours > 0) tooltip.Append($"{timespan.Hours}小时");

                    tooltip.Append($"{timespan.Minutes}分");
                    _trayIcon.SetTooltip($"剩余时间 {tooltip}");
                }
            }
            else if (remainningMilliseconds > 10000)
            {
                // 大于10秒小于1分钟则每5秒更新一次
                if (_eyeshadePreRemainingMilliseconds / 5000 != remainningMilliseconds / 5000)
                {
                    _trayIcon.SetTooltip($"剩余时间 {remainningMilliseconds / 1000}秒");
                }
            }
            else
            {
                // 小于10秒则每秒更新一次
                _trayIcon.SetTooltip($"剩余时间 {remainningMilliseconds / 1000}秒");
            }

            _eyeshadePreProgress = currentProgress;
            _eyeshadePreRemainingMilliseconds = remainningMilliseconds;
        }

        private void EyeshadeModule_IsPausedChanged(object? sender, EventArgs e)
        {
            var module = _eyeshadeModule;
            if (module != null)
            {
                _trayIcon.SetMenuItem(1, module.IsPaused ? "恢复" : "暂停");
            }

            _trayIcon.SetIcon(GetCurrentStateTrayIcon());
        }

        private string GetCurrentStateTrayIcon()
        {
            string trayIcon = @"Images\TrayIcon\100.ico";
            var module = _eyeshadeModule;
            if (module != null)
            {
                var progress = module.Progress;

                if (module.IsPaused) trayIcon = @"Images\TrayIcon\pause.ico";
                else if (module.State == EyeshadeStates.Resting) trayIcon = @"Images\TrayIcon\resting.ico";
                else if (progress > 0.75) trayIcon = @"Images\TrayIcon\100.ico";
                else if (progress > 0.5) trayIcon = @"Images\TrayIcon\75.ico";
                else if (progress > 0.25) trayIcon = @"Images\TrayIcon\50.ico";
                else trayIcon = @"Images\TrayIcon\25.ico";
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, trayIcon);
        }

        private unsafe void ShowRestingWindow()
        {
            if (AppWindow == null) return;

            Root.RequestedTheme = ElementTheme.Dark;
            RestingBackground.Visibility = Visibility.Visible;

            if (AppWindow.Presenter.Kind != Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
            {
                AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
            }

            if (!AppWindow.IsVisible)
            {
                AppWindow.Show();
            }

            // https://github.com/microsoft/microsoft-ui-xaml/issues/8562
            // MoveInZOrderAtTop/SetWindowPos does not activate a window. 
            // When a window that isn't part of the foreground process tries
            // to use SetWindowPos with HWND_TOP, Windows will not allow the
            // window to appear on top of the foreground window

            // 因为2000/XP改变了SetForegroundWindow的执行方式，不允许随便把窗口提前，
            // 打扰用户的工作。可以用附加本线程到最前面窗口的线程，从而欺骗windows。
            var hWndForeground = PInvoke.GetForegroundWindow();
            var foregourndThreadId = PInvoke.GetWindowThreadProcessId(hWndForeground, null);
            var currentThreadId = PInvoke.GetCurrentThreadId();
            if (foregourndThreadId != currentThreadId)
            {
                if (foregourndThreadId != 0 && currentThreadId != 0)
                {
                    PInvoke.AttachThreadInput(foregourndThreadId, currentThreadId, new BOOL(true));
                }
                PInvoke.SetForegroundWindow(new HWND(_hWnd));
                PInvoke.SetFocus(new HWND(_hWnd));
                AppWindow.MoveInZOrderAtTop();
                if (foregourndThreadId != 0 && currentThreadId != 0)
                {
                    PInvoke.AttachThreadInput(foregourndThreadId, currentThreadId, new BOOL(false));
                }
            }
        }

        private void CloseRestingWindow()
        {
            if (AppWindow == null) return;

            Root.RequestedTheme = ElementTheme.Default;
            RestingBackground.Visibility = Visibility.Collapsed;
            AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
            AppWindow.Hide();
        }
        #endregion

        #region TrayIcon
        private void TrayIcon_Select(object? sender, EventArgs e)
        {
            if (AppWindow.IsVisible)
            {
                AppWindow.Hide();
            }
            else
            {
                ShowNearToTrayIcon();
            }
        }

        private void TrayIcon_MenuItemExecute(object? sender, TrayIconMenuItemExecuteArgs e)
        {
            switch (e.Id)
            {
                case 1: // 暂停
                    {
                        var module = _eyeshadeModule;
                        if (module != null)
                        {
                            if (module.IsPaused)
                            {
                                module.Resume();
                            }
                            else
                            {
                                module.Pause();
                            }
                        }
                    }
                    break;
                case 2: // 马上休息
                    {
                        if (_eyeshadeModule?.State == EyeshadeStates.Work)
                        {
                            _eyeshadeModule.Rest();
                        }
                    }
                    break;
                case 3: // 退出
                    {
                        Close();
                    }
                    break;
                case 11: // 推迟8分钟
                    {
                        _eyeshadeModule?.Defer(TimeSpan.FromMinutes(8));
                    }
                    break;
                case 12: // 推迟15分钟
                    {
                        _eyeshadeModule?.Defer(TimeSpan.FromMinutes(15));
                    }
                    break;
                case 13: // 推迟30分钟
                    {
                        _eyeshadeModule?.Defer(TimeSpan.FromMinutes(30));
                    }
                    break;
                default:
                    break;
            }
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            // 用户点击关闭按钮时执行隐藏窗口
            AppWindow.Hide();
            args.Cancel = true;
        }

        /// <summary>
        /// 在托盘附近显示窗口
        /// </summary>
        private unsafe void ShowNearToTrayIcon()
        {
            if (AppWindow == null) return;

            if (PInvoke.IsIconic(new HWND(_hWnd)) || PInvoke.IsZoomed(new HWND(_hWnd))) // 窗口是否最小化或最大化
            {
                PInvoke.ShowWindow(new HWND(_hWnd), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_NORMAL);
            }

            if (AppWindow.Presenter.Kind != Microsoft.UI.Windowing.AppWindowPresenterKind.Default)
            {
                // 从全屏状态恢复到默认状态
                AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
            }

            var size = AppWindow.Size;
            var position = _trayIcon.CalculatePopupWindowPosition(size.Width, size.Height);
            AppWindow.Move(new Windows.Graphics.PointInt32(position.X, position.Y));

            if (!AppWindow.IsVisible)
            {
                AppWindow.Show();
            }

            // https://github.com/microsoft/microsoft-ui-xaml/issues/8562
            // MoveInZOrderAtTop/SetWindowPos does not activate a window. 
            // When a window that isn't part of the foreground process tries
            // to use SetWindowPos with HWND_TOP, Windows will not allow the
            // window to appear on top of the foreground window

            // 因为2000/XP改变了SetForegroundWindow的执行方式，不允许随便把窗口提前，
            // 打扰用户的工作。可以用附加本线程到最前面窗口的线程，从而欺骗windows。
            var hWndForeground = PInvoke.GetForegroundWindow();
            var foregourndThreadId = PInvoke.GetWindowThreadProcessId(hWndForeground, null);
            var currentThreadId = PInvoke.GetCurrentThreadId();
            if (foregourndThreadId != currentThreadId)
            {
                if (foregourndThreadId != 0 && currentThreadId != 0)
                {
                    PInvoke.AttachThreadInput(foregourndThreadId, currentThreadId, new BOOL(true));
                }
                PInvoke.SetForegroundWindow(new HWND(_hWnd));
                PInvoke.SetFocus(new HWND(_hWnd));
                AppWindow.MoveInZOrderAtTop();
                if (foregourndThreadId != 0 && currentThreadId != 0)
                {
                    PInvoke.AttachThreadInput(foregourndThreadId, currentThreadId, new BOOL(false));
                }
            }
        }
        #endregion

        #region Navigation
        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            NavView.SelectedItem = NavView.MenuItems[0];
            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the home page.
            NavView_Navigate(typeof(Views.HomePage), new EntranceNavigationTransitionInfo());
        }

        private void NavView_ItemInvoked(NavigationView sender,
                                 NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked == true)
            {
                NavView_Navigate(typeof(Views.SettingsPage), args.RecommendedNavigationTransitionInfo);
            }
            else if (args.InvokedItemContainer != null)
            {
                var navPageType = Type.GetType(args.InvokedItemContainer.Tag.ToString()!);
                if (navPageType != null)
                    NavView_Navigate(navPageType, args.RecommendedNavigationTransitionInfo);
            }
        }

        private void NavView_Navigate(Type navPageType, NavigationTransitionInfo transitionInfo)
        {
            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (navPageType is not null && !Type.Equals(preNavPageType, navPageType))
            {
                ContentFrame.Navigate(navPageType, null, transitionInfo);
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            TryGoBack();
        }

        private bool TryGoBack()
        {
            if (!ContentFrame.CanGoBack)
                return false;

            // Don't go back if the nav pane is overlayed.
            if (NavView.IsPaneOpen &&
                (NavView.DisplayMode == NavigationViewDisplayMode.Compact ||
                 NavView.DisplayMode == NavigationViewDisplayMode.Minimal))
                return false;

            ContentFrame.GoBack();
            return true;
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            NavView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType == typeof(Views.SettingsPage))
            {
                // SettingsItem is not part of NavView.MenuItems, and doesn't have a Tag.
                NavView.SelectedItem = (NavigationViewItem)NavView.SettingsItem;
                NavView.Header = "设置";
                var settingsPage = ContentFrame.Content as Views.SettingsPage;
                if (settingsPage != null)
                {
                    settingsPage.EyeshadeModule = _eyeshadeModule;
                }
            }
            else if (ContentFrame.SourcePageType != null)
            {
                // Select the nav view item that corresponds to the page being navigated to.
                NavView.SelectedItem = NavView.MenuItems
                            .OfType<NavigationViewItem>()
                            .First(i => i.Tag.Equals(ContentFrame.SourcePageType.FullName?.ToString()));

                if (ContentFrame.SourcePageType == typeof(Views.HomePage)) // 窗口太小了，Header太占空间，主页就不显示Header了
                {
                    NavView.Header = null;
                    var homePage = ContentFrame.Content as Views.HomePage;
                    if (homePage != null)
                    {
                        homePage.EyeshadeModule = _eyeshadeModule;
                    }
                }
                else
                {
                    NavView.Header = ((NavigationViewItem)NavView.SelectedItem)?.Content?.ToString();
                }
            }
        }
        #endregion
    }
}

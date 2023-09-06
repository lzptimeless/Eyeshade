using Eyeshade.Log;
using Eyeshade.Modules;
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.Foundation;

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
        private AlarmClockModule? _alarmClockModule;
        private MediaPlayer _mediaPlayer;
        #endregion

        public MainWindow()
        {
            this.InitializeComponent();

            // 设置窗口大小
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dpi = PInvoke.GetDpiForWindow(new HWND(_hWnd));
            var dpiRate = dpi / 96d;
            AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(300 * dpiRate), (int)(400 * dpiRate)));

            // 设置自定义窗口标题栏
            this.ExtendsContentIntoTitleBar = true;  // enable custom titlebar
            this.SetTitleBar(AppTitleBar);      // set user ui element as titlebar
            AppWindow.SetIcon(@"Images\TrayIcon\100.ico");

            // 设置托盘图标
            _trayIcon = new TrayIcon.TrayIcon(_hWnd, 0);
            _trayIcon.AddMenuItem(0, "关闭菜单"); // 这个选项触发时什么也不做
            _trayIcon.AddMenuItem(1, "打开主窗口");
            _trayIcon.AddMenuItem(2, "退出");
            _trayIcon.Clicked += _trayIcon_Clicked;
            _trayIcon.MenuItemExecute += _trayIcon_MenuItemExecute;

            // 用户点击关闭按钮时执行隐藏窗口
            AppWindow.Closing += AppWindow_Closing;

            // 设置音效播放器
            _mediaPlayer = new MediaPlayer();
        }

        public MainWindow(ILogWrapper logger) : this()
        {
            _logger = logger;
            _alarmClockModule = new AlarmClockModule(logger);
            _alarmClockModule.StateChanged += _alarmClockModule_StateChanged;
            _alarmClockModule.IsPausedChanged += _alarmClockModule_IsPausedChanged;
            _alarmClockModule.ProgressChanged += _alarmClockModule_ProgressChanged;
        }

        public AlarmClockModule? AlarmClockModule => _alarmClockModule;

        private void _alarmClockModule_StateChanged(object? sender, AlarmClockStateChangedArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.State == AlarmClockStates.Resting)
                {
                    ShowRestingWindow();
                }
                else
                {
                    CloseRestingWindow();
                }

                var module = sender as AlarmClockModule;
                if (module != null)
                {
                    UpdateTrayIcon(module.State, module.IsPaused, module.Progress);
                }

                _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Medias\school-chime.mp3"), UriKind.Absolute));
                _mediaPlayer.Play();
            });
        }

        private void _alarmClockModule_ProgressChanged(object? sender, AlarmClockProgressChangedArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var module = sender as AlarmClockModule;
                if (module != null)
                {
                    var state = module.State;
                    UpdateTrayIcon(state, module.IsPaused, e.Progress);

                    if (state == AlarmClockStates.Work && module.RemainingTime <= TimeSpan.FromSeconds(10))
                    {
                        ShowNearToTrayIcon();
                    }
                }
            });
        }

        private void _alarmClockModule_IsPausedChanged(object? sender, AlarmClockIsPausedChangedArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var module = sender as AlarmClockModule;
                if (module != null)
                {
                    UpdateTrayIcon(module.State, e.IsPause, module.Progress);
                }
            });
        }

        private void UpdateTrayIcon(AlarmClockStates state, bool isPaused, double progress)
        {
            if (isPaused) _trayIcon.SetIcon(@"Images\TrayIcon\pause.ico");
            else if (state == AlarmClockStates.Resting) _trayIcon.SetIcon(@"Images\TrayIcon\resting.ico");
            else if (progress > 0.75) _trayIcon.SetIcon(@"Images\TrayIcon\100.ico");
            else if (progress > 0.5) _trayIcon.SetIcon(@"Images\TrayIcon\75.ico");
            else if (progress > 0.25) _trayIcon.SetIcon(@"Images\TrayIcon\50.ico");
            else _trayIcon.SetIcon(@"Images\TrayIcon\25.ico");
        }

        private void ShowRestingWindow()
        {
            Root.RequestedTheme = ElementTheme.Dark;
            RestingBackground.Visibility = Visibility.Visible;
            PInvoke.ShowWindow(new HWND(_hWnd), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_MAXIMIZE);

            // https://github.com/microsoft/microsoft-ui-xaml/issues/8562
            // MoveInZOrderAtTop/SetWindowPos does not activate a window. 
            // When a window that isn't part of the foreground process tries
            // to use SetWindowPos with HWND_TOP, Windows will not allow the
            // window to appear on top of the foreground window
            // AppWindow.MoveInZOrderAtTop();
            PInvoke.SetForegroundWindow(new HWND(_hWnd));
        }

        private void CloseRestingWindow()
        {
            Root.RequestedTheme = ElementTheme.Default;
            RestingBackground.Visibility = Visibility.Collapsed;
            PInvoke.ShowWindow(new HWND(_hWnd), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_NORMAL);

            AppWindow.Hide();
        }

        #region TrayIcon
        private void _trayIcon_Clicked(object? sender, EventArgs e)
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

        private void _trayIcon_MenuItemExecute(object? sender, TrayIconMenuItemExecuteArgs e)
        {
            if (e.Id == 1)
            {
                // 打开主窗口
                ShowNearToTrayIcon();
            }
            else if (e.Id == 2)
            {
                // 退出
                Close();
            }
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            // 用户点击关闭按钮时执行隐藏窗口
            AppWindow.Hide();
            args.Cancel = true;
        }

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            // 显示托盘图标
            _trayIcon.Show(@"Images\TrayIcon\100.ico", Title);

            var module = _alarmClockModule;
            if (module != null)
            {
                UpdateTrayIcon(module.State, module.IsPaused, module.Progress);
            }
        }

        /// <summary>
        /// 在托盘附近显示窗口
        /// </summary>
        private void ShowNearToTrayIcon()
        {
            if (PInvoke.IsIconic(new HWND(_hWnd)) || PInvoke.IsZoomed(new HWND(_hWnd))) // 窗口是否最小化或最大化
            {
                PInvoke.ShowWindow(new HWND(_hWnd), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_NORMAL);
            }

            var size = AppWindow.Size;
            var position = _trayIcon.CalculatePopupWindowPosition(size.Width, size.Height);
            AppWindow.Move(new Windows.Graphics.PointInt32(position.X, position.Y));

            if (!AppWindow.IsVisible)
            {
                AppWindow.Show(true);
            }

            // https://github.com/microsoft/microsoft-ui-xaml/issues/8562
            // MoveInZOrderAtTop/SetWindowPos does not activate a window. 
            // When a window that isn't part of the foreground process tries
            // to use SetWindowPos with HWND_TOP, Windows will not allow the
            // window to appear on top of the foreground window
            // AppWindow.MoveInZOrderAtTop();
            PInvoke.SetForegroundWindow(new HWND(_hWnd));
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
                    settingsPage.AlarmClockModule = AlarmClockModule;
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
                        homePage.AlarmClockModule = AlarmClockModule;
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

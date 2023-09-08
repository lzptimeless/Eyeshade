using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using System.ComponentModel.Design;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Win32;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Eyeshade
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TrayIconTooltipWindow : Window
    {
        private readonly IntPtr _hWnd;

        public TrayIconTooltipWindow()
        {
            this.InitializeComponent();

            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (AppWindow != null)
            {
                AppWindow.IsShownInSwitchers = false;
                var presenter = AppWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                    presenter.SetBorderAndTitleBar(true, false);
                }
            }
        }

        public TrayIconTooltipData Data { get; private set; } = new TrayIconTooltipData();

        public Size CalculateDesiredSize()
        {
            Root.InvalidateMeasure();
            if (Root.DesiredSize.Width > 0)
            {
                return Root.DesiredSize;
            }
            else
            {
                return new Size(200, 35);
            }
        }

        public void Show(int x, int y, int width, int height)
        {
            if (AppWindow != null)
            {
                AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
                AppWindow.Show();
                // https://github.com/microsoft/microsoft-ui-xaml/issues/8562
                // MoveInZOrderAtTop/SetWindowPos does not activate a window. 
                // When a window that isn't part of the foreground process tries
                // to use SetWindowPos with HWND_TOP, Windows will not allow the
                // window to appear on top of the foreground window
                PInvoke.SetForegroundWindow(new Windows.Win32.Foundation.HWND(_hWnd));
                AppWindow.MoveInZOrderAtTop();
            }
        }

        public void Hide()
        {
            AppWindow?.Hide();
        }
    }

    public class TrayIconTooltipData : INotifyPropertyChanged
    {
        private string? _tooltip;
        public string? Tooltip
        {
            get { return _tooltip; }
            set
            {
                _tooltip = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

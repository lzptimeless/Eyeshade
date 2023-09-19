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
using Eyeshade.FuncModule;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.WindowManagement;
using System.Reflection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Eyeshade.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        private bool _isWindowVisible = true;
        private EyeshadeModule? _eyeshadeModule;

        public HomePage()
        {
            Data = new HomeData();
            this.InitializeComponent();
        }

        public EyeshadeModule? EyeshadeModule
        {
            get { return _eyeshadeModule; }
            set
            {
                if (value != _eyeshadeModule)
                {
                    if (_eyeshadeModule != null)
                    {
                        _eyeshadeModule.StateChanged -= EyeshadeModule_StateChanged;
                        _eyeshadeModule.ProgressChanged -= EyeshadeModule_ProgressChanged;
                        _eyeshadeModule.IsUserPausedChanged -= EyeshadeModule_IsPausedChanged;
                    }
                    _eyeshadeModule = value;
                    if (_eyeshadeModule != null)
                    {
                        _eyeshadeModule.StateChanged += EyeshadeModule_StateChanged;
                        _eyeshadeModule.ProgressChanged += EyeshadeModule_ProgressChanged;
                        _eyeshadeModule.IsUserPausedChanged += EyeshadeModule_IsPausedChanged;
                    }
                }
            }
        }

        public HomeData Data { get; private set; }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var appWindow = App.Current.GetMainWindow()?.AppWindow;
            if (appWindow != null)
            {
                _isWindowVisible = appWindow.IsVisible;
                appWindow.Changed += AppWindow_Changed;

                if (_isWindowVisible)
                {
                    ReadData();
                }
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            var appWindow = App.Current.GetMainWindow()?.AppWindow;
            if (appWindow != null)
            {
                appWindow.Changed -= AppWindow_Changed;
            }

            if (_eyeshadeModule != null)
            {
                _eyeshadeModule.StateChanged -= EyeshadeModule_StateChanged;
                _eyeshadeModule.ProgressChanged -= EyeshadeModule_ProgressChanged;
                _eyeshadeModule.IsUserPausedChanged -= EyeshadeModule_IsPausedChanged;
            }
        }

        private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (args.DidVisibilityChange)
            {
                _isWindowVisible = sender.IsVisible;
                if (_isWindowVisible && IsLoaded)
                {
                    ReadData();
                }
            }
        }

        private void EyeshadeModule_IsPausedChanged(object? sender, EventArgs e)
        {
            if (_isWindowVisible)
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    ReadData();
                });
            }
        }

        private void EyeshadeModule_StateChanged(object? sender, EventArgs e)
        {
            if (_isWindowVisible)
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    ReadData();
                });
            }
        }

        private void EyeshadeModule_ProgressChanged(object? sender, EventArgs e)
        {
            if (_isWindowVisible)
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    if (IsLoaded)
                    {
                        ReadData();
                    }
                });
            }
        }

        private void ReadData()
        {
            var module = _eyeshadeModule;
            if (module == null) return;

            Data.RemainingMilliseconds = module.RemainingMilliseconds;
            Data.CountdownProgressValue = Math.Min(100, Math.Max(0, (int)(100 * module.Progress)));
            Data.IsUserPaused = module.IsUserPaused;
            Data.EyeshadeState = module.State;

            if (module.IsUserPaused)
            {
                if (PauseOrResumeCommand.Label != "恢复")
                {
                    PauseOrResumeCommand.Label = "恢复";
                    PauseOrResumeCommand.Description = "恢复计时";
                    PauseOrResumeCommand.IconSource = new SymbolIconSource() { Symbol = Symbol.Play };
                }
            }
            else
            {
                if (PauseOrResumeCommand.Label != "暂停")
                {
                    PauseOrResumeCommand.Label = "暂停";
                    PauseOrResumeCommand.Description = "暂停计时";
                    PauseOrResumeCommand.IconSource = new SymbolIconSource() { Symbol = Symbol.Pause };
                }
            }

            if (module.State == EyeshadeStates.Work)
            {
                if (WorkOrRestCommand.Label != "休息")
                {
                    WorkOrRestCommand.Label = "休息";
                    WorkOrRestCommand.Description = "马上休息";
                    WorkOrRestCommand.IconSource = new FontIconSource() { Glyph = "\uE708" };
                }
            }
            else
            {
                if (WorkOrRestCommand.Label != "工作")
                {
                    WorkOrRestCommand.Label = "工作";
                    WorkOrRestCommand.Description = "马上工作";
                    WorkOrRestCommand.IconSource = new FontIconSource() { Glyph = "\uE706" };
                }
            }
        }

        private void DeferCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var module = EyeshadeModule;
            if (module == null) return;

            if (args.Parameter is int)
            {
                var deferMinutes = (int)args.Parameter;
                module.Defer(TimeSpan.FromMinutes(deferMinutes));
                ReadData();
            }
        }

        private void PauseOrResumeCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var module = EyeshadeModule;
            if (module == null) return;

            if (module.IsUserPaused)
            {
                module.UserResume();
            }
            else
            {
                module.UserPause();
            }
        }

        private void WorkOrRestCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var module = EyeshadeModule;
            if (module == null) return;

            if (module.State == EyeshadeStates.Work)
            {
                module.Rest();
            }
            else
            {
                module.Work();
            }
        }
    }

    public class HomeData : INotifyPropertyChanged
    {
        private int _remainingMilliseconds;
        public int RemainingMilliseconds
        {
            get { return _remainingMilliseconds; }
            set
            {
                if (_remainingMilliseconds != value)
                {
                    _remainingMilliseconds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Countdown));
                    OnPropertyChanged(nameof(CountdownUnit));
                }
            }
        }

        public int Countdown
        {
            get
            {
                int countdown;
                if (_remainingMilliseconds > 60000) countdown = _remainingMilliseconds / 60000;
                else countdown = _remainingMilliseconds / 1000;

                return countdown;
            }
        }

        public string CountdownUnit
        {
            get
            {
                if (_remainingMilliseconds > 60000)
                {
                    return $"分 {_remainingMilliseconds % 60000 / 1000} 秒";
                }
                else
                {
                    return "秒";
                }
            }
        }

        private int _countdownProgressValue;
        public int CountdownProgressValue
        {
            get { return _countdownProgressValue; }
            set
            {
                if (_countdownProgressValue != value)
                {
                    _countdownProgressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isUserPaused;
        public bool IsUserPaused
        {
            get { return _isUserPaused; }
            set
            {
                if (_isUserPaused != value)
                {
                    _isUserPaused = value;
                    OnPropertyChanged();
                }
            }
        }

        private EyeshadeStates _eyeshadeState;
        public EyeshadeStates EyeshadeState
        {
            get { return _eyeshadeState; }
            set
            {
                if (_eyeshadeState != value)
                {
                    _eyeshadeState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StateTitle));
                    OnPropertyChanged(nameof(IsResting));
                }
            }
        }

        public bool IsResting => _eyeshadeState == EyeshadeStates.Resting;

        public string StateTitle
        {
            get
            {
                return _eyeshadeState == EyeshadeStates.Work ? "工作中……" : "休息中……";
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

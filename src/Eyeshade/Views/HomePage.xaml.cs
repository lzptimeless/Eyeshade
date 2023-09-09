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
using Eyeshade.Modules;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.WindowManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Eyeshade.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        private readonly DispatcherTimer _countdownTimer;

        public HomePage()
        {
            Data = new HomeData();

            this.InitializeComponent();

            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Tick += _countdownTimer_Tick;
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        }

        public AlarmClockModule? AlarmClockModule { get; set; }
        public HomeData Data { get; private set; }

        private void _countdownTimer_Tick(object? sender, object e)
        {
            ReadData();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ReadData();
            var appWindow = (App.Current as App)?.MainWindow?.AppWindow;
            if (appWindow != null)
            {
                appWindow.Changed += AppWindow_Changed;
                if (appWindow.IsVisible)
                {
                    _countdownTimer.Start();
                }
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            var appWindow = (App.Current as App)?.MainWindow?.AppWindow;
            if (appWindow != null)
            {
                appWindow.Changed -= AppWindow_Changed;
            }

            _countdownTimer.Stop();
        }

        private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (args.DidVisibilityChange)
            {
                if (sender.IsVisible)
                {
                    ReadData(); // 立刻刷新再等待_countdownTimer刷新
                    _countdownTimer.Start();
                }
                else
                {
                    _countdownTimer.Stop();
                }
            }
        }

        private void ReadData()
        {
            var module = AlarmClockModule;
            if (module == null) return;

            Data.RemainingTime = module.RemainingTime;
            Data.CountdownProgressValue = Math.Max(0, (int)(100 * module.Progress));
            Data.IsPaused = module.IsPaused;
            Data.AlarmClockState = module.State;

            if (module.IsPaused)
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

            if (module.State == AlarmClockStates.Work)
            {
                if (WorkOrRestCommand.Label != "休息")
                {
                    WorkOrRestCommand.Label = "休息";
                    WorkOrRestCommand.Description = "马上休息";
                    WorkOrRestCommand.IconSource = new FontIconSource() { Glyph= "\uE708" };
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
            var module = AlarmClockModule;
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
            var module = AlarmClockModule;
            if (module == null) return;

            if (module.IsPaused)
            {
                module.Resume();
            }
            else
            {
                module.Pause();
            }
            ReadData();
        }

        private void RestCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var module = AlarmClockModule;
            if (module == null) return;

            module.WorkOrRest();
            ReadData();
        }
    }

    public class HomeData : INotifyPropertyChanged
    {
        private TimeSpan _remainingTime;
        public TimeSpan RemainingTime
        {
            get { return _remainingTime; }
            set
            {
                if (_remainingTime != value)
                {
                    _remainingTime = value;
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
                int countdown = 0;
                if (_remainingTime.TotalMinutes > 1) countdown = (int)_remainingTime.TotalMinutes;
                else countdown = (int)_remainingTime.TotalSeconds;

                return countdown;
            }
        }

        public string CountdownUnit
        {
            get
            {
                if (_remainingTime.TotalMinutes > 1)
                {
                    return $"分 {_remainingTime.Seconds} 秒";
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

        private bool _isPaused;
        public bool IsPaused
        {
            get { return _isPaused; }
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged();
                }
            }
        }

        private AlarmClockStates _alarmClockState;
        public AlarmClockStates AlarmClockState
        {
            get { return _alarmClockState; }
            set
            {
                if (_alarmClockState != value)
                {
                    _alarmClockState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StateTitle));
                    OnPropertyChanged(nameof(IsResting));
                }
            }
        }

        public bool IsResting => _alarmClockState == AlarmClockStates.Resting;

        public string StateTitle
        {
            get
            {
                return _alarmClockState == AlarmClockStates.Work ? "工作中……" : "休息中……";
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

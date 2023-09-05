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
            _countdownTimer.Start();
            ReadData();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _countdownTimer.Stop();
        }

        private void ReadData()
        {
            var module = AlarmClockModule;
            if (module == null) return;

            Data.RemainingTime = module.RemainingTime;
            Data.CountdownProgressValue = Math.Max(0, (int)(100 * module.RemainingTime.TotalMinutes / module.CurrentDueTime.TotalMinutes));
            Data.AlarmClockState = module.State;
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
                    OnPropertyChanged(nameof(CountdownText));
                    OnPropertyChanged(nameof(CountdownUnit));
                }
            }
        }

        public string CountdownText
        {
            get
            {
                int countdown = 0;
                if (_remainingTime.TotalMinutes > 1) countdown = (int)Math.Round(_remainingTime.TotalMinutes);
                else countdown = (int)_remainingTime.TotalSeconds;

                return countdown.ToString("00");
            }
        }

        public string CountdownUnit
        {
            get
            {
                return _remainingTime.TotalMinutes > 1 ? "分 " : "秒 ";
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
                }
            }
        }

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

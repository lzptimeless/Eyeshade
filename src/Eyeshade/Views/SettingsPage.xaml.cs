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
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            Data = new SettingsData();
            this.InitializeComponent();
        }

        public AlarmClockModule? AlarmClockModule
        {
            get { return Data.AlarmClockModule; }
            set { Data.AlarmClockModule = value; }
        }
        public SettingsData Data { get; set; }
    }

    public class SettingsData : INotifyPropertyChanged
    {
        public AlarmClockModule? AlarmClockModule { get; set; }
        public TimeSpan WorkTime
        {
            get { return AlarmClockModule != null ? AlarmClockModule.WorkTime : TimeSpan.Zero; }
            set
            {
                if (AlarmClockModule != null && value >= TimeSpan.FromMinutes(1) && value != AlarmClockModule.WorkTime)
                {
                    AlarmClockModule.SetWorkTime(value);
                    OnPropertyChanged();
                }
            }
        }
        public TimeSpan RestingTime
        {
            get { return AlarmClockModule != null ? AlarmClockModule.RestingTime : TimeSpan.Zero; }
            set
            {
                if (AlarmClockModule != null && value >= TimeSpan.FromMinutes(1) && value != AlarmClockModule.RestingTime)
                {
                    AlarmClockModule.SetRestingTime(value);
                    OnPropertyChanged();
                }
            }
        }
        public int RingerVolume
        {
            get { return AlarmClockModule != null ? AlarmClockModule.RingerVolume : 0; }
            set
            {
                if (AlarmClockModule != null && value >= 0 && value != AlarmClockModule.RingerVolume)
                {
                    AlarmClockModule.SetRingerVolume(value);
                    OnPropertyChanged();
                }
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

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
using NLog;
using Windows.ApplicationModel;
using Windows.Media.Playback;
using Windows.Media.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Eyeshade.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private MediaPlayer? _mediaPlayer;

        public SettingsPage()
        {
            Data = new SettingsData();
            Data.Prompt += Data_Prompt;
            DataContext = Data; // 为了Volume Explicit Binding
            this.InitializeComponent();
        }

        public EyeshadeModule? EyeshadeModule
        {
            get { return Data.EyeshadeModule; }
            set { Data.EyeshadeModule = value; }
        }
        public SettingsData Data { get; set; }

        private async void Data_Prompt(object? sender, string e)
        {
            ContentDialog dialog = new ContentDialog();
            dialog.XamlRoot = XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.Title = "操作失败！";
            dialog.PrimaryButtonText = "确认";
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Content = e;

            await dialog.ShowAsync();
        }

        private void VolumeTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null)
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Medias\school-chime.mp3"), UriKind.Absolute));
            }

            _mediaPlayer.Volume = Math.Min(1, Math.Max(0, Data.RingerVolume / 100d));
            if (_mediaPlayer.CurrentState != MediaPlayerState.Playing)
            {
                _mediaPlayer.Play();
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            (_mediaPlayer?.Source as IDisposable)?.Dispose();
            _mediaPlayer?.Dispose();
        }

        private void VolumeSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            var binding = VolumeSlider.GetBindingExpression(Slider.ValueProperty);
            if (binding != null)
            {
                binding.UpdateSource();
            }

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = Math.Min(1, Math.Max(0, Data.RingerVolume / 100d));
            }
        }

        private void VolumeSlider_LostFocus(object sender, RoutedEventArgs e)
        {
            var binding = VolumeSlider.GetBindingExpression(Slider.ValueProperty);
            if (binding != null)
            {
                binding.UpdateSource();
            }

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = Math.Min(1, Math.Max(0, Data.RingerVolume / 100d));
            }
        }
    }

    public class SettingsData : INotifyPropertyChanged
    {
        private EyeshadeModule? _eyeshadeModule;
        public EyeshadeModule? EyeshadeModule
        {
            get { return _eyeshadeModule; }
            set
            {
                if (_eyeshadeModule == value) return;

                _eyeshadeModule = value;

                // 初始化_isStartWithSystem
                if (_eyeshadeModule != null)
                {
                    if (App.IsPackaged)
                    {
                        LoadIsStartWithSystemInPackageMode();
                    }
                    else
                    {
                        var isStartWithSystem = _eyeshadeModule.GetIsStartWithSystem();
                        if (isStartWithSystem != _isStartWithSystem)
                        {
                            _isStartWithSystem = isStartWithSystem;
                            OnPropertyChanged();
                        }
                    }
                }
                else
                {
                    if (_isStartWithSystem != false)
                    {
                        _isStartWithSystem = false;
                        OnPropertyChanged();
                    }
                }
            }
        }

        private bool _isStartWithSystem;
        public bool IsStartWithSystem
        {
            get { return _isStartWithSystem; }
            set
            {
                if (_isStartWithSystem != value)
                {
                    _isStartWithSystem = value;
                    if (App.IsPackaged)
                    {
                        SetIsStartWithSystemInPackageMode(value);
                    }
                    else
                    {
                        EyeshadeModule?.SetIsStartWithSystem(value);
                        OnPropertyChanged();
                    }
                }
            }
        }
        public TimeSpan WorkTime
        {
            get { return EyeshadeModule != null ? EyeshadeModule.WorkTime : TimeSpan.Zero; }
            set
            {
                if (EyeshadeModule != null && value >= TimeSpan.FromMinutes(1) && value != EyeshadeModule.WorkTime)
                {
                    EyeshadeModule.SetWorkTime(value);
                    OnPropertyChanged();
                }
            }
        }
        public TimeSpan RestingTime
        {
            get { return EyeshadeModule != null ? EyeshadeModule.RestingTime : TimeSpan.Zero; }
            set
            {
                if (EyeshadeModule != null && value >= TimeSpan.FromMinutes(1) && value != EyeshadeModule.RestingTime)
                {
                    EyeshadeModule.SetRestingTime(value);
                    OnPropertyChanged();
                }
            }
        }
        public int NotifyTimeSeconds
        {
            get { return EyeshadeModule != null ? (int)EyeshadeModule.NotifyTime.TotalSeconds : 0; }
            set
            {
                if (EyeshadeModule != null && value >= 1 && value != (int)EyeshadeModule.NotifyTime.TotalSeconds)
                {
                    EyeshadeModule.SetNotifyTime(TimeSpan.FromSeconds(value));
                    OnPropertyChanged();
                }
            }
        }
        public int RingerVolume
        {
            get { return EyeshadeModule != null ? EyeshadeModule.RingerVolume : 0; }
            set
            {
                if (EyeshadeModule != null && value >= 0 && value != EyeshadeModule.RingerVolume)
                {
                    EyeshadeModule.SetRingerVolume(value);
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler<string>? Prompt;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region private methods
        private async void LoadIsStartWithSystemInPackageMode()
        {
            try
            {
                var startupTask = await StartupTask.GetAsync("StartWithSystemTaskId");
                if (startupTask != null)
                {
                    switch (startupTask.State)
                    {
                        case StartupTaskState.Disabled:
                            break;
                        case StartupTaskState.DisabledByUser:
                            break;
                        case StartupTaskState.Enabled:
                            _isStartWithSystem = true;
                            break;
                        case StartupTaskState.DisabledByPolicy:
                            break;
                        case StartupTaskState.EnabledByPolicy:
                            _isStartWithSystem = true;
                            break;
                        default:
                            break;
                    }
                }

                OnPropertyChanged(nameof(IsStartWithSystem));
            }
            catch { }
        }

        private async void SetIsStartWithSystemInPackageMode(bool value)
        {
            try
            {
                var startupTask = await StartupTask.GetAsync("StartWithSystemTaskId");
                if (startupTask != null)
                {
                    switch (startupTask.State)
                    {
                        case StartupTaskState.Disabled:
                            {
                                if (value)
                                {
                                    var newState = await startupTask.RequestEnableAsync();
                                    _isStartWithSystem = newState == StartupTaskState.Enabled;
                                    OnPropertyChanged(nameof(IsStartWithSystem));
                                }
                            }
                            break;
                        case StartupTaskState.DisabledByUser:
                            {
                                if (value)
                                {
                                    Prompt?.Invoke(this, "你已禁用此应用在登录后立即运行的功能，但如果你改变了主意，可以在任务管理器的“启动”选项卡中启用此功能。");
                                    _isStartWithSystem = false;
                                    OnPropertyChanged(nameof(IsStartWithSystem));
                                }
                            }
                            break;
                        case StartupTaskState.Enabled:
                            {
                                if (!value)
                                {
                                    startupTask.Disable();
                                    _isStartWithSystem = false;
                                    OnPropertyChanged(nameof(IsStartWithSystem));
                                }
                            }
                            break;
                        case StartupTaskState.DisabledByPolicy:
                            {
                                if (value)
                                {
                                    Prompt?.Invoke(this, "组策略禁用启动，或者此设备不支持启动。");
                                    _isStartWithSystem = false;
                                    OnPropertyChanged(nameof(IsStartWithSystem));
                                }
                            }
                            break;
                        case StartupTaskState.EnabledByPolicy:
                            {
                                if (!value)
                                {
                                    Prompt?.Invoke(this, "由组策略开启的此选项不支持关闭。");
                                    _isStartWithSystem = true;
                                    OnPropertyChanged(nameof(IsStartWithSystem));
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch { }
        }
        #endregion
    }
}
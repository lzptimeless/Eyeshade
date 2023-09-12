using Eyeshade.Log;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Linq;

namespace Eyeshade.FuncModule
{
    public class EyeshadeModule
    {
        #region fields
        private readonly ILogWrapper? _logger;
        private readonly EyeshadeUserConfig _userConfig;
        private readonly CountdownTimer _timer;
        private EyeshadeStates _state;
        #endregion

        public EyeshadeModule(ILogWrapper? logger)
        {
            _logger = logger;
            _userConfig = new EyeshadeUserConfig(logger);
            _timer = new CountdownTimer();
            _state = EyeshadeStates.Work;

            _timer.Completed += Countdown_Completed;
            _timer.ProgressChanged += Countdown_ProgressChanged;
            _timer.IsPausedChanged += Countdown_IsPausedChanged;
            _timer.Reset((int)_userConfig.WorkTime.TotalMilliseconds);
        }

        #region properties
        public TimeSpan WorkTime => _userConfig.WorkTime;
        public TimeSpan RestingTime => _userConfig.RestingTime;
        public TimeSpan NotifyTime => _userConfig.NotifyTime;
        public int RingerVolume => _userConfig.RingerVolume;
        public int TotalMilliseconds => _timer.TotalTime;
        public int RemainingMilliseconds => _timer.RemainingTime;
        public double Progress => _timer.Progress;
        public bool IsPaused => _timer.IsPaused;
        public EyeshadeStates State => _state;
        #endregion

        #region events
        public event EventHandler? StateChanged;
        public event EventHandler? ProgressChanged;
        public event EventHandler? IsPausedChanged;
        #endregion

        #region public methods
        public void Work()
        {
            var workTime = _userConfig.WorkTime;
            _logger?.Info($"Work {workTime}");
            _timer.Reset((int)workTime.TotalMilliseconds);
            _state = EyeshadeStates.Work;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Rest()
        {
            var restingTime = _userConfig.RestingTime;
            _logger?.Info($"Rest {restingTime}");
            _timer.Reset((int)restingTime.TotalMilliseconds);
            _state = EyeshadeStates.Resting;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Defer(TimeSpan value)
        {
            _logger?.Info($"Defer {value}, remaining: {TimeSpan.FromMilliseconds(_timer.RemainingTime)}");
            _timer.Defer((int)value.TotalMilliseconds);
        }

        public void Pause()
        {
            _logger?.Info($"Pause, remaining: {TimeSpan.FromMilliseconds(_timer.RemainingTime)}");
            var result = _timer.Pause();
            _logger?.Info($"Pause result: {result}");
        }

        public void Resume()
        {
            _logger?.Info($"Resume, remaining: {TimeSpan.FromMilliseconds(_timer.RemainingTime)}");
            var result = _timer.Resume();
            _logger?.Info($"Resume result: {result}");
        }

        public void SetWorkTime(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must >= 1 minute");
            if (_userConfig.WorkTime == value) return;

            _logger?.Info($"Set WorkTime {value}");
            _userConfig.WorkTime = value;
            _userConfig.Save();
            if (_state == EyeshadeStates.Work)
            {
                _timer.Change((int)value.TotalMilliseconds);
            }
        }

        public void SetRestingTime(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must >= 1 minute");
            if (_userConfig.RestingTime == value) return;

            _logger?.Info($"Set RestingTime {value}");
            _userConfig.RestingTime = value;
            _userConfig.Save();
            if (_state == EyeshadeStates.Resting)
            {
                _timer.Change((int)value.TotalMilliseconds);
            }
        }

        public void SetNotifyTime(TimeSpan value)
        {
            if (value.TotalSeconds < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must >= second");
            if (_userConfig.NotifyTime == value) return;

            _logger?.Info($"Set NotifyTime {value}");
            _userConfig.NotifyTime = value;
            _userConfig.Save();
        }

        public void SetRingerVolume(int value)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(value), value, "Must >= 0 and <= 100");

            _userConfig.RingerVolume = value;
            _userConfig.Save();
        }

        public bool GetIsStartWithSystem()
        {
            var launcher = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(launcher))
            {
                if (launcher.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    launcher = launcher.Substring(0, launcher.Length - 4) + ".exe";
                }
                return GetIsStartWithSystem(launcher);
            }
            else
            {
                return false;
            }
        }

        public void SetIsStartWithSystem(bool value)
        {
            var launcher = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(launcher))
            {
                if (launcher.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    launcher = launcher.Substring(0, launcher.Length - 4) + ".exe";
                }
                SetIsStartWithSystem(value, launcher);
            }
        }
        #endregion

        #region private methods
        private void Countdown_Completed(object? sender, EventArgs e)
        {
            _logger?.Info($"{_state} countdown completed.");
            if (_state == EyeshadeStates.Work)
            {
                Rest();
            }
            else
            {
                Work();
            }
        }

        private void Countdown_IsPausedChanged(object? sender, EventArgs e)
        {
            IsPausedChanged?.Invoke(this, e);
        }

        private void Countdown_ProgressChanged(object? sender, EventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        private void SetIsStartWithSystem(bool value, string currentLauncherPath)
        {
            if (string.IsNullOrEmpty(currentLauncherPath)) throw new ArgumentNullException(nameof(currentLauncherPath));

            try
            {
                const string autoStartKey = "Eyeshade";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (var registryKey = baseKey.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    bool oldValue = false;
                    string v = (registryKey.GetValue(autoStartKey, null) as string ?? string.Empty).Trim();
                    string? oldLauncherPath = null;
                    if (!string.IsNullOrEmpty(v))
                    {
                        if (v[0] == '"')
                        {
                            int secQuoteIndex = v.IndexOf('"', 1);
                            if (secQuoteIndex > 1) oldLauncherPath = v.Substring(1, secQuoteIndex - 1);
                        }
                        else oldLauncherPath = v.Split(' ')[0];

                        oldValue = string.Equals(oldLauncherPath, currentLauncherPath, StringComparison.OrdinalIgnoreCase);
                    }

                    if (oldValue != value)
                    {
                        if (value)
                        {
                            registryKey.SetValue(autoStartKey, $"\"{currentLauncherPath}\" --start-with-system");
                        }
                        else
                        {
                            registryKey.DeleteValue(autoStartKey, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, "Set run registry failed.");
            }
        }

        private bool GetIsStartWithSystem(string? currentLauncherPath)
        {
            if (string.IsNullOrEmpty(currentLauncherPath)) return false;

            const string autoStartKey = "Eyeshade";
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (var registryKey = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (registryKey == null) return false;

                    string v = (registryKey.GetValue(autoStartKey, null) as string ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(v))
                        return false;
                    else
                    {
                        string? regLauncherPath = null;
                        if (v[0] == '"')
                        {
                            int secQuoteIndex = v.IndexOf('"', 1);
                            if (secQuoteIndex > 1) regLauncherPath = v.Substring(1, secQuoteIndex - 1);
                        }
                        else regLauncherPath = v.Split(' ')[0];

                        return string.Equals(regLauncherPath, currentLauncherPath, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, "Get run registry failed.");
                return false;
            }
        }
        #endregion
    }
}

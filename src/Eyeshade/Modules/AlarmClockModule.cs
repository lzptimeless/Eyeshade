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
using System.Xml.Linq;

namespace Eyeshade.Modules
{
    public class AlarmClockModule
    {
        #region fields
        private readonly ILogWrapper? _logger;
        private readonly AlarmClockConfig _userConfig;
        private readonly Timer _timer;
        private DateTime _timerStartTime;
        private TimeSpan _timerDueTime;
        private bool _timerIsPaused;
        /// <summary>
        /// WorkTime or RestingTime
        /// </summary>
        private TimeSpan _totalTime;
        private AlarmClockStates _state;
        private readonly Timer _progressTimer;
        #endregion

        public AlarmClockModule(ILogWrapper? logger)
        {
            _logger = logger;
            _userConfig = new AlarmClockConfig(logger);
            _state = AlarmClockStates.Work;
            _totalTime = _userConfig.WorkTime;
            _timerDueTime = _totalTime;
            _timer = new Timer(CountdownCallback, null, _timerDueTime, Timeout.InfiniteTimeSpan);
            _timerStartTime = DateTime.Now;
            _progressTimer = new Timer(ProgressCallback, null, Timeout.Infinite, Timeout.Infinite);
            SetNextProgressTimer();
        }

        #region properties
        public TimeSpan WorkTime => _userConfig.WorkTime;
        public TimeSpan RestingTime => _userConfig.RestingTime;
        public int RingerVolume => _userConfig.RingerVolume;
        public TimeSpan TotalTime => _totalTime;
        public TimeSpan RemainingTime
        {
            get
            {
                if (_timerIsPaused)
                {
                    return _timerDueTime;
                }
                else
                {
                    return _timerDueTime - (DateTime.Now - _timerStartTime);
                }
            }
        }
        public double Progress
        {
            get
            {
                if (_totalTime.TotalSeconds == 0) return 1;

                return RemainingTime.TotalSeconds / _totalTime.TotalSeconds;
            }
        }
        public bool IsPaused => _timerIsPaused;
        public AlarmClockStates State => _state;
        #endregion

        #region events
        public event EventHandler<AlarmClockStateChangedArgs>? StateChanged;
        public event EventHandler<AlarmClockProgressChangedArgs>? ProgressChanged;
        public event EventHandler<AlarmClockIsPausedChangedArgs>? IsPausedChanged;
        #endregion

        #region public methods
        public void SetTotalTime(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must bigger than or equal to 1 minute");

            var remaining = RemainingTime;
            _logger?.Info($"Set TotalTime {value}, remaining: {remaining}");

            if (value < remaining)
            {
                remaining = value;
            }

            _totalTime = value;
            _timerDueTime = remaining;
            if (!_timerIsPaused)
            {
                _timer.Change(remaining, Timeout.InfiniteTimeSpan);
                _timerStartTime = DateTime.Now;
                SetNextProgressTimer();
            }
        }

        public void Defer(TimeSpan value)
        {
            var remaining = RemainingTime;
            _logger?.Info($"Defer {value}, remaining: {remaining}");

            remaining += value;
            if (_totalTime < remaining)
            {
                remaining = _totalTime;
            }

            if (remaining < TimeSpan.FromMinutes(1))
            {
                remaining = TimeSpan.FromMinutes(1);
            }

            _timerDueTime = remaining;
            if (!_timerIsPaused)
            {
                _timer.Change(remaining, Timeout.InfiniteTimeSpan);
                _timerStartTime = DateTime.Now;
                SetNextProgressTimer();
            }
        }

        public void WorkOrRest()
        {
            _logger?.Info("WorkOrRest.");
            CountdownCallback(null);
        }

        public void Pause()
        {
            if (_timerIsPaused) return;

            var remaining = RemainingTime;
            _logger?.Info($"Pause, remaining: {remaining}");
            _timerDueTime = remaining;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timerIsPaused = true;
            _progressTimer.Change(Timeout.Infinite, Timeout.Infinite);
            IsPausedChanged?.Invoke(this, new AlarmClockIsPausedChangedArgs(true));
        }

        public void Resume()
        {
            if (!_timerIsPaused) return;

            var remaining = RemainingTime;
            _logger?.Info($"Resume, remaining: {remaining}");
            _timerDueTime = remaining;
            _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
            _timerStartTime = DateTime.Now;
            _timerIsPaused = false;
            SetNextProgressTimer();
            IsPausedChanged?.Invoke(this, new AlarmClockIsPausedChangedArgs(false));
        }

        public void SetWorkTime(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must bigger than or equal to 1 minute");
            if (WorkTime == value) return;

            _logger?.Info($"Set WorkTime {value}");
            _userConfig.WorkTime = value;
            _userConfig.Save();
            if (State == AlarmClockStates.Work)
            {
                SetTotalTime(value);
            }
        }

        public void SetRestingTime(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must bigger than or equal to 1 minute");
            if (RestingTime == value) return;

            _logger?.Info($"Set RestingTime {value}");
            _userConfig.RestingTime = value;
            _userConfig.Save();
            if (State == AlarmClockStates.Resting)
            {
                SetTotalTime(value);
            }
        }

        public void SetRingerVolume(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Must bigger than or equal to 0");

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
        private void CountdownCallback(object? state)
        {
            _state = _state == AlarmClockStates.Work ? AlarmClockStates.Resting : AlarmClockStates.Work;
            _totalTime = _state == AlarmClockStates.Work ? _userConfig.WorkTime : _userConfig.RestingTime;
            _timerDueTime = _totalTime;
            _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
            _timerStartTime = DateTime.Now;
            SetNextProgressTimer();

            _logger?.Info($"Change state to: {_state}, TotalTime: {_totalTime}");

            StateChanged?.Invoke(this, new AlarmClockStateChangedArgs(_state));
        }

        private void SetNextProgressTimer()
        {
            if (_totalTime == TimeSpan.Zero) return;

            var totalms = _totalTime.TotalMilliseconds;
            var remainingms = RemainingTime.TotalMilliseconds;

            double dueTime;
            if (remainingms > totalms * 0.75) dueTime = Math.Ceiling(remainingms - totalms * 0.75);
            else if (remainingms > totalms * 0.5) dueTime = Math.Ceiling(remainingms - totalms * 0.5);
            else if (remainingms > totalms * 0.25) dueTime = Math.Ceiling(remainingms - totalms * 0.25);
            else if (remainingms > 10000) dueTime = Math.Ceiling(remainingms - 10000);
            else dueTime = Math.Ceiling(remainingms);

            _progressTimer.Change((int)Math.Max(1000, dueTime), Timeout.Infinite);
        }

        private void ProgressCallback(object? state)
        {
            var progress = Progress;
            var remainingms = RemainingTime.TotalMilliseconds;

            if (progress > 0.25 || remainingms > 10000) SetNextProgressTimer();
            // else SetNextProgressTimer(); // 这个情况会在CountdownCallback调用，所以这里就不重复设置了

            ProgressChanged?.Invoke(this, new AlarmClockProgressChangedArgs(progress));
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

    public class AlarmClockConfig
    {
        private readonly string ConfigFileName = "user-config.xml";
        private readonly ILogWrapper? _logger;
        private readonly string _configFilePath;

        public AlarmClockConfig(ILogWrapper? logger)
        {
            _logger = logger;
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

            Load();
        }

        public TimeSpan WorkTime { get; set; } = TimeSpan.FromMinutes(45);
        public TimeSpan RestingTime { get; set; } = TimeSpan.FromMinutes(4);
        public int RingerVolume { get; set; } = 100;

        public void Load()
        {
            try
            {
                XDocument xdoc = XDocument.Load(_configFilePath);
                if (xdoc.Root == null) return;

                foreach (var itemNode in xdoc.Root.Elements())
                {
                    if (itemNode.Name.LocalName == nameof(WorkTime))
                    {
                        if (TimeSpan.TryParse(itemNode.Value, out TimeSpan value) && value.TotalMinutes >= 1)
                        {
                            WorkTime = value;
                        }
                    }
                    else if (itemNode.Name.LocalName == nameof(RestingTime))
                    {
                        if (TimeSpan.TryParse(itemNode.Value, out TimeSpan value) && value.TotalMinutes >= 1)
                        {
                            RestingTime = value;
                        }
                    }
                    else if (itemNode.Name.LocalName == nameof(RingerVolume))
                    {
                        if (int.TryParse(itemNode.Value, out int value))
                        {
                            RingerVolume = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Load {ConfigFileName} failed.");
            }
        }

        public void Save()
        {
            try
            {
                XDocument xdoc = new XDocument();
                xdoc.Add(new XElement("UserConfig",
                    new XElement(nameof(WorkTime), WorkTime.ToString()),
                    new XElement(nameof(RestingTime), RestingTime.ToString()),
                    new XElement(nameof(RingerVolume), RingerVolume.ToString())
                ));

                xdoc.Save(_configFilePath);
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Save {ConfigFileName} failed.");
            }
        }
    }

    public class AlarmClockStateChangedArgs : EventArgs
    {
        public AlarmClockStateChangedArgs(AlarmClockStates state)
        {
            State = state;
        }

        public AlarmClockStates State { get; private set; }
    }

    public class AlarmClockProgressChangedArgs : EventArgs
    {
        public AlarmClockProgressChangedArgs(double progress)
        {
            Progress = progress;
        }

        public double Progress { get; private set; }
    }

    public class AlarmClockIsPausedChangedArgs : EventArgs
    {
        public AlarmClockIsPausedChangedArgs(bool isPause)
        {
            IsPause = isPause;
        }

        public bool IsPause { get; private set; }
    }

    public enum AlarmClockStates
    {
        Work,
        Resting
    }
}

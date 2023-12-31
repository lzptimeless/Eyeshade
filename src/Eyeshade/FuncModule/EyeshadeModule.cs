﻿using Eyeshade.Log;
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
        /// <summary>
        /// 当前状态是工作还是休息
        /// </summary>
        private EyeshadeStates _state;
        /// <summary>
        /// 用户是否已请求暂停计时，即调用Pause()
        /// </summary>
        private bool _isPaused;
        /// <summary>
        /// 是否已休眠（智能暂停计时），即调用Sleep()
        /// </summary>
        private bool _isSleeped;
        #endregion

        public EyeshadeModule(string userDataFolder, ILogWrapper? logger)
        {
            _logger = logger;
            _userConfig = new EyeshadeUserConfig(userDataFolder, logger);
            _timer = new CountdownTimer();
            _state = EyeshadeStates.Work;

            _timer.Completed += Countdown_Completed;
            _timer.ProgressChanged += Countdown_ProgressChanged;
            _timer.Reset((int)_userConfig.WorkTime.TotalMilliseconds);
        }

        #region properties
        public TimeSpan WorkTime => _userConfig.WorkTime;
        public TimeSpan RestingTime => _userConfig.RestingTime;
        public TimeSpan NotifyTime => _userConfig.NotifyTime;
        public int RingerVolume => _userConfig.RingerVolume;
        public bool SleepWhenUserLeave => _userConfig.SleepWhenUserLeave;
        public int TotalMilliseconds => _timer.TotalTime;
        public int RemainingMilliseconds => _timer.RemainingTime;
        public double Progress => _timer.Progress;
        public bool IsPaused => _isPaused;
        public bool IsSleeped => _isSleeped;
        public EyeshadeStates State => _state;
        #endregion

        #region events
        public event EventHandler? StateChanged;
        public event EventHandler? ProgressChanged;
        public event EventHandler? IsPausedChanged;
        public event EventHandler? IsSleepedChanged;
        #endregion

        #region public methods
        /// <summary>
        /// 立刻进入工作倒计时，自动取消用户之前的暂停操作，但是不取消休眠
        /// </summary>
        public void Work()
        {
            var workTime = _userConfig.WorkTime;
            _logger?.Info($"Work {workTime}");

            if (_isPaused)
            {
                _isPaused = false;
                IsPausedChanged?.Invoke(this, EventArgs.Empty);
            }

            _timer.Reset((int)workTime.TotalMilliseconds, forcePause: _isSleeped);
            _state = EyeshadeStates.Work;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 立刻进入休息倒计时，自动取消用户之前的暂停操作，忽略休眠标识
        /// </summary>
        public void Rest()
        {
            var restingTime = _userConfig.RestingTime;
            _logger?.Info($"Rest {restingTime}");

            if (_isPaused)
            {
                _isPaused = false;
                IsPausedChanged?.Invoke(this, EventArgs.Empty);
            }

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
            _logger?.Info($"User pause, paused: {_isPaused}, sleeped: {_isSleeped}, state: {_state}, remaining: {TimeSpan.FromMilliseconds(_timer.RemainingTime)}");
            if (!_isPaused)
            {
                _isPaused = true;
                if (!_timer.IsPaused)
                {
                    var result = _timer.Pause();
                    _logger?.Info($"Pause timer, result: {result}");
                }
                IsPausedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Resume()
        {
            _logger?.Info($"User resume, paused: {_isPaused}, sleeped: {_isSleeped}, state: {_state}, remaining: {TimeSpan.FromMilliseconds(_timer.RemainingTime)}");
            if (_isPaused)
            {
                _isPaused = false;
                if (!_isSleeped)
                {
                    var result = _timer.Resume();
                    _logger?.Info($"Resume timer, result: {result}");
                }
                IsPausedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Sleep()
        {
            _logger?.Info($"Smart pause, paused: {_isPaused}, sleeped: {_isSleeped}, state: {_state}, remaining: {TimeSpan.FromMilliseconds(_timer.RemainingTime)}");
            if (!_isSleeped)
            {
                _isSleeped = true;
                if (!_timer.IsPaused && _state == EyeshadeStates.Work)
                {
                    // 注意：休眠只在工作状态暂停，因为休息状态很可能触发用户不期望的用户离开事件，从而导致用户不期望的在休息状态休眠
                    var result = _timer.Pause();
                    _logger?.Info($"Pause timer, result: {result}");
                }
                IsSleepedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Awake()
        {
            _logger?.Info($"Smart resume, paused: {_isPaused}, sleeped: {_isSleeped}, state: {_state}, remaining: {TimeSpan.FromMilliseconds(_timer.RemainingTime)}");
            if (_isSleeped)
            {
                _isSleeped = false;
                if (!_isPaused)
                {
                    var result = _timer.Resume();
                    _logger?.Info($"Resume timer, result: {result}");
                }
                IsSleepedChanged?.Invoke(this, EventArgs.Empty);
            }
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

        public void SetSleepWhenUserLeave(bool value)
        {
            if (_userConfig.SleepWhenUserLeave == value) return;

            _userConfig.SleepWhenUserLeave = value;
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

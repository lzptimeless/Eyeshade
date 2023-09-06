using Eyeshade.Log;
using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        #region properties
        public TimeSpan WorkTime => _userConfig.WorkTime;
        public TimeSpan RestingTime => _userConfig.RestingTime;
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
        public bool IsPaused => _timerIsPaused;
        public AlarmClockStates State => _state;
        #endregion

        #region events
        public event EventHandler<AlarmClockStateChangedArgs>? StateChanged;
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
        #endregion

        #region private methods
        private void CountdownCallback(object? state)
        {
            _state = _state == AlarmClockStates.Work ? AlarmClockStates.Resting : AlarmClockStates.Work;
            _totalTime = _state == AlarmClockStates.Work ? _userConfig.WorkTime : _userConfig.RestingTime;
            _timerDueTime = _totalTime;
            _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
            _timerStartTime = DateTime.Now;

            _logger?.Info($"Change state to: {_state}, TotalTime: {_totalTime}");

            StateChanged?.Invoke(this, new AlarmClockStateChangedArgs(_state));
        }
        #endregion
    }

    public class AlarmClockConfig
    {
        private const string ConfigFilePath = "user-config.xml";
        private readonly ILogWrapper? _logger;

        public AlarmClockConfig(ILogWrapper? logger)
        {
            _logger = logger;
            Load();
        }

        public TimeSpan WorkTime { get; set; } = TimeSpan.FromMinutes(45);
        public TimeSpan RestingTime { get; set; } = TimeSpan.FromMinutes(4);

        public void Load()
        {
            try
            {
                XDocument xdoc = XDocument.Load(ConfigFilePath);
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
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Load {ConfigFilePath} failed.");
            }
        }

        public void Save()
        {
            try
            {
                XDocument xdoc = new XDocument();
                xdoc.Add(new XElement("UserConfig",
                    new XElement(nameof(WorkTime), WorkTime.ToString()),
                    new XElement(nameof(RestingTime), RestingTime.ToString())
                ));

                xdoc.Save(ConfigFilePath);
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Save {ConfigFilePath} failed.");
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

    public enum AlarmClockStates
    {
        Work,
        Resting
    }
}

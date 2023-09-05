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
        private DateTime _timerChangeTime;
        private TimeSpan _timerDueTime;
        #endregion

        public AlarmClockModule(ILogWrapper? logger)
        {
            _logger = logger;
            _userConfig = new AlarmClockConfig(logger);
            _timerDueTime = _userConfig.WorkTime;
            _timer = new Timer(CountdownCallback, null, _timerDueTime, Timeout.InfiniteTimeSpan);
            _timerChangeTime = DateTime.Now;
        }

        #region properties
        public TimeSpan WorkTime => _userConfig.WorkTime;
        public TimeSpan RestingTime => _userConfig.RestingTime;
        public TimeSpan CurrentDueTime => _timerDueTime;
        public TimeSpan RemainingTime => _timerDueTime - (DateTime.Now - _timerChangeTime);
        public AlarmClockStates State { get; private set; }
        #endregion

        #region events
        public event EventHandler<AlarmClockStateChangedArgs>? StateChanged;
        #endregion

        #region public methods
        public void Defer(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must bigger than or equal to 1 minutes");

            _logger?.Info($"Defer {value}");
            _timerDueTime = RemainingTime + value;
            _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
            _timerChangeTime = DateTime.Now;
        }

        public void SetWorkTime(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must bigger than or equal to 1 minutes");
            if (WorkTime == value) return;

            _logger?.Info($"SetWorkTime {value}");
            var increment = value - WorkTime;
            _userConfig.WorkTime = value;
            if (State == AlarmClockStates.Work)
            {
                var remainingTime = RemainingTime + increment;
                if (remainingTime.TotalMinutes > 1)
                {
                    _timerDueTime = remainingTime;
                }
                else
                {
                    _timerDueTime = TimeSpan.FromMinutes(1);
                }
                _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
                _timerChangeTime = DateTime.Now;
            }
            _userConfig.Save();
        }

        public void SetRestingTime(TimeSpan value)
        {
            if (value.TotalMinutes < 1) throw new ArgumentOutOfRangeException(nameof(value), "Must bigger than or equal to 1 minutes");
            if (RestingTime == value) return;

            _logger?.Info($"SetRestingTime {value}");
            var increment = value - RestingTime;
            _userConfig.RestingTime = value;
            if (State == AlarmClockStates.Resting)
            {
                var remainingTime = RemainingTime - increment;
                if (remainingTime.TotalMinutes > 1)
                {
                    _timerDueTime = remainingTime;
                }
                else
                {
                    _timerDueTime = TimeSpan.FromMinutes(1);
                }
                _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
                _timerChangeTime = DateTime.Now;
            }
            _userConfig.Save();
        }
        #endregion

        #region private methods
        private void CountdownCallback(object? state)
        {
            if (State == AlarmClockStates.Work)
            {
                State = AlarmClockStates.Resting;
                _timerDueTime = RestingTime;
            }
            else
            {
                State = AlarmClockStates.Work;
                _timerDueTime = WorkTime;
            }

            _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
            _timerChangeTime = DateTime.Now;
            _logger?.Info($"Change state to: {State}, dueTime: {_timerDueTime}");

            StateChanged?.Invoke(this, new AlarmClockStateChangedArgs(State));
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

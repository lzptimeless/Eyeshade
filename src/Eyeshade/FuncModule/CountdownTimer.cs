using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyeshade.FuncModule
{
    internal class CountdownTimer : IDisposable
    {
        #region fields
        private readonly Timer _timer;
        private TimeSpan _totalTime;
        private TimeSpan _timerDueTime;
        private DateTime _timerStartTime;
        private bool _isPaused;
        #endregion

        public CountdownTimer()
        {
            _timer = new Timer(InnerTimerCallback);
        }

        #region properties
        public TimeSpan TotalTime => _totalTime;
        public TimeSpan RemainingTime
        {
            get
            {
                if (_isPaused)
                {
                    return _timerDueTime;
                }
                else
                {
                    return _timerDueTime - (DateTime.Now - _timerStartTime);
                }
            }
        }
        #endregion

        #region events
        public event EventHandler? TimeOut;
        #endregion

        #region public methods
        public void Countdown(TimeSpan value)
        {
            _totalTime = value;
            _timerDueTime = value;
            _timerStartTime = DateTime.Now;
            _timer.Change(_timerDueTime, Timeout.InfiniteTimeSpan);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
        #endregion

        #region private methods
        private void InnerTimerCallback(object? state)
        {
            TimeOut?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}

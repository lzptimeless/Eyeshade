using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eyeshade.FuncModule
{
    /// <summary>
    /// 倒计时计时器
    /// </summary>
    internal class CountdownTimer : IDisposable
    {
        #region fields
        /// <summary>
        /// 设置给_timer的触发时间间隔，单位毫秒
        /// </summary>
        private const int TimerPeriod = 1000;
        private const int MiniTotalTime = 1000;
        private const int MiniDeferTime = 1000;
        private readonly Timer _timer;
        /// <summary>
        /// 需要进行倒计时的总时长，单位毫秒
        /// </summary>
        private int _totalTime = MiniTotalTime;
        /// <summary>
        /// 剩余时长，单位毫秒
        /// </summary>
        private int _remainingTime;
        /// <summary>
        /// 是否暂停倒计时
        /// </summary>
        private bool _isPaused;
        #endregion

        public CountdownTimer()
        {
            _timer = new Timer(InnerTimerCallback);
        }

        #region properties
        /// <summary>
        /// 需要进行倒计时的总时长，单位毫秒，与<see cref="Reset(int)"/>或<see cref="Change(int)"/>传入的totalTime参数相同
        /// </summary>
        public int TotalTime => _totalTime;
        /// <summary>
        /// 距离倒计时结束的剩余时间，单位毫秒
        /// </summary>
        public int RemainingTime => _remainingTime;
        /// <summary>
        /// 倒计时进度，从1到0逐渐减小，等于剩余时间除以总时间
        /// </summary>
        public double Progress
        {
            get
            {
                if (_totalTime <= 0) return 1;

                return Math.Min(1, _remainingTime / (double)_totalTime);
            }
        }
        /// <summary>
        /// 是否暂停计时
        /// </summary>
        public bool IsPaused => _isPaused;
        /// <summary>
        /// 倒计时是否已经结束
        /// </summary>
        public bool IsCompleted => _remainingTime <= 0;
        #endregion

        #region events
        /// <summary>
        /// 倒计时结束
        /// </summary>
        public event EventHandler? Completed;
        /// <summary>
        /// 进度变化
        /// </summary>
        public event EventHandler? ProgressChanged;
        /// <summary>
        /// 是否暂停属性变化
        /// </summary>
        public event EventHandler? IsPausedChanged;
        #endregion

        #region public methods
        /// <summary>
        /// 重新开始计时，如果<see cref="IsPaused"/>=true则会取消暂停
        /// </summary>
        /// <param name="totalTime">倒计时总时间，单位毫秒</param>
        public void Reset(int totalTime)
        {
            if (totalTime < MiniTotalTime) throw new ArgumentOutOfRangeException(nameof(totalTime), $"Must >= {MiniTotalTime}");

            _totalTime = totalTime;
            _remainingTime = totalTime;
            _timer.Change(TimerPeriod, TimerPeriod);
            if (_isPaused)
            {
                _isPaused = false;
                IsPausedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 修改倒计时总时间，尽量保留倒计时进度，如果新的总时间小于剩余时间则将剩余时间设置为新的总时间
        /// </summary>
        /// <param name="totalTime">新的总时间，单位毫秒</param>
        public void Change(int totalTime)
        {
            if (totalTime < MiniTotalTime) throw new ArgumentOutOfRangeException(nameof(totalTime), $"Must >= {MiniTotalTime}");

            if (totalTime < _remainingTime)
            {
                _remainingTime = totalTime;
            }
            _totalTime = totalTime;
        }

        /// <summary>
        /// 推迟倒计时
        /// </summary>
        /// <param name="value">要推迟的时长，单位毫秒，可以用负数表示加快倒计时进度</param>
        public void Defer(int value)
        {
            var remaining = _remainingTime;
            remaining += value;
            if (_totalTime < remaining)
            {
                // 防止剩余时间大于总时间
                remaining = _totalTime;
            }

            if (remaining < MiniDeferTime)
            {
                // 防止value为负数时导致剩余时间变成负数
                remaining = MiniDeferTime;
            }

            _remainingTime = remaining;
            if (!_isPaused)
            {
                // _timer可能已经处于倒计时结束状态，需要重启
                _timer.Change(TimerPeriod, TimerPeriod);
            }
        }

        /// <summary>
        /// 暂停倒计时，如果已经处于暂停状态则返回false
        /// </summary>
        public bool Pause()
        {
            if (_isPaused) return false;

            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _isPaused = true;
            IsPausedChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// 恢复倒计时，如果没有处于暂停状态或剩余时间等于0即计时结束则返回false
        /// </summary>
        public bool Resume()
        {
            if (!_isPaused && _remainingTime <= 0) return false;

            _timer.Change(TimerPeriod, TimerPeriod);
            _isPaused = false;
            IsPausedChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
        #endregion

        #region private methods
        private void InnerTimerCallback(object? state)
        {
            if (_remainingTime > TimerPeriod)
            {
                _remainingTime -= TimerPeriod;
                ProgressChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _remainingTime = 0;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion
    }
}

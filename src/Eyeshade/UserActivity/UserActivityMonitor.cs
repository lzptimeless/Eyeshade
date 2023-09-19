using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;

namespace Eyeshade.UserActivity
{
    /// <summary>
    /// 监控用户是否离开
    /// </summary>
    internal class UserActivityMonitor : IDisposable
    {
        #region fields
        private const int STATUS_SUCCESS = 0;
        /// <summary>
        /// 检测用户输入定时器的最小时间间隔
        /// </summary>
        private const int DetectIntervalMilliseconds = 1000;
        /// <summary>
        /// 用户输入判定为离开的阈值
        /// </summary>
        private const int UserAbsenceTimeLimitMilliseconds = 4 * 60 * 1000; // 4分钟
        /// <summary>
        /// 检测用户输入的定时器
        /// </summary>
        private readonly Timer _detectTimer;
        /// <summary>
        /// 缓存用户是否离开的状态
        /// </summary>
        private bool _isUserLeave;
        #endregion

        public UserActivityMonitor()
        {
            _detectTimer = new Timer(DetectTimerCallback);
        }

        /// <summary>
        /// 用户是否已经离开
        /// </summary>
        public bool IsUserLeave => _isUserLeave;

        /// <summary>
        /// 用户离开事件
        /// </summary>
        public event EventHandler<UserLeaveArgs>? UserLeave;
        /// <summary>
        /// 用户回来事件
        /// </summary>
        public event EventHandler? UserBack;

        /// <summary>
        /// 开始监控
        /// </summary>
        public void Start()
        {
            _detectTimer.Change(0, Timeout.Infinite);
        }

        public bool GetIsSystemDisplayRequired()
        {
            return GetIsSystemExecutionStateDisplayRequired();
        }

        public void Dispose()
        {
            _detectTimer.Dispose();
        }

        #region private methods
        private void DetectTimerCallback(object? state)
        {
            var userAbsenceTime = GetUserAbsenceTimeMilliseconds();
            if (userAbsenceTime >= UserAbsenceTimeLimitMilliseconds)
            {
                if (!_isUserLeave)
                {
                    _isUserLeave = true;
                    var isDisplayRequired = GetIsSystemExecutionStateDisplayRequired();
                    UserLeave?.Invoke(this, new UserLeaveArgs(isDisplayRequired));
                    _detectTimer.Change(DetectIntervalMilliseconds, DetectIntervalMilliseconds);
                }
            }
            else
            {
                if (_isUserLeave)
                {
                    _isUserLeave = false;
                    UserBack?.Invoke(this, EventArgs.Empty);
                }

                _detectTimer.Change(UserAbsenceTimeLimitMilliseconds - userAbsenceTime, Timeout.Infinite);
            }
        }

        /// <summary>
        /// 获取此时是否有进程向系统申请了不锁屏的请求，通常为视频播放或游戏进程
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Win32Exception"></exception>
        public static unsafe bool GetIsSystemExecutionStateDisplayRequired()
        {
            uint state;
            var result = PInvoke.CallNtPowerInformation(Windows.Win32.System.Power.POWER_INFORMATION_LEVEL.SystemExecutionState,
                null,
                0,
                &state,
                (uint)Marshal.SizeOf<uint>());

            if (result.Value != STATUS_SUCCESS)
                throw new Win32Exception();

            return (state & (uint)Windows.Win32.System.Power.EXECUTION_STATE.ES_DISPLAY_REQUIRED) != 0;
        }

        public static long GetUserAbsenceTimeMilliseconds()
        {
            Windows.Win32.UI.Input.KeyboardAndMouse.LASTINPUTINFO lastInputInfo = new Windows.Win32.UI.Input.KeyboardAndMouse.LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.Input.KeyboardAndMouse.LASTINPUTINFO>();
            if (!PInvoke.GetLastInputInfo(ref lastInputInfo))
                throw new Win32Exception();

            var absenceTime = Environment.TickCount64 - lastInputInfo.dwTime;
            return absenceTime;
        }
        #endregion
    }
}

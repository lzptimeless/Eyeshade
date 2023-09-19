using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.UserActivity
{
    /// <summary>
    /// 用户离开事件参数
    /// </summary>
    public class UserLeaveArgs : EventArgs
    {
        public UserLeaveArgs(bool isSystemDisplayRequired)
        {
            IsSystemDisplayRequired = isSystemDisplayRequired;
        }

        /// <summary>
        /// 此时是否有进程向系统申请了不锁屏的请求，通常为视频播放或游戏进程
        /// </summary>
        public bool IsSystemDisplayRequired { get; private set; }
    }
}

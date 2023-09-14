using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Win32;

namespace Eyeshade.SingleInstance
{
    /// <summary>
    /// 单实例功能
    /// </summary>
    class SingleInstanceFeature : IDisposable
    {
        #region win32
        /// <summary>
        /// 将消息发送到系统中的所有顶级窗口，包括已禁用或不可见的未拥有窗口
        /// </summary>
        private const int HWND_BROADCAST = 0xffff;
        /// <summary>
        /// 本次显示窗口的消息不携带任何参数
        /// </summary>
        private const int MSG_SHOW_LPARAM_EMPTY = 0;
        /// <summary>
        /// 本次显示窗口的消息需要从临时文件获取额外的参数
        /// </summary>
        private const int MSG_SHOW_LPARAM_TMP_FILE = 2;
        #endregion

        #region fields
        /// <summary>
        /// 用以保证单实例的Mutex
        /// </summary>
        private Mutex? _singleInstanceMutex;
        /// <summary>
        /// _singleInstanceMutex的名称
        /// </summary>
        private readonly string _mutexName;
        /// <summary>
        /// 通知已存在的实列打开主窗口的消息名称
        /// </summary>
        private readonly string _msgShowName;
        /// <summary>
        /// 通知已存在的实列打开主窗口的消息Id
        /// </summary>
        private uint _msgShow;
        /// <summary>
        /// 用以进程间传输数据的临时文件名
        /// </summary>
        private readonly string _msgTempFileName;
        #endregion
        /// <summary>
        /// 单实例功能
        /// </summary>
        /// <param name="mutexName">用以保证单实例的Mutex的名称，默认为{Assembly.FullName}Mutex</param>
        /// <param name="wndMsgShowName">通知已存在的实列打开主窗口的消息名称，默认为{Assembly.FullName}Show</param>
        public SingleInstanceFeature(string? mutexName = null, string? wndMsgShowName = null)
        {
            var appName = Assembly.GetEntryAssembly()!.GetName().Name!;
            _mutexName = mutexName ?? $"{appName}Mutex";
            _msgShowName = wndMsgShowName ?? $"{appName}Show";
            _msgTempFileName = $"{appName}Message.txt";
        }

        #region events
        /// <summary>
        /// 收到来自重复实例显示窗口的消息，或收到其它程序发送的显示窗口的消息
        /// </summary>
        public event EventHandler<ShowWindowArgs>? ShowWindow;
        #endregion

        #region public methods
        /// <summary>
        /// 在系统范围注册单实例Mutex，应该在程序初始化函数中调用
        /// </summary>
        /// <returns>true：当前实例为单实例，false：已经存在一个实例，本实例需要退出</returns>
        public bool Register()
        {
            return CreateSingleInstanceMutex();
        }

        /// <summary>
        /// 激活已经存在的实例
        /// </summary>
        /// <param name="args">程序启动参数</param>
        public void Active(string? args = null)
        {
            // 本次启动属于重复启动，直接退出
            // 通知已经存在的实例打开窗口，注意这个操作需要发送窗口消息给已经存在的实例
            int lParam = MSG_SHOW_LPARAM_EMPTY;
            if (!string.IsNullOrEmpty(args))
            {
                // Try to set process startup args
                try
                {
                    string filePath = Path.Combine(Path.GetTempPath(), _msgTempFileName);
                    File.WriteAllText(filePath, args);
                    lParam = MSG_SHOW_LPARAM_TMP_FILE;
                }
                catch { }
            }

            PInvoke.PostMessage(new Windows.Win32.Foundation.HWND(new IntPtr(HWND_BROADCAST)), GetMsgShow(), new Windows.Win32.Foundation.WPARAM(), lParam);
        }

        /// <summary>
        /// 使用主窗口handle初始化用以触发ShowWindow事件的消息
        /// </summary>
        /// <param name="hwnd">MainWindow handle</param>
        public unsafe void InitShowWindowMessage(IntPtr hwnd)
        {
            // 设置本进程（高级权限）的主窗口能够接收来自低级权限进程的消息
            if (!PInvoke.ChangeWindowMessageFilterEx(new Windows.Win32.Foundation.HWND(hwnd), 
                GetMsgShow(), 
                Windows.Win32.UI.WindowsAndMessaging.WINDOW_MESSAGE_FILTER_ACTION.MSGFLT_ALLOW, 
                null))
                throw new Win32Exception();
        }

        /// <summary>
        /// 在MainWindow消息处理函数中调用
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="uMsg">消息</param>
        /// <param name="wParam">消息参数</param>
        /// <param name="lParam">消息参数</param>
        /// <returns>消息处理后的返回值</returns>
        public void ProcessWindowMessage(Windows.Win32.Foundation.HWND hWnd, uint uMsg, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam)
        {
            if (uMsg == GetMsgShow())
            {
                string? args = null;
                try
                {
                    if (lParam == MSG_SHOW_LPARAM_TMP_FILE)
                    {
                        string filePath = Path.Combine(Path.GetTempPath(), _msgTempFileName);
                        args = File.ReadAllText(filePath);
                        File.Delete(filePath);
                    }
                }
                catch { }

                ShowWindow?.Invoke(this, new ShowWindowArgs(args));
            }
        }

        public void Dispose()
        {
            ReleaseSingleInstanceMutex();
        }
        #endregion

        #region private methods
        /// <summary>
        /// 尝试确保本次启动为单实例
        /// </summary>
        /// <returns>成功返回 true，失败返回 false</returns>
        private bool CreateSingleInstanceMutex()
        {
            _singleInstanceMutex = new Mutex(true, _mutexName, out bool mutexCreated);

            if (!mutexCreated)
            {
                _singleInstanceMutex.Close();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            return mutexCreated;
        }

        private void ReleaseSingleInstanceMutex()
        {
            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.Close();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
        }

        /// <summary>
        /// 获取通知已经存在实例主窗口显示的消息Id
        /// </summary>
        /// <returns></returns>
        private uint GetMsgShow()
        {
            if (_msgShow == 0)
                _msgShow = PInvoke.RegisterWindowMessage(_msgShowName);

            return _msgShow;
        }
        #endregion
    }
}

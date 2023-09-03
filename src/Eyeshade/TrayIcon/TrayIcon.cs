using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;

namespace Eyeshade.TrayIcon
{
    public class TrayIcon : IDisposable
    {
        #region fields
        private Windows.Win32.UI.Shell.NOTIFYICONDATAW _notifyData;
        private readonly IntPtr _hWnd;
        private readonly uint _uid;
        private Win32Icon? _icon;
        private uint _wmMessageId;
        private Windows.Win32.UI.Shell.SUBCLASSPROC? _WndProc;
        #endregion

        public TrayIcon(IntPtr hWnd, int id, uint wmMessageId = PInvoke.WM_USER + 1)
        {
            _hWnd = hWnd;
            _uid = (uint)id;
            _wmMessageId = wmMessageId;
        }

        #region events
        public event EventHandler? DoubleClicked;
        #endregion

        #region public methods
        public void Show(string icoFilePath, string? tooltip = null)
        {
            Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS flags = Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE;
            Windows.Win32.UI.WindowsAndMessaging.HICON hIcon = new Windows.Win32.UI.WindowsAndMessaging.HICON(IntPtr.Zero);
            if (!string.IsNullOrEmpty(icoFilePath))
            {
                flags |= Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
                _icon = Win32Icon.FromFile(icoFilePath, 16, 16);
                hIcon = _icon.Handle;
            }

            __char_128 tip = new __char_128();
            if (!string.IsNullOrEmpty(tooltip))
            {
                flags |= Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
                for (int i = 0; i < tooltip.Length && i < tip.Length; i++)
                {
                    tip[i] = tooltip[i];
                }
                tip[tip.Length - 1] = '\0';
            }

            _notifyData = new Windows.Win32.UI.Shell.NOTIFYICONDATAW()
            {
                cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.Shell.NOTIFYICONDATAW>(),
                hWnd = new Windows.Win32.Foundation.HWND(_hWnd),
                uID = _uid,
                Anonymous = new Windows.Win32.UI.Shell.NOTIFYICONDATAW._Anonymous_e__Union() { uVersion = 0x00 },
                uCallbackMessage = _wmMessageId,
                hIcon = hIcon,
                szTip = tip,
                uFlags = flags
            };
            PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_ADD, _notifyData);

            _WndProc = new Windows.Win32.UI.Shell.SUBCLASSPROC(WndProc);
            PInvoke.SetWindowSubclass(new Windows.Win32.Foundation.HWND(_hWnd), _WndProc, 0, 0);
        }

        public void Close()
        {
            var notifyData = new Windows.Win32.UI.Shell.NOTIFYICONDATAW()
            {
                cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.Shell.NOTIFYICONDATAW>(),
                hWnd = new Windows.Win32.Foundation.HWND(_hWnd),
                uID = _uid
            };
            PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_DELETE, notifyData);
        }

        public void Dispose()
        {
            _icon?.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion

        #region private methods
        private Windows.Win32.Foundation.LRESULT WndProc(Windows.Win32.Foundation.HWND hWnd,
            uint uMsg,
            Windows.Win32.Foundation.WPARAM wParam,
            Windows.Win32.Foundation.LPARAM lParam,
            nuint uIdSubclass, nuint dwRefData)
        {
            if (uMsg == _wmMessageId)
            {
                switch ((uint)((IntPtr)lParam).ToInt32())
                {
                    case PInvoke.WM_LBUTTONDBLCLK:
                        {
                            DoubleClicked?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                    default: break;
                }
            }

            return PInvoke.DefWindowProc(hWnd, uMsg, wParam, lParam);
        }
        #endregion
    }

    class Win32Icon : IDisposable
    {
        #region fields
        private readonly Windows.Win32.UI.WindowsAndMessaging.HICON handle;
        private readonly Microsoft.Win32.SafeHandles.SafeFileHandle? _fileHandle;
        #endregion

        private Win32Icon(Microsoft.Win32.SafeHandles.SafeFileHandle fileHandle)
        {
            _fileHandle = fileHandle; //pin
            bool hInstanceAddRef = false;
            fileHandle.DangerousAddRef(ref hInstanceAddRef);
            handle = new Windows.Win32.UI.WindowsAndMessaging.HICON(fileHandle.DangerousGetHandle());
        }

        public Windows.Win32.UI.WindowsAndMessaging.HICON Handle => handle;

        /// <summary>
        /// Loads an icon from an .ico file.
        /// </summary>
        /// <param name="filename">Path to file</param>
        /// <returns>Icon</returns>
        public static Win32Icon FromFile(string filename, int width, int height)
        {
            var handle = PInvoke.LoadImage(null,
                filename,
                Windows.Win32.UI.WindowsAndMessaging.GDI_IMAGE_TYPE.IMAGE_ICON,
                width, height,
                Windows.Win32.UI.WindowsAndMessaging.IMAGE_FLAGS.LR_LOADFROMFILE);
            if (handle == null || handle.IsInvalid)
                throw new Win32Exception();

            return new Win32Icon(handle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Win32Icon()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes the icon
        /// </summary>
        /// <param name="disposing"></param>
        protected void Dispose(bool disposing)
        {
            PInvoke.DestroyIcon(Handle); // also closes filehandle
        }
    }
}

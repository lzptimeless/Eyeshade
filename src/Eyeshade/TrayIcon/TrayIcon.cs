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
    class TrayIcon : IDisposable
    {
        #region fields
        private Windows.Win32.UI.Shell.NOTIFYICONDATAW _notifyData;
        private readonly IntPtr _hWnd;
        private readonly uint _uid;
        private Win32Icon? _icon;
        private uint _wmMessageId;
        private Windows.Win32.UI.Shell.SUBCLASSPROC? _WndProc;
        private Win32PopupMenu? _popupMenu;
        #endregion

        public TrayIcon(IntPtr hWnd, int id, uint wmMessageId = PInvoke.WM_USER + 1)
        {
            _hWnd = hWnd;
            _uid = (uint)id;
            _wmMessageId = wmMessageId;
        }

        #region events
        public event EventHandler? DoubleClicked;
        public event EventHandler<MenuItemExecuteArgs>? MenuItemExecute;
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
            if (!PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_ADD, _notifyData))
                throw new Win32Exception();

            _WndProc = new Windows.Win32.UI.Shell.SUBCLASSPROC(WndProc);
            if (!PInvoke.SetWindowSubclass(new Windows.Win32.Foundation.HWND(_hWnd), _WndProc, 0, 0))
                throw new Win32Exception();
        }

        public bool AddMenuItem(int id, string content)
        {
            if (_popupMenu == null)
            {
                _popupMenu = new Win32PopupMenu();
            }

            return _popupMenu.AddMenuItem(id, content);
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
            if (_WndProc != null)
            {
                PInvoke.RemoveWindowSubclass(new Windows.Win32.Foundation.HWND(_hWnd), _WndProc, 0);
                _WndProc = null;
            }

            if (_popupMenu != null)
            {
                _popupMenu.Dispose();
                _popupMenu = null;
            }

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
                switch ((uint)lParam.Value)
                {
                    case PInvoke.WM_LBUTTONDBLCLK:
                        {
                            DoubleClicked?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                    case PInvoke.WM_RBUTTONUP:
                        {
                            _popupMenu?.Show(_hWnd);
                        }
                        break;
                    default: break;
                }
            }
            else if (uMsg == PInvoke.WM_COMMAND)
            {
                var menuItemId = (int)(wParam & 0x0000FFFF); // LOWORD
                if (_popupMenu?.ContainsMenuItemId(menuItemId) == true)
                    MenuItemExecute?.Invoke(this, new MenuItemExecuteArgs(menuItemId));
            }

            return PInvoke.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
        #endregion
    }

    class Win32Icon : IDisposable
    {
        #region fields
        private Windows.Win32.UI.WindowsAndMessaging.HICON _handle;
        private readonly Microsoft.Win32.SafeHandles.SafeFileHandle? _fileHandle;
        #endregion

        private Win32Icon(Microsoft.Win32.SafeHandles.SafeFileHandle fileHandle)
        {
            _fileHandle = fileHandle; //pin
            bool hInstanceAddRef = false;
            fileHandle.DangerousAddRef(ref hInstanceAddRef);
            _handle = new Windows.Win32.UI.WindowsAndMessaging.HICON(fileHandle.DangerousGetHandle());
        }

        public Windows.Win32.UI.WindowsAndMessaging.HICON Handle => _handle;

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
            if (!_handle.IsNull)
            {
                PInvoke.DestroyIcon(_handle); // also closes filehandle
                _handle = Windows.Win32.UI.WindowsAndMessaging.HICON.Null;
            }
        }
    }

    class Win32PopupMenu : IDisposable
    {
        private Windows.Win32.UI.WindowsAndMessaging.HMENU _hMenu;
        private readonly List<int> _menuItemIds = new List<int>();

        public Win32PopupMenu()
        {
            _hMenu = PInvoke.CreatePopupMenu();
            if (_hMenu.IsNull)
                throw new Win32Exception();
        }

        public unsafe void Show(IntPtr hWnd)
        {
            if (!PInvoke.GetCursorPos(out Point cursor)) return;

            PInvoke.TrackPopupMenu(_hMenu, Windows.Win32.UI.WindowsAndMessaging.TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN,
                cursor.X, cursor.Y, 0, new Windows.Win32.Foundation.HWND(hWnd), null);
        }

        public unsafe bool AddMenuItem(int id, string content)
        {
            fixed (char* lpNewItemLocal = content)
            {
                var result = PInvoke.AppendMenu(_hMenu, Windows.Win32.UI.WindowsAndMessaging.MENU_ITEM_FLAGS.MF_STRING, new UIntPtr((uint)id), lpNewItemLocal);
                if (result)
                {
                    _menuItemIds.Add(id);
                }

                return result;
            }
        }

        public bool ContainsMenuItemId(int id)
        {
            return _menuItemIds.Contains(id);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Win32PopupMenu()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_hMenu.IsNull)
            {
                // DestroyMenu 是递归的，也就是说，它将销毁菜单及其所有子菜单。
                PInvoke.DestroyMenu(_hMenu);
                _hMenu = Windows.Win32.UI.WindowsAndMessaging.HMENU.Null;
            }
        }
    }

    class MenuItemExecuteArgs : EventArgs
    {
        public MenuItemExecuteArgs(int id)
        {
            Id = id;
        }

        public int Id { get; private set; }
    }
}

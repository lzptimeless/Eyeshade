using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Eyeshade.TrayIcon
{
    class TrayIcon : IDisposable
    {
        #region fields
        private Windows.Win32.UI.Shell.NOTIFYICONDATAW _notifyData;
        private readonly IntPtr _hWnd;
        private readonly uint _uid;
        private Win32Icon? _icon;
        private string? _iconPath;
        private string? _tooltip;
        private uint _wmMessageId;
        private Win32PopupMenu? _popupMenu;
        private bool _isShow;
        #endregion

        public TrayIcon(IntPtr hWnd, int id, uint wmMessageId = PInvoke.WM_USER + 1)
        {
            _hWnd = hWnd;
            _uid = (uint)id;
            _wmMessageId = wmMessageId;
        }

        #region events
        public event EventHandler? PopupOpen;
        public event EventHandler? PopupClose;
        public event EventHandler? Select;
        public event EventHandler<TrayIconMenuItemExecuteArgs>? MenuItemExecute;
        #endregion

        #region public methods
        public void Show(string icoFilePath, string? tooltip = null, bool useCustomPopup = true)
        {
            if (_isShow) throw new ApplicationException("TrayIcon has been shown.");

            Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS flags = Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE;
            if (!useCustomPopup)
            {
                // This option will disable NIN_POPUPOPEN and NIN_POPUPCLOSE message
                flags |= Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP;
            }

            Windows.Win32.UI.WindowsAndMessaging.HICON hIcon = Windows.Win32.UI.WindowsAndMessaging.HICON.Null;

            _icon?.Dispose(); // Release old ico
            _icon = null;
            _iconPath = icoFilePath;
            if (!string.IsNullOrEmpty(icoFilePath))
            {
                flags |= Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
                _icon = Win32Icon.FromFile(icoFilePath, 32, 32);
                hIcon = _icon.Handle;
            }

            _tooltip = tooltip;
            __char_128 tip = new __char_128();
            if (!string.IsNullOrEmpty(tooltip))
            {
                flags |= Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
                for (int i = 0; i < tooltip.Length && i < tip.Length - 1; i++)
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
                Anonymous = new Windows.Win32.UI.Shell.NOTIFYICONDATAW._Anonymous_e__Union() { uVersion = PInvoke.NOTIFYICON_VERSION_4 },
                uCallbackMessage = _wmMessageId,
                hIcon = hIcon,
                szTip = tip,
                uFlags = flags
            };
            if (!PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_ADD, _notifyData))
                throw new Win32Exception();

            if (!PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_SETVERSION, _notifyData))
                throw new Win32Exception();

            _isShow = true;
        }

        public void SetIcon(string icoFilePath)
        {
            if (!_isShow) throw new ApplicationException("TrayIcon hasn't shown yet.");
            if (icoFilePath == _iconPath) return;

            _iconPath = icoFilePath;
            _icon?.Dispose(); // Release old ico
            _icon = null;
            if (!string.IsNullOrEmpty(icoFilePath))
            {
                _notifyData.uFlags = Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
                _icon = Win32Icon.FromFile(icoFilePath, 32, 32);
                _notifyData.hIcon = _icon.Handle;

                if (!PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_MODIFY, _notifyData))
                    throw new Win32Exception();
            }
        }

        public void SetTooltip(string? tooltip)
        {
            if (!_isShow) throw new ApplicationException("TrayIcon hasn't shown yet.");
            if (tooltip == _tooltip) return;

            _tooltip = tooltip;
            __char_128 tip = new __char_128();
            if (!string.IsNullOrEmpty(tooltip))
            {
                for (int i = 0; i < tooltip.Length && i < tip.Length - 1; i++)
                {
                    tip[i] = tooltip[i];
                }
                tip[tip.Length - 1] = '\0';
            }

            _notifyData.uFlags = Windows.Win32.UI.Shell.NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
            _notifyData.szTip = tip;
            if (!PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_MODIFY, _notifyData))
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

        public bool AddSubMenu(int subMenuId, string content)
        {
            if (_popupMenu == null)
            {
                _popupMenu = new Win32PopupMenu();
            }

            return _popupMenu.AddSubMenu(subMenuId, content);
        }

        public bool AddSubMenuItem(int subMenuId, int id, string content)
        {
            if (_popupMenu == null)
            {
                _popupMenu = new Win32PopupMenu();
            }

            return _popupMenu.AddSubMenuItem(subMenuId, id, content);
        }

        public bool SetMenuItem(int id, string content)
        {
            if (_popupMenu == null)
            {
                _popupMenu = new Win32PopupMenu();
            }

            return _popupMenu.SetMenuItem(id, content);
        }

        public void Close()
        {
            if (!_isShow) return;

            var notifyData = new Windows.Win32.UI.Shell.NOTIFYICONDATAW()
            {
                cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.Shell.NOTIFYICONDATAW>(),
                hWnd = new Windows.Win32.Foundation.HWND(_hWnd),
                uID = _uid
            };
            PInvoke.Shell_NotifyIcon(Windows.Win32.UI.Shell.NOTIFY_ICON_MESSAGE.NIM_DELETE, notifyData);

            _isShow = false;
        }

        public unsafe RECT CalculatePopupWindowPosition(int windowWidth, int windowHeight)
        {
            Windows.Win32.UI.Shell.NOTIFYICONIDENTIFIER id = new Windows.Win32.UI.Shell.NOTIFYICONIDENTIFIER()
            {
                cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.Shell.NOTIFYICONIDENTIFIER>(),
                hWnd = new HWND(_hWnd),
                uID = _uid,
                guidItem = Guid.Empty
            };
            var hResult = PInvoke.Shell_NotifyIconGetRect(id, out var iconLocation);
            if (!hResult.Succeeded)
            {
                RECT screenRect;
                if (!PInvoke.SystemParametersInfo(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWORKAREA, 0, &screenRect, 0))
                    throw new Win32Exception();

                iconLocation = RECT.FromXYWH(screenRect.Width, screenRect.Height, 2, 2);
            }

            Point anchorPoint = new Point(iconLocation.X + iconLocation.Width / 2, iconLocation.Y + iconLocation.Height / 2);
            const uint TPM_CENTERALIGN = 0x0004;
            const uint TPM_WORKAREA = 0x10000;
            if (!PInvoke.CalculatePopupWindowPosition(anchorPoint, new SIZE(windowWidth, windowHeight), TPM_CENTERALIGN | TPM_WORKAREA, null, out var popupPosition))
                throw new Win32Exception();

            return popupPosition;
        }

        public void ProcessWindowMessage(HWND hWnd, uint uMsg, WPARAM wParam, LPARAM lParam)
        {
            if (uMsg == _wmMessageId)
            {
                switch ((uint)lParam.Value & 0xFFFF) // LOWORD
                {
                    case PInvoke.WM_CONTEXTMENU:
                        {
                            _popupMenu?.Show(_hWnd);
                        }
                        break;
                    case PInvoke.NIN_SELECT:
                        {
                            Select?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                    case PInvoke.NIN_POPUPOPEN:
                        {
                            PopupOpen?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                    case PInvoke.NIN_POPUPCLOSE:
                        {
                            PopupClose?.Invoke(this, EventArgs.Empty);
                        }
                        break;
                    default: break;
                }
            }
            else if (uMsg == PInvoke.WM_COMMAND)
            {
                var menuItemId = (int)(wParam & 0x0000FFFF); // LOWORD
                if (_popupMenu?.ContainsMenuItemId(menuItemId) == true)
                    MenuItemExecute?.Invoke(this, new TrayIconMenuItemExecuteArgs(menuItemId));
            }
        }

        public void Dispose()
        {
            _icon?.Dispose();

            if (_popupMenu != null)
            {
                _popupMenu.Dispose();
                _popupMenu = null;
            }

            GC.SuppressFinalize(this);
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
        private readonly Dictionary<int, Windows.Win32.UI.WindowsAndMessaging.HMENU> _menuItemIds = new Dictionary<int, Windows.Win32.UI.WindowsAndMessaging.HMENU>();
        private readonly Dictionary<int, Windows.Win32.UI.WindowsAndMessaging.HMENU> _subMenus = new Dictionary<int, Windows.Win32.UI.WindowsAndMessaging.HMENU>();

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
                    _menuItemIds.Add(id, _hMenu);
                }

                return result;
            }
        }

        public unsafe bool AddSubMenu(int subMenuId, string content)
        {
            var hSubMenu = PInvoke.CreatePopupMenu();
            if (hSubMenu.IsNull)
            {
                return false;
            }

            fixed (char* lpNewItemLocal = content)
            {
                var result = PInvoke.AppendMenu(_hMenu,
                    Windows.Win32.UI.WindowsAndMessaging.MENU_ITEM_FLAGS.MF_POPUP | Windows.Win32.UI.WindowsAndMessaging.MENU_ITEM_FLAGS.MF_STRING,
                    (uint)hSubMenu.Value, lpNewItemLocal);
                if (result)
                {
                    _subMenus.Add(subMenuId, hSubMenu);
                }
                else
                {
                    PInvoke.DestroyMenu(hSubMenu);
                }

                return result;
            }
        }

        public unsafe bool AddSubMenuItem(int subMenuId, int id, string content)
        {
            if (!_subMenus.TryGetValue(subMenuId, out var hSubMenu)) return false;

            fixed (char* lpNewItemLocal = content)
            {
                var result = PInvoke.AppendMenu(hSubMenu, Windows.Win32.UI.WindowsAndMessaging.MENU_ITEM_FLAGS.MF_STRING, new UIntPtr((uint)id), lpNewItemLocal);
                if (result)
                {
                    _menuItemIds.Add(id, hSubMenu);
                }

                return result;
            }
        }

        public unsafe bool SetMenuItem(int id, string content)
        {
            if (!_menuItemIds.TryGetValue(id, out var hMenu)) return false;

            fixed (char* lpNewItemLocal = content)
            {
                var result = PInvoke.ModifyMenu(hMenu, (uint)id,
                    Windows.Win32.UI.WindowsAndMessaging.MENU_ITEM_FLAGS.MF_BYCOMMAND | Windows.Win32.UI.WindowsAndMessaging.MENU_ITEM_FLAGS.MF_STRING,
                    (uint)id, lpNewItemLocal);
                return result;
            }
        }

        public bool ContainsMenuItemId(int id)
        {
            return _menuItemIds.ContainsKey(id);
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

    class TrayIconMenuItemExecuteArgs : EventArgs
    {
        public TrayIconMenuItemExecuteArgs(int id)
        {
            Id = id;
        }

        public int Id { get; private set; }
    }
}

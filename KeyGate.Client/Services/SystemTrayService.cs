using System.Runtime.InteropServices;

namespace KeyGate.Client.Services;

public partial class SystemTrayService : IDisposable
{
    private const int WM_TRAYICON = 0x0400 + 1;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_COMMAND = 0x0111;
    private const int NIM_ADD = 0;
    private const int NIM_DELETE = 2;
    private const int NIF_MESSAGE = 1;
    private const int NIF_ICON = 2;
    private const int NIF_TIP = 4;
    private const int ID_RESTORE = 1001;
    private const int ID_LOCKEXIT = 1002;
    private const int IMAGE_ICON = 1;
    private const int LR_LOADFROMFILE = 0x10;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint hWnd, uint uMsg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImageW(nint hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern nint TrackPopupMenuEx(nint hMenu, uint uFlags, int x, int y, nint hWnd, nint prcRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenuW(nint hMenu, uint uFlags, nint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint ExtractIconW(nint hInst, string lpszExeFileName, int nIconIndex);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    private nint _hwnd;
    private nint _hIcon;
    private NOTIFYICONDATAW _nid;
    private bool _isVisible;
    private bool _disposed;
    private WndProcDelegate? _wndProcDelegate;

    public event Action? OnRestoreClicked;
    public event Action? OnLockAndExitClicked;

    public void Show(string? iconPath = null, string tooltip = "KeyGate - Unlocked")
    {
        if (_isVisible) return;

        EnsureWindowCreated();

        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
            _hIcon = LoadImageW(0, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            if (_hIcon == 0)
                _hIcon = LoadImageW(0, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
        }

        if (_hIcon == 0)
        {
            var exePath = Environment.ProcessPath ?? "";
            _hIcon = ExtractIconW(0, exePath, 0);
        }

        _nid = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = tooltip
        };

        Shell_NotifyIconW(NIM_ADD, ref _nid);
        _isVisible = true;
    }

    public void Hide()
    {
        if (!_isVisible || _nid.hWnd == 0) return;

        Shell_NotifyIconW(NIM_DELETE, ref _nid);
        _isVisible = false;

        if (_hIcon != 0)
        {
            DestroyIcon(_hIcon);
            _hIcon = 0;
        }

        if (_hwnd != 0)
        {
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint hIcon);

    private void EnsureWindowCreated()
    {
        if (_hwnd != 0) return;

        var hInstance = GetModuleHandleW(null);
        var className = "KeyGateTrayMsg";

        _wndProcDelegate = WndProc;

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            hInstance = hInstance,
            lpszClassName = className,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate)
        };

        RegisterClassExW(ref wc);

        _hwnd = CreateWindowExW(0, className, "KeyGateTray", 0, 0, 0, 0, 0, 0, 0, hInstance, 0);
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMsg = lParam.ToInt32();
            if (mouseMsg == WM_LBUTTONUP)
                OnRestoreClicked?.Invoke();
            else if (mouseMsg == WM_RBUTTONUP)
                ShowContextMenu();
            return 0;
        }

        if (msg == WM_COMMAND)
        {
            int menuId = wParam.ToInt32() & 0xFFFF;
            if (menuId == ID_RESTORE)
                OnRestoreClicked?.Invoke();
            else if (menuId == ID_LOCKEXIT)
                OnLockAndExitClicked?.Invoke();
            return 0;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        GetCursorPos(out var point);

        var hMenu = CreatePopupMenu();
        AppendMenuW(hMenu, 0, ID_RESTORE, "Restore");
        AppendMenuW(hMenu, 0x800, 0, null);
        AppendMenuW(hMenu, 0, ID_LOCKEXIT, "Lock && Exit");

        SetForegroundWindow(_hwnd);
        TrackPopupMenuEx(hMenu, 0x0002 | 0x0020 | 0x0000, point.X, point.Y, _hwnd, 0);
        PostMessageW(_hwnd, 0, 0, 0);
        DestroyMenu(hMenu);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Hide();
        GC.SuppressFinalize(this);
    }
}

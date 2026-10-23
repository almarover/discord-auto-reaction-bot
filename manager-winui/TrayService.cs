using System.Runtime.InteropServices;

namespace DiscordReactionManager_WinUI;

public sealed class TrayService : IDisposable
{
    private const uint WmApp = 0x8000;
    private const uint TrayMessage = WmApp + 31;
    private const uint WmLButtonDblClk = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;
    private const uint MfString = 0;
    private const uint MfSeparator = 0x800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private readonly MainWindow _window;
    private readonly nint _hwnd;
    private readonly nint _icon;
    private readonly SubclassProc _subclassProc;
    private NotifyIconData _data;

    public TrayService(MainWindow window)
    {
        _window = window;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _icon = LoadImage(0, iconPath, ImageIcon, 0, 0, LrLoadFromFile);
        _subclassProc = WindowSubclass;
        SetWindowSubclass(_hwnd, _subclassProc, 1, 0);
        _data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = TrayMessage,
            hIcon = _icon,
            szTip = "Discord Auto Reaction Bot 管理器",
        };
        ShellNotifyIcon(NimAdd, ref _data);
    }

    private nint WindowSubclass(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == TrayMessage)
        {
            uint mouseMessage = unchecked((uint)lParam.ToInt64());
            if (mouseMessage == WmLButtonDblClk)
            {
                _window.ShowFromTray();
                return 0;
            }
            if (mouseMessage == WmRButtonUp)
            {
                ShowContextMenu();
                return 0;
            }
        }
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        nint menu = CreatePopupMenu();
        AppendMenu(menu, MfString, 1, "打开管理器");
        AppendMenu(menu, MfSeparator, 0, null);
        AppendMenu(menu, MfString, 2, "启动 Bot");
        AppendMenu(menu, MfString, 3, "停止 Bot");
        AppendMenu(menu, MfSeparator, 0, null);
        AppendMenu(menu, MfString, 4, "退出管理器");
        GetCursorPos(out Point point);
        SetForegroundWindow(_hwnd);
        uint command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCmd, point.X, point.Y, 0, _hwnd, 0);
        DestroyMenu(menu);
        switch (command)
        {
            case 1: _window.ShowFromTray(); break;
            case 2: _ = AppServices.Bot.StartAsync(); break;
            case 3: _ = AppServices.Bot.StopAsync(); break;
            case 4: _window.ExitManager(); break;
        }
    }

    public void Dispose()
    {
        ShellNotifyIcon(NimDelete, ref _data);
        RemoveWindowSubclass(_hwnd, _subclassProc, 1);
        if (_icon != 0) DestroyIcon(_icon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    private delegate nint SubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW")]
    private static extern nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    private static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);
}

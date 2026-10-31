using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace GSYNC.App.Infrastructure;

/// <summary>Owns the Windows notification-area icon for the lifetime of the main window.</summary>
internal sealed class NotificationAreaIcon : IDisposable
{
    private const uint CallbackMessage = 0x8000 + 42;
    private const uint LeftButtonUp = 0x0202;
    private const uint RightButtonUp = 0x0205;
    private const uint ContextMenu = 0x007B;
    private const uint KeySelect = 0x0401;
    private const uint Select = 0x0400;
    private const uint IconId = 1;
    private readonly IntPtr _window;
    private readonly IntPtr _icon;
    private readonly SubclassProc _subclass;
    private readonly uint _taskbarCreated;
    private readonly uint _activateMessage;
    private readonly Action _show;
    private readonly Action _exit;
    private readonly Func<bool> _isChinese;
    private bool _version4;
    private bool _disposed;
    public bool IsRegistered { get; private set; }

    public NotificationAreaIcon(IntPtr window, string dataRoot, string iconPath, Func<bool> isChinese, Action show, Action exit)
    {
        _window = window;
        _show = show;
        _exit = exit;
        _isChinese = isChinese;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _activateMessage = ActivationMessage(dataRoot);
        _subclass = WindowProcedure;
        _icon = LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x0010 | 0x0040);
        if (_icon == IntPtr.Zero) throw new InvalidOperationException("Cannot load the notification-area icon.");
        if (!SetWindowSubclass(_window, _subclass, (UIntPtr)IconId, UIntPtr.Zero))
        {
            DestroyIcon(_icon);
            throw new InvalidOperationException("Cannot initialize the notification-area icon.");
        }
        AddIcon();
    }

    public static void ActivateExisting(string dataRoot)
    {
        var message = ActivationMessage(dataRoot);
        if (message != 0) PostMessage(new IntPtr(0xffff), message, IntPtr.Zero, IntPtr.Zero);
    }

    private static uint ActivationMessage(string dataRoot)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return RegisterWindowMessage("GSYNC.Activate." + hash);
    }

    private void AddIcon()
    {
        var data = IconData();
        IsRegistered = ShellNotifyIcon(0, ref data);
        if (!IsRegistered) return;
        data.Version = 4;
        _version4 = ShellNotifyIcon(4, ref data);
    }

    private NotifyIconData IconData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = _window, Id = IconId,
        Flags = 0x01 | 0x02 | 0x04 | 0x80, Callback = CallbackMessage, Icon = _icon,
        Tip = "GSYNC", Info = string.Empty, InfoTitle = string.Empty,
    };

    private IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData)
    {
        try
        {
            if (message == _taskbarCreated && message != 0)
            {
                IsRegistered = false;
                AddIcon();
                if (!IsRegistered) _show();
            }
            else if (message == _activateMessage && message != 0) _show();
            else if (message == CallbackMessage)
            {
                var notification = _version4 ? unchecked((uint)lParam.ToInt64()) & 0xffff : unchecked((uint)lParam.ToInt64());
                switch (Classify(notification, _version4))
                {
                    case TrayAction.Show: _show(); break;
                    case TrayAction.Menu: ShowMenu(); break;
                }
                return IntPtr.Zero;
            }
        }
        catch (Exception exception) { Serilog.Log.Error(exception, "Notification-area operation failed."); }
        return DefSubclassProc(window, message, wParam, lParam);
    }

    internal static TrayAction Classify(uint notification, bool version4) => notification switch
    {
        LeftButtonUp or KeySelect or Select => TrayAction.Show,
        ContextMenu => TrayAction.Menu,
        RightButtonUp when !version4 => TrayAction.Menu,
        _ => TrayAction.None,
    };

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            if (!AppendMenu(menu, 0, (UIntPtr)1, _isChinese() ? "退出 GSYNC" : "Exit GSYNC")) return;
            if (!GetCursorPos(out var point)) return;
            SetForegroundWindow(_window);
            var selected = TrackPopupMenuEx(menu, 0x0100 | 0x0002, point.X, point.Y, _window, IntPtr.Zero);
            PostMessage(_window, 0, IntPtr.Zero, IntPtr.Zero);
            if (selected == 1) _exit();
        }
        finally { DestroyMenu(menu); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (IsRegistered) { var data = IconData(); ShellNotifyIcon(2, ref data); IsRegistered = false; }
        RemoveWindowSubclass(_window, _subclass, (UIntPtr)IconId);
        DestroyIcon(_icon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public uint Flags;
        public uint Callback;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    private delegate IntPtr SubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)] private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr window, IntPtr parameters);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}

internal enum TrayAction { None, Show, Menu }

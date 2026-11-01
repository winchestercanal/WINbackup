using System.Runtime.InteropServices;

namespace WINbackup.Services;

/// <summary>
/// Notificação do Windows ao fim de um backup. Usa o ícone da bandeja (Shell_NotifyIcon),
/// que aparece como toast no Windows 10/11 e funciona com o app autocontido e elevado,
/// ao contrário do AppNotificationManager.
/// </summary>
public static class Notifier
{
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    const uint NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10;
    const uint NIIF_INFO = 0x1, NIIF_WARNING = 0x2, NIIF_LARGE_ICON = 0x20, NIIF_USER = 0x4;

    static IntPtr _hwnd, _icon;
    static bool _added;

    public static void Init(IntPtr hwnd)
    {
        _hwnd = hwnd;
        try { ExtractIconEx(Environment.ProcessPath!, 0, out _icon, out _, 1); }
        catch (Exception ex) { CrashLog.Write(ex); }
    }

    public static void Show(string title, string body, bool warning = false)
    {
        if (_hwnd == IntPtr.Zero) return;
        try
        {
            var data = NewData();
            data.uFlags = NIF_ICON | NIF_TIP;
            if (!_added) _added = Shell_NotifyIcon(NIM_ADD, ref data);
            data.uFlags = NIF_INFO | NIF_ICON | NIF_TIP;
            data.szInfoTitle = Trim(title, 63);
            data.szInfo = Trim(body, 255);
            data.dwInfoFlags = _icon != IntPtr.Zero ? NIIF_USER | NIIF_LARGE_ICON : warning ? NIIF_WARNING : NIIF_INFO;
            data.hBalloonIcon = _icon;
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    public static void Shutdown()
    {
        if (!_added) return;
        var data = NewData();
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _added = false;
    }

    static NOTIFYICONDATA NewData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        hIcon = _icon,
        szTip = "WINbackup",
        szInfo = "",
        szInfoTitle = "",
    };

    static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);
}

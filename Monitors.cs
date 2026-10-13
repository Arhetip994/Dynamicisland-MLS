using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DynamicIsland;

public sealed class MonitorInfo
{
    public IntPtr Handle { get; init; }
    public string DeviceName { get; init; } = "";
    public string FriendlyName { get; init; } = "";
    public Rect Bounds { get; init; }
    public Rect WorkArea { get; init; }
    public double DpiScale { get; init; } = 1.0;
    public bool IsPrimary { get; init; }
    public int Index { get; init; }

    public string Title => string.IsNullOrWhiteSpace(FriendlyName)
        ? $"Монитор {Index}{(IsPrimary ? " (Основной)" : "")}"
        : $"{FriendlyName}{(IsPrimary ? " (Основной)" : "")}";

    public string Resolution => $"{(int)Bounds.Width} × {(int)Bounds.Height}";
}

public static class Monitors
{
    const uint MONITOR_DEFAULTTONEAREST = 2;
    const uint MONITORINFOF_PRIMARY = 1;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(Native.POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    public static List<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        int index = 1;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMon, ref info))
            {
                bool primary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0;
                string deviceName = info.szDevice ?? "";
                string friendly = GetFriendlyName(deviceName, index);

                double dpiScale = 1.0;
                try
                {
                    if (GetDpiForMonitor(hMon, 0, out uint dpiX, out _) == 0 && dpiX > 0)
                    {
                        dpiScale = dpiX / 96.0;
                    }
                }
                catch { }

                var bounds = new Rect(info.rcMonitor.Left, info.rcMonitor.Top,
                    info.rcMonitor.Right - info.rcMonitor.Left, info.rcMonitor.Bottom - info.rcMonitor.Top);
                var work = new Rect(info.rcWork.Left, info.rcWork.Top,
                    info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);

                list.Add(new MonitorInfo
                {
                    Handle = hMon,
                    DeviceName = deviceName,
                    FriendlyName = friendly,
                    Bounds = bounds,
                    WorkArea = work,
                    DpiScale = dpiScale,
                    IsPrimary = primary,
                    Index = index++,
                });
            }
            return true;
        }, IntPtr.Zero);

        if (list.Count == 0)
        {
            list.Add(new MonitorInfo
            {
                DeviceName = "\\\\.\\DISPLAY1",
                FriendlyName = "Основной монитор",
                Bounds = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                WorkArea = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
                DpiScale = 1.0,
                IsPrimary = true,
                Index = 1,
            });
        }

        return list;
    }

    static string GetFriendlyName(string deviceName, int index)
    {
        try
        {
            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (EnumDisplayDevices(deviceName, 0, ref dd, 0))
            {
                string desc = dd.DeviceString?.Trim() ?? "";
                if (!string.IsNullOrEmpty(desc) &&
                    !desc.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase) &&
                    !desc.Equals("Универсальный монитор PnP", StringComparison.OrdinalIgnoreCase))
                {
                    return desc;
                }
            }
        }
        catch { }

        return $"Монитор {index}";
    }

    public static MonitorInfo GetPrimary()
    {
        var all = GetAll();
        return all.FirstOrDefault(m => m.IsPrimary) ?? all[0];
    }

    public static MonitorInfo Find(string? deviceName)
    {
        var all = GetAll();
        if (!string.IsNullOrEmpty(deviceName))
        {
            MonitorInfo? match = all.FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return GetPrimary();
    }

    public static MonitorInfo GetFromPoint(Native.POINT pt)
    {
        IntPtr hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        var all = GetAll();
        return all.FirstOrDefault(m => m.Handle == hMon) ?? GetPrimary();
    }

    public static MonitorInfo GetFromWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return GetPrimary();
        IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var all = GetAll();
        return all.FirstOrDefault(m => m.Handle == hMon) ?? GetPrimary();
    }

    public static void PositionWindow(Window window, MonitorInfo monitor)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        double dpi = monitor.DpiScale > 0 ? monitor.DpiScale : 1.0;
        int physWidth = (int)Math.Round(window.Width * dpi);
        int physX = (int)Math.Round(monitor.Bounds.Left + (monitor.Bounds.Width - physWidth) / 2);
        int physY = (int)Math.Round(monitor.Bounds.Top);

        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, IntPtr.Zero, physX, physY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        window.Left = physX / dpi;
        window.Top = physY / dpi;
    }
}

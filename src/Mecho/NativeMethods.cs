using System;
using System.Runtime.InteropServices;

namespace Mecho;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int max);

    /// <summary>Програмата отпред: номерът на процеса и заглавието на прозореца (0, "" – няма).</summary>
    public static (uint Pid, string Title) ForegroundWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return (0, "");
        GetWindowThreadProcessId(hwnd, out uint pid);
        var text = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, text, text.Capacity);
        return (pid, text.ToString());
    }

    // ── Full screen (video, game, presentation) ──

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder name, int max);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int QUNS_BUSY = 2, QUNS_RUNNING_D3D_FULL_SCREEN = 3, QUNS_PRESENTATION_MODE = 4;

    /// <summary>
    /// Is something else full screen on the same monitor as this window (video, game,
    /// presentation)? Windows itself must say a full-screen app runs (the same check it uses
    /// to hold back notifications – so a maximized window, even with an auto-hidden taskbar,
    /// does not count), and the window in front must cover that whole monitor.
    /// </summary>
    public static bool FullscreenOnMonitorOf(IntPtr mine)
    {
        try
        {
            if (SHQueryUserNotificationState(out int state) != 0
                || state is not (QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE)) return false;
            var front = GetForegroundWindow();
            if (front == IntPtr.Zero || front == GetShellWindow() || front == GetDesktopWindow()) return false;
            GetWindowThreadProcessId(front, out uint pid);
            if (pid == (uint)Environment.ProcessId) return false;
            var name = new System.Text.StringBuilder(64);
            GetClassName(front, name, name.Capacity);
            if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;   // the desktop, the taskbar
            var monitor = MonitorFromWindow(front, MONITOR_DEFAULTTONEAREST);
            if (mine != IntPtr.Zero && monitor != MonitorFromWindow(mine, MONITOR_DEFAULTTONEAREST)) return false;
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(front, out var r)) return false;
            var m = info.rcMonitor;
            return r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;

    /// <summary>
    /// Мести прозореца наведнъж (x и y заедно, в цели физически пиксели). Window.Left
    /// и Window.Top поотделно го местеха два пъти в кадъра (накъсване), а и при
    /// дробни DIP мечокът потрепваше с пиксел. Ако вече е там – нищо не прави.
    /// </summary>
    public static void MoveWindow(System.Windows.Window w, double left, double top)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero)
        {
            w.Left = left;
            w.Top = top;
            return;
        }
        double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(w).DpiScaleX;
        int x = (int)Math.Round(left * dpi), y = (int)Math.Round(top * dpi);
        if (GetWindowRect(hwnd, out var r) && r.Left == x && r.Top == y) return;
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    /// <summary>Слага прозореца най-отгоре (над другите „винаги отгоре“), без да му дава фокус.</summary>
    public static void BringToTop(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x80;

    /// <summary>Колко секунди никой не е пипал мишката и клавиатурата.</summary>
    public static double IdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.dwTime) / 1000.0;
    }

    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;

    /// <summary>Кликовете минават през прозореца (и той не се вижда в Alt+Tab).</summary>
    public static void MakeClickThrough(IntPtr hwnd)
    {
        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    private const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>Цъкането по прозореца не му дава фокус (менюто под него не се затваря).</summary>
    public static void MakeNoActivate(IntPtr hwnd)
    {
        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>Скрива прозореца от Alt+Tab.</summary>
    public static void MakeToolWindow(IntPtr hwnd)
    {
        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
    }
}

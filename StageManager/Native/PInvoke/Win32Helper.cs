using System;
using System.Runtime.InteropServices;

namespace StageManager.Native.PInvoke
{
    public static class Win32Helper
    {

        public static void QuitApplication(IntPtr hwnd)
        {
            Win32.SendNotifyMessage(hwnd, Win32.WM_SYSCOMMAND, Win32.SC_CLOSE, IntPtr.Zero);
        }

        public static bool IsCloaked(IntPtr hwnd)
        {
            bool isCloaked;
            var attr = Win32.DwmGetWindowAttribute(hwnd, (int)Win32.DwmWindowAttribute.DWMWA_CLOAKED, out isCloaked, Marshal.SizeOf(typeof(bool)));
            return isCloaked;
        }

        public static bool IsAppWindow(IntPtr hwnd)
        {
            return (Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd)) &&
                   !Win32.GetWindowExStyleLongPtr(hwnd).HasFlag(Win32.WS_EX.WS_EX_NOACTIVATE) &&
                   !Win32.GetWindowStyleLongPtr(hwnd).HasFlag(Win32.WS.WS_CHILD);
        }

        /// <summary>
        /// Whether the shell would offer this window in alt-tab. The exposé shows what alt-tab
        /// shows: a window the user cannot reach that way has no business having a tile either.
        /// </summary>
        /// <remarks>
        /// http://blogs.msdn.com/b/oldnewthing/archive/2007/10/08/5351207.aspx
        /// </remarks>
        public static bool IsAltTabWindow(IntPtr hWnd)
        {
            // Owned windows are tooltips, palettes and other helpers hanging off a main window.
            if (Win32.GetWindow(hWnd, Win32.GW.GW_OWNER) != IntPtr.Zero)
                return false;

            var exStyle = Win32.GetWindowExStyleLongPtr(hWnd);

            // WS_EX_APPWINDOW forces a window into the switcher even when it looks like a tool.
            if (exStyle.HasFlag(Win32.WS_EX.WS_EX_APPWINDOW))
                return true;

            // A tool window is deliberately kept out of alt-tab by its own app. Moonlight's Qt
            // helper carries this style while its SDL streaming window does not, which is
            // exactly the pair that used to produce two tiles for one app.
            if (exStyle.HasFlag(Win32.WS_EX.WS_EX_TOOLWINDOW))
                return false;

            // The shell's own last test: a window whose title bar reports itself invisible is
            // not offered, whatever its styles say.
            var info = new Win32.TitleBarInfo
            {
                Size = (uint)Marshal.SizeOf<Win32.TitleBarInfo>(),
                State = new uint[6]
            };

            if (Win32.GetTitleBarInfo(hWnd, ref info) && (info.State[0] & Win32.STATE_SYSTEM_INVISIBLE) != 0)
                return false;

            return true;
        }

        public static void ForceForegroundWindow(IntPtr hWnd)
        {
            FocusStealer.Steal(hWnd);
        }

        /// <summary>
        /// The size DWM composes into a thumbnail of this window, which is what a preview's
        /// aspect ratio has to be derived from. Empty for a window that has no live thumbnail,
        /// a minimized one above all.
        /// </summary>
        /// <remarks>
        /// The source is the client area, because DwmThumbnail registers with
        /// DWM_TNP_SOURCECLIENTAREAONLY; the window rect would add the caption and the
        /// invisible resize border, and DWM letterboxes any such surplus into empty bands.
        /// </remarks>
        public static System.Drawing.Size PreviewSourceSize(IntPtr hwnd)
        {
            var rect = new Win32.Rect();
            Win32.GetClientRect(hwnd, ref rect);
            return new System.Drawing.Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
    }
}

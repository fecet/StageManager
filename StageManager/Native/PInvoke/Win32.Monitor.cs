using System;
using System.Runtime.InteropServices;

namespace StageManager.Native.PInvoke
{
	public static partial class Win32
	{
		private const uint MONITOR_DEFAULTTONEAREST = 2;

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

		[StructLayout(LayoutKind.Sequential)]
		private struct MONITORINFO
		{
			public int cbSize;
			public Rect rcMonitor;
			public Rect rcWork;
			public uint dwFlags;
		}

		[DllImport("user32.dll")]
		private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

		/// <summary>Work area (monitor minus taskbar) of the monitor the window sits on.</summary>
		public static Rect GetWorkArea(IntPtr hwnd)
		{
			var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
			var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
			if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref mi))
				return mi.rcWork;

			return default;
		}
	}
}

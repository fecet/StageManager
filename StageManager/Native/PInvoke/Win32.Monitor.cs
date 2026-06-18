using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StageManager.Native.PInvoke
{
	public static partial class Win32
	{
		private const uint MONITOR_DEFAULTTONEAREST = 2;

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

		private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr dwData);

		[DllImport("user32.dll")]
		private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

		public readonly struct MonitorArea
		{
			public MonitorArea(IntPtr handle, Rect work) { Handle = handle; Work = work; }
			public IntPtr Handle { get; }
			public Rect Work { get; }
		}

		/// <summary>All monitors with their work areas (physical pixels), primary first.</summary>
		public static List<MonitorArea> GetMonitors()
		{
			var result = new List<MonitorArea>();
			EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, hdc, rc, data) =>
			{
				var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
				if (GetMonitorInfo(hMon, ref mi))
					result.Add(new MonitorArea(hMon, mi.rcWork));
				return true;
			}, IntPtr.Zero);
			return result;
		}

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

		/// <summary>Handle of the monitor the window mostly sits on.</summary>
		public static IntPtr GetMonitor(IntPtr hwnd) => MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

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

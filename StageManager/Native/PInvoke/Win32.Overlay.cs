using System;
using System.Runtime.InteropServices;

namespace StageManager.Native.PInvoke
{
	public static partial class Win32
	{
		public const uint LWA_ALPHA = 0x2;

		public const int SM_XVIRTUALSCREEN = 76;
		public const int SM_YVIRTUALSCREEN = 77;
		public const int SM_CXVIRTUALSCREEN = 78;
		public const int SM_CYVIRTUALSCREEN = 79;

		[DllImport("user32.dll")]
		public static extern int GetSystemMetrics(int nIndex);

		[DllImport("user32.dll")]
		public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

		[DllImport("dwmapi.dll")]
		public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

		[StructLayout(LayoutKind.Sequential)]
		public struct WINDOWPLACEMENT
		{
			public int length;
			public int flags;
			public int showCmd;
			public int ptMinX, ptMinY;
			public int ptMaxX, ptMaxY;
			public int rcLeft, rcTop, rcRight, rcBottom;
		}

		[DllImport("user32.dll")]
		public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		public static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

		/// <summary>Restore the window (no activate) directly to a rect, atomically, so it
		/// never flashes at its previous position/size.</summary>
		public static void RestoreToRect(IntPtr hwnd, Rect r)
		{
			var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
			GetWindowPlacement(hwnd, ref wp);
			wp.flags = 0;
			wp.showCmd = (int)SW.SW_SHOWNOACTIVATE;
			wp.rcLeft = r.Left; wp.rcTop = r.Top; wp.rcRight = r.Right; wp.rcBottom = r.Bottom;
			SetWindowPlacement(hwnd, ref wp);
		}

		/// <summary>Suppress the window's own show/restore/move transition animations.</summary>
		public static void DisableTransitions(IntPtr hwnd)
		{
			int enabled = 1;
			DwmSetWindowAttribute(hwnd, (int)DwmWindowAttribute.DWMWA_TRANSITIONS_FORCEDISABLED, ref enabled, sizeof(int));
		}
	}
}

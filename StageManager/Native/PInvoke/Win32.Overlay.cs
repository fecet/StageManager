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

		/// <summary>Suppress the window's own show/restore/move transition animations.</summary>
		public static void DisableTransitions(IntPtr hwnd)
		{
			int enabled = 1;
			DwmSetWindowAttribute(hwnd, (int)DwmWindowAttribute.DWMWA_TRANSITIONS_FORCEDISABLED, ref enabled, sizeof(int));
		}
	}
}

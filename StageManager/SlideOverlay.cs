using StageManager.Native.Interop;
using StageManager.Native.PInvoke;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace StageManager
{
	/// <summary>
	/// A transparent, click-through, topmost window spanning the whole virtual
	/// screen. It hosts DWM thumbnails of real windows and animates their
	/// destination rectangle, so window content slides smoothly (GPU-composited)
	/// without moving the real window frame by frame.
	/// </summary>
	public class SlideOverlay : Window
	{
		private int _originX;
		private int _originY;

		public SlideOverlay()
		{
			WindowStyle = WindowStyle.None;
			AllowsTransparency = true;
			ShowInTaskbar = false;
			ShowActivated = false;
			Topmost = true;
			ResizeMode = ResizeMode.NoResize;
			Background = Brushes.Transparent;
			Width = 1;
			Height = 1;
			Left = 0;
			Top = 0;
		}

		private IntPtr Handle => new WindowInteropHelper(this).Handle;

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			var h = Handle;
			var ex = Win32.GetWindowExStyleLongPtr(h);
			Win32.SetWindowStyleExLongPtr(h, ex
				| Win32.WS_EX.WS_EX_LAYERED | Win32.WS_EX.WS_EX_TRANSPARENT
				| Win32.WS_EX.WS_EX_TOOLWINDOW | Win32.WS_EX.WS_EX_NOACTIVATE);
			Win32.SetLayeredWindowAttributes(h, 0, 255, Win32.LWA_ALPHA);

			_originX = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
			_originY = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
			var cx = Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN);
			var cy = Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN);
			Win32.SetWindowPos(h, Win32.HWND_TOPMOST, _originX, _originY, cx, cy,
				Win32.SetWindowPosFlags.DoNotActivate);
		}

		/// <summary>Slide a thumbnail of <paramref name="sourceWindow"/> from one screen
		/// rect to another (physical px), then invoke <paramref name="onDone"/>.</summary>
		public void Slide(IntPtr sourceWindow, Win32.Rect from, Win32.Rect to, TimeSpan duration, Action onDone)
		{
			// Show once and keep the overlay up: it is transparent and click-through,
			// so leaving it shown avoids the flicker of show/hide-ing a topmost
			// layered window on every switch.
			if (!IsVisible)
				Show();

			if (NativeMethods.DwmRegisterThumbnail(Handle, sourceWindow, out var thumb) != 0)
			{
				onDone?.Invoke();
				return;
			}

			var clock = Stopwatch.StartNew();
			var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(8) };
			timer.Tick += (s, e) =>
			{
				var t = Math.Min(1.0, clock.Elapsed.TotalMilliseconds / duration.TotalMilliseconds);
				var k = 1.0 - Math.Pow(1.0 - t, 3); // ease-out cubic
				Apply(thumb, Lerp(from, to, k));

				if (t >= 1.0)
				{
					timer.Stop();
					Apply(thumb, to);

					onDone?.Invoke(); // reveal the real window at the stage
					var cleanup = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(70) };
					cleanup.Tick += (cs, ce) =>
					{
						cleanup.Stop();
						NativeMethods.DwmUnregisterThumbnail(thumb);
					};
					cleanup.Start();
				}
			};
			Apply(thumb, from);
			timer.Start();
		}

		private void Apply(IntPtr thumb, Win32.Rect screenRect)
		{
			var props = new DWM_THUMBNAIL_PROPERTIES
			{
				fVisible = true,
				dwFlags = (int)(DWM_TNP.DWM_TNP_VISIBLE | DWM_TNP.DWM_TNP_OPACITY | DWM_TNP.DWM_TNP_RECTDESTINATION | DWM_TNP.DWM_TNP_SOURCECLIENTAREAONLY),
				opacity = 255,
				fSourceClientAreaOnly = true,
				rcDestination = new RECT
				{
					left = screenRect.Left - _originX,
					top = screenRect.Top - _originY,
					right = screenRect.Right - _originX,
					bottom = screenRect.Bottom - _originY
				}
			};
			NativeMethods.DwmUpdateThumbnailProperties(thumb, ref props);
		}

		private static Win32.Rect Lerp(Win32.Rect a, Win32.Rect b, double k) => new Win32.Rect
		{
			Left = (int)(a.Left + (b.Left - a.Left) * k),
			Top = (int)(a.Top + (b.Top - a.Top) * k),
			Right = (int)(a.Right + (b.Right - a.Right) * k),
			Bottom = (int)(a.Bottom + (b.Bottom - a.Bottom) * k)
		};
	}
}

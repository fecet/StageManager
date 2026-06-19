using StageManager.Native.PInvoke;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StageManager
{
	/// <summary>
	/// An opaque, topmost, click-through window that briefly shows a static bitmap
	/// over the stage to mask a window's repaint (the white restore/resize frame),
	/// then hides. Positioned in physical pixels via SetWindowPos.
	/// </summary>
	public class CoverWindow : Window
	{
		private readonly Image _image = new Image { Stretch = Stretch.Fill };

		public CoverWindow()
		{
			WindowStyle = WindowStyle.None;
			ResizeMode = ResizeMode.NoResize;
			ShowInTaskbar = false;
			ShowActivated = false;
			Topmost = true;
			Background = Brushes.Black;
			Content = _image;
			Width = 1;
			Height = 1;
			Left = 0;
			Top = 0;
		}

		private IntPtr Handle => new WindowInteropHelper(this).Handle;

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			var ex = Win32.GetWindowExStyleLongPtr(Handle);
			Win32.SetWindowStyleExLongPtr(Handle, ex
				| Win32.WS_EX.WS_EX_TOOLWINDOW | Win32.WS_EX.WS_EX_NOACTIVATE | Win32.WS_EX.WS_EX_TRANSPARENT);
		}

		/// <summary>Show <paramref name="bmp"/> stretched over the screen rect (physical px).</summary>
		public void ShowAt(System.Drawing.Bitmap bmp, Win32.Rect r)
		{
			var hb = bmp.GetHbitmap();
			try
			{
				_image.Source = Imaging.CreateBitmapSourceFromHBitmap(hb, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			}
			finally { Win32.DeleteObject(hb); }

			if (!IsVisible)
				Show();

			Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
				Win32.SetWindowPosFlags.DoNotActivate | Win32.SetWindowPosFlags.ShowWindow);
		}

		public void HideCover() => Hide();
	}
}

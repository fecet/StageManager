using StageManager.Native.PInvoke;
using System;
using System.Collections;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace StageManager
{
	/// <summary>
	/// Interaction logic for ExposeOverlay.xaml. A persistent, topmost floating widget
	/// pinned to the top-right corner of ONE monitor: a single vertical column of live
	/// DWM thumbnails (the windows on that monitor) that scrolls when there are more than
	/// fit. It raises <see cref="TileClicked"/> when a tile is clicked and never covers the
	/// desktop. Positioning uses physical pixels (the monitor work area) so it lands on the
	/// right monitor regardless of per-monitor DPI.
	/// </summary>
	public partial class ExposeOverlay : Window
	{
		private const double EdgeGap = 12;
		private Win32.Rect _work; // target monitor work area, physical px

		public ExposeOverlay()
		{
			InitializeComponent();
			SizeChanged += (_, _) => AnchorToCorner();
			Loaded += (_, _) =>
			{
				var scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
				if (scale > 0)
					MaxHeight = (_work.Bottom - _work.Top) / scale - 2 * EdgeGap;
				AnchorToCorner();
			};
		}

		private IntPtr Handle => new WindowInteropHelper(this).Handle;

		/// <summary>Raised when a tile is clicked; arg is the clicked tile's view-model.</summary>
		public event EventHandler<WindowTile> TileClicked;

		/// <summary>Set the monitor (work area in physical px) this widget lives on.</summary>
		public void SetMonitor(Win32.Rect work) => _work = work;

		public void SetTiles(IEnumerable tiles) => tilesControl.ItemsSource = tiles;

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			var h = Handle;
			var ex = Win32.GetWindowExStyleLongPtr(h);
			// Stay out of alt-tab and never steal focus. AllowsTransparency already supplies
			// WS_EX_LAYERED and drives the per-pixel alpha buffer.
			Win32.SetWindowStyleExLongPtr(h, ex
				| Win32.WS_EX.WS_EX_TOOLWINDOW | Win32.WS_EX.WS_EX_NOACTIVATE);

			// Park on the target monitor first so the window adopts that monitor's DPI before
			// it lays out; the final top-right anchor happens once it has a size.
			Win32.SetWindowPos(h, Win32.HWND_TOPMOST, _work.Left, _work.Top, 0, 0,
				Win32.SetWindowPosFlags.DoNotActivate | Win32.SetWindowPosFlags.IgnoreResize);
		}

		// Pin to the top-right of this monitor's work area in physical px, keeping the right
		// edge fixed so the strip grows downward, and re-assert topmost.
		private void AnchorToCorner()
		{
			var rect = new Win32.Rect();
			Win32.GetWindowRect(Handle, ref rect);
			var physicalWidth = rect.Right - rect.Left;
			var gap = (int)(EdgeGap * VisualTreeHelper.GetDpi(this).DpiScaleX);

			var left = _work.Right - physicalWidth - gap;
			var top = _work.Top + gap;
			Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, left, top, 0, 0,
				Win32.SetWindowPosFlags.DoNotActivate | Win32.SetWindowPosFlags.IgnoreResize);
		}

		private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement { DataContext: WindowTile tile })
				TileClicked?.Invoke(this, tile);
		}
	}
}

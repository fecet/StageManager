using StageManager.Native.PInvoke;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace StageManager
{
	/// <summary>
	/// Interaction logic for ExposeOverlay.xaml. A persistent, topmost floating widget on
	/// ONE monitor: a vertically-centered, scrollable column of live DWM thumbnails (the
	/// windows on that monitor). When the column scrolls, tiles fade out toward the top and
	/// bottom edges. It raises <see cref="TileClicked"/> when a tile is clicked, never
	/// covers the desktop, and positions in physical pixels so it lands on the right monitor
	/// regardless of per-monitor DPI.
	/// </summary>
	public partial class ExposeOverlay : Window
	{
		private const double EdgeGap = 12;
		private const byte BaseThumbOpacity = 150;
		private Win32.Rect _work; // target monitor work area, physical px

		public ExposeOverlay()
		{
			InitializeComponent();
			scroll.ScrollChanged += (_, _) => UpdateFade();
			SizeChanged += (_, _) => { AnchorToMonitor(); UpdateFade(); };
			Loaded += (_, _) =>
			{
				var scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
				if (scale > 0)
					MaxHeight = (_work.Bottom - _work.Top) / scale - 2 * EdgeGap;
				AnchorToMonitor();
				UpdateFade();
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
			// it lays out; the final anchor happens once it has a size.
			Win32.SetWindowPos(h, Win32.HWND_TOPMOST, _work.Left, _work.Top, 0, 0,
				Win32.SetWindowPosFlags.DoNotActivate | Win32.SetWindowPosFlags.IgnoreResize);
		}

		// Pin to this monitor's right edge, vertically centered, in physical px; re-assert topmost.
		private void AnchorToMonitor()
		{
			var rect = new Win32.Rect();
			Win32.GetWindowRect(Handle, ref rect);
			var physicalWidth = rect.Right - rect.Left;
			var physicalHeight = rect.Bottom - rect.Top;
			var gap = (int)(EdgeGap * VisualTreeHelper.GetDpi(this).DpiScaleX);
			var workHeight = _work.Bottom - _work.Top;

			var left = _work.Right - physicalWidth - gap;
			var top = _work.Top + Math.Max(gap, (workHeight - physicalHeight) / 2);
			Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, left, top, 0, 0,
				Win32.SetWindowPosFlags.DoNotActivate | Win32.SetWindowPosFlags.IgnoreResize);
		}

		// Fade tiles out toward the top and bottom of the viewport while the column scrolls.
		// The OpacityMask trick does not work here because the DWM thumbnails are composited
		// outside WPF, so each tile is faded explicitly: its container Opacity (the chrome)
		// and the DWM preview's ThumbnailOpacity.
		private void UpdateFade()
		{
			if (scroll is null)
				return;

			var viewport = scroll.ViewportHeight;
			if (viewport <= 0)
				return;

			var scrolling = scroll.ExtentHeight > viewport + 1;
			var band = Math.Min(viewport * 0.22, 140);

			for (int i = 0; i < tilesControl.Items.Count; i++)
			{
				if (tilesControl.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
					continue;
				if (!scroll.IsAncestorOf(container))
					continue;

				var center = container.TransformToAncestor(scroll).Transform(new Point(0, container.ActualHeight / 2)).Y;
				var factor = 1.0;
				if (scrolling && band > 0)
				{
					if (center < band)
						factor = center / band;
					else if (center > viewport - band)
						factor = (viewport - center) / band;
					factor = Math.Max(0, Math.Min(1, factor));
				}

				container.Opacity = factor;
				foreach (var thumb in FindVisualChildren<DwmThumbnail>(container))
					thumb.ThumbnailOpacity = (byte)(BaseThumbOpacity * factor);
			}
		}

		private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
		{
			var count = VisualTreeHelper.GetChildrenCount(root);
			for (int i = 0; i < count; i++)
			{
				var child = VisualTreeHelper.GetChild(root, i);
				if (child is T match)
					yield return match;
				foreach (var descendant in FindVisualChildren<T>(child))
					yield return descendant;
			}
		}

		private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement { DataContext: WindowTile tile })
				TileClicked?.Invoke(this, tile);
		}
	}
}

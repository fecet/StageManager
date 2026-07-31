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
	/// Interaction logic for ExposeOverlay.xaml. A single persistent, topmost floating widget
	/// showing every tracked window as a live DWM thumbnail, laid out by
	/// <see cref="MasonryPanel"/>. It sits on one monitor (the primary) and is pinned there in
	/// physical pixels so per-monitor DPI cannot misplace it. When the tiles scroll, they fade
	/// out toward the top and bottom edges. It raises <see cref="TileClicked"/> when a tile is
	/// clicked and never covers the desktop.
	/// </summary>
	public partial class ExposeOverlay : Window
	{
		private const double EdgeGap = 12;
		private const byte BaseThumbOpacity = 235; // keep in sync with the DwmThumbnail in XAML
		private const double ContentPadding = 8; // the panel Border around the ScrollViewer
		private const double MaxWidthFraction = 0.5; // widget may claim at most half the work area
		private Win32.Rect _work; // target monitor work area, physical px

		public ExposeOverlay()
		{
			InitializeComponent();
			scroll.ScrollChanged += (_, _) => UpdateFade();
			PreviewMouseWheel += OnMouseWheel;
			SizeChanged += (_, _) => { AnchorToMonitor(); UpdateFade(); };
			Loaded += (_, _) =>
			{
				ApplyWorkAreaLimits();
				AnchorToMonitor();
				UpdateFade();
			};
		}

		/// <summary>Height the packed tiles may reach before the masonry widens (DIP).</summary>
		public static readonly DependencyProperty ContentHeightLimitProperty = DependencyProperty.Register(
			nameof(ContentHeightLimit), typeof(double), typeof(ExposeOverlay),
			new PropertyMetadata(double.PositiveInfinity));

		public double ContentHeightLimit
		{
			get => (double)GetValue(ContentHeightLimitProperty);
			set => SetValue(ContentHeightLimitProperty, value);
		}

		/// <summary>Width the masonry may grow to (DIP).</summary>
		public static readonly DependencyProperty ContentWidthLimitProperty = DependencyProperty.Register(
			nameof(ContentWidthLimit), typeof(double), typeof(ExposeOverlay),
			new PropertyMetadata(double.PositiveInfinity));

		public double ContentWidthLimit
		{
			get => (double)GetValue(ContentWidthLimitProperty);
			set => SetValue(ContentWidthLimitProperty, value);
		}

		/// <summary>Shape the packed tiles aim for: this monitor's own aspect ratio.</summary>
		public static readonly DependencyProperty ContentAspectProperty = DependencyProperty.Register(
			nameof(ContentAspect), typeof(double), typeof(ExposeOverlay),
			new PropertyMetadata(1.0));

		public double ContentAspect
		{
			get => (double)GetValue(ContentAspectProperty);
			set => SetValue(ContentAspectProperty, value);
		}

		// Translate the monitor work area into the DIP budget the masonry may use. Done at
		// Loaded, not OnSourceInitialized: the per-monitor DPI context is not settled that
		// early, so WorkArea would read as raw pixels and the caps would come out far too big.
		private void ApplyWorkAreaLimits()
		{
			var dpi = VisualTreeHelper.GetDpi(this);
			if (dpi.DpiScaleX <= 0 || dpi.DpiScaleY <= 0)
				return;

			var workWidth = (_work.Right - _work.Left) / dpi.DpiScaleX;
			var workHeight = (_work.Bottom - _work.Top) / dpi.DpiScaleY;

			MaxHeight = workHeight - 2 * EdgeGap;
			ContentHeightLimit = MaxHeight - 2 * ContentPadding;
			ContentWidthLimit = workWidth * MaxWidthFraction - 2 * ContentPadding;
			ContentAspect = workWidth / workHeight;
		}

		private IntPtr Handle => new WindowInteropHelper(this).Handle;

		/// <summary>Raised when a tile is clicked; arg is the clicked tile's view-model.</summary>
		public event EventHandler<WindowTile> TileClicked;

		/// <summary>Raised on wheel notches over the widget; arg is signed notches, up positive.</summary>
		public event EventHandler<int> ScaleStepped;

		// The wheel zooms rather than scrolls. Scrolling a switcher is the wrong gesture anyway:
		// the point is to see every window at once, so shrinking until they fit beats paging
		// through them. Handled so the ScrollViewer underneath does not also act on the notch.
		private void OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			ScaleStepped?.Invoke(this, e.Delta / Mouse.MouseWheelDeltaForOneLine);
			e.Handled = true;
		}

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

		// Pin to this monitor's right edge, vertically centered, in physical px; re-assert
		// topmost. Size comes from the WPF layout (ActualWidth/Height x DPI), not
		// GetWindowRect, which can still report the pre-resize rect inside SizeChanged and
		// would mis-center the strip.
		private void AnchorToMonitor()
		{
			var dpi = VisualTreeHelper.GetDpi(this);
			var physicalWidth = (int)(ActualWidth * dpi.DpiScaleX);
			var physicalHeight = (int)(ActualHeight * dpi.DpiScaleY);
			if (physicalWidth <= 0 || physicalHeight <= 0)
				return;

			var gap = (int)(EdgeGap * dpi.DpiScaleX);
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

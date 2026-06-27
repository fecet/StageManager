using StageManager.Native.Interop;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StageManager
{
	/// <summary>
	/// Interaction logic for DwmThumbnail.xaml
	/// </summary>
	public partial class DwmThumbnail : UserControl
	{
		public DwmThumbnail()
		{
			InitializeComponent();
			LayoutUpdated += DwmThumbnail_LayoutUpdated;
		}

		private IntPtr _dwmThumbnail;
		private Window _window;
		private Point? _dpiScaleFactor;

		public static readonly DependencyProperty PreviewHandleProperty = DependencyProperty.Register(nameof(PreviewHandle),
			   typeof(IntPtr),
			   typeof(DwmThumbnail),
			   new PropertyMetadata(IntPtr.Zero));

		public IntPtr PreviewHandle
		{
			get { return (IntPtr)GetValue(PreviewHandleProperty); }
			set { SetValue(PreviewHandleProperty, value); }
		}

		// 0..255; passed straight to DWM_THUMBNAIL_PROPERTIES.opacity so the live preview
		// can be made translucent (the desktop behind it shows through).
		public static readonly DependencyProperty ThumbnailOpacityProperty = DependencyProperty.Register(nameof(ThumbnailOpacity),
			   typeof(byte),
			   typeof(DwmThumbnail),
			   new PropertyMetadata((byte)255));

		public byte ThumbnailOpacity
		{
			get { return (byte)GetValue(ThumbnailOpacityProperty); }
			set { SetValue(ThumbnailOpacityProperty, value); }
		}

		private Point GetDpiScaleFactor()
		{
			if (_dpiScaleFactor is null)
			{
				var source = PresentationSource.FromVisual(this);
				_dpiScaleFactor = source?.CompositionTarget != null ? new Point(source.CompositionTarget.TransformToDevice.M11, source.CompositionTarget.TransformToDevice.M22) : new Point(1.0d, 1.0d);
			}

			return _dpiScaleFactor.Value;
		}

		protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
		{
			_dpiScaleFactor = null;
			base.OnDpiChanged(oldDpi, newDpi);
		}

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);

			if (nameof(PreviewHandle).Equals(e.Property.Name))
			{
				// Drop any thumbnail bound to the previous handle; UpdateThumbnailProperties
				// re-registers against the new handle once the control is connected.
				if (_dwmThumbnail != IntPtr.Zero)
				{
					NativeMethods.DwmUnregisterThumbnail(_dwmThumbnail);
					_dwmThumbnail = IntPtr.Zero;
				}

				UpdateThumbnailProperties();
			}

			if (nameof(ThumbnailOpacity).Equals(e.Property.Name))
				UpdateThumbnailProperties();

			if (nameof(IsVisible).Equals(e.Property.Name) && !(bool)e.NewValue && _dwmThumbnail != IntPtr.Zero)
			{
				NativeMethods.DwmUnregisterThumbnail(_dwmThumbnail);
				_dwmThumbnail = IntPtr.Zero;
			}
		}

		private void DwmThumbnail_LayoutUpdated(object? sender, EventArgs e)
		{
			UpdateThumbnailProperties();
		}

		public static Rect BoundsRelativeTo(FrameworkElement element, Visual relativeTo)
		{
			// Use the element's own render bounds, not its layout slot (which is in the
			// PARENT's coordinates): nested inside the tile template, the slot carries the
			// parent's offset and double-counts it through TransformToVisual, drifting the
			// thumbnail off the clickable tile.
			return element.TransformToVisual(relativeTo)
						  .TransformBounds(new Rect(element.RenderSize));
		}

		// Register only when the control is hosted in a window with a real HWND. Inside
		// an ItemsControl that is not guaranteed at the moment PreviewHandle is first set.
		private bool TryRegister()
		{
			if (_dwmThumbnail != IntPtr.Zero)
				return true;

			var window = FindWindow();
			if (window is null)
				return false;

			var windowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
			if (windowHandle == IntPtr.Zero)
				return false;

			return NativeMethods.DwmRegisterThumbnail(windowHandle, PreviewHandle, out _dwmThumbnail) == 0;
		}

		private Window FindWindow() => _window ??= Window.GetWindow(this);

		private void UpdateThumbnailProperties()
		{
			var window = FindWindow();

			// This control is reused inside an ItemsControl, so LayoutUpdated can fire
			// while it is detached from (or not yet attached to) the owning window's
			// visual tree. TransformToVisual across separate trees throws, so place the
			// thumbnail only once the control is connected under its window.
			if (window is null || PresentationSource.FromVisual(this) is null || !window.IsAncestorOf(this))
				return;

			if (PreviewHandle == IntPtr.Zero || !TryRegister())
				return;

			var dpi = GetDpiScaleFactor();

			var previewBounds = BoundsRelativeTo(this, window);

			var thumbnailRect = new RECT
			{
				top = (int)(previewBounds.Top * dpi.Y),
				left = (int)(previewBounds.Left * dpi.X),
				bottom = (int)((previewBounds.Bottom - Margin.Top - Margin.Bottom) * dpi.Y) + 1,
				right = (int)((previewBounds.Right - Margin.Left - Margin.Right) * dpi.X) + 1
			};

			var props = new DWM_THUMBNAIL_PROPERTIES
			{
				fVisible = true,
				dwFlags = (int)(DWM_TNP.DWM_TNP_VISIBLE | DWM_TNP.DWM_TNP_OPACITY | DWM_TNP.DWM_TNP_RECTDESTINATION | DWM_TNP.DWM_TNP_SOURCECLIENTAREAONLY),
				opacity = ThumbnailOpacity,
				rcDestination = thumbnailRect,
				fSourceClientAreaOnly = true
			};
			NativeMethods.DwmUpdateThumbnailProperties(_dwmThumbnail, ref props);
		}
	}
}

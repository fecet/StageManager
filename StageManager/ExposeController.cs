using StageManager.Native;
using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StageManager
{
	/// <summary>
	/// Bridges <see cref="WindowsManager"/> to one floating exposé widget per monitor: each
	/// widget shows only the windows currently on its monitor. Windows are routed by
	/// <see cref="Win32.GetMonitor"/>, and re-routed to another widget when dragged to
	/// another monitor. It never repositions or resizes real windows. All collection and
	/// IsFocused mutations are marshalled onto the UI thread because WindowsManager events
	/// can arrive off-thread.
	/// </summary>
	public class ExposeController
	{
		private sealed class MonitorView
		{
			public IntPtr Monitor;
			public ObservableCollection<WindowTile> Tiles { get; } = new ObservableCollection<WindowTile>();
		}

		private const double TileWidth = 180; // keep in sync with the DwmThumbnail width in XAML

		private readonly WindowsManager _windowsManager;
		private readonly List<MonitorView> _views = new List<MonitorView>();
		private readonly Dictionary<IntPtr, IntPtr> _windowMonitor = new Dictionary<IntPtr, IntPtr>();

		public ExposeController(WindowsManager windowsManager)
		{
			_windowsManager = windowsManager;
		}

		// Register a monitor's widget; the widget renders that monitor's tile collection.
		public void AddMonitor(IntPtr monitor, ExposeOverlay overlay)
		{
			var view = new MonitorView { Monitor = monitor };
			overlay.SetTiles(view.Tiles);
			overlay.TileClicked += OnTileClicked;
			_views.Add(view);
		}

		public void Start()
		{
			foreach (var window in _windowsManager.Windows.ToList())
				AddTile(window);

			_windowsManager.WindowCreated += OnWindowCreated;
			_windowsManager.WindowDestroyed += OnWindowDestroyed;
			_windowsManager.WindowUpdated += OnWindowUpdated;
			_windowsManager.UntrackedFocus += OnUntrackedFocus;
		}

		private void OnWindowCreated(IWindow window, bool firstCreate) => Dispatch(() => AddTile(window));

		private void OnWindowDestroyed(IWindow window) => Dispatch(() => RemoveTile(window.Handle));

		private void OnWindowUpdated(IWindow window, WindowUpdateType type)
		{
			if (type == WindowUpdateType.Foreground)
				Dispatch(() => SetFocused(window.Handle));
			else if (type == WindowUpdateType.MoveEnd || type == WindowUpdateType.Move)
				Dispatch(() => { Reroute(window); UpdateAspect(window); });
		}

		private void OnUntrackedFocus(object sender, IntPtr handle) => Dispatch(() => SetFocused(IntPtr.Zero));

		private void OnTileClicked(object sender, WindowTile tile) => FocusTile(tile);

		// The widget for the monitor the window mostly sits on (fall back to the first).
		private MonitorView ViewFor(IntPtr handle)
		{
			var monitor = Win32.GetMonitor(handle);
			return _views.FirstOrDefault(v => v.Monitor == monitor) ?? _views.FirstOrDefault();
		}

		private void AddTile(IWindow window)
		{
			var view = ViewFor(window.Handle);
			if (view is null || view.Tiles.Any(t => t.Handle == window.Handle))
				return;

			view.Tiles.Add(new WindowTile(window.Handle)
			{
				Title = window.Title,
				Icon = ExtractIcon(window),
				IsFocused = window.IsFocused,
				ThumbHeight = ThumbHeightFor(window)
			});
			_windowMonitor[window.Handle] = view.Monitor;
		}

		// Thumbnail height for the fixed column width, from the window's real aspect ratio.
		// A minimized/unknown window has no usable rect, so fall back to 16:9.
		private static double ThumbHeightFor(IWindow window)
		{
			var location = window.Location;
			if (location is null || location.Width <= 0 || location.Height <= 0)
				return TileWidth * 9.0 / 16.0;

			return Math.Clamp(TileWidth * location.Height / location.Width, 60, 320);
		}

		private void UpdateAspect(IWindow window)
		{
			foreach (var view in _views)
				if (view.Tiles.FirstOrDefault(t => t.Handle == window.Handle) is WindowTile tile)
					tile.ThumbHeight = ThumbHeightFor(window);
		}

		private void RemoveTile(IntPtr handle)
		{
			foreach (var view in _views)
			{
				if (view.Tiles.FirstOrDefault(t => t.Handle == handle) is WindowTile tile)
					view.Tiles.Remove(tile);
			}
			_windowMonitor.Remove(handle);
		}

		// Move a window's tile to the widget of its current monitor when it changed monitors.
		private void Reroute(IWindow window)
		{
			var monitor = Win32.GetMonitor(window.Handle);
			if (_windowMonitor.TryGetValue(window.Handle, out var previous) && previous == monitor)
				return;

			RemoveTile(window.Handle);
			AddTile(window);
		}

		// Raise the clicked window to the front without changing its size or position.
		private void FocusTile(WindowTile tile)
		{
			var window = _windowsManager.Windows.FirstOrDefault(w => w.Handle == tile.Handle);
			if (window is null)
				return;

			if (window.IsMinimized)
				window.ShowNormal();

			FocusStealer.Steal(window.Handle);
			window.BringToTop();
		}

		private void SetFocused(IntPtr handle)
		{
			foreach (var view in _views)
				foreach (var tile in view.Tiles)
					tile.IsFocused = tile.Handle == handle;
		}

		private static ImageSource ExtractIcon(IWindow window)
		{
			if (window is not WindowsWindow concrete)
				return null;

			using var icon = concrete.ExtractIcon();
			if (icon is null)
				return null;

			var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
				BitmapSizeOptions.FromEmptyOptions());
			source.Freeze();
			return source;
		}

		private static void Dispatch(Action action) => Application.Current.Dispatcher.Invoke(action);
	}
}

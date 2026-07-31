using StageManager.Native;
using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StageManager
{
	/// <summary>
	/// Bridges <see cref="WindowsManager"/> to the exposé widget: one tile per tracked window,
	/// regardless of which monitor that window sits on, so a single widget covers the whole
	/// desktop. It never repositions or resizes real windows. All collection and IsFocused
	/// mutations are marshalled onto the UI thread because WindowsManager events can arrive
	/// off-thread.
	/// </summary>
	public class ExposeController
	{
		// The widest a tile gets: the masonry column width in XAML minus the tile's 2px border
		// on each side. A tile narrower than this is one whose window fills less of its monitor.
		private const double MaxTileWidth = 180;

		// The narrowest a tile gets, as a fraction of MaxTileWidth. A tile has to stay legible
		// however small its window is, so the scale is remapped into [MinTileScale, 1] rather
		// than clamped at the floor, which would flatten every small window onto one size.
		private const double MinTileScale = 0.5;

		private readonly WindowsManager _windowsManager;
		private readonly ObservableCollection<WindowTile> _tiles = new ObservableCollection<WindowTile>();

		public ExposeController(WindowsManager windowsManager)
		{
			_windowsManager = windowsManager;
		}

		/// <summary>Attach the widget that renders the tiles.</summary>
		public void SetOverlay(ExposeOverlay overlay)
		{
			overlay.SetTiles(_tiles);
			overlay.TileClicked += OnTileClicked;
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

		// Resize on every update, not just on drag: MoveStart/MoveEnd only fire for mouse-driven
		// move/resize, so maximize, snap, restore and app-driven resizes would otherwise leave
		// the tile stuck at the size the window had when its tile was created.
		private void OnWindowUpdated(IWindow window, WindowUpdateType type)
		{
			Dispatch(() =>
			{
				ResizeTile(window);
				if (type == WindowUpdateType.Foreground)
					SetFocused(window.Handle);
			});
		}

		private void OnUntrackedFocus(object sender, IntPtr handle) => Dispatch(() => SetFocused(IntPtr.Zero));

		private void OnTileClicked(object sender, WindowTile tile) => FocusTile(tile);

		private void AddTile(IWindow window)
		{
			if (_tiles.Any(t => t.Handle == window.Handle))
				return;

			var tile = new WindowTile(window.Handle)
			{
				Title = window.Title,
				Icon = ExtractIcon(window),
				IsFocused = window.IsFocused
			};
			Resize(tile, window.Handle);
			_tiles.Add(tile);
		}

		// A tile carries two independent facts about its window: the aspect ratio as its shape,
		// and how much of its own monitor the window fills as its width. Measuring the fill
		// against the monitor rather than in raw pixels is what makes the two boxes comparable -
		// a maximized window reads the same whether it sits on the GPD's 1080p panel or on a 4K
		// screen, and a small tool window stays small on either.
		//
		// The ratio is deliberately unclamped: DWM letterboxes the thumbnail at the source's own
		// ratio, so any other height shows up as an empty band. Extreme ratios are absorbed by
		// the masonry packing instead.
		private static void Resize(WindowTile tile, IntPtr handle)
		{
			var source = Win32Helper.PreviewSourceSize(handle);
			if (source.Width <= 0 || source.Height <= 0)
			{
				// No usable geometry at all (a minimized window whose placement is empty too).
				tile.ThumbWidth = MaxTileWidth;
				tile.ThumbHeight = MaxTileWidth * 9.0 / 16.0;
				return;
			}

			var work = Win32.GetWorkArea(handle);
			var workArea = (double)(work.Right - work.Left) * (work.Bottom - work.Top);
			var fill = workArea > 0
				? Math.Min(1, (double)source.Width * source.Height / workArea)
				: 1;

			// Square-root turns the area fraction back into a linear one, so a window covering a
			// quarter of its screen gets half the width rather than a quarter.
			var scale = MinTileScale + (1 - MinTileScale) * Math.Sqrt(fill);

			tile.ThumbWidth = MaxTileWidth * scale;
			tile.ThumbHeight = tile.ThumbWidth * source.Height / source.Width;
		}

		private void ResizeTile(IWindow window)
		{
			if (_tiles.FirstOrDefault(t => t.Handle == window.Handle) is WindowTile tile)
				Resize(tile, window.Handle);
		}

		private void RemoveTile(IntPtr handle)
		{
			if (_tiles.FirstOrDefault(t => t.Handle == handle) is WindowTile tile)
				_tiles.Remove(tile);
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
			foreach (var tile in _tiles)
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

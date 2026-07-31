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
		// The tile grid: a square unit, and a tile spanning a whole number of units in each
		// direction. Keep in sync with the MasonryPanel in XAML.
		private const double Unit = 88;
		private const double UnitGap = 8;
		private const double TileBorder = 2; // the tile Border's BorderThickness, per side

		// A window filling at least this much of its monitor earns the two-unit-wide tile.
		// Measured as an area fraction, so the threshold is half the screen in each dimension.
		private const double WideTileFill = 0.25;

		// Tallest a tile may get. A 2x4 tile from some 1:2 window would tower over the grid and
		// leave a column of holes beside it; three units is as far as the packing absorbs well.
		private const int MaxSpanHeight = 3;

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

		// A tile is a whole number of grid units in both directions, so the tiles pave a
		// rectangle instead of ragging out like a masonry column. How much of its own monitor
		// the window fills picks the width - measured against the monitor rather than in raw
		// pixels, so a maximized window reads the same on the GPD's 1080p panel as on a 4K
		// screen - and its aspect ratio picks the height, rounded to the nearest unit.
		//
		// Rounding the height means the tile no longer matches the window's ratio, so the
		// thumbnail is center-cropped to fill it (see DwmThumbnail). Letting the tile keep the
		// exact ratio instead is what leaves the grid ragged.
		private static void Resize(WindowTile tile, IntPtr handle)
		{
			tile.IsMinimized = Win32.IsIconic(handle);
			if (tile.IsMinimized)
			{
				// A minimized window has no live thumbnail, so it keeps a square one-unit tile
				// carrying nothing but its icon.
				tile.ThumbWidth = Extent(1);
				tile.ThumbHeight = Extent(1);
				return;
			}

			var source = Win32Helper.PreviewSourceSize(handle);
			if (source.Width <= 0 || source.Height <= 0)
				return; // nothing to derive a shape from; leave the tile at the size it has

			var work = Win32.GetWorkArea(handle);
			var workArea = (double)(work.Right - work.Left) * (work.Bottom - work.Top);
			var fill = workArea > 0 ? (double)source.Width * source.Height / workArea : 1;

			var spanWidth = fill >= WideTileFill ? 2 : 1;
			var ratio = (double)source.Height / source.Width;
			var spanHeight = Math.Clamp((int)Math.Round(spanWidth * ratio), 1, MaxSpanHeight);

			tile.ThumbWidth = Extent(spanWidth);
			tile.ThumbHeight = Extent(spanHeight);
		}

		// Content size of a tile spanning this many grid units, with the tile's own border
		// taken out - what the thumbnail itself gets.
		private static double Extent(int span) =>
			span * Unit + (span - 1) * UnitGap - 2 * TileBorder;

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

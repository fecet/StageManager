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
		// The masonry grid. A tile spans one unit or two, never a fraction, so tiles line up on
		// the grid the way Windows tiles do. Keep in sync with the MasonryPanel in XAML.
		private const double UnitWidth = 88;
		private const double UnitGap = 8;
		private const double TileBorder = 2; // the tile Border's BorderThickness, per side

		// A window filling at least this much of its monitor earns the two-unit tile. Measured
		// as an area fraction, so the threshold is half the screen in each dimension.
		private const double WideTileFill = 0.25;

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

		// A tile carries two facts about its window: how much of its own monitor the window
		// fills picks the tile's span, and the aspect ratio gives its height. Measuring the fill
		// against the monitor rather than in raw pixels is what makes the two boxes comparable -
		// a maximized window reads the same whether it sits on the GPD's 1080p panel or on a 4K
		// screen, and a small tool window stays small on either.
		//
		// The height is deliberately unquantized: DWM letterboxes the thumbnail at the source's
		// own ratio, so any other height shows up as an empty band. Only the width snaps to the
		// grid; the height differences are absorbed by the masonry packing.
		private static void Resize(WindowTile tile, IntPtr handle)
		{
			tile.IsMinimized = Win32.IsIconic(handle);
			if (tile.IsMinimized)
			{
				// A minimized window has no live thumbnail, so it keeps a square one-unit tile
				// carrying nothing but its icon.
				tile.ThumbWidth = TileWidth(1);
				tile.ThumbHeight = TileWidth(1);
				return;
			}

			var source = Win32Helper.PreviewSourceSize(handle);
			if (source.Width <= 0 || source.Height <= 0)
				return; // nothing to derive a shape from; leave the tile at the size it has

			var work = Win32.GetWorkArea(handle);
			var workArea = (double)(work.Right - work.Left) * (work.Bottom - work.Top);
			var fill = workArea > 0 ? (double)source.Width * source.Height / workArea : 1;

			var width = TileWidth(fill >= WideTileFill ? 2 : 1);
			tile.ThumbWidth = width;
			tile.ThumbHeight = width * source.Height / source.Width;
		}

		// Content width of a tile spanning this many grid units, with the tile's own border
		// taken out - what the thumbnail itself gets.
		private static double TileWidth(int span) =>
			span * UnitWidth + (span - 1) * UnitGap - 2 * TileBorder;

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

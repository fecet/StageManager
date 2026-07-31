using System;
using System.ComponentModel;
using System.Windows.Media;

namespace StageManager
{
	/// <summary>
	/// View-model for a single window tile in the exposé grid: the target window
	/// handle to mirror, its title, app icon, and whether it is currently focused.
	/// </summary>
	public class WindowTile : INotifyPropertyChanged
	{
		private string _title;
		private ImageSource _icon;
		private bool _isFocused;
		private bool _isMinimized;
		private double _thumbWidth = 84;
		private double _thumbHeight = 84;

		public WindowTile(IntPtr handle)
		{
			Handle = handle;
		}

		public IntPtr Handle { get; }

		public string Title
		{
			get => _title;
			set
			{
				if (_title == value)
					return;

				_title = value;
				OnPropertyChanged(nameof(Title));
			}
		}

		public ImageSource Icon
		{
			get => _icon;
			set
			{
				if (_icon == value)
					return;

				_icon = value;
				OnPropertyChanged(nameof(Icon));
			}
		}

		// A minimized window has no live thumbnail to mirror, so its tile shows the app icon
		// on a square instead of a DWM preview.
		public bool IsMinimized
		{
			get => _isMinimized;
			set
			{
				if (_isMinimized == value)
					return;

				_isMinimized = value;
				OnPropertyChanged(nameof(IsMinimized));
			}
		}

		// Thumbnail width, set from how much of its monitor the window fills: a two-unit tile
		// belongs to a window covering its screen, a one-unit tile to a small window.
		public double ThumbWidth
		{
			get => _thumbWidth;
			set
			{
				if (_thumbWidth == value)
					return;

				_thumbWidth = value;
				OnPropertyChanged(nameof(ThumbWidth));
				OnPropertyChanged(nameof(LabelMaxWidth));
			}
		}

		// Thumbnail height, set from the window's real aspect ratio against ThumbWidth.
		public double ThumbHeight
		{
			get => _thumbHeight;
			set
			{
				if (_thumbHeight == value)
					return;

				_thumbHeight = value;
				OnPropertyChanged(nameof(ThumbHeight));
			}
		}

		// The title chip tracks the tile it sits on instead of a fixed width, leaving the
		// rounded corners clear on a tile of any size.
		public double LabelMaxWidth => Math.Max(0, _thumbWidth - 8);

		public bool IsFocused
		{
			get => _isFocused;
			set
			{
				if (_isFocused == value)
					return;

				_isFocused = value;
				OnPropertyChanged(nameof(IsFocused));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}

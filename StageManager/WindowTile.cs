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
		private double _thumbHeight = 108;

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

		// Thumbnail height for a fixed column width, set from the window's real aspect ratio.
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

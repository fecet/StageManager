using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace StageManager
{
	/// <summary>
	/// A per-monitor strip view (the orchestration lives in MainWindow). It only
	/// renders the scenes routed to its monitor and forwards clicks to the shared
	/// SwitchSceneCommand.
	/// </summary>
	public partial class StageWindow : Window
	{
		public StageWindow(ICollectionView scenes, ICommand switchSceneCommand)
		{
			Scenes = scenes;
			SwitchSceneCommand = switchSceneCommand;
			InitializeComponent();
		}

		public ICollectionView Scenes { get; }

		public ICommand SwitchSceneCommand { get; }

		public IntPtr Handle => new System.Windows.Interop.WindowInteropHelper(this).Handle;
	}
}

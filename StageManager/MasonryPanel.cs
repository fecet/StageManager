using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace StageManager
{
	/// <summary>
	/// Masonry layout for the exposé tiles: fixed column width, each tile keeping its own
	/// natural height, packed into the shortest column. Column count grows only as needed —
	/// the panel packs into one column first and adds another whenever the tallest column
	/// would exceed <see cref="MaxColumnHeight"/>, up to <see cref="MaxColumns"/>.
	///
	/// A uniform tile height is what forced the old clamp, and a clamped tile no longer
	/// matches the aspect ratio DWM letterboxes its thumbnail into, which is where the
	/// empty bands came from. Here the tile height is the window's true aspect ratio and
	/// the height differences are absorbed by the packing instead.
	/// </summary>
	public class MasonryPanel : Panel
	{
		private int _columns = 1;
		private readonly List<Point> _origins = new List<Point>();

		public static readonly DependencyProperty ColumnWidthProperty = DependencyProperty.Register(
			nameof(ColumnWidth), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(180d, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double ColumnWidth
		{
			get => (double)GetValue(ColumnWidthProperty);
			set => SetValue(ColumnWidthProperty, value);
		}

		public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
			nameof(Gap), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double Gap
		{
			get => (double)GetValue(GapProperty);
			set => SetValue(GapProperty, value);
		}

		// The height a single column may reach before another column is opened. The panel
		// lives in a ScrollViewer, which measures with infinite height, so the usable
		// height cannot be read from availableSize and is supplied by the owning window.
		public static readonly DependencyProperty MaxColumnHeightProperty = DependencyProperty.Register(
			nameof(MaxColumnHeight), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double MaxColumnHeight
		{
			get => (double)GetValue(MaxColumnHeightProperty);
			set => SetValue(MaxColumnHeightProperty, value);
		}

		public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
			nameof(MaxColumns), typeof(int), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public int MaxColumns
		{
			get => (int)GetValue(MaxColumnsProperty);
			set => SetValue(MaxColumnsProperty, value);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			foreach (UIElement child in InternalChildren)
				child.Measure(new Size(ColumnWidth, double.PositiveInfinity));

			var maxColumns = Math.Max(1, MaxColumns);
			_columns = 1;
			while (_columns < maxColumns && Pack(_columns) > MaxColumnHeight)
				_columns++;

			var height = Pack(_columns);
			return new Size(_columns * ColumnWidth + (_columns - 1) * Gap, height);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			for (int i = 0; i < InternalChildren.Count; i++)
			{
				var child = InternalChildren[i];
				child.Arrange(new Rect(_origins[i], new Size(ColumnWidth, child.DesiredSize.Height)));
			}
			return finalSize;
		}

		// Place every child into the shortest column, recording its origin, and return the
		// height of the tallest column.
		private double Pack(int columns)
		{
			var heights = new double[columns];
			_origins.Clear();

			foreach (UIElement child in InternalChildren)
			{
				var column = ShortestColumn(heights);
				_origins.Add(new Point(column * (ColumnWidth + Gap), heights[column]));
				heights[column] += child.DesiredSize.Height + Gap;
			}

			var tallest = 0d;
			foreach (var height in heights)
				tallest = Math.Max(tallest, height);

			// The trailing gap belongs between tiles, not below the last one.
			return Math.Max(0, tallest - Gap);
		}

		private static int ShortestColumn(double[] heights)
		{
			var shortest = 0;
			for (int i = 1; i < heights.Length; i++)
				if (heights[i] < heights[shortest])
					shortest = i;
			return shortest;
		}
	}
}

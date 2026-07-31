using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace StageManager
{
	/// <summary>
	/// Masonry layout on a fixed grid of <see cref="UnitWidth"/> columns. A tile spans a whole
	/// number of units — like a Windows tile, so the grid's edges stay aligned — and is packed
	/// into the lowest run of adjacent columns wide enough to hold it. Column count grows only
	/// as needed: the panel starts at the widest tile's span and opens another column whenever
	/// the tallest one would exceed <see cref="MaxColumnHeight"/>, up to <see cref="MaxColumns"/>.
	///
	/// Only the width snaps to the grid. A tile's height is its window's true aspect ratio,
	/// because DWM letterboxes the thumbnail at that ratio and any other height shows up as an
	/// empty band, so heights stay ragged and the packing absorbs the difference.
	/// </summary>
	public class MasonryPanel : Panel
	{
		private int _columns = 1;
		private readonly List<Point> _origins = new List<Point>();

		/// <summary>Width of one grid unit. A tile spans a whole number of these.</summary>
		public static readonly DependencyProperty UnitWidthProperty = DependencyProperty.Register(
			nameof(UnitWidth), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(88d, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double UnitWidth
		{
			get => (double)GetValue(UnitWidthProperty);
			set => SetValue(UnitWidthProperty, value);
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

		/// <summary>Most grid units the panel may open, not most tiles per row.</summary>
		public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
			nameof(MaxColumns), typeof(int), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(6, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public int MaxColumns
		{
			get => (int)GetValue(MaxColumnsProperty);
			set => SetValue(MaxColumnsProperty, value);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var maxColumns = Math.Max(1, MaxColumns);
			var widest = 0;

			foreach (UIElement child in InternalChildren)
			{
				child.Measure(new Size(Extent(maxColumns), double.PositiveInfinity));
				widest = Math.Max(widest, SpanOf(child));
			}

			// Start wide enough for the widest tile, then open columns until the tallest one fits.
			_columns = Math.Min(maxColumns, Math.Max(1, widest));
			while (_columns < maxColumns && Pack(_columns) > MaxColumnHeight)
				_columns++;

			var height = Pack(_columns);
			return new Size(Extent(_columns), height);
		}

		/// <summary>Width covered by this many units, gaps between them included.</summary>
		private double Extent(int units) => units * UnitWidth + (units - 1) * Gap;

		/// <summary>How many grid units a tile covers, from the width it asked for.</summary>
		private int SpanOf(UIElement child) =>
			Math.Max(1, (int)Math.Round((child.DesiredSize.Width + Gap) / (UnitWidth + Gap)));

		protected override Size ArrangeOverride(Size finalSize)
		{
			for (int i = 0; i < InternalChildren.Count; i++)
			{
				var child = InternalChildren[i];
				child.Arrange(new Rect(_origins[i], child.DesiredSize));
			}
			return finalSize;
		}

		// Place every child at the lowest run of columns its span fits into, recording its
		// origin, and return the height of the tallest column. A two-unit tile straddles two
		// columns and pushes both down, which is what keeps the grid's edges aligned.
		private double Pack(int columns)
		{
			var heights = new double[columns];
			_origins.Clear();

			foreach (UIElement child in InternalChildren)
			{
				var span = Math.Min(columns, SpanOf(child));
				var start = LowestRun(heights, span);
				var top = RunTop(heights, start, span);

				_origins.Add(new Point(start * (UnitWidth + Gap), top));

				var bottom = top + child.DesiredSize.Height + Gap;
				for (int c = start; c < start + span; c++)
					heights[c] = bottom;
			}

			var tallest = 0d;
			foreach (var height in heights)
				tallest = Math.Max(tallest, height);

			// The trailing gap belongs between tiles, not below the last one.
			return Math.Max(0, tallest - Gap);
		}

		// First column of the run of `span` adjacent columns whose top is lowest. A tile cannot
		// overlap the one above it in any column it straddles, so the run's top is the highest
		// of them.
		private static int LowestRun(double[] heights, int span)
		{
			var best = 0;
			var lowest = double.MaxValue;

			for (int start = 0; start + span <= heights.Length; start++)
			{
				var top = RunTop(heights, start, span);
				if (top < lowest)
				{
					lowest = top;
					best = start;
				}
			}

			return best;
		}

		private static double RunTop(double[] heights, int start, int span)
		{
			var top = 0d;
			for (int c = start; c < start + span; c++)
				top = Math.Max(top, heights[c]);
			return top;
		}
	}
}

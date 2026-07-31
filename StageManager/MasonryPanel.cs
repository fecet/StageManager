using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace StageManager
{
	/// <summary>
	/// Masonry layout over a grid of <see cref="UnitWidth"/> columns. Tiles are sized freely by
	/// their owner; the grid only bounds the packing, so each tile occupies as many adjacent
	/// columns as its width needs and is centred in them. It goes into the lowest run of columns
	/// that can hold it, which is what keeps a wide tile from being laid over a neighbour.
	///
	/// Column count grows only as needed: the panel starts at the widest tile's span and opens
	/// another column whenever the tallest one would exceed <see cref="MaxColumnHeight"/>, up to
	/// <see cref="MaxColumns"/>. Tile heights are their windows' true aspect ratios, so they stay
	/// ragged and the packing absorbs the difference.
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

		/// <summary>
		/// How many grid units a tile occupies: the fewest whose extent covers the width it
		/// asked for. Tile widths are continuous, so this rounds up rather than to nearest —
		/// a tile must never be laid over a column its neighbour also holds.
		/// </summary>
		private int SpanOf(UIElement child) =>
			Math.Max(1, (int)Math.Ceiling((child.DesiredSize.Width + Gap) / (UnitWidth + Gap) - 1e-6));

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
		// origin, and return the height of the tallest column. A multi-unit tile straddles
		// that many columns and pushes all of them down, and a tile narrower than the run it
		// occupies is centred in it.
		private double Pack(int columns)
		{
			var heights = new double[columns];
			_origins.Clear();

			foreach (UIElement child in InternalChildren)
			{
				var span = Math.Min(columns, SpanOf(child));
				var start = LowestRun(heights, span);
				var top = RunTop(heights, start, span);
				var inset = Math.Max(0, Extent(span) - child.DesiredSize.Width) / 2;

				_origins.Add(new Point(start * (UnitWidth + Gap) + inset, top));

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

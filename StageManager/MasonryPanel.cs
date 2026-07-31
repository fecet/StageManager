using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace StageManager
{
	/// <summary>
	/// Packs freely-sized tiles into the smallest rectangle they fit, each tile landing at the
	/// lowest free spot that can hold it. Nothing is snapped to a column: a tile occupies exactly
	/// its own width, so a narrow one does not reserve a column's worth of space beside it and
	/// the only gaps left are the deliberate <see cref="Gap"/> between tiles.
	///
	/// The width grows only as needed. The panel starts at its widest tile and widens by
	/// <see cref="WidthStep"/> until the packed height fits <see cref="HeightLimit"/>, up to
	/// <see cref="WidthLimit"/>. Tile heights are their windows' true aspect ratios, so they stay
	/// ragged and the packing absorbs the difference.
	/// </summary>
	public class MasonryPanel : Panel
	{
		private readonly List<Point> _origins = new List<Point>();
		private Size _packed;

		public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
			nameof(Gap), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double Gap
		{
			get => (double)GetValue(GapProperty);
			set => SetValue(GapProperty, value);
		}

		/// <summary>How much wider the panel gets on each attempt to make the content fit.</summary>
		public static readonly DependencyProperty WidthStepProperty = DependencyProperty.Register(
			nameof(WidthStep), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(96d, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double WidthStep
		{
			get => (double)GetValue(WidthStepProperty);
			set => SetValue(WidthStepProperty, value);
		}

		// The height the content may reach before the panel is widened instead. The panel lives
		// in a ScrollViewer, which measures with infinite height, so the usable height cannot be
		// read from availableSize and is supplied by the owning window.
		public static readonly DependencyProperty HeightLimitProperty = DependencyProperty.Register(
			nameof(HeightLimit), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double HeightLimit
		{
			get => (double)GetValue(HeightLimitProperty);
			set => SetValue(HeightLimitProperty, value);
		}

		public static readonly DependencyProperty WidthLimitProperty = DependencyProperty.Register(
			nameof(WidthLimit), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double WidthLimit
		{
			get => (double)GetValue(WidthLimitProperty);
			set => SetValue(WidthLimitProperty, value);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var widest = 0d;
			foreach (UIElement child in InternalChildren)
			{
				child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				widest = Math.Max(widest, child.DesiredSize.Width);
			}

			var limit = Math.Max(widest, WidthLimit);
			var width = widest;
			while (width < limit && Pack(width).Height > HeightLimit)
				width = Math.Min(limit, width + WidthStep);

			// Report the rectangle the tiles actually occupy, not the width they were packed
			// into: the last step usually overshoots, and the difference would be dead margin.
			_packed = Pack(width);
			return _packed;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			for (int i = 0; i < InternalChildren.Count; i++)
			{
				var child = InternalChildren[i];
				child.Arrange(new Rect(_origins[i], child.DesiredSize));
			}
			return finalSize;
		}

		// Place every child at the lowest point it fits, leftmost among ties, and return the
		// extent of the result. Candidate positions are the left edge and the right edge of
		// every tile already placed: an optimal packing only ever butts a tile against one of
		// those, so there is no need to scan every offset.
		private Size Pack(double width)
		{
			_origins.Clear();
			var placed = new List<Rect>();
			var extent = new Size(0, 0);

			foreach (UIElement child in InternalChildren)
			{
				var size = child.DesiredSize;
				var tileWidth = Math.Min(size.Width, width);
				var spot = LowestSpot(placed, width, tileWidth);

				_origins.Add(spot);
				placed.Add(new Rect(spot, new Size(tileWidth, size.Height)));
				extent.Width = Math.Max(extent.Width, spot.X + tileWidth);
				extent.Height = Math.Max(extent.Height, spot.Y + size.Height);
			}

			return extent;
		}

		private Point LowestSpot(List<Rect> placed, double width, double tileWidth)
		{
			var best = new Point(0, TopAt(placed, 0, tileWidth));

			foreach (var rect in placed)
			{
				var x = rect.Right + Gap;
				if (x + tileWidth > width + 0.01)
					continue;

				var y = TopAt(placed, x, tileWidth);
				if (y < best.Y || (y == best.Y && x < best.X))
					best = new Point(x, y);
			}

			return best;
		}

		// The lowest y at which a tile of this width can sit at this x without touching one
		// already placed.
		private double TopAt(List<Rect> placed, double x, double tileWidth)
		{
			var top = 0d;

			foreach (var rect in placed)
				if (rect.Left < x + tileWidth && x < rect.Right)
					top = Math.Max(top, rect.Bottom + Gap);

			return top;
		}
	}
}

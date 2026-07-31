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
		// Most candidate widths a single measure will try.
		private const int MaxSweepSteps = 64;

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

		// Finite on purpose. The measure sweep walks candidate widths up to this value, so an
		// infinite default would spin the UI thread forever if the real limit never arrived.
		public static readonly DependencyProperty WidthLimitProperty = DependencyProperty.Register(
			nameof(WidthLimit), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(1200d, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double WidthLimit
		{
			get => (double)GetValue(WidthLimitProperty);
			set => SetValue(WidthLimitProperty, value);
		}

		/// <summary>Width over height the packed tiles should come closest to.</summary>
		public static readonly DependencyProperty TargetAspectProperty = DependencyProperty.Register(
			nameof(TargetAspect), typeof(double), typeof(MasonryPanel),
			new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public double TargetAspect
		{
			get => (double)GetValue(TargetAspectProperty);
			set => SetValue(TargetAspectProperty, value);
		}

		// Try every width the tiles could be packed into and keep the one whose result comes
		// closest to TargetAspect, ignoring any that overflow HeightLimit. Widening only when
		// the content overflows would leave the tiles in a single tall column whenever they
		// happen to fit, which is the one shape an overview should never be.
		protected override Size MeasureOverride(Size availableSize)
		{
			var widest = 0d;
			foreach (UIElement child in InternalChildren)
			{
				child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
				widest = Math.Max(widest, child.DesiredSize.Width);
			}

			var limit = Math.Max(widest, WidthLimit);
			var best = widest;
			var bestScore = double.MaxValue;
			var fits = false;

			// Bounded by step count, not just by `limit`: the sweep runs during measure, on the
			// UI thread, so a limit that is infinite or absurd has to end the loop rather than
			// hang the window with no content ever drawn.
			var steps = WidthStep > 0 ? (int)Math.Min(MaxSweepSteps, (limit - widest) / WidthStep) : 0;

			for (var step = 0; step <= steps; step++)
			{
				var width = widest + step * WidthStep;
				var size = Pack(width);
				var overflows = size.Height > HeightLimit;

				// Prefer any width that fits over any that does not, and among equals the one
				// closest to the target shape. Log keeps "twice as wide" and "half as wide"
				// the same distance from it.
				if (fits && overflows)
					continue;

				var score = size.Height > 0
					? Math.Abs(Math.Log(size.Width / size.Height / TargetAspect))
					: double.MaxValue;

				if (!fits && !overflows)
				{
					fits = true;
					bestScore = score;
					best = width;
					continue;
				}

				if (score < bestScore)
				{
					bestScore = score;
					best = width;
				}
			}

			// Report the rectangle the tiles actually occupy, not the width they were packed
			// into: the chosen step usually overshoots, and the difference would be dead margin.
			_packed = Pack(best);
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

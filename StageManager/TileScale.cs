using System;
using System.Globalization;
using Microsoft.Win32;

namespace StageManager
{
	/// <summary>
	/// The exposé's zoom level, persisted so the widget comes back the size it was left.
	/// Steps are multiplicative, so a notch feels the same at either end of the range.
	/// </summary>
	public static class TileScale
	{
		private const string Key = @"SOFTWARE\StageManager";
		private const string Value = "TileScale";

		private const double Min = 0.4;
		private const double Max = 2.5;
		private const double Factor = 1.1;

		public static double Step(double scale, int steps) =>
			Math.Clamp(scale * Math.Pow(Factor, steps), Min, Max);

		public static double Load()
		{
			using var key = Registry.CurrentUser.OpenSubKey(Key);
			var stored = key?.GetValue(Value)?.ToString();

			return double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
				? Math.Clamp(scale, Min, Max)
				: 1.0;
		}

		public static void Save(double scale)
		{
			using var key = Registry.CurrentUser.CreateSubKey(Key);
			key.SetValue(Value, scale.ToString("R", CultureInfo.InvariantCulture), RegistryValueKind.String);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace DilaRacing
{
    public enum WheelId { NeoX, FPro }
    public enum ThemeMode { Solid, BottomToTopOnce, BottomToTopRepeat, VerticalGradient }
    public enum PreviewMode { Theme, RevLimit, PitLimiter, Both, Identify }

    public sealed class WheelSettings
    {
        public string StartColor { get; set; }
        public string EndColor { get; set; }
        public double Brightness { get; set; }
        public ThemeMode Mode { get; set; }
        public double FadeSeconds { get; set; }
        public bool RevFlash { get; set; }
        public bool PitFlash { get; set; }
        public double FlashHz { get; set; }
        public int FallbackRedlineRpm { get; set; }
        public bool ShowWhenIdle { get; set; }
        // One row per physical level, BOTTOM first. Each channel occurs exactly once.
        public string BottomToTopRows { get; set; }

        public static int Count(WheelId wheel) { return wheel == WheelId.NeoX ? 12 : 18; }
        public static WheelSettings Defaults(WheelId wheel)
        {
            return new WheelSettings {
                StartColor = "#FFFFFF", EndColor = "#0080FF", Brightness = 25,
                Mode = ThemeMode.Solid, FadeSeconds = 6, RevFlash = true, PitFlash = true,
                FlashHz = 2, FallbackRedlineRpm = 0, ShowWhenIdle = true,
                BottomToTopRows = string.Join(",", Enumerable.Range(1, Count(wheel)))
            };
        }

        public WheelSettings Copy() { return (WheelSettings)MemberwiseClone(); }

        public static Color ParseColor(string value)
        {
            int rgb;
            if (value == null || value.Length != 7 || value[0] != '#' ||
                !int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                throw new ArgumentException("Colours must be #RRGGBB.");
            return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        public double[] Heights(WheelId wheel)
        {
            int count = Count(wheel);
            var rows = (BottomToTopRows ?? "").Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (rows.Length == 0) throw new ArgumentException("Enter the LED numbers, with the bottom row first.");
            var heights = new double[count];
            var seen = new HashSet<int>();
            for (int row = 0; row < rows.Length; row++)
            {
                var values = rows[row].Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (values.Length == 0) throw new ArgumentException("Each row needs at least one LED number.");
                foreach (string value in values)
                {
                    int number;
                    if (!int.TryParse(value, out number) || number < 1 || number > count || !seen.Add(number))
                        throw new ArgumentException("Use each LED number from 1 to " + count + " exactly once.");
                    heights[number - 1] = rows.Length == 1 ? 0 : (double)row / (rows.Length - 1);
                }
            }
            if (seen.Count != count) throw new ArgumentException("The layout must include all " + count + " LED numbers.");
            return heights;
        }

        public void Validate(WheelId wheel)
        {
            ParseColor(StartColor); ParseColor(EndColor); Heights(wheel);
            RequireRange(Brightness, 0, 100, "Brightness");
            RequireRange(FadeSeconds, 1, 30, "Fade duration");
            RequireRange(FlashHz, 0.5, 5, "Flash frequency");
            RequireRange(FallbackRedlineRpm, 0, 30000, "Redline override");
            if (!Enum.IsDefined(typeof(ThemeMode), Mode)) throw new ArgumentException("Unknown theme mode.");
        }

        private static void RequireRange(double value, double min, double max, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
                throw new ArgumentException(name + " must be between " + min + " and " + max + ".");
        }
    }

    // Published snapshots are never edited by the UI or the render loop.
    public sealed class WheelTheme
    {
        public readonly WheelSettings Settings;
        public readonly double[] Heights;
        public readonly Color Start, End;
        public readonly double Epoch;
        public WheelTheme(WheelSettings settings, WheelId wheel, double epoch)
        {
            settings.Validate(wheel);
            Settings = settings.Copy(); Heights = Settings.Heights(wheel);
            Start = WheelSettings.ParseColor(Settings.StartColor);
            End = WheelSettings.ParseColor(Settings.EndColor); Epoch = epoch;
        }
    }

    public struct Telemetry
    {
        public bool Running, PitLimiter, Redline;
        public double Rpm;
        public static Telemetry Preview(PreviewMode mode)
        {
            return new Telemetry { Running = true,
                PitLimiter = mode == PreviewMode.PitLimiter || mode == PreviewMode.Both,
                Redline = mode == PreviewMode.RevLimit || mode == PreviewMode.Both };
        }
    }

    public static class Lighting
    {
        // Writes the complete frame, including black during the off phase, so no lower
        // profile layer leaks through warning flashes. No IO, sleeps or UI work here.
        public static Color At(WheelTheme theme, int led, Telemetry data, double time)
        {
            var s = theme.Settings;
            if (!data.Running && !s.ShowWhenIdle) return Color.Black;
            double elapsed = Math.Max(0, time - theme.Epoch);
            bool on = (elapsed * s.FlashHz) % 1 < 0.5;
            Color color;
            if (data.Running && s.PitFlash && data.PitLimiter)
                color = on ? Color.Blue : Color.White;
            else if (data.Running && s.RevFlash && (data.Redline ||
                (s.FallbackRedlineRpm > 0 && data.Rpm >= s.FallbackRedlineRpm)))
                color = on ? Color.Red : Color.Black;
            else
            {
                double fraction = 0;
                if (s.Mode == ThemeMode.VerticalGradient) fraction = theme.Heights[led];
                else if (s.Mode != ThemeMode.Solid)
                {
                    double progress = elapsed / s.FadeSeconds;
                    bool reverse = false;
                    if (s.Mode == ThemeMode.BottomToTopRepeat)
                    {
                        // A 25% hold at each end; the return also travels bottom to top.
                        double cycle = progress % 2.5;
                        reverse = cycle >= 1.25;
                        progress = reverse ? cycle - 1.25 : cycle;
                    }
                    fraction = Clamp((progress - theme.Heights[led] * 0.6) / 0.4);
                    fraction = fraction * fraction * (3 - 2 * fraction);
                    if (reverse) fraction = 1 - fraction;
                }
                color = Blend(theme.Start, theme.End, fraction);
            }
            return Dim(color, s.Brightness / 100);
        }

        public static Color Dim(Color c, double brightness)
        {
            return Color.FromArgb((int)Math.Round(c.R * brightness), (int)Math.Round(c.G * brightness), (int)Math.Round(c.B * brightness));
        }
        private static double Clamp(double x) { return Math.Max(0, Math.Min(1, x)); }
        private static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb((int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }
    }
}

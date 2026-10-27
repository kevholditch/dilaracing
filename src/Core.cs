using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace DilaRacing
{
    public enum WheelId { NeoX, FPro }
    public enum ThemeMode { Solid, BottomToTopOnce, BottomToTopRepeat, VerticalGradient, Pulse, Twinkle }
    public enum PreviewMode { Theme, RevLimit, PitLimiter, Both, Identify, IdleTheme, TractionControl, Abs, Assists, TcAdjustment, AbsAdjustment }

    public sealed class WheelSettings
    {
        public string StartColor { get; set; }
        public string EndColor { get; set; }
        public double Brightness { get; set; }
        public ThemeMode Mode { get; set; }
        // Nullable fields distinguish older settings from explicit new choices.
        // Mode remains readable for migration of existing saved themes.
        public ThemeMode? IdleMode { get; set; }
        public ThemeMode? RacingMode { get; set; }
        public double FadeSeconds { get; set; }
        public bool RevFlash { get; set; }
        public bool PitFlash { get; set; }
        // Keep the existing FlashHz key for pit-limiter settings in saved profiles.
        public double FlashHz { get; set; }
        public double RevFlashHz { get; set; }
        // One-based button channels; zero disables the indicator, including in older settings.
        public int TractionControlLed { get; set; }
        public int AbsLed { get; set; }
        public bool TcAlwaysBlue { get; set; }
        public bool AbsAlwaysRed { get; set; }
        public bool TcAdjustmentFlash { get; set; }
        public bool AbsAdjustmentFlash { get; set; }
        public RevStripSettings RevStrip { get; set; }
        public int FallbackRedlineRpm { get; set; }
        public bool ShowWhenIdle { get; set; }
        // One row per physical level, BOTTOM first. Each channel occurs exactly once.
        public string BottomToTopRows { get; set; }

        // Older saved settings have no RevFlashHz. Json.NET calls this constructor,
        // giving them the faster default without resetting their theme or pit speed.
        public WheelSettings() { RevFlashHz = 6; RevStrip = new RevStripSettings(); }

        public static int Count(WheelId wheel) { return wheel == WheelId.NeoX ? 12 : 18; }
        public static WheelSettings Defaults(WheelId wheel)
        {
            return new WheelSettings {
                StartColor = "#FFFFFF", EndColor = "#0080FF", Brightness = 25,
                Mode = ThemeMode.Solid, FadeSeconds = 6, RevFlash = true, PitFlash = true,
                IdleMode = ThemeMode.BottomToTopRepeat, RacingMode = ThemeMode.VerticalGradient,
                FlashHz = 2, FallbackRedlineRpm = 0, ShowWhenIdle = true,
                BottomToTopRows = string.Join(",", Enumerable.Range(1, Count(wheel)))
            };
        }

        public WheelSettings Copy()
        {
            var copy = (WheelSettings)MemberwiseClone();
            copy.RevStrip = RevStrip.Copy();
            return copy;
        }

        public ThemeMode ModeFor(bool running)
        {
            if (!running) return IdleMode ?? Mode;
            // Existing animated themes become the idle animation; preserve older
            // solid/gradient racing themes and all colours, layout and speeds.
            return RacingMode ?? (Mode == ThemeMode.BottomToTopOnce || Mode == ThemeMode.BottomToTopRepeat
                ? ThemeMode.VerticalGradient : Mode);
        }

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
            if (RevStrip == null) throw new ArgumentException("Missing rev strip settings.");
            RevStrip.Validate();
            RequireRange(Brightness, 0, 100, "Brightness");
            RequireRange(FadeSeconds, 1, 30, "Fade duration");
            RequireRange(FlashHz, 0.5, 5, "Flash frequency");
            RequireRange(RevFlashHz, 1, 10, "Rev flash frequency");
            RequireRange(FallbackRedlineRpm, 0, 30000, "Redline override");
            RequireRange(TractionControlLed, 0, Count(wheel), "Traction control LED");
            RequireRange(AbsLed, 0, Count(wheel), "ABS LED");
            if (!Enum.IsDefined(typeof(ThemeMode), Mode)) throw new ArgumentException("Unknown theme mode.");
            if (!Enum.IsDefined(typeof(ThemeMode), ModeFor(false)) || !Enum.IsDefined(typeof(ThemeMode), ModeFor(true)))
                throw new ArgumentException("Unknown idle or racing theme mode.");
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
        public bool TcActive, AbsActive;
        public double? TcPulseSeconds, AbsPulseSeconds;
        public double? TcAdjustmentSeconds, AbsAdjustmentSeconds;
        public double Rpm;
        public bool RedlineHeld;
        public double? RedlinePulseSeconds;
        public double? ThemeElapsedSeconds;
        public static Telemetry Preview(PreviewMode mode)
        {
            return new Telemetry { Running = mode != PreviewMode.IdleTheme,
                PitLimiter = mode == PreviewMode.PitLimiter || mode == PreviewMode.Both,
                Redline = mode == PreviewMode.RevLimit || mode == PreviewMode.Both,
                TcActive = mode == PreviewMode.TractionControl || mode == PreviewMode.Assists,
                AbsActive = mode == PreviewMode.Abs || mode == PreviewMode.Assists };
        }
    }

    // Each renderer owns its playback clock. Restart when entering idle/racing
    // or applying settings; warning overlays do not interrupt theme playback.
    public sealed class ThemePlayback
    {
        private WheelTheme lastTheme;
        private bool lastRunning;
        private double started, lastTime;
        public void Reset() { lastTheme = null; }
        public Telemetry Apply(WheelTheme theme, Telemetry data, double time)
        {
            if (!object.ReferenceEquals(theme, lastTheme) || data.Running != lastRunning || time < lastTime)
                started = time;
            lastTheme = theme; lastRunning = data.Running; lastTime = time;
            data.ThemeElapsedSeconds = Math.Max(0, time - started);
            return data;
        }
    }

    // Per-renderer state: limiter RPM can bounce across SimHub's redline threshold.
    // Keep the warning through brief signal gaps, but clear it on game exit/disable.
    public sealed class RevWarningHold
    {
        public const double HoldSeconds = 0.3;
        private double lastActive = double.NegativeInfinity;
        private double lastTime = double.NegativeInfinity;
        private double pulseStarted;
        private bool wasActive;

        public void Reset()
        {
            lastActive = lastTime = double.NegativeInfinity;
            wasActive = false; pulseStarted = 0;
        }

        public Telemetry Apply(WheelTheme theme, Telemetry data, double time)
        {
            data.RedlineHeld = false;
            data.RedlinePulseSeconds = null;
            if (!data.Running || !theme.Settings.RevFlash)
            {
                Reset();
                data.Redline = false;
                return data;
            }
            if (time < lastTime) Reset();
            lastTime = time;
            bool active = data.Redline || (theme.Settings.FallbackRedlineRpm > 0 && data.Rpm >= theme.Settings.FallbackRedlineRpm);
            if (active)
            {
                // A fresh SimHub redline pulse starts bright immediately. A sustained
                // signal instead uses the configured fallback flashing rhythm.
                if (!wasActive) pulseStarted = time;
                lastActive = time;
            }
            wasActive = active;
            data.Redline = time - lastActive < HoldSeconds;
            data.RedlineHeld = data.Redline && !active;
            if (data.Redline) data.RedlinePulseSeconds = Math.Max(0, time - pulseStarted);
            return data;
        }
    }

    // Independent clocks start bright on each intervention edge. A sustained
    // SimHub signal gets a rapid visual judder; no inferred wheel-slip telemetry
    // or hold after the intervention ends is used.
    public sealed class InterventionPlayback
    {
        private bool tcWasActive, absWasActive;
        private double tcStarted, absStarted, lastTime;
        public void Reset() { tcWasActive = absWasActive = false; lastTime = 0; }
        public Telemetry Apply(WheelTheme theme, Telemetry data, double time)
        {
            data.TcPulseSeconds = data.AbsPulseSeconds = null;
            if (!data.Running) { Reset(); return data; }
            if (time < lastTime) Reset();
            lastTime = time;
            bool tc = data.TcActive && theme.Settings.TractionControlLed > 0;
            bool abs = data.AbsActive && theme.Settings.AbsLed > 0;
            if (tc && !tcWasActive) tcStarted = time;
            if (abs && !absWasActive) absStarted = time;
            tcWasActive = tc; absWasActive = abs;
            if (tc) data.TcPulseSeconds = Math.Max(0, time - tcStarted);
            if (abs) data.AbsPulseSeconds = Math.Max(0, time - absStarted);
            return data;
        }
    }

    public static class Lighting
    {
        // Writes opaque colours in both warning phases so no lower profile layer
        // leaks through. No IO, sleeps or UI work here.
        public static Color At(WheelTheme theme, int led, Telemetry data, double time)
        {
            var s = theme.Settings;
            if (!data.Running && !s.ShowWhenIdle) return Color.Black;
            double elapsed = Math.Max(0, time - theme.Epoch);
            bool on = (elapsed * s.FlashHz) % 1 < 0.5;
            double intensity = 1;
            Color color;
            if (data.Running && data.AbsActive && s.AbsLed == led + 1)
                color = ((data.AbsPulseSeconds ?? elapsed) * 10) % 1 < 0.5 ? Color.Red : Color.FromArgb(96, 0, 0);
            else if (data.Running && data.TcActive && s.TractionControlLed == led + 1)
                color = ((data.TcPulseSeconds ?? elapsed) * 10) % 1 < 0.5 ? Color.Blue : Color.FromArgb(0, 0, 96);
            else if (s.AbsLed == led + 1 && s.AbsAdjustmentFlash && AdjustmentFeedback.IsActive(data.AbsAdjustmentSeconds))
                color = AdjustmentFeedback.ColorAt(Color.Red, data.AbsAdjustmentSeconds.Value);
            else if (s.TractionControlLed == led + 1 && s.TcAdjustmentFlash && AdjustmentFeedback.IsActive(data.TcAdjustmentSeconds))
                color = AdjustmentFeedback.ColorAt(Color.Blue, data.TcAdjustmentSeconds.Value);
            else if (s.AbsLed == led + 1 && s.AbsAlwaysRed)
                color = Color.Red;
            else if (s.TractionControlLed == led + 1 && s.TcAlwaysBlue)
                color = Color.Blue;
            else if (data.Running && s.PitFlash && data.PitLimiter)
                color = on ? Color.Blue : Color.White;
            else if (data.Running && s.RevFlash && (data.Redline ||
                (s.FallbackRedlineRpm > 0 && data.Rpm >= s.FallbackRedlineRpm)))
            {
                double revElapsed = data.RedlinePulseSeconds ?? elapsed;
                bool revOn = !data.RedlineHeld && (revElapsed * s.RevFlashHz) % 1 < 0.5;
                color = revOn ? Color.Red : Color.FromArgb(96, 0, 0);
            }
            else
            {
                double fraction = 0;
                ThemeMode mode = s.ModeFor(data.Running);
                if (mode == ThemeMode.Pulse)
                {
                    // Independent, reproducible random-looking twinkles. Random
                    // choices stay fixed for each event, never changing per frame.
                    double duration = Math.Max(4, s.FadeSeconds);
                    double position = Math.Max(0, data.ThemeElapsedSeconds ?? elapsed) / duration + Noise(led, 0, 17);
                    uint cycle = (uint)Math.Floor(position);
                    double phase = position - Math.Floor(position);
                    double start = 0.05 + Noise(led, cycle, 23) * 0.2;
                    double length = 0.4 + Noise(led, cycle, 41) * 0.25;
                    double progress = (phase - start) / length;
                    fraction = Noise(led, cycle, 67);
                    intensity = progress > 0 && progress < 1 && Noise(led, cycle, 89) > 0.18
                        ? Math.Pow(Math.Sin(Math.PI * progress), 2) : 0;
                }
                else if (mode == ThemeMode.Twinkle)
                {
                    // Complementary pairs keep at least half the channels at
                    // half brightness or above. Different pair speeds/phases
                    // avoid a whole-wheel pulse while keeping every fade smooth.
                    int pair = led / 2;
                    double position = Math.Max(0, data.ThemeElapsedSeconds ?? elapsed)
                        / Math.Max(4, s.FadeSeconds) * (0.85 + 0.3 * Noise(pair, 0, 101))
                        + Noise(pair, 0, 103);
                    double wave = Math.Sin(position * Math.PI * 2);
                    intensity = 0.5 + 0.5 * ((led % 2 == 0) ? wave : -wave);
                    // Colour changes blend across cycle boundaries, including
                    // while a button remains lit.
                    uint cycle = (uint)Math.Floor(position);
                    double blend = position - Math.Floor(position);
                    blend = blend * blend * (3 - 2 * blend);
                    double from = Noise(led, cycle, 107);
                    fraction = from + (Noise(led, cycle + 1, 107) - from) * blend;
                }
                else if (mode == ThemeMode.VerticalGradient) fraction = theme.Heights[led];
                else if (mode != ThemeMode.Solid)
                {
                    double progress = (data.ThemeElapsedSeconds ?? elapsed) / s.FadeSeconds;
                    bool reverse = false;
                    if (mode == ThemeMode.BottomToTopRepeat)
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
            return Dim(color, s.Brightness / 100 * intensity);
        }

        public static Color Dim(Color c, double brightness)
        {
            return Color.FromArgb((int)Math.Round(c.R * brightness), (int)Math.Round(c.G * brightness), (int)Math.Round(c.B * brightness));
        }
        private static double Clamp(double x) { return Math.Max(0, Math.Min(1, x)); }
        private static double Noise(int led, uint cycle, uint salt)
        {
            unchecked
            {
                uint value = (uint)(led + 1) * 0x9E3779B9u ^ cycle * 0x85EBCA6Bu ^ salt;
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16;
                return value / (double)uint.MaxValue;
            }
        }
        private static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb((int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }
    }

    public static class AdjustmentFeedback
    {
        public const double Duration = 0.4;
        public static bool IsActive(double? elapsed)
        { return elapsed.HasValue && elapsed.Value >= 0 && elapsed.Value < Duration; }
        // Two white ticks contrast with an always-on blue/red base. The active
        // intervention judder remains higher priority than adjustment feedback.
        public static Color ColorAt(Color baseColor, double elapsed)
        { return elapsed < 0.09 || (elapsed >= 0.18 && elapsed < 0.27) ? Color.White : baseColor; }
    }

    public sealed class AssistLevelChanges
    {
        private string context;
        private int? tc, abs;
        public void Reset() { context = null; tc = abs = null; }
        public void Update(bool running, string source, int tcLevel, int absLevel, out bool tcChanged, out bool absChanged)
        {
            tcChanged = absChanged = false;
            if (!running) { Reset(); return; }
            if (context != source) { Reset(); context = source; }
            int? nextTc = tcLevel >= 0 ? (int?)tcLevel : null;
            int? nextAbs = absLevel >= 0 ? (int?)absLevel : null;
            tcChanged = tc.HasValue && nextTc.HasValue && tc != nextTc;
            absChanged = abs.HasValue && nextAbs.HasValue && abs != nextAbs;
            tc = nextTc; abs = nextAbs;
        }
    }
}

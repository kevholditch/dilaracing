using System;
using System.Drawing;

namespace DilaRacing
{
    public enum RevStripMode { Off, Progressive, Converging, Minimal }
    public enum RevStripColorMode { GreenAmberRed, SingleColor }

    public sealed class RevStripSettings
    {
        public RevStripMode Mode { get; set; }
        public RevStripColorMode ColorMode { get; set; }
        public string SingleColor { get; set; }
        public double Brightness { get; set; }
        public double StartPercent { get; set; }
        public double ShiftPercent { get; set; }
        public double FlashHz { get; set; }
        public bool Reverse { get; set; }
        public RevStripSettings()
        { Mode = RevStripMode.Progressive; SingleColor = "#FFFFFF"; Brightness = 25; StartPercent = 70; ShiftPercent = 97; FlashHz = 6; }
        public RevStripSettings Copy() { return (RevStripSettings)MemberwiseClone(); }
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(RevStripMode), Mode)) throw new ArgumentException("Unknown rev strip mode.");
            if (!Enum.IsDefined(typeof(RevStripColorMode), ColorMode)) throw new ArgumentException("Unknown rev strip colour mode.");
            WheelSettings.ParseColor(SingleColor);
            Range(Brightness, 0, 100, "Rev strip brightness");
            Range(StartPercent, 50, 95, "Rev strip start");
            Range(ShiftPercent, 80, 100, "Shift cue");
            Range(FlashHz, 1, 10, "Rev strip flash speed");
            if (StartPercent >= ShiftPercent) throw new ArgumentException("Rev strip start must be below the shift cue.");
        }
        private static void Range(double value, double min, double max, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
                throw new ArgumentException(name + " must be between " + min + " and " + max + ".");
        }
    }

    public struct RevStripTelemetry
    {
        public bool Running, Redline, RedlineHeld;
        public double Percent, PulseSeconds;
    }

    // Renderer-local state. Fresh redline edges start bright and brief signal
    // gaps remain dim red, matching the button warning without depending on it.
    public sealed class RevStripPlayback
    {
        private bool wasRed, wasShift;
        private double lastRed = double.NegativeInfinity, pulseStarted, shiftStarted, lastTime;
        public void Reset() { wasRed = wasShift = false; lastRed = double.NegativeInfinity; lastTime = 0; }
        public RevStripTelemetry Apply(RevStripSettings settings, RevStripTelemetry data, double time)
        {
            data.RedlineHeld = false; data.PulseSeconds = 0;
            if (!data.Running || settings.Mode == RevStripMode.Off) { Reset(); return data; }
            if (time < lastTime) Reset();
            lastTime = time;
            bool valid = !double.IsNaN(data.Percent) && !double.IsInfinity(data.Percent) && data.Percent > 0;
            bool red = data.Redline || (valid && data.Percent >= 100);
            if (red) { if (!wasRed) pulseStarted = time; lastRed = time; }
            wasRed = red;
            data.Redline = red || time - lastRed < RevWarningHold.HoldSeconds;
            data.RedlineHeld = data.Redline && !red;
            bool shift = valid && data.Percent >= settings.ShiftPercent && !data.Redline;
            if (shift && !wasShift) shiftStarted = time;
            wasShift = shift;
            data.PulseSeconds = data.Redline ? Math.Max(0, time - pulseStarted) : shift ? Math.Max(0, time - shiftStarted) : 0;
            return data;
        }
    }

    public static class RevStripLighting
    {
        public const int Count = 9;
        public static Color At(RevStripSettings settings, int led, RevStripTelemetry data)
        {
            if (!data.Running || settings.Mode == RevStripMode.Off) return Color.Black;
            bool bright = (data.PulseSeconds * settings.FlashHz) % 1 < 0.5;
            bool single = settings.ColorMode == RevStripColorMode.SingleColor;
            Color selected = single ? WheelSettings.ParseColor(settings.SingleColor) : Color.Red;
            Color color;
            if (data.Redline)
                color = bright && !data.RedlineHeld ? selected : Lighting.Dim(selected, 96.0 / 255);
            else if (double.IsNaN(data.Percent) || double.IsInfinity(data.Percent) || data.Percent <= 0)
                return Color.Black;
            else if (data.Percent >= settings.ShiftPercent)
                color = single ? (bright ? selected : Lighting.Dim(selected, 64.0 / 255)) : settings.Mode == RevStripMode.Minimal
                    ? (bright ? Color.White : Color.FromArgb(64, 64, 64))
                    : (bright ? Color.Blue : Color.FromArgb(0, 0, 64));
            else
            {
                int channel = settings.Reverse ? Count - 1 - led : led;
                bool converge = settings.Mode == RevStripMode.Converging;
                int rank = converge ? Math.Min(channel, Count - 1 - channel) : channel;
                int stages = converge ? 5 : Count;
                double start = settings.Mode == RevStripMode.Minimal ? Math.Max(settings.StartPercent, settings.ShiftPercent - 10) : settings.StartPercent;
                double threshold = start + (settings.ShiftPercent - start) * rank / stages;
                if (data.Percent < threshold) return Color.Black;
                double position = (double)rank / stages;
                color = single ? selected : settings.Mode != RevStripMode.Minimal && position < 1.0 / 3 ? Color.Lime
                    : position < 2.0 / 3 ? Color.Orange : Color.Red;
            }
            return Lighting.Dim(color, settings.Brightness / 100);
        }

        // One deterministic sweep is shared by the local and hardware previews.
        public static RevStripTelemetry Demo(double elapsed)
        {
            double phase = elapsed % 5;
            double percent = phase < 3.5 ? 50 + phase / 3.5 * 50 : phase < 4.4 ? 102 : 50;
            return new RevStripTelemetry { Running = true, Percent = percent, Redline = percent >= 100 };
        }
    }
}

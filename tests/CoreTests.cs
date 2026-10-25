using System;
using System.Drawing;
using DilaRacing;

internal static class CoreTests
{
    private static int assertions;
    private static void Check(bool ok, string name) { assertions++; if (!ok) throw new Exception(name); }
    private static bool Same(Color a, Color b) { return a.ToArgb() == b.ToArgb(); }
    private static void Invalid(Action action, string name)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
    public static int Main()
    {
        try
        {
            var s = WheelSettings.Defaults(WheelId.NeoX); s.Brightness = 100;
            s.BottomToTopRows = "1,2,3,4\n5,6,7,8\n9,10,11,12";
            s.Mode = ThemeMode.BottomToTopOnce; s.FadeSeconds = 10;
            var theme = new WheelTheme(s, WheelId.NeoX, 0);
            var drive = new Telemetry { Running = true };
            Check(Same(Lighting.At(theme, 0, drive, 0), Color.White), "Fade starts white");
            Check(Same(Lighting.At(theme, 11, drive, 0), Color.White), "Top starts white");
            Check(!Same(Lighting.At(theme, 0, drive, 2), Color.White), "Bottom changes first");
            Check(Same(Lighting.At(theme, 11, drive, 2), Color.White), "Top waits");
            Check(Same(Lighting.At(theme, 11, drive, 10), theme.End), "Top reaches end colour");
            Check(Same(Lighting.At(theme, 0, drive, 40), theme.End), "Once mode holds");
            var red = new Telemetry { Running = true, Redline = true };
            Check(Same(Lighting.At(theme, 0, red, 0), Color.Red), "Redline on phase");
            Check(Same(Lighting.At(theme, 0, red, 0.3), Color.Black), "Redline off phase opaque black");
            red.PitLimiter = true;
            Check(Same(Lighting.At(theme, 0, red, 0), Color.Blue), "Limiter wins over redline");
            Check(Same(Lighting.At(theme, 0, red, 0.3), Color.White), "Limiter alternates white");
            red.Running = false;
            Check(Same(Lighting.At(theme, 0, red, 0), Color.White), "Stale warnings ignored out of game");
            s.ShowWhenIdle = false;
            Check(Same(Lighting.At(new WheelTheme(s, WheelId.NeoX, 0), 0, red, 0), Color.Black), "Idle off");
            s.ShowWhenIdle = true; s.RevFlash = false; s.PitFlash = false;
            Check(Same(Lighting.At(new WheelTheme(s, WheelId.NeoX, 0), 0, Telemetry.Preview(PreviewMode.Both), 0), Color.White), "Disabled warnings do not override");
            s.RevFlash = true; s.FallbackRedlineRpm = 8000;
            Check(Same(Lighting.At(new WheelTheme(s, WheelId.NeoX, 0), 0, new Telemetry { Running = true, Rpm = 8000 }, 0), Color.Red), "Fixed RPM threshold");
            s.FallbackRedlineRpm = 0;
            s.Mode = ThemeMode.BottomToTopRepeat;
            var loop = new WheelTheme(s, WheelId.NeoX, 0);
            Check(Same(Lighting.At(loop, 11, drive, 12), loop.End), "Loop holds end");
            Check(Same(Lighting.At(loop, 0, drive, 24), loop.Start), "Loop returns to start");
            Check(Same(Lighting.At(loop, 0, drive, 25), loop.Start), "Loop boundary continuous");
            s.Mode = ThemeMode.VerticalGradient;
            var gradient = new WheelTheme(s, WheelId.NeoX, 0);
            Check(Same(Lighting.At(gradient, 0, drive, 0), gradient.Start), "Gradient bottom");
            Check(Same(Lighting.At(gradient, 11, drive, 0), gradient.End), "Gradient top");
            s.Brightness = 0;
            Check(Same(Lighting.At(new WheelTheme(s, WheelId.NeoX, 0), 0, drive, 0), Color.Black), "Zero brightness");
            s.Brightness = 100;
            var snapshot = new WheelTheme(s, WheelId.NeoX, 0); s.StartColor = "#FF0000";
            Check(Same(snapshot.Start, Color.White), "Snapshot isolated from edits");
            s.BottomToTopRows = "1,1"; Invalid(() => s.Validate(WheelId.NeoX), "Duplicate mapping rejected");
            s.BottomToTopRows = "1,2"; Invalid(() => s.Validate(WheelId.NeoX), "Incomplete mapping rejected");
            s = WheelSettings.Defaults(WheelId.NeoX); s.Brightness = double.NaN;
            Invalid(() => s.Validate(WheelId.NeoX), "NaN rejected");
            s.Brightness = 25; s.StartColor = "Blue";
            Invalid(() => s.Validate(WheelId.NeoX), "Invalid colour rejected");
            s = WheelSettings.Defaults(WheelId.FPro);
            Check(s.Heights(WheelId.FPro).Length == 18, "F-Pro channel count");
            Invalid(() => s.Validate(WheelId.NeoX), "Wrong wheel mapping rejected");
            Console.WriteLine("PASS: " + assertions + " lighting assertions."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); return 1; }
    }
}

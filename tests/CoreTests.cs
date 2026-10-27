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
            s.RacingMode = ThemeMode.BottomToTopOnce; s.FadeSeconds = 10;
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
            Check(Same(Lighting.At(theme, 0, red, 0.3), Color.FromArgb(96, 0, 0)), "Redline dim phase stays red");
            var hold = new RevWarningHold();
            Check(theme.Settings.RevFlashHz == 6 && theme.Settings.FlashHz == 2, "Separate default rev and pit rates");
            var onset = hold.Apply(theme, red, 1.1);
            Check(Same(Lighting.At(theme, 0, onset, 1.1), Color.Red), "Warning starts bright independently of theme clock");
            var sustained = hold.Apply(theme, red, 1.2);
            Check(Same(Lighting.At(theme, 0, sustained, 1.2), Color.FromArgb(96, 0, 0)), "Sustained signal uses faster fallback rhythm");
            var dip = hold.Apply(theme, drive, 1.28);
            Check(Same(Lighting.At(theme, 0, dip, 1.28), Color.FromArgb(96, 0, 0)), "Signal dip stays dim even in fallback bright phase");
            var retrigger = hold.Apply(theme, red, 1.29);
            Check(Same(Lighting.At(theme, 0, retrigger, 1.29), Color.Red), "Next redline pulse immediately retriggers bright red");
            var fastSettings = s.Copy(); fastSettings.RevFlashHz = 10;
            var fastTheme = new WheelTheme(fastSettings, WheelId.NeoX, 0);
            Check(Same(Lighting.At(fastTheme, 0, red, 0.06), Color.FromArgb(96, 0, 0)), "Custom rev speed is applied");
            Check(Same(Lighting.At(fastTheme, 0, Telemetry.Preview(PreviewMode.Both), 0.06), Color.Blue), "Rev speed does not alter pit rhythm or priority");
            hold.Reset();
            Check(hold.Apply(theme, red, 0).Redline, "Redline activates immediately");
            var held = hold.Apply(theme, drive, 0.26);
            Check(held.Redline, "Brief RPM dip retains warning");
            var heldColor = Lighting.At(theme, 0, held, 0.26);
            Check(heldColor.A == 255 && heldColor.R > 0 && heldColor.G == 0 && heldColor.B == 0,
                "RPM dip and dim phase cannot reveal theme");
            Check(!hold.Apply(theme, drive, 0.31).Redline, "Warning clears after hold expires");
            hold.Apply(theme, red, 1);
            hold.Apply(theme, red, 1.2);
            Check(hold.Apply(theme, drive, 1.4).Redline, "Repeated pulses extend hold");
            Check(!hold.Apply(theme, new Telemetry(), 1.41).Redline, "Game exit clears hold immediately");
            Check(!hold.Apply(theme, drive, 1.42).Redline, "New session has no stale hold");
            hold.Apply(theme, red, 2);
            Check(!hold.Apply(theme, drive, 0).Redline, "Clock reset clears stale hold");
            var disabledSettings = s.Copy(); disabledSettings.RevFlash = false;
            hold.Apply(theme, red, 3);
            Check(!hold.Apply(new WheelTheme(disabledSettings, WheelId.NeoX, 0), drive, 3.1).Redline,
                "Disabling redline clears hold immediately");
            var dimSettings = s.Copy(); dimSettings.Brightness = 25;
            var dimTheme = new WheelTheme(dimSettings, WheelId.NeoX, 0);
            Check(Same(Lighting.At(dimTheme, 0, red, 0), Color.FromArgb(64, 0, 0)), "Bright red respects brightness");
            Check(Same(Lighting.At(dimTheme, 0, red, 0.3), Color.FromArgb(24, 0, 0)), "Dim red respects brightness");
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
            var fallbackTheme = new WheelTheme(s, WheelId.NeoX, 0);
            hold.Reset();
            hold.Apply(fallbackTheme, new Telemetry { Running = true, Rpm = 8000 }, 0);
            Check(hold.Apply(fallbackTheme, new Telemetry { Running = true, Rpm = 7900 }, 0.1).Redline,
                "RPM threshold uses the same hold");
            s.FallbackRedlineRpm = 0;
            s.RacingMode = ThemeMode.BottomToTopRepeat;
            var loop = new WheelTheme(s, WheelId.NeoX, 0);
            Check(Same(Lighting.At(loop, 11, drive, 12), loop.End), "Loop holds end");
            Check(Same(Lighting.At(loop, 0, drive, 24), loop.Start), "Loop returns to start");
            Check(Same(Lighting.At(loop, 0, drive, 25), loop.Start), "Loop boundary continuous");
            s.RacingMode = ThemeMode.VerticalGradient;
            var gradient = new WheelTheme(s, WheelId.NeoX, 0);
            Check(Same(Lighting.At(gradient, 0, drive, 0), gradient.Start), "Gradient bottom");
            Check(Same(Lighting.At(gradient, 11, drive, 0), gradient.End), "Gradient top");
            Check(Same(Lighting.At(gradient, 11, drive, 123), gradient.End), "Racing gradient stays static over time");
            var idle = Telemetry.Preview(PreviewMode.IdleTheme);
            Check(!idle.Running, "Idle preview uses idle state");
            Check(Same(Lighting.At(gradient, 11, idle, 0), gradient.Start), "Idle uses animated mode independently");
            Check(!Same(Lighting.At(gradient, 0, idle, 2), gradient.Start), "Idle animation progresses");
            Check(Same(Lighting.At(gradient, 11, idle, 2), gradient.Start), "Idle animation follows physical rows");
            var playback = new ThemePlayback();
            playback.Apply(gradient, idle, 100);
            var moving = playback.Apply(gradient, idle, 102);
            Check(!Same(Lighting.At(gradient, 0, moving, 102), gradient.Start), "Playback advances from state entry");
            var racing = playback.Apply(gradient, drive, 103);
            Check(Same(Lighting.At(gradient, 11, racing, 103), gradient.End), "Entering racing immediately selects gradient");
            var stopped = playback.Apply(gradient, idle, 104);
            Check(Same(Lighting.At(gradient, 0, stopped, 104), gradient.Start), "Returning to idle restarts animation");
            playback.Apply(gradient, idle, 105);
            Check(playback.Apply(gradient, idle, 0).ThemeElapsedSeconds == 0, "Theme clock reset is handled");
            var swapped = s.Copy(); swapped.IdleMode = ThemeMode.Solid; swapped.RacingMode = ThemeMode.BottomToTopOnce;
            var swappedTheme = new WheelTheme(swapped, WheelId.NeoX, 0);
            Check(Same(Lighting.At(swappedTheme, 0, idle, 10), swappedTheme.Start), "Idle can independently select solid");
            playback.Apply(swappedTheme, drive, 200);
            var warning = playback.Apply(swappedTheme, Telemetry.Preview(PreviewMode.PitLimiter), 202);
            Check(warning.ThemeElapsedSeconds == 2, "Warning does not reset animation clock");
            var resumed = playback.Apply(swappedTheme, drive, 203);
            Check(!Same(Lighting.At(swappedTheme, 0, resumed, 203), swappedTheme.Start), "Racing animation resumes after warning");
            playback.Apply(swappedTheme, idle, 204);
            Check(playback.Apply(swappedTheme, drive, 205).ThemeElapsedSeconds == 0, "Racing once animation restarts on reentry");
            var replacement = new WheelTheme(swapped, WheelId.NeoX, 206);
            Check(playback.Apply(replacement, drive, 206).ThemeElapsedSeconds == 0, "New settings restart playback");
            swapped.IdleMode = (ThemeMode)99;
            Invalid(() => swapped.Validate(WheelId.NeoX), "Invalid idle mode rejected");
            swapped.IdleMode = ThemeMode.Solid; swapped.RacingMode = (ThemeMode)99;
            Invalid(() => swapped.Validate(WheelId.NeoX), "Invalid racing mode rejected");
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
            s = WheelSettings.Defaults(WheelId.NeoX); s.RevFlashHz = 0;
            Invalid(() => s.Validate(WheelId.NeoX), "Zero rev speed rejected");
            s.RevFlashHz = double.NaN;
            Invalid(() => s.Validate(WheelId.NeoX), "Invalid rev speed rejected");
            s = WheelSettings.Defaults(WheelId.FPro);
            Check(s.Heights(WheelId.FPro).Length == 18, "F-Pro channel count");
            Invalid(() => s.Validate(WheelId.NeoX), "Wrong wheel mapping rejected");
            var assistSettings = WheelSettings.Defaults(WheelId.NeoX);
            assistSettings.Brightness = 100; assistSettings.RacingMode = ThemeMode.Solid;
            var assistData = Telemetry.Preview(PreviewMode.Assists);
            Check(Same(Lighting.At(new WheelTheme(assistSettings, WheelId.NeoX, 0), 0, assistData, 0), Color.White), "Assist LEDs disabled by default");
            assistSettings.TractionControlLed = 1; assistSettings.AbsLed = 12;
            var assistTheme = new WheelTheme(assistSettings, WheelId.NeoX, 0);
            var pulses = new InterventionPlayback();
            var first = pulses.Apply(assistTheme, assistData, 10.075);
            Check(Same(Lighting.At(assistTheme, 0, first, 10.075), Color.Blue), "TC begins bright on its assigned LED");
            Check(Same(Lighting.At(assistTheme, 11, first, 10.075), Color.Red), "ABS begins bright on last assigned LED");
            Check(Same(Lighting.At(assistTheme, 1, first, 10.075), Color.White), "Unassigned LEDs retain theme");
            var judder = pulses.Apply(assistTheme, assistData, 10.135);
            Check(Same(Lighting.At(assistTheme, 0, judder, 10.135), Color.FromArgb(0, 0, 96)), "TC sustained intervention judders dim blue");
            Check(Same(Lighting.At(assistTheme, 11, judder, 10.135), Color.FromArgb(96, 0, 0)), "ABS sustained intervention judders dim red");
            var nextPulse = pulses.Apply(assistTheme, assistData, 10.185);
            Check(Same(Lighting.At(assistTheme, 0, nextPulse, 10.185), Color.Blue), "Sustained TC returns bright at 10 Hz");
            var clear = pulses.Apply(assistTheme, drive, 10.19);
            Check(Same(Lighting.At(assistTheme, 0, clear, 10.19), Color.White), "Intervention end immediately restores theme");
            var retriggerAssist = pulses.Apply(assistTheme, assistData, 10.195);
            Check(Same(Lighting.At(assistTheme, 11, retriggerAssist, 10.195), Color.Red), "New intervention retriggers bright immediately");
            pulses.Reset();
            pulses.Apply(assistTheme, Telemetry.Preview(PreviewMode.TractionControl), 20);
            var independent = pulses.Apply(assistTheme, assistData, 20.06);
            Check(Same(Lighting.At(assistTheme, 0, independent, 20.06), Color.FromArgb(0, 0, 96)), "TC keeps its own phase");
            Check(Same(Lighting.At(assistTheme, 11, independent, 20.06), Color.Red), "ABS starts independently of TC phase");
            assistData.PitLimiter = true; assistData.Redline = true;
            var priority = pulses.Apply(assistTheme, assistData, 20.07);
            Check(Lighting.At(assistTheme, 0, priority, 20.07).R == 0, "TC overrides global warnings on assigned LED");
            Check(Lighting.At(assistTheme, 11, priority, 20.07).B == 0, "ABS overrides pit limiter on assigned LED");
            Check(Same(Lighting.At(assistTheme, 1, priority, 20.07), Color.Blue), "Global warnings remain on other LEDs");
            assistSettings.AbsLed = 1;
            var shared = new WheelTheme(assistSettings, WheelId.NeoX, 0);
            Check(Same(Lighting.At(shared, 0, Telemetry.Preview(PreviewMode.Assists), 0), Color.Red), "ABS wins shared LED");
            Check(Same(Lighting.At(shared, 0, Telemetry.Preview(PreviewMode.TractionControl), 0), Color.Blue), "Shared LED still supports TC alone");
            assistData.Running = false;
            var exited = pulses.Apply(assistTheme, assistData, 20.08);
            Check(exited.TcPulseSeconds == null && exited.AbsPulseSeconds == null, "Game exit resets assist clocks");
            Check(Same(Lighting.At(assistTheme, 0, exited, 0), Color.White), "Stale assist telemetry ignored idle");
            Check(pulses.Apply(assistTheme, Telemetry.Preview(PreviewMode.Assists), 21).TcPulseSeconds == 0, "Game reentry starts bright");
            Check(pulses.Apply(assistTheme, Telemetry.Preview(PreviewMode.Assists), 0).TcPulseSeconds == 0, "Assist clock rewind resets");
            assistSettings.Brightness = 25;
            var dimAssist = new WheelTheme(assistSettings, WheelId.NeoX, 0);
            Check(Same(Lighting.At(dimAssist, 0, Telemetry.Preview(PreviewMode.Abs), 0.06), Color.FromArgb(24, 0, 0)), "Assist judder respects brightness");
            assistSettings.TractionControlLed = 13;
            Invalid(() => assistSettings.Validate(WheelId.NeoX), "Out-of-range TC LED rejected");
            assistSettings.TractionControlLed = 0; assistSettings.AbsLed = -1;
            Invalid(() => assistSettings.Validate(WheelId.NeoX), "Negative ABS LED rejected");
            var fproAssist = WheelSettings.Defaults(WheelId.FPro); fproAssist.AbsLed = 18; fproAssist.Brightness = 100;
            Check(Same(Lighting.At(new WheelTheme(fproAssist, WheelId.FPro, 0), 17, Telemetry.Preview(PreviewMode.Abs), 0), Color.Red), "F-Pro final channel supports ABS");
            var strip = new RevStripSettings { Brightness = 100 };
            var rpm = new RevStripTelemetry { Running = true, Percent = 69.9 };
            Check(Same(RevStripLighting.At(strip, 0, rpm), Color.Black), "Strip dark below buildup");
            rpm.Percent = 70;
            Check(Same(RevStripLighting.At(strip, 0, rpm), Color.Lime), "First strip LED at start threshold");
            Check(Same(RevStripLighting.At(strip, 1, rpm), Color.Black), "Strip fills sequentially");
            rpm.Percent = 96;
            Check(Same(RevStripLighting.At(strip, 2, rpm), Color.Lime), "Progressive green third");
            Check(Same(RevStripLighting.At(strip, 3, rpm), Color.Orange), "Progressive amber third");
            Check(Same(RevStripLighting.At(strip, 8, rpm), Color.Red), "Progressive red final third");
            rpm.Percent = 97;
            Check(Same(RevStripLighting.At(strip, 0, rpm), Color.Blue), "Shift cue starts at configured threshold");
            rpm.PulseSeconds = 0.1;
            Check(Same(RevStripLighting.At(strip, 8, rpm), Color.FromArgb(0, 0, 64)), "Shift cue dim phase remains blue");
            strip.Reverse = true; rpm.Percent = 70; rpm.PulseSeconds = 0;
            Check(Same(RevStripLighting.At(strip, 8, rpm), Color.Lime) && Same(RevStripLighting.At(strip, 0, rpm), Color.Black), "Strip reversal changes physical direction");
            strip.Reverse = false; strip.Mode = RevStripMode.Converging;
            Check(Same(RevStripLighting.At(strip, 0, rpm), Color.Lime) && Same(RevStripLighting.At(strip, 8, rpm), Color.Lime), "Converging starts at both ends");
            Check(Same(RevStripLighting.At(strip, 4, rpm), Color.Black), "Converging centre waits");
            rpm.Percent = 96;
            Check(Same(RevStripLighting.At(strip, 4, rpm), Color.Red), "Converging centre ends red");
            for (int i = 0; i < 4; i++) Check(Same(RevStripLighting.At(strip, i, rpm), RevStripLighting.At(strip, 8 - i, rpm)), "Converging symmetry " + i);
            strip.Mode = RevStripMode.Minimal; rpm.Percent = 86.9;
            Check(Same(RevStripLighting.At(strip, 0, rpm), Color.Black), "Minimal waits until late RPM");
            rpm.Percent = 87;
            Check(Same(RevStripLighting.At(strip, 0, rpm), Color.Orange), "Minimal begins amber");
            rpm.Percent = 97;
            Check(Same(RevStripLighting.At(strip, 8, rpm), Color.White), "Minimal shifts white");
            var stripPlayback = new RevStripPlayback();
            rpm.Redline = true;
            var stripRed = stripPlayback.Apply(strip, rpm, 10.1);
            Check(Same(RevStripLighting.At(strip, 0, stripRed), Color.Red), "Strip fresh redline starts bright");
            var stripDim = stripPlayback.Apply(strip, rpm, 10.2);
            Check(Same(RevStripLighting.At(strip, 8, stripDim), Color.FromArgb(96, 0, 0)), "Strip redline flashes dim red");
            rpm.Redline = false; rpm.Percent = 92;
            var stripHeld = stripPlayback.Apply(strip, rpm, 10.25);
            Check(stripHeld.RedlineHeld && Same(RevStripLighting.At(strip, 0, stripHeld), Color.FromArgb(96, 0, 0)), "Strip avoids theme leaks during redline dips");
            Check(!stripPlayback.Apply(strip, rpm, 10.51).Redline, "Strip redline hold expires");
            rpm.Percent = 101;
            Check(stripPlayback.Apply(strip, rpm, 11).Redline, "RPM percentage also detects redline");
            rpm.Running = false;
            Check(Same(RevStripLighting.At(strip, 0, stripPlayback.Apply(strip, rpm, 11.1)), Color.Black), "Game exit blanks strip immediately");
            rpm.Running = true; rpm.Percent = 80;
            Check(!stripPlayback.Apply(strip, rpm, 12).Redline, "New session has no old strip warning");
            foreach (double invalidPercent in new[] { double.NaN, double.PositiveInfinity, 0, -1 })
            {
                rpm.Percent = invalidPercent;
                Check(Same(RevStripLighting.At(strip, 0, stripPlayback.Apply(strip, rpm, 13)), Color.Black), "Invalid RPM blanks strip " + invalidPercent);
            }
            rpm.Redline = true;
            Check(Same(RevStripLighting.At(strip, 0, stripPlayback.Apply(strip, rpm, 14)), Color.Red), "SimHub redline still works without percentage");
            strip.Mode = RevStripMode.Off;
            Check(Same(RevStripLighting.At(strip, 0, stripPlayback.Apply(strip, rpm, 15)), Color.Black), "Off blanks warnings as well as buildup");
            strip.Mode = RevStripMode.Progressive; strip.Brightness = 25;
            Check(Same(RevStripLighting.At(strip, 0, stripPlayback.Apply(strip, rpm, 16)), Color.FromArgb(64, 0, 0)), "Strip has independent brightness");
            Check(stripPlayback.Apply(strip, rpm, 0).PulseSeconds == 0, "Strip handles clock rewind");
            var copySource = WheelSettings.Defaults(WheelId.NeoX); var copy = copySource.Copy(); copy.RevStrip.Mode = RevStripMode.Off;
            Check(copySource.RevStrip.Mode == RevStripMode.Progressive, "Strip snapshot deep copy");
            strip.StartPercent = 90; strip.ShiftPercent = 85;
            Invalid(() => strip.Validate(), "Reversed RPM thresholds rejected");
            strip = new RevStripSettings { ShiftPercent = double.NaN };
            Invalid(() => strip.Validate(), "Invalid shift threshold rejected");
            strip = new RevStripSettings { Mode = (RevStripMode)99 };
            Invalid(() => strip.Validate(), "Invalid strip mode rejected");
            var builtIns = ThemePresets.BuiltIns();
            Check(builtIns.Count == 3, "Three built-in palettes");
            Check(builtIns[0].Name == "Steel & Ice" && builtIns[0].Settings.StartColor == "#607580" && builtIns[0].Settings.EndColor == "#DCE6E8", "Steel palette values");
            Check(builtIns[1].Settings.StartColor == "#707070" && builtIns[2].Settings.EndColor == "#E0B878", "White and warm palette values");
            foreach (WheelId targetWheel in new[] { WheelId.NeoX, WheelId.FPro })
            {
                var target = WheelSettings.Defaults(targetWheel);
                target.TractionControlLed = WheelSettings.Count(targetWheel); target.AbsLed = 2;
                target.RevStrip.Reverse = true;
                target.Brightness = 80; target.FadeSeconds = 2; target.RevFlash = false;
                target.PitFlash = false; target.ShowWhenIdle = false; target.FallbackRedlineRpm = 9000;
                foreach (var preset in builtIns)
                {
                    var loaded = preset.ForWheel(target, targetWheel);
                    loaded.Validate(targetWheel);
                    Check(loaded.Brightness == 25 && loaded.FadeSeconds == 8 && loaded.ModeFor(false) == ThemeMode.BottomToTopRepeat && loaded.ModeFor(true) == ThemeMode.VerticalGradient, "Preset replaces theme preferences on " + targetWheel);
                    Check(loaded.RevFlash && loaded.PitFlash && loaded.ShowWhenIdle && loaded.FallbackRedlineRpm == 0 && loaded.RevFlashHz == 6 && loaded.FlashHz == 2, "Preset replaces effect settings on " + targetWheel);
                    Check(loaded.TractionControlLed == WheelSettings.Count(targetWheel) && loaded.AbsLed == 2 && loaded.BottomToTopRows == target.BottomToTopRows && loaded.RevStrip.Reverse, "Preset preserves physical calibration on " + targetWheel);
                    loaded.RevStrip.Mode = RevStripMode.Off;
                    Check(preset.Settings.RevStrip.Mode == RevStripMode.Progressive, "Preset copy is isolated");
                }
            }
            var customSettings = WheelSettings.Defaults(WheelId.FPro);
            customSettings.StartColor = "#123456"; customSettings.Brightness = 31;
            customSettings.RevFlash = false; customSettings.FlashHz = 4; customSettings.FallbackRedlineRpm = 7654;
            customSettings.RevStrip.Mode = RevStripMode.Minimal; customSettings.RevStrip.ShiftPercent = 95;
            var library = ThemePresets.Save(new System.Collections.Generic.List<ThemePreset>(), "  My Circuit  ", WheelId.FPro, customSettings, false);
            Check(library[0].Name == "My Circuit", "Custom name trimmed");
            var imported = library[0].ForWheel(WheelSettings.Defaults(WheelId.NeoX), WheelId.NeoX);
            Check(imported.StartColor == "#123456" && imported.Brightness == 31 && !imported.RevFlash && imported.FlashHz == 4 && imported.FallbackRedlineRpm == 7654 && imported.RevStrip.Mode == RevStripMode.Minimal && imported.RevStrip.ShiftPercent == 95, "Custom theme restores all lighting preferences across wheels");
            customSettings.StartColor = "#654321"; customSettings.RevStrip.Mode = RevStripMode.Converging;
            Check(library[0].Settings.StartColor == "#123456" && library[0].Settings.RevStrip.Mode == RevStripMode.Minimal, "Saving custom theme takes independent snapshot");
            Invalid(() => ThemePresets.Save(library, "my circuit", WheelId.FPro, customSettings, false), "Duplicate name rejected ignoring case");
            Invalid(() => ThemePresets.Save(library, "Clean White", WheelId.FPro, customSettings, false), "Built-in names protected");
            Invalid(() => ThemePresets.Save(library, "Missing", WheelId.FPro, customSettings, true), "Update requires existing custom theme");
            Invalid(() => ThemePresets.ValidateName("\n"), "Empty theme name rejected");
            Invalid(() => ThemePresets.ValidateName("bad\nname"), "Multiline theme name rejected");
            var updated = ThemePresets.Save(library, "My Circuit", WheelId.FPro, customSettings, true);
            Check(updated.Count == 1 && updated[0].Settings.StartColor == "#654321" && library[0].Settings.StartColor == "#123456", "Explicit update replaces only copied library");
            var verified = ThemePresets.ValidateLibrary(new ThemeLibrarySettings { Themes = updated });
            verified[0].Settings.RevStrip.Mode = RevStripMode.Off;
            Check(updated[0].Settings.RevStrip.Mode == RevStripMode.Converging, "Loaded library is independent");
            Invalid(() => ThemePresets.ValidateLibrary(new ThemeLibrarySettings { Version = 99 }), "Unsupported library version rejected");
            var identity = WheelSettings.Defaults(WheelId.NeoX);
            identity.TractionControlLed = 1; identity.AbsLed = 2; identity.Brightness = 100;
            identity.TcAlwaysBlue = true; identity.AbsAlwaysRed = true;
            identity.TcAdjustmentFlash = true; identity.AbsAdjustmentFlash = true;
            var identityTheme = new WheelTheme(identity, WheelId.NeoX, 0);
            Check(Same(Lighting.At(identityTheme, 0, drive, 0), Color.Blue), "TC identity overrides racing theme");
            Check(Same(Lighting.At(identityTheme, 1, idle, 0), Color.Red), "ABS identity overrides idle animation");
            Check(Same(Lighting.At(identityTheme, 1, Telemetry.Preview(PreviewMode.PitLimiter), 0), Color.Red), "Always-red stays red over global pit effect");
            var adjusting = drive; adjusting.TcAdjustmentSeconds = 0;
            Check(Same(Lighting.At(identityTheme, 0, adjusting, 0), Color.White), "First adjustment tick is white");
            adjusting.TcAdjustmentSeconds = 0.12;
            Check(Same(Lighting.At(identityTheme, 0, adjusting, 0), Color.Blue), "Adjustment gap uses assist colour");
            adjusting.TcAdjustmentSeconds = 0.2;
            Check(Same(Lighting.At(identityTheme, 0, adjusting, 0), Color.White), "Second adjustment tick is white");
            adjusting.TcAdjustmentSeconds = 0.4;
            Check(Same(Lighting.At(identityTheme, 0, adjusting, 0), Color.Blue), "Adjustment ends after 400ms");
            adjusting.AbsAdjustmentSeconds = 0.2;
            Check(Same(Lighting.At(identityTheme, 1, adjusting, 0), Color.White), "ABS adjustment ticks independently");
            adjusting.AbsActive = true; adjusting.AbsPulseSeconds = 0.06;
            Check(Same(Lighting.At(identityTheme, 1, adjusting, 0), Color.FromArgb(96, 0, 0)), "Actual intervention overrides white ticks and identity");
            identity.AbsAdjustmentFlash = false; identity.AbsAlwaysRed = false;
            var noFeedback = new WheelTheme(identity, WheelId.NeoX, 0);
            adjusting.AbsActive = false; adjusting.AbsAdjustmentSeconds = 0.12;
            Check(Same(Lighting.At(noFeedback, 1, adjusting, 0), Color.White), "Disabled ABS options return to theme");
            identity.ShowWhenIdle = false;
            Check(Same(Lighting.At(new WheelTheme(identity, WheelId.NeoX, 0), 0, idle, 0), Color.Black), "Idle-off still blanks always-on indicators");
            identity.AbsLed = 1; identity.AbsAlwaysRed = true;
            Check(Same(Lighting.At(new WheelTheme(identity, WheelId.NeoX, 0), 0, drive, 0), Color.Red), "ABS identity wins shared LED");
            identity.TractionControlLed = 0; identity.AbsLed = 0;
            Check(Same(Lighting.At(new WheelTheme(identity, WheelId.NeoX, 0), 0, adjusting, 0), Color.White), "Off LED disables identity and adjustment output");
            Check(!AdjustmentFeedback.IsActive(double.NaN) && !AdjustmentFeedback.IsActive(-1) && !AdjustmentFeedback.IsActive(double.PositiveInfinity), "Invalid adjustment times ignored");
            var changes = new AssistLevelChanges(); bool tcChange, absChange;
            changes.Update(true, "game|car", 3, 4, out tcChange, out absChange);
            Check(!tcChange && !absChange, "First level sample establishes baseline without flashing");
            changes.Update(true, "game|car", 3, 4, out tcChange, out absChange);
            Check(!tcChange && !absChange, "Unchanged levels do not flash");
            changes.Update(true, "game|car", 4, 4, out tcChange, out absChange);
            Check(tcChange && !absChange, "TC increment detected independently");
            changes.Update(true, "game|car", 4, 3, out tcChange, out absChange);
            Check(!tcChange && absChange, "ABS decrement detected independently");
            changes.Update(true, "other|car", 9, 9, out tcChange, out absChange);
            Check(!tcChange && !absChange, "New game/car does not falsely flash");
            changes.Update(true, "other|car", -1, -1, out tcChange, out absChange);
            Check(!tcChange && !absChange, "Unavailable levels do not falsely flash");
            changes.Update(true, "other|car", 2, 2, out tcChange, out absChange);
            Check(!tcChange && !absChange, "Restored telemetry rebaselines");
            changes.Update(false, null, 0, 0, out tcChange, out absChange);
            changes.Update(true, "other|car", 7, 7, out tcChange, out absChange);
            Check(!tcChange && !absChange, "Game exit resets level tracking");
            var savedFeedback = ThemePresets.Save(new System.Collections.Generic.List<ThemePreset>(), "Feedback", WheelId.NeoX, identity, false)[0].ForWheel(WheelSettings.Defaults(WheelId.FPro), WheelId.FPro);
            Check(savedFeedback.TcAlwaysBlue && savedFeedback.AbsAlwaysRed && savedFeedback.TcAdjustmentFlash && !savedFeedback.AbsAdjustmentFlash, "Custom presets carry feedback toggles to other wheel");
            Check((int)ThemeMode.Solid == 0 && (int)ThemeMode.BottomToTopRepeat == 2 && (int)ThemeMode.VerticalGradient == 3 && (int)ThemeMode.Pulse == 4, "Pulse preserves existing saved mode numbers");
            foreach (WheelId pulseWheel in new[] { WheelId.NeoX, WheelId.FPro })
            {
                var pulseSettings = WheelSettings.Defaults(pulseWheel);
                pulseSettings.IdleMode = pulseSettings.RacingMode = ThemeMode.Pulse;
                pulseSettings.StartColor = "#FF8000"; pulseSettings.EndColor = "#0080FF";
                pulseSettings.Brightness = 100; pulseSettings.FadeSeconds = 8;
                var pulseTheme = new WheelTheme(pulseSettings, pulseWheel, 0);
                foreach (var state in new[] { idle, drive })
                {
                    bool mixed = false, different = false, changingEvents = false, smooth = true;
                    var everLit = new bool[WheelSettings.Count(pulseWheel)];
                    var everDark = new bool[everLit.Length];
                    for (int sample = 0; sample < 800; sample++)
                    {
                        double time = sample * 0.1;
                        int lit = 0;
                        var firstColor = Lighting.At(pulseTheme, 0, state, time);
                        for (int channel = 0; channel < everLit.Length; channel++)
                        {
                            var c = Lighting.At(pulseTheme, channel, state, time);
                            var next = Lighting.At(pulseTheme, channel, state, time + 0.01);
                            bool isLit = !Same(c, Color.Black);
                            everLit[channel] |= isLit; everDark[channel] |= !isLit;
                            if (isLit) lit++;
                            different |= !Same(c, firstColor);
                            changingEvents |= !Same(c, Lighting.At(pulseTheme, channel, state, time + 8));
                            smooth &= Math.Abs(c.R - next.R) <= 4 && Math.Abs(c.G - next.G) <= 4 && Math.Abs(c.B - next.B) <= 4;
                        }
                        mixed |= lit > 0 && lit < everLit.Length;
                    }
                    Check(mixed && different, "Twinkles illuminate different buttons independently " + pulseWheel);
                    Check(changingEvents, "Twinkle choices change from event to event " + pulseWheel);
                    Check(smooth, "Twinkles fade smoothly, including event boundaries " + pulseWheel);
                    Check(Array.TrueForAll(everLit, x => x) && Array.TrueForAll(everDark, x => x), "Each button twinkles and rests " + pulseWheel);
                    Check(Same(Lighting.At(pulseTheme, 0, state, 17), Lighting.At(pulseTheme, 0, state, 17)), "Twinkle sampling deterministic per frame " + pulseWheel);
                }
                Check(Same(Lighting.At(pulseTheme, 0, Telemetry.Preview(PreviewMode.RevLimit), 0), Color.Red), "Redline remains bright over dark pulse trough");
                Check(Same(Lighting.At(pulseTheme, 0, Telemetry.Preview(PreviewMode.PitLimiter), 0), Color.Blue), "Pit effect remains bright over dark pulse trough");
                pulseSettings.TractionControlLed = 1; pulseSettings.TcAlwaysBlue = true;
                var pulseIdentity = new WheelTheme(pulseSettings, pulseWheel, 0);
                Check(Same(Lighting.At(pulseIdentity, 0, drive, 0), Color.Blue), "Always-colour indicator is not dimmed by pulse");
                pulseSettings.TcAlwaysBlue = false; pulseSettings.Brightness = 25;
                var dimPulse = new WheelTheme(pulseSettings, pulseWheel, 0);
                var dimSample = Lighting.At(dimPulse, 0, drive, 4);
                Check(dimSample.R <= 64 && dimSample.G <= 64 && dimSample.B <= 64, "Twinkle obeys brightness ceiling");
                pulseSettings.FadeSeconds = 1; pulseSettings.Brightness = 100;
                var slowPulse = new WheelTheme(pulseSettings, pulseWheel, 0);
                pulseSettings.FadeSeconds = 4;
                var minPulse = new WheelTheme(pulseSettings, pulseWheel, 0);
                Check(Same(Lighting.At(slowPulse, 0, drive, 2), Lighting.At(minPulse, 0, drive, 2)), "Twinkle minimum pace prevents rapid flashing");
                var pulseClock = new ThemePlayback();
                pulseClock.Apply(pulseTheme, idle, 100);
                Check(Same(Lighting.At(pulseTheme, 0, pulseClock.Apply(pulseTheme, idle, 104), 104), Lighting.At(pulseTheme, 0, idle, 4)), "Twinkle uses renderer playback clock");
                Check(Same(Lighting.At(pulseTheme, 0, pulseClock.Apply(pulseTheme, drive, 105), 105), Lighting.At(pulseTheme, 0, drive, 0)), "Switching idle/racing restarts twinkle playback");
            }
            Check((int)ThemeMode.Twinkle == 5 && (int)ThemeMode.Pulse == 4, "Twinkle appends a mode without changing saved Pulse themes");
            foreach (WheelId wheel in new[] { WheelId.NeoX, WheelId.FPro })
            foreach (double duration in new[] { 4.0, 8.0, 30.0 })
            {
                var settings = WheelSettings.Defaults(wheel);
                settings.IdleMode = settings.RacingMode = ThemeMode.Twinkle;
                settings.StartColor = settings.EndColor = "#FFFFFF";
                settings.Brightness = 100; settings.FadeSeconds = duration;
                settings.Validate(wheel);
                var twinkleTheme = new WheelTheme(settings, wheel, 0);
                foreach (var state in new[] { idle, drive })
                {
                    bool dense = true, smooth = true, staggered = false;
                    int count = WheelSettings.Count(wheel);
                    var lows = new bool[count]; var highs = new bool[count];
                    for (int sample = 0; sample < 2400; sample++)
                    {
                        double time = sample * 0.05;
                        int visible = 0;
                        for (int led = 0; led < count; led++)
                        {
                            var c = Lighting.At(twinkleTheme, led, state, time);
                            if (c.R >= 127) visible++;
                            lows[led] |= c.R < 26; highs[led] |= c.R > 229;
                            smooth &= Math.Abs(c.R - Lighting.At(twinkleTheme, led, state, time + 0.01).R) <= 4;
                            if (led >= 2) staggered |= Math.Abs(c.R - Lighting.At(twinkleTheme, 0, state, time).R) > 50;
                        }
                        dense &= visible >= count / 2;
                    }
                    Check(dense, "Twinkle keeps at least half visibly lit at every sampled frame " + wheel + "/" + duration);
                    Check(smooth && staggered, "Twinkle is smooth and staggered " + wheel + "/" + duration);
                    Check(Array.TrueForAll(lows, x => x) && Array.TrueForAll(highs, x => x), "Every Twinkle LED brightens and dims " + wheel);
                }
                settings.StartColor = "#FF8000"; settings.EndColor = "#0080FF"; settings.Brightness = 25;
                twinkleTheme = new WheelTheme(settings, wheel, 0);
                bool smoothColour = true, bounded = true;
                for (int sample = 0; sample < 2400; sample++)
                for (int led = 0; led < WheelSettings.Count(wheel); led++)
                {
                    var c = Lighting.At(twinkleTheme, led, drive, sample * 0.05);
                    var next = Lighting.At(twinkleTheme, led, drive, sample * 0.05 + 0.01);
                    bounded &= c.R <= 64 && c.G <= 64 && c.B <= 64;
                    smoothColour &= Math.Abs(c.R - next.R) <= 2 && Math.Abs(c.G - next.G) <= 2 && Math.Abs(c.B - next.B) <= 2;
                }
                Check(bounded && smoothColour, "Twinkle honours brightness and blends colours across cycles " + wheel);
                Check(Same(Lighting.At(twinkleTheme, 0, Telemetry.Preview(PreviewMode.PitLimiter), 0), Color.FromArgb(0, 0, 64)), "Pit warning overrides Twinkle");
                Check(Same(Lighting.At(twinkleTheme, 0, Telemetry.Preview(PreviewMode.RevLimit), 0), Color.FromArgb(64, 0, 0)), "Rev warning overrides Twinkle");
                var clock = new ThemePlayback();
                clock.Apply(twinkleTheme, idle, 100);
                Check(Same(Lighting.At(twinkleTheme, 0, clock.Apply(twinkleTheme, idle, 104), 104), Lighting.At(twinkleTheme, 0, idle, 4)), "Twinkle follows playback clock");
            }
            var oneColor = new RevStripSettings { ColorMode = RevStripColorMode.SingleColor, SingleColor = "#40C080", Brightness = 100 };
            var chosen = WheelSettings.ParseColor(oneColor.SingleColor);
            foreach (var pattern in new[] { RevStripMode.Progressive, RevStripMode.Converging, RevStripMode.Minimal })
            {
                oneColor.Mode = pattern;
                var stripData = new RevStripTelemetry { Running = true, Percent = 96 };
                for (int channel = 0; channel < 9; channel++) Check(Same(RevStripLighting.At(oneColor, channel, stripData), chosen), "Single colour fills active LEDs in " + pattern);
                stripData.Percent = 98;
                Check(Same(RevStripLighting.At(oneColor, 0, stripData), chosen), "Single-colour shift cue stays chosen colour");
                stripData.PulseSeconds = 0.1;
                Check(Same(RevStripLighting.At(oneColor, 0, stripData), Lighting.Dim(chosen, 64.0 / 255)), "Single-colour dim shift cue");
                stripData.Redline = true; stripData.PulseSeconds = 0;
                Check(Same(RevStripLighting.At(oneColor, 0, stripData), chosen), "Single-colour limiter stays chosen colour");
                stripData.RedlineHeld = true;
                Check(Same(RevStripLighting.At(oneColor, 0, stripData), Lighting.Dim(chosen, 96.0 / 255)), "Held limiter dims chosen colour");
                stripData.Running = false;
                Check(Same(RevStripLighting.At(oneColor, 0, stripData), Color.Black), "Single-colour idle remains dark");
            }
            oneColor.ColorMode = (RevStripColorMode)99;
            Invalid(() => oneColor.Validate(), "Invalid strip colour mode rejected");
            oneColor.ColorMode = RevStripColorMode.SingleColor; oneColor.SingleColor = "bad";
            Invalid(() => oneColor.Validate(), "Invalid strip custom colour rejected");
            Console.WriteLine("PASS: " + assertions + " lighting assertions."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); return 1; }
    }
}

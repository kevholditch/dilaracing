using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class PreviewProgram
{
    private static string simHubPath;
    [STAThread]
    public static int Main(string[] args)
    {
        simHubPath = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\SimHub";
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try { Run(args.Length > 1 ? args[1] : "preview.png"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        string path = Path.Combine(simHubPath, name);
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string output)
    {
        var legacySettings = DilaRacing.WheelSettings.Defaults(DilaRacing.WheelId.NeoX);
        legacySettings.StartColor = "#11AA33"; legacySettings.FlashHz = 3.5;
        legacySettings.Mode = DilaRacing.ThemeMode.BottomToTopRepeat;
        legacySettings.BottomToTopRows = "10,9,5,4\n3,6\n2,7\n1,8,11,12";
        var legacyJson = Newtonsoft.Json.Linq.JObject.FromObject(legacySettings);
        legacyJson.Remove("RevFlashHz");
        legacyJson.Remove("IdleMode"); legacyJson.Remove("RacingMode");
        legacyJson.Remove("TractionControlLed"); legacyJson.Remove("AbsLed");
        legacyJson.Remove("RevStrip");
        legacyJson.Remove("TcAlwaysBlue"); legacyJson.Remove("AbsAlwaysRed");
        legacyJson.Remove("TcAdjustmentFlash"); legacyJson.Remove("AbsAdjustmentFlash");
        var migrated = legacyJson.ToObject<DilaRacing.WheelSettings>();
        migrated.Validate(DilaRacing.WheelId.NeoX);
        if (migrated.TcAlwaysBlue || migrated.AbsAlwaysRed || migrated.TcAdjustmentFlash || migrated.AbsAdjustmentFlash)
            throw new Exception("New assist feedback options must default off in older settings.");
        if (migrated.RevStrip.Mode != DilaRacing.RevStripMode.Progressive || migrated.RevStrip.ShiftPercent != 97)
            throw new Exception("Legacy settings must gain default strip settings.");
        if (migrated.RevStrip.ColorMode != DilaRacing.RevStripColorMode.GreenAmberRed || migrated.RevStrip.SingleColor != "#FFFFFF")
            throw new Exception("Legacy rev settings must retain the multicolour scheme.");
        if (migrated.TractionControlLed != 0 || migrated.AbsLed != 0)
            throw new Exception("Older settings must leave intervention indicators disabled.");
        if (migrated.RevFlashHz != 6 || migrated.FlashHz != 3.5 || migrated.StartColor != "#11AA33")
            throw new Exception("Older settings must gain the new rev speed without changing saved colours/pit speed.");
        // Exercise the enclosing settings object too: Json.NET can populate
        // constructor-created nested objects instead of creating new ones.
        var envelope = new Newtonsoft.Json.Linq.JObject();
        envelope["Version"] = 1; envelope["NeoX"] = legacyJson;
        var migratedPlugin = envelope.ToObject<DilaRacing.PluginSettings>();
        if (migratedPlugin.NeoX.ModeFor(false) != DilaRacing.ThemeMode.BottomToTopRepeat ||
            migratedPlugin.NeoX.ModeFor(true) != DilaRacing.ThemeMode.VerticalGradient ||
            migratedPlugin.NeoX.BottomToTopRows != legacySettings.BottomToTopRows)
            throw new Exception("Legacy animation/layout must migrate to animated idle and static racing.");
        legacyJson["Mode"] = (int)DilaRacing.ThemeMode.BottomToTopOnce;
        if (envelope.ToObject<DilaRacing.PluginSettings>().NeoX.ModeFor(false) != DilaRacing.ThemeMode.BottomToTopOnce)
            throw new Exception("Legacy once animation must not be replaced by new defaults.");
        migrated.RevFlashHz = 8.5;
        migrated.IdleMode = DilaRacing.ThemeMode.Solid;
        migrated.RacingMode = DilaRacing.ThemeMode.BottomToTopRepeat;
        migrated.TractionControlLed = 12; migrated.AbsLed = 3;
        migrated.RevStrip.Mode = DilaRacing.RevStripMode.Converging; migrated.RevStrip.ShiftPercent = 96;
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<DilaRacing.WheelSettings>(Newtonsoft.Json.JsonConvert.SerializeObject(migrated));
        if (restored.RevFlashHz != 8.5) throw new Exception("Custom rev speed must survive settings serialization.");
        if (restored.RevStrip.Mode != DilaRacing.RevStripMode.Converging || restored.RevStrip.ShiftPercent != 96)
            throw new Exception("Rev strip mode and thresholds must persist.");
        if (restored.TractionControlLed != 12 || restored.AbsLed != 3)
            throw new Exception("Intervention LED assignments must survive settings serialization.");
        if (restored.ModeFor(false) != DilaRacing.ThemeMode.Solid || restored.ModeFor(true) != DilaRacing.ThemeMode.BottomToTopRepeat ||
            restored.BottomToTopRows != legacySettings.BottomToTopRows)
            throw new Exception("Both theme choices and the layout must survive settings serialization.");
        Console.WriteLine("PASS: legacy settings migration, independent modes, layout and rev-speed persistence.");
        var application = new Application();
        var plugin = new DilaRacing.DilaPlugin();
        var effect = new DilaRacing.WheelEffect();
        Console.WriteLine("Effect ContainerType: " + effect.ContainerType);
        DilaRacing.DilaPlugin.Current = plugin;
        var input = new SimHub.Plugins.DataPlugins.RGBDriverCommon.TestLedsGameData { GameRunning = true, PitLimiterOn = true, RPMRedlineReached = true };
        var result = new SimHub.Plugins.DataPlugins.RGBDriver.LedResult(new WoteverCommon.HSLPreference());
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        var color = result[0];
        if (!((color.B > 0 && color.R == 0 && color.G == 0) || (color.R > 0 && color.R == color.G && color.G == color.B)))
            throw new Exception("SDK test data did not produce a blue/white limiter frame.");
        input.PitLimiterOn = false;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        input.RPMRedlineReached = false;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].R == 0 || result[0].G != 0 || result[0].B != 0)
            throw new Exception("A brief redline signal gap should retain the red warning.");
        // Install a configured snapshot only into this isolated test instance;
        // calling Apply would write actual SimHub settings from the smoke test.
        var assistSettings = DilaRacing.WheelSettings.Defaults(DilaRacing.WheelId.NeoX);
        assistSettings.TractionControlLed = 1; assistSettings.AbsLed = 12;
        assistSettings.RacingMode = DilaRacing.ThemeMode.Solid; assistSettings.Brightness = 100;
        typeof(DilaRacing.DilaPlugin).GetField("neo", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(plugin, new DilaRacing.WheelTheme(assistSettings, DilaRacing.WheelId.NeoX, DilaRacing.DilaPlugin.Now));
        input.TCEnabled = true; input.AbsEnabled = true;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].ToArgb() != System.Drawing.Color.White.ToArgb() || result[11].ToArgb() != System.Drawing.Color.White.ToArgb())
            throw new Exception("Enabled assists must not be treated as active interventions.");
        input.TCActive = true; input.AbsActive = true;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].ToArgb() != System.Drawing.Color.Blue.ToArgb() || result[11].ToArgb() != System.Drawing.Color.Red.ToArgb() || result[1].ToArgb() != System.Drawing.Color.White.ToArgb())
            throw new Exception("SDK active intervention signals must light only the assigned buttons.");
        input.TCActive = false; input.AbsActive = false;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].ToArgb() != System.Drawing.Color.White.ToArgb() || result[11].ToArgb() != System.Drawing.Color.White.ToArgb())
            throw new Exception("Clearing SDK interventions must restore the theme.");
        plugin.BeginTest(DilaRacing.WheelId.NeoX, DilaRacing.PreviewMode.Assists, 0);
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].B == 0 || result[0].R != 0 || result[11].R == 0 || result[11].B != 0)
            throw new Exception("Hardware assist test must simulate both intervention signals.");
        plugin.StopTest();
        Console.WriteLine("PASS: SDK TC/ABS active vs enabled, assigned channels, clearing and hardware-test routing.");
        var stripEffect = new DilaRacing.RevStripEffect();
        var stripInput = new SimHub.Plugins.DataPlugins.RGBDriverCommon.LedsGameData { GameRunning = true, RPMToRedlinePercent = 70 };
        var stripResult = new SimHub.Plugins.DataPlugins.RGBDriver.LedResult(new WoteverCommon.HSLPreference());
        stripEffect.SetResultBase(stripInput, stripResult, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (stripResult[0].G == 0 || stripResult[0].R != 0 || stripResult[8].G != 0)
            throw new Exception("SDK RPM percentage must drive the dedicated strip buildup.");
        stripInput.RPMToRedlinePercent = 98;
        stripEffect.SetResultBase(stripInput, stripResult, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (stripResult[8].B == 0 || stripResult[8].R != 0) throw new Exception("SDK shift cue must fill all nine LEDs blue.");
        stripInput.RPMRedlineReached = true;
        stripEffect.SetResultBase(stripInput, stripResult, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (stripResult[8].R == 0 || stripResult[8].B != 0) throw new Exception("SDK redline must override strip shift cue.");
        stripInput.GameRunning = false;
        stripEffect.SetResultBase(stripInput, stripResult, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (stripResult[8].R != 0) throw new Exception("Idle must blank the strip even with stale redline.");
        plugin.BeginRevStripTest();
        if (plugin.ActiveRevStripTest() == null || plugin.ActiveTest(DilaRacing.WheelId.NeoX) != null)
            throw new Exception("Rev strip test must be independent of button tests.");
        // Move this isolated test's start time to the final redline phase.
        typeof(DilaRacing.DilaPlugin).GetField("revStripTest", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(plugin, new DilaRacing.HardwareTest(DilaRacing.WheelId.NeoX, DilaRacing.PreviewMode.Theme, 0, DilaRacing.DilaPlugin.Now + 1));
        stripEffect.SetResultBase(stripInput, stripResult, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (stripResult[0].R == 0 || stripResult[0].B != 0) throw new Exception("Strip hardware test must run without a game.");
        plugin.StopTest();
        if (plugin.ActiveRevStripTest() != null) throw new Exception("Stop must cancel strip tests.");
        stripEffect.SetResultBase(stripInput, stripResult, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (stripResult[0].R != 0) throw new Exception("Stopped strip test must return to live idle state.");
        var profilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dila Racing - Neo X Rev Strip.ledsprofile");
        var stripProfile = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(profilePath));
        if ((string)stripProfile["LedContainers"][0]["ContainerType"] != stripEffect.ContainerType ||
            (int)stripProfile["LedContainers"][0]["StartPosition"] != 1)
            throw new Exception("Packaged strip profile must reference the correct effect and start channel.");
        Console.WriteLine("PASS: SDK rev strip RPM mapping, shift/redline/idle, independent hardware test and packaged profile.");
        effect.Wheel = DilaRacing.WheelId.FPro;
        input.GameRunning = false;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[17].R == 0 || result[17].R != result[17].B) throw new Exception("F-Pro did not return to its idle theme.");
        plugin.BeginTest(DilaRacing.WheelId.FPro, DilaRacing.PreviewMode.Identify, 17);
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].R != 0 || result[17].R == 0) throw new Exception("Identify did not isolate the selected channel.");
        plugin.StopTest();
        DilaRacing.DilaPlugin.Current = null;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[17].R != 0) throw new Exception("Unloaded plugin should output black.");
        Console.WriteLine("PASS: SDK effect smoke checks (limiter priority, redline hold, F-Pro length, idle, identify, unload).");
        var feedbackPlugin = new DilaRacing.DilaPlugin();
        var feedbackSettings = DilaRacing.WheelSettings.Defaults(DilaRacing.WheelId.NeoX);
        feedbackSettings.Brightness = 100; feedbackSettings.TractionControlLed = 1; feedbackSettings.AbsLed = 2;
        feedbackSettings.TcAlwaysBlue = feedbackSettings.AbsAlwaysRed = true;
        feedbackSettings.TcAdjustmentFlash = feedbackSettings.AbsAdjustmentFlash = true;
        typeof(DilaRacing.DilaPlugin).GetField("neo", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(feedbackPlugin, new DilaRacing.WheelTheme(feedbackSettings, DilaRacing.WheelId.NeoX, 0));
        // SimHub's GameData/status objects are read-only. Exercise the exact
        // normalised-level handler used by DataUpdate without changing real telemetry.
        var updateLevels = typeof(DilaRacing.DilaPlugin).GetMethod("UpdateAssistLevels", BindingFlags.Instance | BindingFlags.NonPublic);
        updateLevels.Invoke(feedbackPlugin, new object[] { true, "Test|Car", 3, 4, DilaRacing.DilaPlugin.Now });
        var feedbackInput = new DilaRacing.Telemetry { Running = true };
        if (feedbackPlugin.WithAdjustmentFeedback(DilaRacing.WheelId.NeoX, feedbackInput, DilaRacing.DilaPlugin.Now).TcAdjustmentSeconds.HasValue)
            throw new Exception("First SDK level sample must not flash.");
        updateLevels.Invoke(feedbackPlugin, new object[] { true, "Test|Car", 4, 4, DilaRacing.DilaPlugin.Now });
        var changedLevels = feedbackPlugin.WithAdjustmentFeedback(DilaRacing.WheelId.NeoX, feedbackInput, DilaRacing.DilaPlugin.Now);
        if (!changedLevels.TcAdjustmentSeconds.HasValue || changedLevels.AbsAdjustmentSeconds.HasValue)
            throw new Exception("Normalised SDK TC changes must trigger feedback before diagnostic throttle.");
        updateLevels.Invoke(feedbackPlugin, new object[] { true, "Test|Car", 4, 5, DilaRacing.DilaPlugin.Now });
        if (!feedbackPlugin.WithAdjustmentFeedback(DilaRacing.WheelId.FPro, feedbackInput, DilaRacing.DilaPlugin.Now).AbsAdjustmentSeconds.HasValue)
            throw new Exception("Game setting changes must be visible to either wheel.");
        updateLevels.Invoke(feedbackPlugin, new object[] { true, "Test|Other car", 4, 5, DilaRacing.DilaPlugin.Now });
        if (feedbackPlugin.WithAdjustmentFeedback(DilaRacing.WheelId.NeoX, feedbackInput, DilaRacing.DilaPlugin.Now).TcAdjustmentSeconds.HasValue)
            throw new Exception("Game/car change must clear stale level pulses.");
        feedbackPlugin.NotifyAdjustment(DilaRacing.WheelId.NeoX, false);
        if (!feedbackPlugin.WithAdjustmentFeedback(DilaRacing.WheelId.NeoX, new DilaRacing.Telemetry(), DilaRacing.DilaPlugin.Now).TcAdjustmentSeconds.HasValue ||
            feedbackPlugin.WithAdjustmentFeedback(DilaRacing.WheelId.FPro, feedbackInput, DilaRacing.DilaPlugin.Now).TcAdjustmentSeconds.HasValue)
            throw new Exception("Direct rotary actions must work idle and only affect their assigned wheel.");
        if (feedbackPlugin.WithAdjustmentFeedback(DilaRacing.WheelId.NeoX, feedbackInput, DilaRacing.DilaPlugin.Now + 1).TcAdjustmentSeconds.HasValue)
            throw new Exception("Adjustment feedback must expire.");
        DilaRacing.DilaPlugin.Current = feedbackPlugin;
        var feedbackEffect = new DilaRacing.WheelEffect();
        var editorInput = new SimHub.Plugins.DataPlugins.RGBDriverCommon.TestLedsGameData { GameRunning = true };
        feedbackEffect.SetResultBase(editorInput, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].ToArgb() != System.Drawing.Color.Blue.ToArgb() || result[1].ToArgb() != System.Drawing.Color.Red.ToArgb())
            throw new Exception("LED editor data must ignore live adjustment events but retain colour overrides.");
        feedbackPlugin.BeginTest(DilaRacing.WheelId.NeoX, DilaRacing.PreviewMode.TcAdjustment, 0);
        feedbackEffect.SetResultBase(editorInput, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].ToArgb() != System.Drawing.Color.White.ToArgb()) throw new Exception("Hardware adjustment preview must produce a white tick.");
        feedbackPlugin.StopTest(); DilaRacing.DilaPlugin.Current = null;
        Console.WriteLine("PASS: SDK level-change routing, no initial flash, rotary action targeting/expiry, idle colour overrides and editor isolation.");
        var customSettings = DilaRacing.WheelSettings.Defaults(DilaRacing.WheelId.FPro);
        customSettings.StartColor = "#123456"; customSettings.Brightness = 31;
        customSettings.FadeSeconds = 9; customSettings.RevFlash = false;
        customSettings.RevStrip.Mode = DilaRacing.RevStripMode.Minimal;
        customSettings.RevStrip.ColorMode = DilaRacing.RevStripColorMode.SingleColor;
        customSettings.RevStrip.SingleColor = "#40C080";
        customSettings.TcAlwaysBlue = customSettings.AbsAlwaysRed = true;
        customSettings.TcAdjustmentFlash = customSettings.AbsAdjustmentFlash = true;
        customSettings.IdleMode = customSettings.RacingMode = DilaRacing.ThemeMode.Pulse;
        var library = new DilaRacing.ThemeLibrarySettings { Themes = DilaRacing.ThemePresets.Save(new List<DilaRacing.ThemePreset>(), "Circuit Night", DilaRacing.WheelId.FPro, customSettings, false) };
        var libraryJson = Newtonsoft.Json.JsonConvert.SerializeObject(library);
        var loadedLibrary = DilaRacing.ThemePresets.ValidateLibrary(Newtonsoft.Json.JsonConvert.DeserializeObject<DilaRacing.ThemeLibrarySettings>(libraryJson));
        if (loadedLibrary[0].Settings.StartColor != "#123456" || loadedLibrary[0].Settings.RevStrip.Mode != DilaRacing.RevStripMode.Minimal)
            throw new Exception("Custom theme library must survive restart serialization.");
        if (loadedLibrary[0].Settings.RevStrip.ColorMode != DilaRacing.RevStripColorMode.SingleColor || loadedLibrary[0].Settings.RevStrip.SingleColor != "#40C080")
            throw new Exception("Custom strip colour and scheme must survive serialization.");
        if (loadedLibrary[0].Settings.ModeFor(false) != DilaRacing.ThemeMode.Pulse || loadedLibrary[0].Settings.ModeFor(true) != DilaRacing.ThemeMode.Pulse)
            throw new Exception("Pulse must persist for both idle and racing in custom themes.");
        typeof(DilaRacing.DilaPlugin).GetField("customThemes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plugin, loadedLibrary);
        var settings = new DilaRacing.SettingsControl(plugin);
        var tabs = ((StackPanel)((ScrollViewer)settings.Content).Content).Children.OfType<TabControl>().Single();
        for (int wheelIndex = 0; wheelIndex < 2; wheelIndex++)
        {
            var editor = ((TabItem)tabs.Items[wheelIndex]).Content;
            var choice = (ComboBox)editor.GetType().GetField("presetChoice", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            var read = editor.GetType().GetMethod("Read", BindingFlags.Instance | BindingFlags.NonPublic);
            choice.SelectedItem = choice.Items.OfType<DilaRacing.ThemePreset>().Single(x => x.Name == "Circuit Night");
            var draft = (DilaRacing.WheelSettings)read.Invoke(editor, null);
            if (draft.StartColor != "#123456" || draft.Brightness != 31 || draft.FadeSeconds != 9 || draft.RevFlash || draft.RevStrip.Mode != DilaRacing.RevStripMode.Minimal)
                throw new Exception("Custom theme selection must populate all controls on wheel " + wheelIndex);
            if (!draft.TcAlwaysBlue || !draft.AbsAlwaysRed || !draft.TcAdjustmentFlash || !draft.AbsAdjustmentFlash)
                throw new Exception("Feedback checkboxes must persist and load on both wheel tabs.");
            if (draft.ModeFor(false) != DilaRacing.ThemeMode.Pulse || draft.ModeFor(true) != DilaRacing.ThemeMode.Pulse)
                throw new Exception("Pulse custom theme must load into both mode selectors on each wheel.");
            if (draft.RevStrip.ColorMode != DilaRacing.RevStripColorMode.SingleColor || draft.RevStrip.SingleColor != "#40C080")
                throw new Exception("Custom strip colour must load in the Neo X editor and remain portable through F-Pro themes.");
            choice.SelectedItem = choice.Items.OfType<DilaRacing.ThemePreset>().Single(x => x.Name == "Steel & Ice");
            draft = (DilaRacing.WheelSettings)read.Invoke(editor, null);
            var active = plugin.Theme((DilaRacing.WheelId)wheelIndex).Settings;
            if (draft.StartColor != "#607580" || draft.EndColor != "#DCE6E8" || draft.Brightness != 25 || draft.FadeSeconds != 8 ||
                !draft.RevFlash || draft.RevStrip.Mode != DilaRacing.RevStripMode.Progressive ||
                draft.BottomToTopRows != active.BottomToTopRows || draft.TractionControlLed != active.TractionControlLed || draft.AbsLed != active.AbsLed)
                throw new Exception("Built-in preset must fill both wheel editors while preserving mappings.");
            if (active.StartColor == "#607580") throw new Exception("Preset selection must remain a draft until Save & apply.");
            if (draft.TcAlwaysBlue || draft.AbsAlwaysRed || draft.TcAdjustmentFlash || draft.AbsAdjustmentFlash)
                throw new Exception("Built-in theme selection must populate feedback checkboxes as well.");
            if (draft.RevStrip.ColorMode != DilaRacing.RevStripColorMode.GreenAmberRed)
                throw new Exception("Built-in theme must restore the default strip colour scheme.");
            foreach (string selector in new[] { "idleMode", "racingMode" })
            {
                var modeChoice = (ComboBox)editor.GetType().GetField(selector, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                if (!modeChoice.Items[(int)DilaRacing.ThemeMode.Pulse].ToString().StartsWith("Pulse"))
                    throw new Exception("Pulse needs a labelled option on both wheel mode selectors.");
                modeChoice.SelectedIndex = (int)DilaRacing.ThemeMode.Pulse;
            }
            draft = (DilaRacing.WheelSettings)read.Invoke(editor, null);
            if (draft.ModeFor(false) != DilaRacing.ThemeMode.Pulse || draft.ModeFor(true) != DilaRacing.ThemeMode.Pulse)
                throw new Exception("Selecting Pulse must read back correctly on both wheels.");
        }
        Console.WriteLine("PASS: theme library serialization and WPF preset selection on both wheels, all preferences, physical mapping and draft isolation.");
        Console.WriteLine("PASS: Pulse mode persistence and both idle/racing selectors on both wheels.");
        settings.Measure(new Size(1000, 2700));
        settings.Arrange(new Rect(0, 0, 1000, 2700));
        settings.UpdateLayout();
        var target = new RenderTargetBitmap(1000, 2700, 96, 96, PixelFormats.Pbgra32);
        target.Render(settings);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
        using (var stream = File.Create(output)) encoder.Save(stream);
        Console.WriteLine("Rendered: " + output);
        application.Shutdown();
    }
}

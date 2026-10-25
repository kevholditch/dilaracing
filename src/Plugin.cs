using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;

namespace DilaRacing
{
    internal sealed class AdjustmentPulse
    {
        public readonly double At;
        public AdjustmentPulse(double at) { At = at; }
    }
    public sealed class PluginSettings
    {
        public int Version { get; set; }
        public WheelSettings NeoX { get; set; }
        public WheelSettings FPro { get; set; }
        // Leave wheels unset so Json.NET creates each saved wheel independently.
        // Pre-populating new defaults would hide absent fields in older settings.
        public PluginSettings() { Version = 1; }
    }

    public sealed class HardwareTest
    {
        public readonly WheelId Wheel;
        public readonly PreviewMode Mode;
        public readonly int Led;
        public readonly double EndsAt;
        public HardwareTest(WheelId wheel, PreviewMode mode, int led, double endsAt)
        { Wheel = wheel; Mode = mode; Led = led; EndsAt = endsAt; }
    }

    [PluginName("Dila Racing")]
    [PluginAuthor("Dila Racing")]
    [PluginDescription("Themes and telemetry lighting for Simagic Neo X and Cube Controls F-Pro.")]
    public sealed class DilaPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        public static DilaPlugin Current;
        private volatile WheelTheme neo;
        private volatile WheelTheme fpro;
        private volatile HardwareTest test;
        private volatile HardwareTest revStripTest;
        private volatile string diagnostic = "No game data received. Use SimHub's LED test-data editor to test effects.";
        private volatile string loadWarning = "";
        private double nextDiagnosticUpdate;
        private List<ThemePreset> customThemes = new List<ThemePreset>();
        private string themeLibraryError = "";
        private readonly AssistLevelChanges levelChanges = new AssistLevelChanges();
        private string levelContext;
        private volatile AdjustmentPulse neoTc, neoAbs, fproTc, fproAbs, sharedTc, sharedAbs;
        public int ThemeLibraryRevision { get; private set; }
        public PluginManager PluginManager { get; set; }
        public string LeftMenuTitle { get { return "Dila Racing"; } }
        public ImageSource PictureIcon { get { return null; } }
        public static double Now { get { return Clock.Elapsed.TotalSeconds; } }
        public string Diagnostic { get { return diagnostic; } }
        public string LoadWarning { get { return loadWarning; } }

        public DilaPlugin()
        {
            neo = new WheelTheme(WheelSettings.Defaults(WheelId.NeoX), WheelId.NeoX, Now);
            fpro = new WheelTheme(WheelSettings.Defaults(WheelId.FPro), WheelId.FPro, Now);
        }

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;
            try
            {
                var saved = this.ReadCommonSettings<PluginSettings>("Settings", () => new PluginSettings());
                if (saved == null || saved.Version != 1) throw new ArgumentException("Unsupported settings version.");
                var newNeo = new WheelTheme(saved.NeoX ?? WheelSettings.Defaults(WheelId.NeoX), WheelId.NeoX, Now);
                var newFpro = new WheelTheme(saved.FPro ?? WheelSettings.Defaults(WheelId.FPro), WheelId.FPro, Now);
                neo = newNeo; fpro = newFpro;
            }
            catch (Exception error)
            {
                loadWarning = "Could not load settings; using defaults. " + error.Message;
                SimHub.Logging.Current.Warn("Dila Racing: " + loadWarning);
            }
            try
            {
                customThemes = ThemePresets.ValidateLibrary(this.ReadCommonSettings<ThemeLibrarySettings>("Themes", () => new ThemeLibrarySettings()));
            }
            catch (Exception error)
            {
                themeLibraryError = "Could not load custom themes: " + error.Message;
                loadWarning += "\n" + themeLibraryError;
                SimHub.Logging.Current.Warn("Dila Racing: " + themeLibraryError);
            }
            this.AddAction("NeoXTcAdjusted", (a, b) => NotifyAdjustment(WheelId.NeoX, false));
            this.AddAction("NeoXAbsAdjusted", (a, b) => NotifyAdjustment(WheelId.NeoX, true));
            this.AddAction("FProTcAdjusted", (a, b) => NotifyAdjustment(WheelId.FPro, false));
            this.AddAction("FProAbsAdjusted", (a, b) => NotifyAdjustment(WheelId.FPro, true));
            Interlocked.Exchange(ref Current, this);
        }

        public WheelTheme Theme(WheelId wheel) { return wheel == WheelId.NeoX ? neo : fpro; }
        public void NotifyAdjustment(WheelId wheel, bool abs)
        {
            var pulse = new AdjustmentPulse(Now);
            if (wheel == WheelId.NeoX) { if (abs) neoAbs = pulse; else neoTc = pulse; }
            else { if (abs) fproAbs = pulse; else fproTc = pulse; }
        }
        public Telemetry WithAdjustmentFeedback(WheelId wheel, Telemetry input, double now)
        {
            input.TcAdjustmentSeconds = AdjustmentAge(wheel == WheelId.NeoX ? neoTc : fproTc, input.Running ? sharedTc : null, now);
            input.AbsAdjustmentSeconds = AdjustmentAge(wheel == WheelId.NeoX ? neoAbs : fproAbs, input.Running ? sharedAbs : null, now);
            return input;
        }
        private static double? AdjustmentAge(AdjustmentPulse local, AdjustmentPulse shared, double now)
        {
            double started = Math.Max(local == null ? double.NegativeInfinity : local.At, shared == null ? double.NegativeInfinity : shared.At);
            double age = now - started;
            return AdjustmentFeedback.IsActive(age) ? (double?)age : null;
        }
        private void UpdateAssistLevels(bool running, string context, int tcLevel, int absLevel, double now)
        {
            bool tcChanged, absChanged;
            if (context != levelContext) { sharedTc = sharedAbs = null; levelContext = context; }
            levelChanges.Update(running, context, tcLevel, absLevel, out tcChanged, out absChanged);
            if (!running) { sharedTc = sharedAbs = null; }
            if (tcChanged) sharedTc = new AdjustmentPulse(now);
            if (absChanged) sharedAbs = new AdjustmentPulse(now);
        }
        public List<ThemePreset> Presets()
        {
            var all = ThemePresets.BuiltIns();
            all.AddRange(customThemes.Select(x => x.Copy()));
            return all;
        }
        public void SavePreset(string name, WheelId wheel, WheelSettings settings, bool replace)
        {
            if (themeLibraryError.Length > 0) throw new InvalidOperationException(themeLibraryError + " Existing library has not been overwritten.");
            var next = ThemePresets.Save(customThemes, name, wheel, settings, replace);
            this.SaveCommonSettings("Themes", new ThemeLibrarySettings { Themes = next });
            customThemes = next;
            ThemeLibraryRevision++;
        }

        public void Apply(WheelId wheel, WheelSettings settings)
        {
            var next = new WheelTheme(settings, wheel, Now);
            var saved = new PluginSettings {
                NeoX = (wheel == WheelId.NeoX ? next : neo).Settings.Copy(),
                FPro = (wheel == WheelId.FPro ? next : fpro).Settings.Copy()
            };
            this.SaveCommonSettings("Settings", saved);
            if (wheel == WheelId.NeoX) neo = next; else fpro = next;
            loadWarning = themeLibraryError;
            StopTest();
        }

        public void BeginTest(WheelId wheel, PreviewMode mode, int led)
        {
            if (led < 0 || led >= WheelSettings.Count(wheel)) throw new ArgumentOutOfRangeException("led");
            test = new HardwareTest(wheel, mode, led, Now + 5);
        }
        public void StopTest() { test = null; revStripTest = null; }
        public void BeginRevStripTest()
        { revStripTest = new HardwareTest(WheelId.NeoX, PreviewMode.Theme, 0, Now + 5); }
        public HardwareTest ActiveRevStripTest()
        { var value = revStripTest; return value != null && Now < value.EndsAt ? value : null; }
        public HardwareTest ActiveTest(WheelId wheel)
        {
            var value = test;
            return value != null && value.Wheel == wheel && Now < value.EndsAt ? value : null;
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            double now = Now;
            // Assist levels are absent from LedsGameData. Read only SimHub's
            // normalised levels here; all existing warning signals stay in the renderer.
            bool running = data.GameRunning && data.NewData != null;
            var levels = data.NewData;
            string context = running ? data.GameName + "|" + (levels.CarId ?? levels.CarModel) : null;
            UpdateAssistLevels(running, context, running ? levels.TCLevel : -1, running ? levels.ABSLevel : -1, now);
            if (now < nextDiagnosticUpdate) return;
            nextDiagnosticUpdate = now + 0.25;
            if (!data.GameRunning || data.NewData == null)
            {
                diagnostic = "No active driving telemetry. Warnings fall back to the idle theme.";
                return;
            }
            var d = data.NewData;
            diagnostic = string.Format("{0} | RPM {1:0} | SimHub redline {2} | Pit limiter {3}\nFlags: yellow {4}, blue {5}, green {6}, white {7}, black {8}, chequered {9}\nTC active {10} | ABS active {11}\nA zero value does not prove a game supports that signal; verify it during a session.",
                data.GameName, d.Rpms, d.CarSettings_RPMRedLineReached, d.PitLimiterOn,
                d.Flag_Yellow, d.Flag_Blue, d.Flag_Green, d.Flag_White, d.Flag_Black, d.Flag_Checkered, d.TCActive, d.ABSActive);
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager) { return new SettingsControl(this); }
        public void End(PluginManager pluginManager)
        {
            StopTest();
            neoTc = neoAbs = fproTc = fproAbs = sharedTc = sharedAbs = null;
            Interlocked.CompareExchange(ref Current, null, this);
        }
    }
}

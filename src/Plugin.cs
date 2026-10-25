using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;

namespace DilaRacing
{
    public sealed class PluginSettings
    {
        public int Version { get; set; }
        public WheelSettings NeoX { get; set; }
        public WheelSettings FPro { get; set; }
        public PluginSettings() { Version = 1; NeoX = WheelSettings.Defaults(WheelId.NeoX); FPro = WheelSettings.Defaults(WheelId.FPro); }
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
        private volatile string diagnostic = "No game data received. Use SimHub's LED test-data editor to test effects.";
        private volatile string loadWarning = "";
        private double nextDiagnosticUpdate;
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
            Interlocked.Exchange(ref Current, this);
        }

        public WheelTheme Theme(WheelId wheel) { return wheel == WheelId.NeoX ? neo : fpro; }

        public void Apply(WheelId wheel, WheelSettings settings)
        {
            var next = new WheelTheme(settings, wheel, Now);
            var saved = new PluginSettings {
                NeoX = (wheel == WheelId.NeoX ? next : neo).Settings.Copy(),
                FPro = (wheel == WheelId.FPro ? next : fpro).Settings.Copy()
            };
            this.SaveCommonSettings("Settings", saved);
            if (wheel == WheelId.NeoX) neo = next; else fpro = next;
            loadWarning = "";
            StopTest();
        }

        public void BeginTest(WheelId wheel, PreviewMode mode, int led)
        {
            if (led < 0 || led >= WheelSettings.Count(wheel)) throw new ArgumentOutOfRangeException("led");
            test = new HardwareTest(wheel, mode, led, Now + 5);
        }
        public void StopTest() { test = null; }
        public HardwareTest ActiveTest(WheelId wheel)
        {
            var value = test;
            return value != null && value.Wheel == wheel && Now < value.EndsAt ? value : null;
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            // Diagnostics only. Rendering consumes LedsGameData, so SimHub's test editor
            // stays authoritative. We never read a game's raw shared memory/UDP data.
            double now = Now;
            if (now < nextDiagnosticUpdate) return;
            nextDiagnosticUpdate = now + 0.25;
            if (!data.GameRunning || data.NewData == null)
            {
                diagnostic = "No active driving telemetry. Warnings fall back to the idle theme.";
                return;
            }
            var d = data.NewData;
            diagnostic = string.Format("{0} | RPM {1:0} | SimHub redline {2} | Pit limiter {3}\nFlags: yellow {4}, blue {5}, green {6}, white {7}, black {8}, chequered {9}\nA zero value does not prove a game supports that signal; verify it during a session.",
                data.GameName, d.Rpms, d.CarSettings_RPMRedLineReached, d.PitLimiterOn,
                d.Flag_Yellow, d.Flag_Blue, d.Flag_Green, d.Flag_White, d.Flag_Black, d.Flag_Checkered);
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager) { return new SettingsControl(this); }
        public void End(PluginManager pluginManager)
        {
            StopTest();
            Interlocked.CompareExchange(ref Current, null, this);
        }
    }
}

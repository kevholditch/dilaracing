using System.Collections.Generic;
using System.Drawing;
using System.Windows.Controls;
using SimHub.Plugins.DataPlugins.RGBDriver;
using SimHub.Plugins.DataPlugins.RGBDriver.Settings;
using SimHub.Plugins.DataPlugins.RGBDriverCommon;
using SimHub.Plugins.DataPlugins.RGBDriver.LedsContainers.Base;
using SimHub.Plugins.DataPlugins.RGBDriver.LedsContainers.Attributes;

namespace DilaRacing
{
    [ContainerMetadata(90, "Dila Racing wheel theme", "Wheel colours and driving effects", "Dila Racing", restrictedColorSetCompatible: false)]
    public sealed class WheelEffect : LedsContainerBase
    {
        private readonly RevWarningHold revWarning = new RevWarningHold();
        private readonly ThemePlayback playback = new ThemePlayback();
        private readonly InterventionPlayback interventions = new InterventionPlayback();
        private HardwareTest lastTest;
        private WheelTheme lastTheme;
        private string lastGame, lastCar;
        public WheelId Wheel { get; set; }
        public override void LoadDefaultSettings() { Wheel = WheelId.NeoX; }
        public override bool IsAllowedOutOfGame { get { return true; } }
        protected override bool IsActive(LedsGameData data) { return true; }
        protected override UserControl GetEditControl() { return null; }
        public override Control MainLedsEditor
        {
            get
            {
                var panel = new StackPanel { Margin = new System.Windows.Thickness(8) };
                panel.Children.Add(new TextBlock { Text = "Choose the wheel. Edit colours, warnings and layout in Dila Racing.", TextWrapping = System.Windows.TextWrapping.Wrap });
                var choice = new ComboBox { ItemsSource = new[] { "Simagic Neo X buttons (12)", "Cube Controls F-Pro (18)" }, SelectedIndex = (int)Wheel, Margin = new System.Windows.Thickness(0, 8, 0, 8) };
                choice.SelectionChanged += delegate { Wheel = (WheelId)choice.SelectedIndex; };
                panel.Children.Add(choice);
                panel.Children.Add(new TextBlock { Text = "Use this in the button lighting profile. Neo X encoders and RPM LEDs use separate SimHub profiles.", TextWrapping = System.Windows.TextWrapping.Wrap });
                return new UserControl { Content = panel };
            }
        }
        public override LedResult SetResultBase(LedsGameData data, LedResult result, List<Profile> includedFrom)
        {
            int count = WheelSettings.Count(Wheel);
            result.Clear(count);
            var plugin = DilaPlugin.Current;
            if (plugin == null)
            {
                revWarning.Reset();
                playback.Reset();
                interventions.Reset();
                for (int i = 0; i < count; i++) result[i] = Color.Black;
                return result;
            }
            var theme = plugin.Theme(Wheel);
            string car = data.CarId ?? data.CarModel;
            if (!object.ReferenceEquals(theme, lastTheme) || lastGame != data.GameName || lastCar != car)
            {
                revWarning.Reset();
                playback.Reset();
                interventions.Reset();
            }
            lastTheme = theme; lastGame = data.GameName; lastCar = car;
            var input = new Telemetry { Running = data.GameRunning, PitLimiter = data.PitLimiterOn,
                Redline = data.RPMRedlineReached, Rpm = data.Rpms,
                TcActive = data.TCActive, AbsActive = data.AbsActive };
            var test = plugin.ActiveTest(Wheel);
            if (!object.ReferenceEquals(test, lastTest)) { playback.Reset(); interventions.Reset(); }
            lastTest = test;
            double time = DilaPlugin.Now;
            if (test != null)
            {
                revWarning.Reset();
                input = Telemetry.Preview(test.Mode);
                double phase = (time - (test.EndsAt - 5)) % 1.2;
                if (test.Mode == PreviewMode.TcAdjustment) input.TcAdjustmentSeconds = phase;
                if (test.Mode == PreviewMode.AbsAdjustment) input.AbsAdjustmentSeconds = phase;
            }
            else
            {
                input = revWarning.Apply(theme, input, time);
                if (!(data is TestLedsGameData)) input = plugin.WithAdjustmentFeedback(Wheel, input, time);
            }
            input = playback.Apply(theme, input, time);
            input = interventions.Apply(theme, input, time);
            for (int i = 0; i < count; i++)
                result[i] = test != null && test.Mode == PreviewMode.Identify
                    ? (i == test.Led ? Lighting.Dim(Color.White, theme.Settings.Brightness / 100) : Color.Black)
                    : Lighting.At(theme, i, input, time);
            return result;
        }
    }
}

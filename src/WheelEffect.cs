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
                for (int i = 0; i < count; i++) result[i] = Color.Black;
                return result;
            }
            var theme = plugin.Theme(Wheel);
            var input = new Telemetry { Running = data.GameRunning, PitLimiter = data.PitLimiterOn,
                Redline = data.RPMRedlineReached, Rpm = data.Rpms };
            var test = plugin.ActiveTest(Wheel);
            if (test != null) input = Telemetry.Preview(test.Mode);
            double time = DilaPlugin.Now;
            for (int i = 0; i < count; i++)
                result[i] = test != null && test.Mode == PreviewMode.Identify
                    ? (i == test.Led ? Lighting.Dim(Color.White, theme.Settings.Brightness / 100) : Color.Black)
                    : Lighting.At(theme, i, input, time);
            return result;
        }
    }
}

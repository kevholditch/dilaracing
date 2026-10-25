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
    [ContainerMetadata(91, "Dila Racing Neo X rev strip", "Nine RPM LEDs with selectable shift-light patterns", "Dila Racing", restrictedColorSetCompatible: false)]
    public sealed class RevStripEffect : LedsContainerBase
    {
        private readonly RevStripPlayback playback = new RevStripPlayback();
        private WheelTheme lastTheme;
        private HardwareTest lastTest;
        private string lastGame, lastCar;
        public override void LoadDefaultSettings() { }
        public override bool IsAllowedOutOfGame { get { return true; } }
        protected override bool IsActive(LedsGameData data) { return true; }
        protected override UserControl GetEditControl() { return null; }
        public override Control MainLedsEditor
        {
            get { return new UserControl { Content = new TextBlock {
                Text = "For the Neo X's nine telemetry/rev LEDs only. Choose the pattern, brightness and thresholds in Dila Racing > Simagic Neo X. RPM uses SimHub's configured car redline.",
                TextWrapping = System.Windows.TextWrapping.Wrap, Margin = new System.Windows.Thickness(8) } }; }
        }
        public override LedResult SetResultBase(LedsGameData data, LedResult result, List<Profile> includedFrom)
        {
            result.Clear(RevStripLighting.Count);
            var plugin = DilaPlugin.Current;
            if (plugin == null)
            {
                playback.Reset(); lastTheme = null;
                for (int i = 0; i < RevStripLighting.Count; i++) result[i] = Color.Black;
                return result;
            }
            var theme = plugin.Theme(WheelId.NeoX);
            var test = plugin.ActiveRevStripTest();
            string car = data.CarId ?? data.CarModel;
            if (!object.ReferenceEquals(theme, lastTheme) || !object.ReferenceEquals(test, lastTest) || lastGame != data.GameName || lastCar != car)
                playback.Reset();
            lastTheme = theme; lastTest = test; lastGame = data.GameName; lastCar = car;
            double now = DilaPlugin.Now;
            var input = test == null
                ? new RevStripTelemetry { Running = data.GameRunning, Percent = data.RPMToRedlinePercent, Redline = data.RPMRedlineReached }
                : RevStripLighting.Demo(now - (test.EndsAt - 5));
            input = playback.Apply(theme.Settings.RevStrip, input, now);
            for (int i = 0; i < RevStripLighting.Count; i++) result[i] = RevStripLighting.At(theme.Settings.RevStrip, i, input);
            return result;
        }
    }
}

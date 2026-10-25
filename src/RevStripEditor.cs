using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DilaRacing
{
    internal sealed class RevStripEditor : StackPanel
    {
        private readonly ComboBox mode, colorMode;
        private readonly Button colorButton;
        private string singleColor;
        private readonly Slider brightness, start, shift, flash;
        private readonly CheckBox reverse;
        private readonly List<Border> lamps = new List<Border>();
        private readonly TextBlock status;
        private readonly RevStripPlayback playback = new RevStripPlayback();
        private RevStripSettings preview;
        private double epoch;
        public RevStripEditor(DilaPlugin plugin, RevStripSettings settings, Action changed)
        {
            preview = settings.Copy(); epoch = DilaPlugin.Now;
            Children.Add(SettingsControl.Note("Import Dila Racing - Neo X Rev Strip.ledsprofile into Devices > Simagic Neo X > LEDs > Telemetry Leds. Keep the Dila button profile selected separately.", 13));
            mode = new ComboBox { ItemsSource = new[] { "Off", "Progressive — left to right", "Converging — outside to centre", "Minimal — late buildup" }, SelectedIndex = (int)settings.Mode, MinWidth = 310, HorizontalAlignment = HorizontalAlignment.Left };
            Children.Add(mode);
            Children.Add(SettingsControl.Note("Rev strip colours", 13));
            var colorRow = new WrapPanel();
            colorMode = new ComboBox { ItemsSource = new[] { "Green / amber / red", "Single colour" }, SelectedIndex = (int)settings.ColorMode, MinWidth = 200, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            singleColor = settings.SingleColor;
            colorButton = SettingsControl.Button("Colour " + singleColor, delegate {
                using (var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = WheelSettings.ParseColor(singleColor) })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    singleColor = "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2");
                    colorButton.Content = "Colour " + singleColor; changed();
                }
            });
            colorButton.IsEnabled = settings.ColorMode == RevStripColorMode.SingleColor;
            colorRow.Children.Add(colorMode); colorRow.Children.Add(colorButton); Children.Add(colorRow);
            Children.Add(SettingsControl.Note("Green/amber/red keeps the blue shift cue (white in Minimal) and red limiter warning. Single colour uses your chosen colour throughout buildup, shift cue and limiter, flashing bright/dim at the thresholds. Idle and Off are dark.", 13));
            brightness = Slider("Strip brightness (%)", 0, 100, settings.Brightness, changed);
            start = Slider("Begin buildup (% of SimHub redline)", 50, 95, settings.StartPercent, changed);
            shift = Slider("Shift cue (% of SimHub redline)", 80, 100, settings.ShiftPercent, changed);
            flash = Slider("Strip flash speed (cycles per second)", 1, 10, settings.FlashHz, changed);
            reverse = new CheckBox { Content = "Reverse strip direction", IsChecked = settings.Reverse, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(0, 6, 0, 6) };
            Children.Add(reverse);
            Children.Add(SettingsControl.Note("Set the correct redline for each car in SimHub Car settings. Minimal begins within the final 10 percentage points before the shift cue. A 97% cue is a starting point, not a measured optimal shift RPM.", 13));
            var row = new WrapPanel();
            for (int i = 0; i < RevStripLighting.Count; i++)
            {
                var lamp = new Border { Width = 32, Height = 20, Margin = new Thickness(2), CornerRadius = new CornerRadius(4), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
                lamps.Add(lamp); row.Children.Add(lamp);
            }
            Children.Add(row);
            status = SettingsControl.Note("Local RPM sweep preview", 13); Children.Add(status);
            var buttons = new WrapPanel();
            buttons.Children.Add(SettingsControl.Button("Preview strip / restart", delegate {
                try { RestartPreview(Read()); } catch (Exception ex) { status.Text = ex.Message; }
            }));
            buttons.Children.Add(SettingsControl.Button("Test saved strip for 5 seconds", delegate { plugin.BeginRevStripTest(); }));
            buttons.Children.Add(SettingsControl.Button("Stop test", plugin.StopTest));
            Children.Add(buttons);
            Children.Add(SettingsControl.Note("Save & apply below saves these settings too. Hardware tests use saved settings and require the rev-strip profile to be selected.", 13));
            mode.SelectionChanged += delegate { changed(); };
            colorMode.SelectionChanged += delegate { colorButton.IsEnabled = colorMode.SelectedIndex == (int)RevStripColorMode.SingleColor; changed(); };
            reverse.Checked += delegate { changed(); }; reverse.Unchecked += delegate { changed(); };
            Tick();
        }
        private Slider Slider(string label, double min, double max, double value, Action changed)
        {
            var text = SettingsControl.Note(label + ": " + value.ToString("0.#"), 13); Children.Add(text);
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 340, HorizontalAlignment = HorizontalAlignment.Left };
            slider.ValueChanged += delegate { text.Text = label + ": " + slider.Value.ToString("0.#"); changed(); };
            Children.Add(slider); return slider;
        }
        public RevStripSettings Read()
        {
            var settings = new RevStripSettings { Mode = (RevStripMode)mode.SelectedIndex, Brightness = brightness.Value,
                ColorMode = (RevStripColorMode)colorMode.SelectedIndex, SingleColor = singleColor,
                StartPercent = start.Value, ShiftPercent = shift.Value, FlashHz = flash.Value, Reverse = reverse.IsChecked == true };
            settings.Validate(); return settings;
        }
        public bool ReverseDirection { get { return reverse.IsChecked == true; } }
        public void LoadSettings(RevStripSettings settings)
        {
            mode.SelectedIndex = (int)settings.Mode; brightness.Value = settings.Brightness;
            colorMode.SelectedIndex = (int)settings.ColorMode; singleColor = settings.SingleColor;
            colorButton.Content = "Colour " + singleColor;
            start.Value = settings.StartPercent; shift.Value = settings.ShiftPercent;
            flash.Value = settings.FlashHz; reverse.IsChecked = settings.Reverse;
            RestartPreview(settings);
        }
        public void RestartPreview(RevStripSettings settings)
        { preview = settings.Copy(); epoch = DilaPlugin.Now; playback.Reset(); }
        public void Tick()
        {
            double now = DilaPlugin.Now;
            var data = playback.Apply(preview, RevStripLighting.Demo(now - epoch), now);
            status.Text = "Local preview: " + data.Percent.ToString("0") + "% of redline — " + preview.Mode;
            for (int i = 0; i < lamps.Count; i++)
            {
                var color = RevStripLighting.At(preview, i, data);
                lamps[i].Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DrawingColor = System.Drawing.Color;

namespace DilaRacing
{
    public sealed class SettingsControl : UserControl
    {
        private readonly DilaPlugin plugin;
        private readonly DispatcherTimer timer;
        private readonly TextBlock diagnostic;
        private readonly List<WheelEditor> editors = new List<WheelEditor>();
        public SettingsControl(DilaPlugin plugin)
        {
            this.plugin = plugin;
            Background = new SolidColorBrush(Color.FromRgb(19, 24, 32));
            Foreground = Brushes.WhiteSmoke;
            FontSize = 14;
            var root = new StackPanel { Margin = new Thickness(24), MaxWidth = 980, HorizontalAlignment = HorizontalAlignment.Left };
            root.Children.Add(new TextBlock { Text = "DILA RACING", FontSize = 28, FontWeight = FontWeights.Bold });
            root.Children.Add(Note("Wheel lighting, powered by SimHub telemetry", 16));
            diagnostic = Note(plugin.Diagnostic, 13);
            diagnostic.Margin = new Thickness(0, 8, 0, 16);
            root.Children.Add(diagnostic);
            var tabs = new TabControl { Background = Background, Foreground = Foreground, Padding = new Thickness(12) };
            foreach (WheelId wheel in new[] { WheelId.NeoX, WheelId.FPro })
            {
                var editor = new WheelEditor(plugin, wheel); editors.Add(editor);
                tabs.Items.Add(new TabItem { Header = wheel == WheelId.NeoX ? "Simagic Neo X" : "Cube Controls F-Pro", Content = editor });
            }
            tabs.Items.Add(new TabItem { Header = "iFlag boxes", Content = FlagsPanel() });
            root.Children.Add(tabs);
            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += delegate {
                diagnostic.Text = plugin.Diagnostic + (plugin.LoadWarning.Length > 0 ? "\n" + plugin.LoadWarning : "");
                foreach (var editor in editors) editor.Tick();
            };
            Loaded += delegate { timer.Start(); };
            Unloaded += delegate { timer.Stop(); plugin.StopTest(); };
        }

        private UIElement FlagsPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(8) };
            panel.Children.Add(Title("Race flags through SimHub"));
            panel.Children.Add(Note("The two boxes can share one native matrix profile. Their Arduino connections and race-flag animations stay in SimHub.", 14));
            panel.Children.Add(Note("1. Open Arduino > RGB Matrix and create a new profile called Dila Racing Flags.\n\n2. Add a game-running group containing SimHub's Flags effect. Enable the flags you want. Leave the idle output blank.\n\n3. In the Arduino hardware settings, assign the same matrix content to both boxes. Keep their existing hardware type and rotation.\n\n4. Select this profile for LMU, RaceRoom and AC Evo. Use SimHub's test-data panel to check each flag on both boxes.", 14));
            panel.Children.Add(Title("Switching from TrackThirtyTwo"));
            panel.Children.Add(Note("Export your current matrix profile first, then select the new native profile for testing. Only the selected profile should supply the matrix output. You can switch back from SimHub's profile manager.", 14));
            panel.Children.Add(Title("Game signals"));
            panel.Children.Add(Note("The diagnostic line shows the normalised flag values received from SimHub. A value of zero can mean inactive or unavailable. Confirm each flag in a session for each game; this plugin does not invent unsupported flags.\n\nFlag enable switches, animations, brightness and rotation are currently configured in SimHub's matrix editor. This tab is the setup guide for the first version.", 14));
            return panel;
        }

        internal static TextBlock Note(string text, double size)
        { return new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 10), Foreground = Brushes.LightGray, MaxWidth = 880 }; }
        internal static TextBlock Title(string text)
        { return new TextBlock { Text = text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 6), Foreground = Brushes.WhiteSmoke }; }
        internal static Button Button(string text, Action action)
        {
            var button = new Button { Content = text, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 4, 10, 4), Foreground = Brushes.Black, Background = Brushes.Gainsboro };
            button.Click += delegate { action(); }; return button;
        }
    }

    internal sealed class WheelEditor : StackPanel
    {
        private readonly DilaPlugin plugin;
        private readonly WheelId wheel;
        private readonly ComboBox idleMode, racingMode, previewMode, identify, tcLed, absLed;
        private readonly Slider brightness, fade, frequency, revFrequency;
        private readonly CheckBox rev, pit, idle, tcAlways, absAlways, tcAdjust, absAdjust;
        private readonly TextBox rows, fallback;
        private readonly TextBlock status;
        private readonly List<Border> lamps = new List<Border>();
        private readonly Button startButton, endButton;
        private string startColor, endColor;
        private WheelTheme preview;
        private readonly ThemePlayback previewPlayback = new ThemePlayback();
        private readonly InterventionPlayback previewInterventions = new InterventionPlayback();
        private bool dirty;
        private readonly RevStripEditor revStripEditor;
        private RevStripSettings savedStrip;
        private readonly ComboBox presetChoice;
        private readonly TextBox presetName;
        private readonly TextBlock presetStatus;
        private readonly Button updatePreset;
        private bool loadingPreset;
        private int libraryRevision = -1;

        public WheelEditor(DilaPlugin plugin, WheelId wheel)
        {
            this.plugin = plugin; this.wheel = wheel;
            Margin = new Thickness(8);
            var s = plugin.Theme(wheel).Settings;
            savedStrip = s.RevStrip.Copy();
            startColor = s.StartColor; endColor = s.EndColor;
            preview = new WheelTheme(s, wheel, DilaPlugin.Now);
            Children.Add(SettingsControl.Note("Import the matching Dila Racing .ledsprofile into this wheel's BUTTON lighting profile once. Changes below take effect when you press Save & apply.", 14));
            if (wheel == WheelId.NeoX)
                Children.Add(SettingsControl.Note("Button effects control 12 channels. The nine rev LEDs use the separate Dila rev-strip profile; the 41 encoder channels keep their native SimHub profile.", 13));
            Children.Add(SettingsControl.Title("Theme presets"));
            presetChoice = new ComboBox { MinWidth = 310, HorizontalAlignment = HorizontalAlignment.Left };
            Children.Add(presetChoice);
            presetStatus = SettingsControl.Note("Current wheel settings", 13); Children.Add(presetStatus);
            Children.Add(SettingsControl.Note("Selecting a theme loads all lighting preferences below. Save & apply sends them to this wheel. Button layout, TC/ABS LED assignments and strip direction stay with each wheel. Custom themes are shared between both tabs.", 13));
            var presetActions = new WrapPanel();
            presetName = new TextBox { Width = 210, MaxLength = 60, ToolTip = "Name for your custom theme", Foreground = Brushes.Black, Background = Brushes.White, Margin = new Thickness(0, 4, 10, 4), VerticalContentAlignment = VerticalAlignment.Center };
            presetActions.Children.Add(presetName);
            presetActions.Children.Add(SettingsControl.Button("Save as new theme", delegate { SavePreset(false); }));
            updatePreset = SettingsControl.Button("Update saved theme", delegate { SavePreset(true); });
            updatePreset.IsEnabled = false; presetActions.Children.Add(updatePreset);
            presetActions.Children.Add(SettingsControl.Button("Save & apply to wheel", Save));
            Children.Add(presetActions);
            Children.Add(SettingsControl.Title("Theme"));
            idleMode = AddMode("When idle", s.ModeFor(false));
            racingMode = AddMode("When racing", s.ModeFor(true));
            Children.Add(SettingsControl.Note("Switches using SimHub's game-running status. Stopping the car does not select idle. Both modes share the colours, brightness, fade duration and button layout below.", 13));
            var colors = new WrapPanel();
            startButton = SettingsControl.Button("Start " + startColor, delegate { ChooseColor(true); });
            endButton = SettingsControl.Button("End " + endColor, delegate { ChooseColor(false); });
            colors.Children.Add(startButton); colors.Children.Add(endButton); Children.Add(colors);
            brightness = AddSlider("Brightness (%)", 0, 100, s.Brightness, 1);
            fade = AddSlider("Animation duration (seconds)", 1, 30, s.FadeSeconds, 0.5);
            Children.Add(SettingsControl.Note("Twinkle gives a fuller Christmas-tree effect: at least half the buttons stay at half brightness or above while others gently fade, using shades between your two colours. Pulse keeps the sparse effect with longer dark pauses. Duration controls both (minimum 4 seconds); try 6–8 seconds.", 13));
            idle = AddCheck("Show theme when no game is running", s.ShowWhenIdle);
            if (wheel == WheelId.NeoX)
            {
                revStripEditor = new RevStripEditor(plugin, s.RevStrip, MarkDirty);
                Children.Add(new Expander { Header = "Neo X rev strip", Content = revStripEditor, IsExpanded = true, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(0, 16, 0, 8) });
            }
            Children.Add(SettingsControl.Title("Driving effects"));
            rev = AddCheck("Flash bright / dim red at SimHub's redline", s.RevFlash);
            revFrequency = AddSlider("Rev warning speed (cycles per second)", 1, 10, s.RevFlashHz, 0.5);
            Children.Add(SettingsControl.Note("Each new redline pulse starts bright red. If SimHub holds the signal on, this speed sets the flashing rhythm. Brief signal gaps stay dim red.", 13));
            pit = AddCheck("Alternate blue / white when the pit limiter is active", s.PitFlash);
            frequency = AddSlider("Pit-limiter speed (cycles per second)", 0.5, 5, s.FlashHz, 0.5);
            Children.Add(SettingsControl.Note("Pit-limiter lighting takes priority over redline. Both override the theme. When the signal ends, the theme resumes.", 13));
            Children.Add(SettingsControl.Note("Optional RPM threshold (0 = use only SimHub's redline; a nonzero value adds a fixed threshold for this wheel)", 13));
            fallback = new TextBox { Text = s.FallbackRedlineRpm.ToString(CultureInfo.InvariantCulture), Width = 100, HorizontalAlignment = HorizontalAlignment.Left, Foreground = Brushes.Black, Background = Brushes.White };
            Children.Add(fallback);
            Children.Add(SettingsControl.Title("Traction control & ABS"));
            tcLed = AddLedSelector("Traction control — blue", s.TractionControlLed, "Always blue", s.TcAlwaysBlue, s.TcAdjustmentFlash, out tcAlways, out tcAdjust);
            absLed = AddLedSelector("ABS — red", s.AbsLed, "Always red", s.AbsAlwaysRed, s.AbsAdjustmentFlash, out absAlways, out absAdjust);
            Children.Add(SettingsControl.Note("The selected button pulses rapidly between bright and dim colour while SimHub reports intervention, then returns to the current effect. These indicators override other effects on their selected LEDs; ABS wins if both use the same LED. Off disables an indicator.", 13));
            Children.Add(SettingsControl.Note("Uses active intervention signals, not the TC/ABS level or enabled setting. Game support varies. Use Identify below to find the button numbers.", 13));
            Children.Add(SettingsControl.Note("Always colour keeps the selected button red/blue while the theme is visible. Intervention judder still takes priority. Flash on adjustment gives two white ticks when SimHub reports a level change. Both options are independent and need an LED selected.", 13));
            Children.Add(SettingsControl.Note("For direct rotary feedback, bind both rotary directions in SimHub Controls and events to DilaPlugin." + (wheel == WheelId.NeoX ? "NeoXTcAdjusted / NeoXAbsAdjusted" : "FProTcAdjusted / FProAbsAdjusted") + ". These actions only flash the indicator; keep your game control bindings. This also works outside a race when idle lighting is enabled.", 13));
            var layout = new StackPanel();
            layout.Children.Add(SettingsControl.Note("Enter physical rows from BOTTOM to TOP, one row per line. Separate LED numbers with commas. Include every number exactly once. The initial single row is uncalibrated; use Identify to locate LEDs before using a travelling fade.", 13));
            rows = new TextBox { Text = s.BottomToTopRows, AcceptsReturn = true, MinHeight = 80, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Foreground = Brushes.Black, Background = Brushes.White, FontFamily = new FontFamily("Consolas") };
            layout.Children.Add(rows);
            var identifyRow = new WrapPanel();
            identify = new ComboBox { Width = 90, Margin = new Thickness(0, 5, 10, 5) };
            for (int i = 1; i <= WheelSettings.Count(wheel); i++) identify.Items.Add("LED " + i);
            identify.SelectedIndex = 0;
            identifyRow.Children.Add(identify);
            identifyRow.Children.Add(SettingsControl.Button("Identify LED for 5 seconds", delegate { RunTest(PreviewMode.Identify); }));
            layout.Children.Add(identifyRow);
            Children.Add(new Expander { Header = "Calibrate bottom-to-top layout", Content = layout, Margin = new Thickness(0, 16, 0, 8), Foreground = Brushes.WhiteSmoke });
            Children.Add(SettingsControl.Title("Preview"));
            Children.Add(SettingsControl.Note("Channels are shown in numeric order, not the physical wheel layout. Preview is local; hardware changes require Save & apply or a hardware test.", 13));
            var previewRow = new WrapPanel();
            for (int i = 0; i < WheelSettings.Count(wheel); i++)
            {
                var lamp = new Border { Width = 35, Height = 35, CornerRadius = new CornerRadius(7), Margin = new Thickness(2), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = new TextBlock { Text = (i + 1).ToString(), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                lamps.Add(lamp); previewRow.Children.Add(lamp);
            }
            Children.Add(previewRow);
            previewMode = new ComboBox { ItemsSource = new[] { "Racing theme", "Redline", "Pit limiter", "Both (priority test)", "Identify selected LED", "Idle theme", "Traction control active", "ABS active", "TC + ABS active", "TC adjustment", "ABS adjustment" }, SelectedIndex = (int)PreviewMode.IdleTheme, MinWidth = 240, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8) };
            Children.Add(previewMode);
            var buttons = new WrapPanel();
            buttons.Children.Add(SettingsControl.Button("Preview / restart fade", RefreshPreview));
            buttons.Children.Add(SettingsControl.Button("Save & apply", Save));
            buttons.Children.Add(SettingsControl.Button("Test saved settings for 5 seconds", delegate { RunTest((PreviewMode)previewMode.SelectedIndex); }));
            buttons.Children.Add(SettingsControl.Button("Stop hardware test", delegate { plugin.StopTest(); status.Text = "Hardware test stopped."; }));
            Children.Add(buttons);
            status = SettingsControl.Note("Ready. Calibrate the layout before using the travelling fade.", 13);
            Children.Add(status);
            idleMode.SelectionChanged += delegate { MarkDirty(); };
            racingMode.SelectionChanged += delegate { MarkDirty(); };
            previewMode.SelectionChanged += delegate { previewPlayback.Reset(); previewInterventions.Reset(); };
            tcLed.SelectionChanged += delegate { MarkDirty(); };
            absLed.SelectionChanged += delegate { MarkDirty(); };
            brightness.ValueChanged += delegate { MarkDirty(); };
            fade.ValueChanged += delegate { MarkDirty(); };
            frequency.ValueChanged += delegate { MarkDirty(); };
            revFrequency.ValueChanged += delegate { MarkDirty(); };
            rows.TextChanged += delegate { MarkDirty(); };
            fallback.TextChanged += delegate { MarkDirty(); };
            foreach (var box in new[] { idle, rev, pit, tcAlways, absAlways, tcAdjust, absAdjust }) { box.Checked += delegate { MarkDirty(); }; box.Unchecked += delegate { MarkDirty(); }; }
            presetChoice.SelectionChanged += delegate {
                if (loadingPreset) return;
                var selected = presetChoice.SelectedItem as ThemePreset;
                updatePreset.IsEnabled = selected != null && !ThemePresets.IsBuiltIn(selected.Name);
                if (selected != null) LoadPreset(selected);
            };
            RefreshPresetList(null);
            Tick();
        }

        private void MarkDirty()
        {
            if (loadingPreset) return;
            dirty = true; status.Text = "Unsaved changes. Preview to inspect; Save & apply to send to the wheel.";
            presetStatus.Text = "Modified — save as a new theme, or update a selected custom theme.";
        }
        private void RefreshPresetList(string selectedName)
        {
            loadingPreset = true;
            try
            {
                presetChoice.Items.Clear(); presetChoice.Items.Add("Current settings / custom");
                int selected = 0;
                foreach (var preset in plugin.Presets())
                {
                    int index = presetChoice.Items.Add(preset);
                    if (string.Equals(preset.Name, selectedName, StringComparison.OrdinalIgnoreCase)) selected = index;
                }
                presetChoice.SelectedIndex = selected;
                var item = presetChoice.SelectedItem as ThemePreset;
                updatePreset.IsEnabled = item != null && !ThemePresets.IsBuiltIn(item.Name);
                libraryRevision = plugin.ThemeLibraryRevision;
            }
            finally { loadingPreset = false; }
        }
        private void LoadPreset(ThemePreset preset)
        {
            try
            {
                var current = plugin.Theme(wheel).Settings.Copy();
                current.BottomToTopRows = rows.Text; current.TractionControlLed = tcLed.SelectedIndex; current.AbsLed = absLed.SelectedIndex;
                if (revStripEditor != null) current.RevStrip.Reverse = revStripEditor.ReverseDirection;
                var value = preset.ForWheel(current, wheel);
                loadingPreset = true;
                startColor = value.StartColor; endColor = value.EndColor;
                startButton.Content = "Start " + startColor; endButton.Content = "End " + endColor;
                idleMode.SelectedIndex = (int)value.ModeFor(false); racingMode.SelectedIndex = (int)value.ModeFor(true);
                brightness.Value = value.Brightness; fade.Value = value.FadeSeconds;
                frequency.Value = value.FlashHz; revFrequency.Value = value.RevFlashHz;
                rev.IsChecked = value.RevFlash; pit.IsChecked = value.PitFlash; idle.IsChecked = value.ShowWhenIdle;
                tcAlways.IsChecked = value.TcAlwaysBlue; absAlways.IsChecked = value.AbsAlwaysRed;
                tcAdjust.IsChecked = value.TcAdjustmentFlash; absAdjust.IsChecked = value.AbsAdjustmentFlash;
                fallback.Text = value.FallbackRedlineRpm.ToString(CultureInfo.InvariantCulture);
                savedStrip = value.RevStrip.Copy();
                if (revStripEditor != null) revStripEditor.LoadSettings(value.RevStrip);
                preview = new WheelTheme(value, wheel, DilaPlugin.Now);
                previewPlayback.Reset(); previewInterventions.Reset();
                presetName.Text = ThemePresets.IsBuiltIn(preset.Name) ? "" : preset.Name;
                dirty = true;
                presetStatus.Text = "Loaded: " + preset.Name;
                status.Text = "Theme loaded and previewed. Save & apply to send it to this wheel.";
            }
            catch (Exception ex) { status.Text = "Could not load theme: " + ex.Message; }
            finally { loadingPreset = false; }
        }
        private void SavePreset(bool replace)
        {
            try
            {
                var selected = presetChoice.SelectedItem as ThemePreset;
                if (replace && (selected == null || ThemePresets.IsBuiltIn(selected.Name)))
                    throw new ArgumentException("Select a custom theme to update.");
                string name = replace ? selected.Name : presetName.Text;
                plugin.SavePreset(name, wheel, Read(), replace);
                name = name.Trim(); RefreshPresetList(name); presetName.Text = name;
                presetStatus.Text = "Saved theme: " + name;
                status.Text = "Theme saved for both wheels. Save & apply to send these settings to this wheel.";
            }
            catch (Exception ex) { status.Text = "Could not save theme: " + ex.Message; }
        }
        private ComboBox AddLedSelector(string label, int selected, string alwaysLabel, bool alwaysEnabled, bool adjustmentEnabled, out CheckBox always, out CheckBox adjustment)
        {
            Children.Add(SettingsControl.Note(label, 13));
            var row = new WrapPanel();
            var choice = new ComboBox { MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
            choice.Items.Add("Off");
            for (int i = 1; i <= WheelSettings.Count(wheel); i++) choice.Items.Add("LED " + i);
            choice.SelectedIndex = selected;
            row.Children.Add(choice);
            always = new CheckBox { Content = alwaysLabel, IsChecked = alwaysEnabled, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(12, 2, 12, 8) };
            adjustment = new CheckBox { Content = "Flash on adjustment", IsChecked = adjustmentEnabled, Foreground = Brushes.WhiteSmoke, Margin = new Thickness(0, 2, 0, 8) };
            row.Children.Add(always); row.Children.Add(adjustment); Children.Add(row); return choice;
        }
        private ComboBox AddMode(string label, ThemeMode selected)
        {
            Children.Add(SettingsControl.Note(label, 13));
            var choice = new ComboBox { ItemsSource = new[] { "Solid colour", "Bottom to top — once", "Bottom to top — repeat", "Static vertical gradient", "Pulse — sparse lights", "Twinkle — Christmas tree" }, SelectedIndex = (int)selected, MinWidth = 290, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
            Children.Add(choice); return choice;
        }
        private Slider AddSlider(string label, double min, double max, double value, double step)
        {
            var text = SettingsControl.Note(label + ": " + value.ToString("0.#"), 13); Children.Add(text);
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, Width = 340, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
            slider.ValueChanged += delegate { text.Text = label + ": " + slider.Value.ToString("0.#"); };
            Children.Add(slider); return slider;
        }
        private CheckBox AddCheck(string text, bool value)
        {
            var box = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 5, 0, 5), Foreground = Brushes.WhiteSmoke };
            Children.Add(box); return box;
        }
        private void ChooseColor(bool start)
        {
            using (var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = WheelSettings.ParseColor(start ? startColor : endColor) })
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                string hex = "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2");
                if (start) { startColor = hex; startButton.Content = "Start " + hex; }
                else { endColor = hex; endButton.Content = "End " + hex; }
                MarkDirty();
            }
        }
        private WheelSettings Read()
        {
            int rpm;
            if (!int.TryParse(fallback.Text, out rpm)) throw new ArgumentException("RPM threshold must be a whole number (0 disables it).");
            var value = new WheelSettings { StartColor = startColor, EndColor = endColor, Brightness = brightness.Value,
                Mode = (ThemeMode)idleMode.SelectedIndex, IdleMode = (ThemeMode)idleMode.SelectedIndex,
                RacingMode = (ThemeMode)racingMode.SelectedIndex, FadeSeconds = fade.Value, FlashHz = frequency.Value, RevFlashHz = revFrequency.Value,
                RevFlash = rev.IsChecked == true, PitFlash = pit.IsChecked == true, ShowWhenIdle = idle.IsChecked == true,
                TractionControlLed = tcLed.SelectedIndex, AbsLed = absLed.SelectedIndex,
                TcAlwaysBlue = tcAlways.IsChecked == true, AbsAlwaysRed = absAlways.IsChecked == true,
                TcAdjustmentFlash = tcAdjust.IsChecked == true, AbsAdjustmentFlash = absAdjust.IsChecked == true,
                RevStrip = revStripEditor == null ? savedStrip.Copy() : revStripEditor.Read(),
                BottomToTopRows = rows.Text, FallbackRedlineRpm = rpm };
            value.Validate(wheel); return value;
        }
        private void RefreshPreview()
        {
            try { preview = new WheelTheme(Read(), wheel, DilaPlugin.Now); if (revStripEditor != null) revStripEditor.RestartPreview(preview.Settings.RevStrip); previewInterventions.Reset(); status.Text = dirty ? "Previewing unsaved settings; hardware still uses saved settings." : "Preview restarted."; }
            catch (Exception ex) { status.Text = ex.Message; }
        }
        private void Save()
        {
            try { var settings = Read(); plugin.Apply(wheel, settings); preview = new WheelTheme(settings, wheel, DilaPlugin.Now); if (revStripEditor != null) revStripEditor.RestartPreview(settings.RevStrip); previewInterventions.Reset(); dirty = false; status.Text = "Saved and applied. Fade restarted."; }
            catch (Exception ex) { status.Text = "Could not save: " + ex.Message; }
        }
        private void RunTest(PreviewMode selected)
        {
            plugin.BeginTest(wheel, selected, identify.SelectedIndex);
            status.Text = "Hardware test runs for 5 seconds using SAVED settings. The wheel's Dila Racing profile must be active.";
        }
        public void Tick()
        {
            if (libraryRevision != plugin.ThemeLibraryRevision)
            {
                var selectedPreset = presetChoice.SelectedItem as ThemePreset;
                RefreshPresetList(selectedPreset == null ? null : selectedPreset.Name);
            }
            if (revStripEditor != null) revStripEditor.Tick();
            var selected = (PreviewMode)previewMode.SelectedIndex;
            var input = Telemetry.Preview(selected);
            double now = DilaPlugin.Now;
            double adjustmentPhase = (now - preview.Epoch) % 1.2;
            if (selected == PreviewMode.TcAdjustment) input.TcAdjustmentSeconds = adjustmentPhase;
            if (selected == PreviewMode.AbsAdjustment) input.AbsAdjustmentSeconds = adjustmentPhase;
            input = previewPlayback.Apply(preview, input, now);
            input = previewInterventions.Apply(preview, input, now);
            for (int i = 0; i < lamps.Count; i++)
            {
                DrawingColor color = selected == PreviewMode.Identify
                    ? (i == identify.SelectedIndex ? Lighting.Dim(DrawingColor.White, preview.Settings.Brightness / 100) : DrawingColor.Black)
                    : Lighting.At(preview, i, input, now);
                lamps[i].Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
                ((TextBlock)lamps[i].Child).Foreground = color.GetBrightness() > 0.45 ? Brushes.Black : Brushes.White;
            }
        }
    }
}

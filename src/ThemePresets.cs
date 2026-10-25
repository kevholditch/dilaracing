using System;
using System.Collections.Generic;
using System.Linq;

namespace DilaRacing
{
    public sealed class ThemePreset
    {
        public string Name { get; set; }
        public WheelId SourceWheel { get; set; }
        public WheelSettings Settings { get; set; }
        public override string ToString() { return Name; }
        public ThemePreset Copy()
        { return new ThemePreset { Name = Name, SourceWheel = SourceWheel, Settings = Settings.Copy() }; }
        public WheelSettings ForWheel(WheelSettings current, WheelId target)
        {
            var result = Settings.Copy();
            // Keep physical calibration local to each wheel when sharing themes.
            result.BottomToTopRows = current.BottomToTopRows;
            result.TractionControlLed = current.TractionControlLed;
            result.AbsLed = current.AbsLed;
            result.RevStrip.Reverse = current.RevStrip.Reverse;
            result.Validate(target);
            return result;
        }
    }

    public sealed class ThemeLibrarySettings
    {
        public int Version { get; set; }
        public List<ThemePreset> Themes { get; set; }
        public ThemeLibrarySettings() { Version = 1; Themes = new List<ThemePreset>(); }
    }

    public static class ThemePresets
    {
        public static List<ThemePreset> BuiltIns()
        {
            return new List<ThemePreset> {
                BuiltIn("Steel & Ice", "#607580", "#DCE6E8"),
                BuiltIn("Clean White", "#707070", "#E0E0E0"),
                BuiltIn("Warm Instruments", "#805020", "#E0B878")
            };
        }
        private static ThemePreset BuiltIn(string name, string start, string end)
        {
            var settings = WheelSettings.Defaults(WheelId.NeoX);
            settings.StartColor = start; settings.EndColor = end;
            settings.FadeSeconds = 8;
            return new ThemePreset { Name = name, SourceWheel = WheelId.NeoX, Settings = settings };
        }
        public static bool IsBuiltIn(string name)
        { return BuiltIns().Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)); }
        public static string ValidateName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 60 || name.Any(char.IsControl))
                throw new ArgumentException("Enter a theme name of 1–60 characters, without line breaks.");
            if (IsBuiltIn(name)) throw new ArgumentException("That name belongs to a built-in preset. Choose your own name.");
            return name;
        }
        public static List<ThemePreset> ValidateLibrary(ThemeLibrarySettings library)
        {
            if (library == null || library.Version != 1 || library.Themes == null)
                throw new ArgumentException("Unsupported theme library.");
            var result = new List<ThemePreset>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in library.Themes)
            {
                if (item == null || item.Settings == null || !Enum.IsDefined(typeof(WheelId), item.SourceWheel))
                    throw new ArgumentException("Invalid saved theme.");
                string name = ValidateName(item.Name);
                if (!names.Add(name)) throw new ArgumentException("Duplicate saved theme: " + name);
                item.Settings.Validate(item.SourceWheel);
                var copy = item.Copy(); copy.Name = name; result.Add(copy);
            }
            return result;
        }
        public static List<ThemePreset> Save(List<ThemePreset> existing, string name, WheelId wheel, WheelSettings settings, bool replace)
        {
            name = ValidateName(name); settings.Validate(wheel);
            var result = existing.Select(x => x.Copy()).ToList();
            int index = result.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (!replace && index >= 0) throw new ArgumentException("That theme already exists. Use Update saved theme or choose another name.");
            if (replace && index < 0) throw new ArgumentException("Select an existing custom theme to update.");
            var item = new ThemePreset { Name = name, SourceWheel = wheel, Settings = settings.Copy() };
            if (replace) result[index] = item; else result.Add(item);
            return result;
        }
    }
}

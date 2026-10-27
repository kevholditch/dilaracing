# Setup and testing

Native C# / WPF SimHub plugin for Simagic Neo X buttons (12 channels), its separate rev strip (9 channels), and Cube Controls F-Pro lighting (18 channels). Uses SimHub's installed SDK. It does not open USB/serial ports or read game-specific telemetry.

## Status

This is the first prototype. It compiles against the installed SDK, passes automated core lighting checks and direct SDK rendering smoke checks. Its WPF settings screen has been rendered and visually inspected. Physical LED mappings and live behaviour must be checked on your hardware and games. No physical button layout is assumed: the initial layout is one uncalibrated row. The Neo X's encoder lights retain their native SimHub profiles. Its rev strip uses a separate Dila effect/profile; the F-Pro button profile does not include a Neo X rev strip.

## Build

From PowerShell in this folder:

```powershell
.\build.ps1
```

Uses the .NET Framework compiler shipped with Windows; no package downloads needed. Override `-SimHubPath` if SimHub is installed elsewhere. Output is in `dist`. SimHub assemblies are referenced, not redistributed.

Run `tools\Render-Preview.ps1` after building for the SDK smoke checks and a screenshot in `artifacts\settings-preview.png`. This renders offscreen; it does not install the DLL, open SimHub, or send physical LED output.

## Install and connect a wheel

1. Export your current wheel and matrix profiles through SimHub so you can restore them.
2. Exit SimHub. Copy `dist\DilaRacing.SimHub.dll` into the folder containing `SimHubWPF.exe` (normally `C:\Program Files (x86)\SimHub`). Windows may request administrator permission.
3. Start SimHub and enable Dila Racing when prompted, or in its plugin settings. Open the Dila Racing page.
4. Open the wheel's **button** lighting editor. Import the matching `.ledsprofile` from `dist`, select it, and select profile-based button lighting instead of default button colours. Exact control labels depend on SimHub's device page. For Neo X, use the 12-channel button editor, not the encoder or rev-strip editor.
5. If the import does not resolve the effect, create a blank button profile and add **Dila Racing wheel theme** from **Dila Racing** in the effects library. Select the correct wheel. The DLL must be loaded before importing these profiles.
6. Select this profile for each of LMU, RaceRoom and AC Evo. SimHub may remember different last-used profiles for each game. Existing TrackThirtyTwo profiles can remain saved; select only the intended output profile for the channel being tested.
7. Configure the theme in Dila Racing, preview it and press **Save & apply**. The plugin's brightness multiplies SimHub's device/profile brightness. It defaults to 25%.

## Theme presets and custom themes

Both wheel tabs have a **Theme presets** selector. Selecting a theme fills in the lighting controls and restarts the local preview. Press **Save & apply to wheel** to activate it on that wheel; selection alone does not write hardware or change saved wheel settings.

| Built-in theme | Bottom colour | Top colour |
| --- | --- | --- |
| Steel & Ice | `#607580` | `#DCE6E8` |
| Clean White | `#707070` | `#E0E0E0` |
| Warm Instruments | `#805020` | `#E0B878` |

All three start at 25% brightness, with an eight-second repeating idle fade and static racing gradient. They enable idle lighting, redline and pit-limiter effects, use rev flashes at 6 Hz and pit flashes at 2 Hz, and reset the fixed button RPM override to zero. The Neo X strip uses Progressive, 25% brightness, 70% buildup, 97% shift cue and 6 Hz flashing. These are full lighting presets, not colour-only changes.

Physical configuration stays local to each wheel: button rows, TC/ABS LED assignments (including Off), and rev-strip direction are preserved when loading any theme. This avoids transferring Neo X channel numbers onto the F-Pro. Each wheel must still have its own calibrated layout. The F-Pro retains the theme's strip preferences for sharing back to the Neo X, but has no dedicated Neo X strip output.

To save your own theme, adjust the controls, enter a name beside **Save as new theme**, then click that button. The validated settings are saved to the shared custom-theme library and appear on both wheel tabs. **Update saved theme** replaces the selected custom theme with the current controls; built-in themes cannot be overwritten. Duplicate names are rejected regardless of case. Saving a library theme does not apply it to hardware: use **Save & apply to wheel** as well when ready. Editing controls marks the theme as modified without changing the saved library entry.

Custom themes persist in SimHub's common plugin settings under the separate `Themes` key (`DilaPlugin.Themes.json`). They survive restarts and ordinary wheel-settings saves. No extra LED profile import is needed for presets.

## Effects

- Separate **When idle** and **When racing** selectors for each wheel: solid colour, static vertical gradient, bottom-to-top fade once or repeatedly, **Pulse — sparse lights**, or **Twinkle — Christmas tree**. New setups default to repeating idle animation and a static racing gradient. Existing animated themes become the idle mode with a static racing gradient; existing solid/gradient themes retain their mode in both states. Saved colours, layout, brightness and speeds are preserved.
- **Twinkle — Christmas tree** gives a fuller, gently shimmering display. Complementary fades keep at least half the LED channels at half their configured brightness or above, with staggered timing and smoothly changing shades between your two colours. **Pulse — sparse lights** retains the previous effect with longer dark pauses. Both are available independently for idle and racing on both wheels and can be saved in custom themes. **Animation duration** controls their pace with a four-second minimum; try 6–8 seconds. Brightness limits the peaks; dark theme colours will still look dim. Physical rows do not affect these effects. Warning, adjustment and always-colour overrides retain their priority. Existing Pulse selections stay Pulse; select Twinkle and save/apply to use the fuller effect.
- Switching follows SimHub's `LedsGameData.GameRunning` value. A stationary car still uses the racing mode while this is true; menus and pauses follow whatever status the game supplies through SimHub. This is not a speed-based parked-car detector.
- Adjustable two-colour theme, brightness, fade duration and independent rev-warning/pit-limiter flash speeds. Rev warning defaults to 6 cycles/second; the pit limiter retains its saved speed (2 by default).
- Redline: alternates bright red (`#FF0000`) and dim red (`#600000`), both scaled by your brightness. Each rising edge of SimHub's `LedsGameData.RPMRedlineReached` starts bright red immediately; a sustained signal uses the configured rev-warning speed. During brief signal gaps the warning stays dim red for up to 300 ms, then the theme resumes. This follows the redline pulses SimHub provides, not engine audio or an assumed fuel-cut signal. The hold clears on game exit, game/car change, settings changes, or disabling the warning. Use SimHub's car settings for per-car redline calibration. An optional fixed RPM threshold (0 disables) triggers the same behaviour; this threshold is per wheel, not per car.
- Pit limiter: SimHub's `LedsGameData.PitLimiterOn`, alternating blue/white. It takes priority over redline, then the theme.
- **Traction control & ABS:** each wheel has separate **Off / LED number** selectors using the same one-based channel numbers as Identify and the layout. TC pulses blue (`#0000FF` / `#000060`); ABS pulses red (`#FF0000` / `#600000`). Both respect brightness and default to Off, including when loading older saved settings. Select the buttons and press **Save & apply**.
- These indicators use SimHub's `LedsGameData.TCActive` and `AbsActive`, not assist levels or enabled flags. Each new intervention starts bright immediately; a sustained active signal produces a 10 Hz bright/dim judder. This is a visual pulse, not a measurement of the assist's physical modulation frequency. When intervention ends, the previous effect returns immediately. Only assigned LEDs are overridden, taking priority over pit limiter/redline/theme. If both assists share one LED and intervene together, ABS takes priority. Stale intervention values are ignored when no game is running.
- TC/ABS intervention availability depends on the game and car. The diagnostic line displays SimHub's normalised TC/ABS active values; no intervention is invented from throttle, brake or wheel slip when a game does not expose it.
- Beside each assist LED selector, **Always blue** (TC) or **Always red** (ABS) gives the assigned button a fixed base colour, including idle when idle lighting is enabled. These colours override the theme and global pit/redline effects on that button; real assist intervention still uses its rapid bright/dim judder. If the same LED has both colour overrides, ABS red wins. Selecting Off disables all output from that assist. Brightness and idle-off still apply.
- **Flash on adjustment** is independent for TC and ABS. When SimHub's normalised `TCLevel` or `ABSLevel` changes, the assigned button gives two white ticks over its blue/red colour, lasting 400 ms. First samples, unavailable negative levels, and new game/car baselines do not trigger feedback. This reports a level change; it does not infer steering movement. A new adjustment restarts the ticks. Active intervention has priority over adjustment feedback, which has priority over the fixed base colours and global/theme effects.
- For direct rotary feedback when a game does not expose levels (or while idle), bind both directions of the relevant rotary through SimHub **Controls and events** to the matching Dila Racing action: `DilaPlugin.NeoXTcAdjusted`, `DilaPlugin.NeoXAbsAdjusted`, `DilaPlugin.FProTcAdjusted`, or `DilaPlugin.FProAbsAdjusted`. These actions only notify the indicator; they do not change the game's TC/ABS level, so keep existing game control bindings. Direct actions target their named wheel; telemetry level changes are available to either wheel. No binding is required when automatic level-change feedback works. Reaching a level limit cannot be detected as rotation from telemetry alone; bind the direct action if you want that feedback.
- All four fixed-colour/adjustment checkboxes default off, are stored per wheel, and are saved/restored by custom themes. The three built-in themes also leave them off. Use **TC adjustment** or **ABS adjustment** in the preview/hardware-test selector to test double ticks (enable the corresponding checkbox and select an LED first). SimHub's LED test-data editor ignores live rotary/level-change events, so live telemetry cannot interfere with an editor test.
- Theme animation resumes at its ongoing time when a warning ends. Save/apply, entering idle/racing, changing game/car, or starting/ending a hardware test restarts the fade. Repeating fades hold both end colours and fade back bottom to top. Both theme modes share colours, brightness, fade duration and the calibrated physical layout. A static gradient places the start colour at the bottom, the end colour at the top, and intermediate shades between.
- Out of game, stale warning fields are ignored. Idle theme can be disabled. SimHub controls the game-running status; there is no independent raw telemetry freshness watchdog.
- Local preview does not write hardware. Choose **Idle theme** or **Racing theme** to preview either state. Hardware tests use saved settings and expire after 5 seconds. Closing the settings page or unloading the plugin cancels tests.
- To test assist lights, assign the LEDs, save, then choose **Traction control active**, **ABS active** or **TC + ABS active** in Preview and use **Test saved settings for 5 seconds**. Off selections keep their normal effect even during these tests.

## Neo X rev strip: one-time profile import

1. Start SimHub with the updated Dila Racing plugin installed and enabled.
2. Open **Devices > Simagic Neo X > LEDs**. Under **Telemetry Leds**, use **Import profile** to import `dist\Dila Racing - Neo X Rev Strip.ledsprofile`, then select **Dila Racing - Neo X Rev Strip**. This is the separate nine-LED rev/telemetry profile, not Buttons lighting or the whole-wheel Individual leds profile. Leave **Individual leds profiles** disabled, as in the existing Dila setup.
3. Keep **Dila Racing - Neo X Buttons** selected for Buttons lighting. Export the old rev profile if you want an easy rollback. If the new effect cannot be resolved, confirm the updated plugin loaded, then add **Dila Racing Neo X rev strip** from the Dila Racing effects library to a blank nine-LED telemetry profile at position 1.
4. Open **Dila Racing > Simagic Neo X > Neo X rev strip**. Select a mode, brightness and thresholds, then use **Save & apply** at the bottom of the page.
5. Use **Test saved strip for 5 seconds**. It sweeps synthetic RPM from 50% through the shift cue and redline, then back down; it works without a game. This test uses saved settings and affects only the strip. The local preview uses draft settings after **Preview strip / restart**. **Stop test**, leaving the settings page, applying settings, or unloading the plugin cancels the hardware test.
6. Start each game and confirm the rev-strip profile remains selected: SimHub may remember a different profile per game. Set/check the current car's redline in SimHub **Car settings** and verify that the cue appears at the intended RPM. The plugin consumes `LedsGameData.RPMToRedlinePercent` (0–100 percentage units) and `RPMRedlineReached`; it does not calculate optimal shift RPM from the engine's power curve.

| Mode | Buildup | Shift cue |
| --- | --- | --- |
| Progressive | Nine LEDs fill left to right: three green, three amber, three red | Whole strip bright/dim blue |
| Converging | Pairs fill from both ends towards a red centre LED | Whole strip bright/dim blue |
| Minimal | Late amber-to-red buildup, starting no earlier than 10 percentage points before the shift cue | Whole strip bright/dim white |
| Off | Black | Black |

Defaults: buildup starts at 70% of SimHub's redline, shift cue at 97%, brightness 25%, flash speed 6 cycles/second. The 97% cue is an adjustable starting point, not a car-specific optimal shift recommendation. At SimHub redline or 100% of its redline RPM, all enabled modes switch to bright/dim red. Fresh redline edges start bright and brief dips remain dim red for up to 300 ms. The strip redline warning is independent of the button warning checkbox and fixed button RPM override. Reverse strip direction if physical LED ordering runs the other way. Converging is symmetric, so reversing it looks identical.

**Rev strip colours** is independent of the pattern selector. **Green / amber / red** keeps the pattern colours and shift/limiter cues described above (Minimal uses its late amber/red buildup). **Single colour** enables a colour picker and uses the chosen colour for buildup, shift cue and limiter, with bright/dim flashing at the thresholds. It works with Progressive, Converging and Minimal and respects strip brightness, reverse direction, Off and idle. Existing settings keep the multicolour scheme. The scheme and chosen colour are included in saved custom themes. This controls the dedicated Neo X strip; button warning colours remain separate.

The strip is black out of game. Missing, zero or invalid RPM percentages leave buildup dark; an explicit SimHub redline signal can still show the warning. SimHub's device/profile brightness also scales the strip brightness. After this one-time import, changing modes or colour schemes requires only **Save & apply**, not another import.

## Calibrate bottom-to-top movement

Expand **Calibrate bottom-to-top layout**. Use **Identify LED for 5 seconds** to locate each channel. Enter the channel numbers for each physical row, with the bottom row first. Every channel must occur exactly once. For example, this is syntax only, **not a verified wheel layout**:

```text
1,2,3,4
5,6,7,8
9,10,11,12
```

The first row begins changing first; the last row completes last. The preview shows channel order, not a wheel illustration. Encoders represented by multiple channels will need a dedicated mapping in a later version.

## Two iFlag boxes: reuse native SimHub matrix output

If you already use a third-party matrix profile, check SimHub's Arduino hardware page to confirm that SimHub handles the connection. Export the current profile before switching to native flags.

1. In **Arduino > RGB Matrix**, create a separate **Dila Racing Flags** profile.
2. Add a **game running** group and the native **Flags** effect. Enable the desired available flags; remove gear/spotter/idle content if the boxes should display only flags.
3. In Arduino hardware configuration, assign the same matrix content to both existing boards. Keep their current matrix type and rotation. If each needs different content, use separate content slots instead.
4. Select the profile for LMU, RaceRoom and AC Evo. Return to the prior profile to roll back.
5. Use the matrix test-data controls to test every enabled flag on both boxes. No firmware changes are part of this prototype.

Flag switches, patterns, priority, brightness and rotation remain in SimHub's matrix editor. The plugin's iFlag tab is a setup guide, not a replacement matrix editor. Native flag availability depends on the installed SimHub version and the game. Do not assume a zero flag/limiter value means supported-but-inactive: verify in a live session. The plugin diagnostics show the normalised values, not a capability declaration.

## Test checklist

Automated core tests: independent idle/racing modes, state transitions and playback restarts, fade progression/endpoints/repeat continuity, priority, blink phases, idle handling, disabled effects, threshold fallback, brightness, immutable snapshots and mapping validation. SDK smoke checks also cover migration of legacy settings and persistence of both mode choices and the layout.

Integration/hardware checks:

- DLL loads; effect appears in the LED library; both profiles import and select the intended wheel.
- Settings survive a SimHub restart and a game change. No errors in SimHub's log.
- In the LED test-data editor, toggle Game Running, redline and pit limiter. The effect uses the supplied `LedsGameData`, so editor data remains authoritative.
- With idle set to repeat and racing set to static gradient, verify animation when Game Running is false, a stationary bottom-to-top gradient when true, and a restarted animation on returning to idle. Verify redline and pit warnings still override the racing gradient, then restore it when cleared.
- Identify every button channel and save a verified physical layout for each wheel.
- Test simultaneous pit-limiter/redline, warnings clearing, idle off, and 5-second test expiry.
- Test TC and ABS separately and together, including sharing an LED, enabled-but-inactive assists, clearing interventions, and returning to idle. Confirm real interventions in each game/car; the hardware test confirms the lighting path, not that a game supplies the signal.
- Test all three rev-strip modes, reverse direction, threshold/shift/redline boundaries, idle, settings persistence and profile switching between games. Verify the physical nine-LED order and actual shift RPM on the Neo X; SDK tests alone cannot confirm the physical output.
- LMU, RaceRoom and AC Evo: observe real limiter on/off, redline and the flags each game exposes. Compare with SimHub's available properties. Record missing signals instead of substituting pit-lane or speed heuristics.
- Both flag boxes show the selected native flags, at the correct orientation. Test reconnects and game exits.

## Remove / roll back

Select the original profiles in SimHub, exit SimHub and remove only `DilaRacing.SimHub.dll`. No device firmware or existing profiles are changed by the build. Settings saved by SimHub may remain for later reinstall.

## Sources

- [SimHub plugin and effect SDK](https://github.com/SHWotever/SimHub/wiki/Plugin-and-extensions-SDKs)
- [LED editor and test data](https://github.com/SHWotever/SimHub/wiki/LED-Editor-guide)
- [iFlag and multiple RGB matrices](https://github.com/SHWotever/SimHub/wiki/Arduino-RGB-Matrix)

SDK examples used as the API reference are installed in `SimHub\PluginSdk`. Implementation uses the demonstrated interfaces and does not alter private SimHub objects or copy TrackThirtyTwo's implementation.

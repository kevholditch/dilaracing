# Setup and testing

Native C# / WPF SimHub plugin for Simagic Neo X button lighting (12 channels) and Cube Controls F-Pro lighting (18 channels). Uses SimHub's installed SDK. It does not open USB/serial ports or read game-specific telemetry.

## Status

This is the first prototype. It compiles against the installed SDK, passes 27 core lighting assertions and passes direct SDK rendering smoke checks using SimHub's `TestLedsGameData`. Its WPF settings screen has been rendered and visually inspected. SimHub discovery, profile imports, physical LED mappings and live LMU/RaceRoom/AC Evo behaviour still require integration and hardware checks. No physical wheel layout is assumed: the initial layout is one uncalibrated row. The Neo X's encoder lights and rev strip are separate channels and are not controlled by this version.

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

## Effects

- Solid colour; static vertical gradient; bottom-to-top fade once or repeatedly.
- Adjustable two-colour theme, brightness, fade duration and flash frequency.
- Redline: SimHub's `LedsGameData.RPMRedlineReached`; optional fixed RPM threshold (0 disables). Use SimHub's car settings for per-car redline calibration. The fixed threshold in this prototype is per wheel, not per car.
- Pit limiter: SimHub's `LedsGameData.PitLimiterOn`, alternating blue/white. It takes priority over redline, then the theme.
- Theme animation resumes at its ongoing time when a warning ends. Save/apply restarts the fade. Repeating fades hold both end colours and fade back bottom to top.
- Out of game, stale warning fields are ignored. Idle theme can be disabled. SimHub controls the game-running status; there is no independent raw telemetry freshness watchdog.
- Local preview does not write hardware. Hardware tests use saved settings and expire after 5 seconds. Closing the settings page or unloading the plugin cancels tests.

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

Automated core tests: fade progression/endpoints/repeat continuity, priority, blink phases, idle handling, disabled effects, threshold fallback, brightness, immutable snapshots and mapping validation.

Integration/hardware checks:

- DLL loads; effect appears in the LED library; both profiles import and select the intended wheel.
- Settings survive a SimHub restart and a game change. No errors in SimHub's log.
- In the LED test-data editor, toggle Game Running, redline and pit limiter. The effect uses the supplied `LedsGameData`, so editor data remains authoritative.
- Identify every button channel and save a verified physical layout for each wheel.
- Test simultaneous pit-limiter/redline, warnings clearing, idle off, and 5-second test expiry.
- LMU, RaceRoom and AC Evo: observe real limiter on/off, redline and the flags each game exposes. Compare with SimHub's available properties. Record missing signals instead of substituting pit-lane or speed heuristics.
- Both flag boxes show the selected native flags, at the correct orientation. Test reconnects and game exits.

## Remove / roll back

Select the original profiles in SimHub, exit SimHub and remove only `DilaRacing.SimHub.dll`. No device firmware or existing profiles are changed by the build. Settings saved by SimHub may remain for later reinstall.

## Sources

- [SimHub plugin and effect SDK](https://github.com/SHWotever/SimHub/wiki/Plugin-and-extensions-SDKs)
- [LED editor and test data](https://github.com/SHWotever/SimHub/wiki/LED-Editor-guide)
- [iFlag and multiple RGB matrices](https://github.com/SHWotever/SimHub/wiki/Arduino-RGB-Matrix)

SDK examples used as the API reference are installed in `SimHub\PluginSdk`. Implementation uses the demonstrated interfaces and does not alter private SimHub objects or copy TrackThirtyTwo's implementation.

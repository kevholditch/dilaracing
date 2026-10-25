using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class PreviewProgram
{
    private static string simHubPath;
    [STAThread]
    public static int Main(string[] args)
    {
        simHubPath = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\SimHub";
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try { Run(args.Length > 1 ? args[1] : "preview.png"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        string path = Path.Combine(simHubPath, name);
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(string output)
    {
        var application = new Application();
        var plugin = new DilaRacing.DilaPlugin();
        var effect = new DilaRacing.WheelEffect();
        Console.WriteLine("Effect ContainerType: " + effect.ContainerType);
        DilaRacing.DilaPlugin.Current = plugin;
        var input = new SimHub.Plugins.DataPlugins.RGBDriverCommon.TestLedsGameData { GameRunning = true, PitLimiterOn = true, RPMRedlineReached = true };
        var result = new SimHub.Plugins.DataPlugins.RGBDriver.LedResult(new WoteverCommon.HSLPreference());
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        var color = result[0];
        if (!((color.B > 0 && color.R == 0 && color.G == 0) || (color.R > 0 && color.R == color.G && color.G == color.B)))
            throw new Exception("SDK test data did not produce a blue/white limiter frame.");
        effect.Wheel = DilaRacing.WheelId.FPro;
        input.GameRunning = false;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[17].R == 0 || result[17].R != result[17].B) throw new Exception("F-Pro did not return to its idle theme.");
        plugin.BeginTest(DilaRacing.WheelId.FPro, DilaRacing.PreviewMode.Identify, 17);
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[0].R != 0 || result[17].R == 0) throw new Exception("Identify did not isolate the selected channel.");
        plugin.StopTest();
        DilaRacing.DilaPlugin.Current = null;
        effect.SetResultBase(input, result, new List<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>());
        if (result[17].R != 0) throw new Exception("Unloaded plugin should output black.");
        Console.WriteLine("PASS: SDK effect smoke checks (limiter priority, F-Pro length, idle, identify, unload).");
        var settings = new DilaRacing.SettingsControl(plugin);
        settings.Measure(new Size(1000, 1500));
        settings.Arrange(new Rect(0, 0, 1000, 1500));
        settings.UpdateLayout();
        var target = new RenderTargetBitmap(1000, 1500, 96, 96, PixelFormats.Pbgra32);
        target.Render(settings);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
        using (var stream = File.Create(output)) encoder.Save(stream);
        Console.WriteLine("Rendered: " + output);
        application.Shutdown();
    }
}

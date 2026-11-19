using System.Globalization;
using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Theming;

namespace SaglikTakip.Screenshots;

/// <summary>
/// Usage: <c>dotnet run --project tools/Screenshots -- [icons|screenshots|all]</c> (default: screenshots).
/// "icons" rasterizes assets/logo.svg into assets/app.ico and assets/logo-256.png; "screenshots" writes
/// the README images to screenshots/ at 1x and 2x. No window is shown and the focus is never taken.
/// </summary>
internal static class Program
{
    private static readonly int[] IconSizes = [16, 20, 24, 32, 40, 48, 64, 256];

    [STAThread]
    private static int Main(string[] args)
    {
        // Before any window handle exists: a DPI unaware process always measures at 96 DPI, which makes
        // the 1x images identical on every machine; the 2x images are scaled explicitly.
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "screenshots";
        var root = FindRepositoryRoot();
        if (command is "icons" or "all")
        {
            WriteIcons(root);
        }

        if (command is "screenshots" or "all")
        {
            return WriteScreenshots(root);
        }

        return command is "icons" ? 0 : Usage();
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: SaglikTakip.Screenshots [icons|screenshots|all]");
        return 2;
    }

    private static void WriteIcons(string root)
    {
        var svg = Path.Combine(root, "assets", "logo.svg");
        var frames = IconSizes.Select(size => SvgRasterizer.Render(svg, size)).ToList();
        try
        {
            IconWriter.Write(Path.Combine(root, "assets", "app.ico"), frames);
            frames[^1].Save(Path.Combine(root, "assets", "logo-256.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            frames.ForEach(frame => frame.Dispose());
        }

        Console.WriteLine("assets/app.ico, assets/logo-256.png");
    }

    private static int WriteScreenshots(string root)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // A message box would be a visible window: record the request and fail the run instead.
        var unexpected = new List<string>();
        Dialogs.Override = (text, caption, _, _) =>
        {
            unexpected.Add(caption + ": " + text);
            return DialogResult.No;
        };

        var output = Path.Combine(root, "screenshots");
        foreach (var scale in new[] { 1F, 2F })
        {
            Theme.Use(ThemePalette.Light, scale);
            foreach (var (name, minimizeBox, build) in Screens.All)
            {
                using var services = SyntheticData.CreateServices();
                using var form = build(services);
                using var image = OffscreenCapture.Capture(form, scale, minimizeBox);
                var file = scale > 1 ? $"{name}-2x.png" : $"{name}.png";
                OffscreenCapture.Save(image, Path.Combine(output, file));
                Console.WriteLine($"screenshots/{file} ({image.Width}x{image.Height})");
            }
        }

        Theme.Use(ThemePalette.Light);
        if (unexpected.Count > 0)
        {
            Console.Error.WriteLine("A screen tried to open a message box:");
            unexpected.ForEach(Console.Error.WriteLine);
            return 1;
        }

        return 0;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SaglikTakip.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("SaglikTakip.sln was not found above the tool's folder.");
    }
}

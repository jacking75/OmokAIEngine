using System;
using System.IO;
using Svg.Skia;
using SkiaSharp;

// Usage: SvgToPng <inputDir> <outputDir> [--scale 1.0] [--force]
// Rasterizes all *.svg files in inputDir to PNG in outputDir.
// Skips files where PNG is newer than SVG unless --force is passed.

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: SvgToPng <inputDir> <outputDir> [--scale N] [--force]");
    return 1;
}

string inputDir = args[0];
string outputDir = args[1];
float scale = 1.0f;
bool force = false;

for (int i = 2; i < args.Length; i++)
{
    if (args[i] == "--force") force = true;
    else if (args[i] == "--scale" && i + 1 < args.Length)
        float.TryParse(args[++i], out scale);
}

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"Input directory not found: {inputDir}");
    return 1;
}

Directory.CreateDirectory(outputDir);

int converted = 0;
int skipped = 0;

foreach (string svgPath in Directory.GetFiles(inputDir, "*.svg"))
{
    string name = Path.GetFileNameWithoutExtension(svgPath);
    string pngPath = Path.Combine(outputDir, name + ".png");

    if (!force && File.Exists(pngPath) &&
        File.GetLastWriteTimeUtc(pngPath) >= File.GetLastWriteTimeUtc(svgPath))
    {
        Console.WriteLine($"  skip  {name}.png (up to date)");
        skipped++;
        continue;
    }

    try
    {
        RasterizeFile(svgPath, pngPath, scale);
        Console.WriteLine($"  wrote {name}.png");
        converted++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  ERROR {name}: {ex.Message}");
    }
}

Console.WriteLine($"Done: {converted} converted, {skipped} skipped.");
return 0;

static void RasterizeFile(string svgPath, string pngPath, float scale)
{
    using var svg = new SKSvg();
    svg.Load(svgPath);

    if (svg.Picture == null)
        throw new InvalidOperationException("SVG could not be parsed.");

    var bounds = svg.Picture.CullRect;
    int width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
    int height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));

    var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(scale);
    canvas.DrawPicture(svg.Picture);
    canvas.Flush();

    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var stream = File.OpenWrite(pngPath);
    data.SaveTo(stream);
}

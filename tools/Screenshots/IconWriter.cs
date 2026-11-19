using System.Drawing;
using System.Drawing.Imaging;

namespace SaglikTakip.Screenshots;

/// <summary>
/// Writes a Windows .ico file. Frames up to 48 px are stored as 32-bit bitmaps (with an AND mask) for
/// the widest compatibility; larger frames are stored as PNG, as Windows expects for 256 px.
/// </summary>
internal static class IconWriter
{
    private const int PngThreshold = 64;

    public static void Write(string path, IReadOnlyList<Bitmap> frames)
    {
        var images = frames.Select(Encode).ToList();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0); // Reserved.
        writer.Write((ushort)1); // Icon.
        writer.Write((ushort)frames.Count);

        var offset = 6 + (16 * frames.Count);
        for (var i = 0; i < frames.Count; i++)
        {
            var size = frames[i].Width;
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0); // No palette.
            writer.Write((byte)0);
            writer.Write((ushort)1); // Planes.
            writer.Write((ushort)32); // Bits per pixel.
            writer.Write(images[i].Length);
            writer.Write(offset);
            offset += images[i].Length;
        }

        foreach (var image in images)
        {
            writer.Write(image);
        }
    }

    private static byte[] Encode(Bitmap frame)
    {
        if (frame.Width >= PngThreshold)
        {
            using var png = new MemoryStream();
            frame.Save(png, ImageFormat.Png);
            return png.ToArray();
        }

        var size = frame.Width;
        var maskStride = ((size + 31) / 32) * 4;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // BITMAPINFOHEADER; the height covers the color image and the mask.
        writer.Write(40);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0); // BI_RGB.
        writer.Write((size * size * 4) + (maskStride * size));
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        // Pixels bottom-up, BGRA.
        for (var y = size - 1; y >= 0; y--)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = frame.GetPixel(x, y);
                writer.Write(pixel.B);
                writer.Write(pixel.G);
                writer.Write(pixel.R);
                writer.Write(pixel.A);
            }
        }

        // AND mask: a set bit marks a transparent pixel; alpha already carries it, but old readers use the mask.
        for (var y = size - 1; y >= 0; y--)
        {
            var row = new byte[maskStride];
            for (var x = 0; x < size; x++)
            {
                if (frame.GetPixel(x, y).A == 0)
                {
                    row[x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }

            writer.Write(row);
        }

        return stream.ToArray();
    }
}

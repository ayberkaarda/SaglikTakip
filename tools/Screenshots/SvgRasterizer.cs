using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Xml.Linq;

namespace SaglikTakip.Screenshots;

/// <summary>
/// Rasterizes the small subset of SVG used by assets/logo.svg with GDI+: a viewBox, rect (with rx),
/// circle and path elements with absolute M, L, H, V and Z commands, solid fills and round strokes.
/// Anything else is rejected, so the logo cannot silently drift from what this tool can draw.
/// </summary>
internal static class SvgRasterizer
{
    public static Bitmap Render(string svgPath, int size)
    {
        var document = XDocument.Load(svgPath);
        var root = document.Root ?? throw new InvalidDataException("Empty SVG.");
        var viewBox = Numbers(root.Attribute("viewBox")?.Value ?? throw new InvalidDataException("The SVG needs a viewBox."));

        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);
        graphics.ScaleTransform(size / viewBox[2], size / viewBox[3]);
        graphics.TranslateTransform(-viewBox[0], -viewBox[1]);

        foreach (var element in root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "title":
                case "desc":
                    break;
                case "rect":
                    Draw(graphics, element, Rect(element));
                    break;
                case "circle":
                    Draw(graphics, element, Circle(element));
                    break;
                case "path":
                    Draw(graphics, element, Path(element.Attribute("d")?.Value ?? string.Empty));
                    break;
                default:
                    throw new InvalidDataException($"Unsupported SVG element <{element.Name.LocalName}>.");
            }
        }

        return bitmap;
    }

    private static void Draw(Graphics graphics, XElement element, GraphicsPath path)
    {
        using (path)
        {
            if (Paint(element, "fill") is { } fill)
            {
                using var brush = new SolidBrush(fill);
                graphics.FillPath(brush, path);
            }

            if (Paint(element, "stroke") is { } stroke)
            {
                using var pen = new Pen(stroke, Number(element, "stroke-width", 1));
                if (element.Attribute("stroke-linecap")?.Value == "round")
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                }

                if (element.Attribute("stroke-linejoin")?.Value == "round")
                {
                    pen.LineJoin = LineJoin.Round;
                }

                graphics.DrawPath(pen, path);
            }
        }
    }

    private static GraphicsPath Rect(XElement element)
    {
        var x = Number(element, "x", 0);
        var y = Number(element, "y", 0);
        var width = Number(element, "width", 0);
        var height = Number(element, "height", 0);
        var radius = Number(element, "rx", 0);
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(new RectangleF(x, y, width, height));
            return path;
        }

        var d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath Circle(XElement element)
    {
        var r = Number(element, "r", 0);
        var path = new GraphicsPath();
        path.AddEllipse(Number(element, "cx", 0) - r, Number(element, "cy", 0) - r, 2 * r, 2 * r);
        return path;
    }

    private static GraphicsPath Path(string data)
    {
        var path = new GraphicsPath();
        var tokens = Tokenize(data);
        var current = PointF.Empty;
        var start = PointF.Empty;
        var command = ' ';
        for (var i = 0; i < tokens.Count;)
        {
            if (char.IsLetter(tokens[i][0]))
            {
                command = tokens[i][0];
                i++;
            }

            switch (command)
            {
                case 'M':
                    path.StartFigure();
                    current = start = new PointF(Parse(tokens[i]), Parse(tokens[i + 1]));
                    i += 2;
                    command = 'L'; // Further pairs after M are line segments.
                    break;
                case 'L':
                    current = LineTo(path, current, new PointF(Parse(tokens[i]), Parse(tokens[i + 1])));
                    i += 2;
                    break;
                case 'H':
                    current = LineTo(path, current, new PointF(Parse(tokens[i]), current.Y));
                    i++;
                    break;
                case 'V':
                    current = LineTo(path, current, new PointF(current.X, Parse(tokens[i])));
                    i++;
                    break;
                case 'Z':
                    path.CloseFigure();
                    current = start;
                    break;
                default:
                    throw new InvalidDataException($"Unsupported path command '{command}'.");
            }
        }

        return path;
    }

    private static PointF LineTo(GraphicsPath path, PointF from, PointF to)
    {
        path.AddLine(from, to);
        return to;
    }

    private static List<string> Tokenize(string data)
    {
        var tokens = new List<string>();
        var number = new System.Text.StringBuilder();
        foreach (var c in data)
        {
            if (char.IsLetter(c))
            {
                Flush();
                tokens.Add(c.ToString());
            }
            else if (c is ' ' or ',' or '\n' or '\r' or '\t')
            {
                Flush();
            }
            else
            {
                if (c == '-' && number.Length > 0)
                {
                    Flush();
                }

                number.Append(c);
            }
        }

        Flush();
        return tokens;

        void Flush()
        {
            if (number.Length > 0)
            {
                tokens.Add(number.ToString());
                number.Clear();
            }
        }
    }

    private static Color? Paint(XElement element, string attribute)
    {
        var value = element.Attribute(attribute)?.Value;
        if (value is null || value == "none")
        {
            return null;
        }

        if (value.Length != 7 || value[0] != '#')
        {
            throw new InvalidDataException($"Only #RRGGBB colors are supported ({attribute}=\"{value}\").");
        }

        var rgb = int.Parse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }

    private static float Number(XElement element, string attribute, float fallback) =>
        element.Attribute(attribute)?.Value is { } value ? Parse(value) : fallback;

    private static float[] Numbers(string value) =>
        value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToArray();

    private static float Parse(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}

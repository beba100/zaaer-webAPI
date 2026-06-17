using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Security.Cryptography;
using System.Text;

namespace zaaerIntegration.Utilities
{
    public static class ResortTicketGateIconGenerator
    {
        public static byte[] RenderPng(string stationCode, int size)
        {
            var label = BuildLabel(stationCode);
            var bg = ResolveColor(stationCode);

            using var bitmap = new Bitmap(size, size);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            using var brush = new SolidBrush(bg);
            graphics.FillEllipse(brush, 4, 4, size - 8, size - 8);

            using var font = new Font("Segoe UI", Math.Max(12, size / 4), FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            graphics.DrawString(label, font, textBrush, new RectangleF(0, 0, size, size), format);

            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }

        public static string ResolveThemeColor(string stationCode)
        {
            var color = ResolveColor(stationCode);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static string BuildLabel(string stationCode)
        {
            var normalized = (stationCode ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized == "entry")
            {
                return "IN";
            }

            if (normalized == "games")
            {
                return "GM";
            }

            if (normalized == "pool")
            {
                return "PL";
            }

            var parts = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                return new string(new[] { parts[^1][0], parts[^1].Length > 1 ? parts[^1][1] : parts[0][0] }).ToUpperInvariant();
            }

            return normalized.Length >= 2
                ? normalized[..2].ToUpperInvariant()
                : normalized.ToUpperInvariant();
        }

        private static Color ResolveColor(string stationCode)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((stationCode ?? "gate").ToLowerInvariant()));
            var hue = (bytes[0] + bytes[1] * 256) % 360;
            return ColorFromHsl(hue, 0.52, 0.42);
        }

        private static Color ColorFromHsl(double h, double s, double l)
        {
            h /= 360d;
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            var r = HueToRgb(p, q, h + 1d / 3d);
            var g = HueToRgb(p, q, h);
            var b = HueToRgb(p, q, h - 1d / 3d);
            return Color.FromArgb(
                (int)Math.Round(r * 255),
                (int)Math.Round(g * 255),
                (int)Math.Round(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0)
            {
                t += 1;
            }

            if (t > 1)
            {
                t -= 1;
            }

            if (t < 1d / 6d)
            {
                return p + (q - p) * 6 * t;
            }

            if (t < 1d / 2d)
            {
                return q;
            }

            if (t < 2d / 3d)
            {
                return p + (q - p) * (2d / 3d - t) * 6;
            }

            return p;
        }
    }
}

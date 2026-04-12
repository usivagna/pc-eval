// Minimal stub for Microsoft.Maui.Graphics.Color to allow the shared
// DisplayLogic / ProcessorLogic source files to compile in a plain .NET
// unit-test project (without the full MAUI framework).

// ReSharper disable once CheckNamespace
namespace Microsoft.Maui.Graphics
{
    public sealed class Color
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        private Color(float r, float g, float b, float a = 1f)
        {
            R = r; G = g; B = b; A = a;
        }

        public static Color FromArgb(string hex)
        {
            // Strip leading '#'
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                float r = Convert.ToInt32(hex[..2], 16) / 255f;
                float g = Convert.ToInt32(hex[2..4], 16) / 255f;
                float b = Convert.ToInt32(hex[4..6], 16) / 255f;
                return new Color(r, g, b);
            }
            if (hex.Length == 8)
            {
                float a = Convert.ToInt32(hex[..2], 16) / 255f;
                float r = Convert.ToInt32(hex[2..4], 16) / 255f;
                float g = Convert.ToInt32(hex[4..6], 16) / 255f;
                float b = Convert.ToInt32(hex[6..8], 16) / 255f;
                return new Color(r, g, b, a);
            }
            return new Color(0, 0, 0);
        }

        public static Color FromRgb(int r, int g, int b) =>
            new(r / 255f, g / 255f, b / 255f);

        // Named colour shortcuts used in tests / logic
        public static Color Gray  => FromArgb("808080");
        public static Color Red   => FromArgb("FF0000");
        public static Color Green => FromArgb("008000");

        public override string ToString() =>
            $"#{(int)(R*255):X2}{(int)(G*255):X2}{(int)(B*255):X2}";

        public override bool Equals(object? obj) =>
            obj is Color c && c.R == R && c.G == G && c.B == B && c.A == A;

        public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    }
}

// Provide the global 'Colors' alias expected by the shared source files
// ─────────────────────────────────────────────────────────────────────
namespace Microsoft.Maui.Graphics
{
    public static class Colors
    {
        public static Color Gray  => Color.Gray;
        public static Color Red   => Color.Red;
        public static Color Green => Color.Green;
        public static Color White => Color.FromArgb("FFFFFF");
        public static Color Black => Color.FromArgb("000000");
    }
}

namespace PlayerModelLib;

public static class OklchConverter
{
    public static (double L, double C, double H) RgbToOklch(byte r, byte g, byte b)
    {
        // sRGB -> Linear
        double lr = ToLinear(r);
        double lg = ToLinear(g);
        double lb = ToLinear(b);

        // Linear RGB -> LMS
        double l = 0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb;
        double m = 0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb;
        double s = 0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb;

        // Cube root
        double l_ = Math.Cbrt(l);
        double m_ = Math.Cbrt(m);
        double s_ = Math.Cbrt(s);

        // LMS -> OKLab
        double L = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_;
        double a = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_;
        double bLab = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_;

        // OKLab -> OKLCH
        double C = Math.Sqrt(a * a + bLab * bLab);
        double H = Math.Atan2(bLab, a) * (180.0 / Math.PI);
        if (H < 0) H += 360.0;

        return (L, C, H);
    }

    public static (byte R, byte G, byte B) OklchToRgb(double L, double C, double H)
    {
        // OKLCH -> OKLab
        double hRad = H * (Math.PI / 180.0);
        double a = C * Math.Cos(hRad);
        double b = C * Math.Sin(hRad);

        // OKLab -> cube-root LMS
        double l_ = L + 0.3963377774 * a + 0.2158037573 * b;
        double m_ = L - 0.1055613458 * a - 0.0638541728 * b;
        double s_ = L - 0.0894841775 * a - 1.2914855480 * b;

        // Cube
        double l = l_ * l_ * l_;
        double m = m_ * m_ * m_;
        double s = s_ * s_ * s_;

        // LMS -> Linear RGB
        double lr = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        double lg = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        double lb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;

        // Linear -> sRGB
        return (ToSRGB(lr), ToSRGB(lg), ToSRGB(lb));
    }

    private static double ToLinear(byte channel)
    {
        double v = channel / 255.0;
        return v <= 0.04045
            ? v / 12.92
            : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static byte ToSRGB(double linear)
    {
        double v = linear <= 0.0031308
            ? 12.92 * linear
            : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(v * 255.0, 0.0, 255.0));
    }
}
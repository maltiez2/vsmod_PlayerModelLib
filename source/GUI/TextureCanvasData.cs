using Vintagestory.API.Common;

namespace PlayerModelLib;

public readonly struct TextureCanvasSkinPartConfig
{
    public readonly int Width;
    public readonly int Height;
    public readonly int Colors;
}

public class TextureCanvasData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int[] Colors { get; set; } = [];
    public int[] Pixels { get; set; } = [];

    public string Serialize()
    {
        return Convert.ToBase64String(ToBytes());
    }

    public static TextureCanvasData Deserialize(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return Blank(1, 1);

        try
        {
            return FromBytes(Convert.FromBase64String(data));
        }
        catch
        {
            return Blank(1, 1);
        }
    }

    public byte[] ToBytes()
    {
        int colorCount = Colors.Length;
        int bitsPerIndex = colorCount <= 1 ? 1 :
                           (int)Math.Ceiling(Math.Log2(colorCount));

        int pixelCount = Pixels.Length;
        int headerSize = 4 + 4 + 4 + 4;
        int colorsSize = colorCount * 4;
        // Pack pixels tightly at bit level
        int pixelsBitCount = pixelCount * bitsPerIndex;
        int pixelsSize = (pixelsBitCount + 7) / 8; // round up to nearest byte

        int totalSize = headerSize + colorsSize + pixelsSize;
        byte[] buffer = new byte[totalSize];
        int offset = 0;

        WriteInt32(buffer, ref offset, Width);
        WriteInt32(buffer, ref offset, Height);
        WriteInt32(buffer, ref offset, colorCount);
        WriteInt32(buffer, ref offset, pixelCount);

        foreach (int color in Colors)
            WriteInt32(buffer, ref offset, color);

        // Build color -> index lookup
        Dictionary<int, int> colorIndexMap = new(colorCount);
        for (int i = 0; i < Colors.Length; i++)
            colorIndexMap.TryAdd(Colors[i], i);

        // Write pixel indices packed at bit level
        int bitOffset = 0;
        foreach (int pixel in Pixels)
        {
            int index = colorIndexMap.TryGetValue(pixel, out int idx) ? idx : 0;
            WriteBits(buffer, offset, ref bitOffset, index, bitsPerIndex);
        }

        return buffer;
    }

    public static TextureCanvasData FromBytes(byte[] data)
    {
        int offset = 0;

        if (!TryReadInt32(data, ref offset, out int width) ||
            !TryReadInt32(data, ref offset, out int height) ||
            !TryReadInt32(data, ref offset, out int colorCount) ||
            !TryReadInt32(data, ref offset, out int pixelCount))
            return Blank(1, 1);

        if (width < 1 || height < 1 ||
            colorCount < 0 || pixelCount < 0 ||
            pixelCount != width * height)
            return Blank(width < 1 ? 1 : width, height < 1 ? 1 : height);

        int bitsPerIndex = colorCount <= 1 ? 1 :
                           (int)Math.Ceiling(Math.Log2(colorCount));

        int requiredColorBytes = colorCount * 4;
        int requiredPixelBytes = (pixelCount * bitsPerIndex + 7) / 8;
        if (data.Length - offset < requiredColorBytes + requiredPixelBytes)
            return Blank(width, height);

        int[] colors = new int[colorCount];
        for (int i = 0; i < colorCount; i++)
            colors[i] = ReadInt32(data, ref offset);

        int[] pixels = new int[pixelCount];
        int bitOffset = 0;
        for (int i = 0; i < pixelCount; i++)
        {
            int index = ReadBits(data, offset, ref bitOffset, bitsPerIndex);
            pixels[i] = index < colorCount ? colors[index] : 0;
        }

        return new TextureCanvasData
        {
            Width = width,
            Height = height,
            Colors = colors,
            Pixels = pixels
        };
    }

    public BakedBitmap ToBitmap()
    {
        return new BakedBitmap
        {
            Width = Width,
            Height = Height,
            TexturePixels = Pixels
        };
    }


    private static TextureCanvasData Blank(int width, int height) => new()
    {
        Width = width,
        Height = height,
        Colors = [],
        Pixels = new int[width * height]
    };

    private static bool TryReadInt32(byte[] data, ref int offset, out int value)
    {
        if (data.Length - offset < 4)
        {
            value = 0;
            return false;
        }
        value = ReadInt32(data, ref offset);
        return true;
    }

    private static void WriteBits(byte[] buffer, int byteOffset, ref int bitOffset, int value, int bitCount)
    {
        for (int i = bitCount - 1; i >= 0; i--)
        {
            int bit = (value >> i) & 1;
            int byteIndex = byteOffset + bitOffset / 8;
            int bitIndex = 7 - (bitOffset % 8); // MSB first
            if (bit == 1)
                buffer[byteIndex] |= (byte)(1 << bitIndex);
            bitOffset++;
        }
    }

    private static int ReadBits(byte[] data, int byteOffset, ref int bitOffset, int bitCount)
    {
        int value = 0;
        for (int i = 0; i < bitCount; i++)
        {
            int byteIndex = byteOffset + bitOffset / 8;
            int bitIndex = 7 - (bitOffset % 8); // MSB first
            int bit = (data[byteIndex] >> bitIndex) & 1;
            value = (value << 1) | bit;
            bitOffset++;
        }
        return value;
    }

    private static void WriteInt32(byte[] buffer, ref int offset, int value)
    {
        buffer[offset++] = (byte)(value >> 24);
        buffer[offset++] = (byte)(value >> 16);
        buffer[offset++] = (byte)(value >> 8);
        buffer[offset++] = (byte)value;
    }
    
    private static int ReadInt32(byte[] data, ref int offset)
    {
        int value = (data[offset] << 24) |
                    (data[offset + 1] << 16) |
                    (data[offset + 2] << 8) |
                    data[offset + 3];
        offset += 4;
        return value;
    }
}
using SkiaSharp;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace PlayerModelLib;

public static class TextureUtils
{
    public const string TexturesCategory = "textures";
    public const string TexturesPrefixPath = "textures/";
    public const string RotationSuffix = "@";
    public const int ValidRotation90 = 90;
    public const int ValidRotation180 = 180;
    public const int ValidRotation270 = 270;
    public const int MinAlphaValue = 0;
    public const int MaxAlphaValue = 255;

    public static bool GetOrInsertTexture(
        ICoreClientAPI api,
        CompositeTexture compositeTexture,
        out int textureSubId,
        out TextureAtlasPosition texturePosition,
        ITextureAtlasAPI? targetAtlas = null,
        float alphaTest = 0f)
    {
        ClientMain game = api.World as ClientMain ?? throw new InvalidCastException();
        TextureAtlasManager atlasManager = (targetAtlas ?? api.EntityTextureAtlas) as TextureAtlasManager ?? throw new InvalidCastException();

        compositeTexture.Baked = Bake(game.AssetManager, compositeTexture);
        AssetLocationAndSource assetLocationAndSource = new(compositeTexture.Baked.BakedName, "Shape file ", compositeTexture.Base);

        return atlasManager.GetOrInsertTexture(
            compositeTexture.Baked.BakedName,
            out textureSubId,
            out texturePosition,
            () => TextureAtlasManager.LoadCompositeBitmap(game, assetLocationAndSource),
            alphaTest);
    }

    public static bool GetOrInsertTexture(ICoreClientAPI api, RecusiveOverlaysTexture recursiveOverlaysTexture, out int textureSubId, out TextureAtlasPosition texturePosition, ITextureAtlasAPI? targetAtlas = null, string? debugCode = null)
    {
        ClientMain game = api.World as ClientMain ?? throw new InvalidCastException();
        TextureAtlasManager atlasManager = (targetAtlas ?? api.EntityTextureAtlas) as TextureAtlasManager ?? throw new InvalidCastException();

        return GetOrInsertTexture(game, atlasManager, recursiveOverlaysTexture, out textureSubId, out texturePosition, debugCode: debugCode);
    }

    public static bool GetOrInsertTexture(
        ClientMain game,
        TextureAtlasManager atlasManager,
        RecusiveOverlaysTexture recursiveOverlaysTexture,
        out int textureSubId,
        out TextureAtlasPosition texturePosition,
        float alphaTest = 0f,
        string? debugCode = null)
    {
        AssetLocation uniqueName = BuildUniqueName(recursiveOverlaysTexture);

        return atlasManager.GetOrInsertTexture(
            uniqueName,
            out textureSubId,
            out texturePosition,
            () => LoadCompositeBitmap(game, recursiveOverlaysTexture, debugCode: debugCode),
            alphaTest);
    }

    public static void DebugPrintRecusiveOverlaysTexture(RecusiveOverlaysTexture node, int depth = 0)
    {
        string indent = new(' ', depth * 2);
        Debug.WriteLine($"{indent}[{node.BlendMode}] {node.Texture.Base}");

        foreach (RecusiveOverlaysTexture child in node.Overlays)
        {
            DebugPrintRecusiveOverlaysTexture(child, depth + 1);
        }
    }

    public static BakedBitmap LoadCompositeBitmap(ClientMain game, RecusiveOverlaysTexture recursiveOverlaysTexture, bool calledFromItself = false, string? debugCode = null, int depth = 0)
    {
        BakedBitmap? result = null;
        if (recursiveOverlaysTexture.Canvas && recursiveOverlaysTexture.SerializedCanvas != null)
        {
            string serializedData = recursiveOverlaysTexture.SerializedCanvas;
            TextureCanvasData canvasData = TextureCanvasData.Deserialize(serializedData);
            result = canvasData.ToBitmap();
        }
        
        if (!recursiveOverlaysTexture.SolidColor && !recursiveOverlaysTexture.Canvas)
        {
            BakedCompositeTexture baked = Bake(game.AssetManager, recursiveOverlaysTexture.Texture);
            AssetLocationAndSource baseLoc = new(baked.BakedName, "RecursiveOverlay", recursiveOverlaysTexture.Texture.Base);
            result = TextureAtlasManager.LoadCompositeBitmap(game, baseLoc);
        }

        if (recursiveOverlaysTexture.Overlays.Count == 0)
        {
            int color = ColorUtil.Hex2Int(recursiveOverlaysTexture.Color ?? "#00000000");
            result ??= CreateSolidColorBitmap(recursiveOverlaysTexture.SizeOverride.X, recursiveOverlaysTexture.SizeOverride.Y, color);
            return result;
        }

        foreach (RecusiveOverlaysTexture overlay in recursiveOverlaysTexture.Overlays)
        {
            BakedBitmap overlayBitmap = LoadCompositeBitmap(game, overlay, calledFromItself: true, debugCode: debugCode, depth: depth + 1);

            if (result == null && recursiveOverlaysTexture.SolidColor)
            {
                int color = ColorUtil.Hex2Int(recursiveOverlaysTexture.Color ?? "#00000000");
                result = CreateSolidColorBitmap(overlayBitmap.Width, overlayBitmap.Height, color);
            }

            if (overlayBitmap?.TexturePixels == null)
            {
                continue;
            }

            if (result.Width != overlayBitmap.Width || result.Height != overlayBitmap.Height)
            {
                game.Logger.Warning(
                    "RecursiveOverlay: overlay texture ({0}x{1}) does not match base texture size ({2}x{3}), ignoring.",
                    overlayBitmap.Width, overlayBitmap.Height,
                    result.Width, result.Height);
                continue;
            }

            TextureOverlayBlendingUtils.Blend(overlay.BlendMode, result.TexturePixels, overlayBitmap.TexturePixels, overlay.Color);
        }

        if (result == null && recursiveOverlaysTexture.SolidColor)
        {
            int color = ColorUtil.Hex2Int(recursiveOverlaysTexture.Color ?? "#00000000");
            result = CreateSolidColorBitmap(recursiveOverlaysTexture.SizeOverride.X, recursiveOverlaysTexture.SizeOverride.Y, color);
        }

        return result;
    }



    public static BakedCompositeTexture Bake(IAssetManager assetManager, CompositeTexture compositeTexture)
    {
        compositeTexture.WildCardNoFiles = null;

        ResolveWildcard(assetManager, compositeTexture);

        BakedCompositeTexture bakedTexture = new();
        bakedTexture.BakedName = compositeTexture.Base.Clone();

        ApplyBlendedOverlays(compositeTexture, bakedTexture);
        ApplyRotation(compositeTexture, bakedTexture);
        ApplyAlpha(compositeTexture, bakedTexture);
        BakeAlternates(assetManager, compositeTexture, bakedTexture);
        BakeTiles(assetManager, compositeTexture, bakedTexture);

        return bakedTexture;
    }

    

    public static void DiagnoseBakedBitmapAlpha(BakedBitmap bakedBitmap)
    {
        int[] pixels = bakedBitmap.Pixels;
        int fullyTransparent = 0;
        int fullyOpaque = 0;
        int semiTransparent = 0;

        foreach (int argb in pixels)
        {
            byte a = (byte)((argb >> 24) & 0xFF);

            if (a == 0) fullyTransparent++;
            else if (a == 255) fullyOpaque++;
            else semiTransparent++;
        }

        Debug.WriteLine($"Total pixels   : {pixels.Length}");
        Debug.WriteLine($"Fully opaque   : {fullyOpaque}");
        Debug.WriteLine($"Semi-transparent: {semiTransparent}");
        Debug.WriteLine($"Fully transparent: {fullyTransparent}");
    }

    public static SKBitmap ToSKBitmap(this BakedBitmap bakedBitmap)
    {
        int width = bakedBitmap.Width;
        int height = bakedBitmap.Height;

        SKImageInfo imageInfo = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap skBitmap = new(imageInfo);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int argb = bakedBitmap.Pixels[y * width + x];

                byte a = (byte)((argb >> 24) & 0xFF);
                byte r = (byte)((argb >> 16) & 0xFF);
                byte g = (byte)((argb >> 8) & 0xFF);
                byte b = (byte)(argb & 0xFF);

                skBitmap.SetPixel(x, y, new SKColor(r, g, b, a));
            }
        }

        return skBitmap;
    }

    public static bool ExportBakedBitmapAsPng(BakedBitmap bakedBitmap, string filePath)
    {
        bakedBitmap.ToSKBitmap().Save(filePath);
        return true;
    }

    
    
    private static AssetLocation BuildUniqueName(RecusiveOverlaysTexture node)
    {
        System.Text.StringBuilder sb = new();
        AppendNodeName(node, sb);
        //long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); sb.AppendLine($"{timestamp}"); // For DEBUGing to avoid caching
        return new AssetLocation("recursiveoverlay", sb.ToString());
    }

    private static void AppendNodeName(RecusiveOverlaysTexture node, System.Text.StringBuilder sb)
    {
        if (node.SolidColor)
        {
            sb.Append($"{node.Color}-{node.SizeOverride.X}-{node.SizeOverride.Y}");
        }
        else if (node.Canvas)
        {
            sb.Append($"{node.SerializedCanvas?.GetHashCode()}");
        }
        else
        {
            sb.Append(node.Texture.Base.ToShortString());
        }

        if (node.Texture.Rotation != 0)
        {
            sb.Append(RotationSuffix);
            sb.Append(node.Texture.Rotation);
        }

        if (node.Texture.Alpha != MaxAlphaValue)
        {
            sb.Append(CompositeTexture.AlphaSeparator);
            sb.Append(node.Texture.Alpha);
        }

        if (node.Overlays.Count == 0) return;

        sb.Append('[');
        foreach (RecusiveOverlaysTexture child in node.Overlays)
        {
            sb.Append((int)child.BlendMode);
            sb.Append(':');
            AppendNodeName(child, sb);
            sb.Append('|');
        }
        sb.Append(']');
    }

    private static void ResolveWildcard(IAssetManager assetManager, CompositeTexture compositeTexture)
    {
        if (!compositeTexture.Base.EndsWithWildCard) return;

        CompositeTexture.wildcardsCache ??= new Dictionary<AssetLocation, List<IAsset>>();

        if (!CompositeTexture.wildcardsCache.TryGetValue(compositeTexture.Base, out List<IAsset> matchingAssets))
        {
            string pathWithoutWildcard = compositeTexture.Base.Path[..^1];
            matchingAssets = CompositeTexture.wildcardsCache[compositeTexture.Base] =
                assetManager.GetManyInCategory(TexturesCategory, pathWithoutWildcard, compositeTexture.Base.Domain);
        }

        if (matchingAssets.Count == 0)
        {
            compositeTexture.WildCardNoFiles = compositeTexture.Base;
            compositeTexture.Base = new AssetLocation("unknown");
            return;
        }

        if (matchingAssets.Count == 1)
        {
            compositeTexture.Base = matchingAssets[0].Location.CloneWithoutPrefixAndEnding(TexturesPrefixPath.Length);
            return;
        }

        ExpandWildcardToAlternates(compositeTexture, matchingAssets);
    }

    private static void ExpandWildcardToAlternates(CompositeTexture compositeTexture, List<IAsset> matchingAssets)
    {
        int originalAlternatesLength = compositeTexture.Alternates?.Length ?? 0;
        CompositeTexture[] expandedAlternates = new CompositeTexture[originalAlternatesLength + matchingAssets.Count - 1];

        if (compositeTexture.Alternates != null)
        {
            Array.Copy(compositeTexture.Alternates, expandedAlternates, compositeTexture.Alternates.Length);
        }

        CompositeTexture.basicTexturesCache ??= new Dictionary<AssetLocation, CompositeTexture>();

        for (int i = 0; i < matchingAssets.Count; i++)
        {
            AssetLocation newLocation = matchingAssets[i].Location.CloneWithoutPrefixAndEnding(TexturesPrefixPath.Length);

            if (i == 0)
            {
                compositeTexture.Base = newLocation;
                continue;
            }

            expandedAlternates[originalAlternatesLength + i - 1] = CreateAlternateTexture(compositeTexture, newLocation);
        }

        compositeTexture.Alternates = expandedAlternates;
    }

    private static CompositeTexture CreateAlternateTexture(CompositeTexture sourceTexture, AssetLocation newLocation)
    {
        bool isBasicTexture = sourceTexture.Rotation == 0 && sourceTexture.Alpha == MaxAlphaValue;

        if (isBasicTexture)
        {
            if (!CompositeTexture.basicTexturesCache.TryGetValue(newLocation, out CompositeTexture cachedTexture))
            {
                cachedTexture = CompositeTexture.basicTexturesCache[newLocation] = new CompositeTexture(newLocation);
            }
            return cachedTexture;
        }

        return new CompositeTexture(newLocation)
        {
            Rotation = sourceTexture.Rotation,
            Alpha = sourceTexture.Alpha
        };
    }

    private static void ApplyBlendedOverlays(CompositeTexture compositeTexture, BakedCompositeTexture bakedTexture)
    {
        if (compositeTexture.BlendedOverlays == null)
        {
            bakedTexture.TextureFilenames = new AssetLocation[] { compositeTexture.Base };
            return;
        }

        bakedTexture.TextureFilenames = new AssetLocation[compositeTexture.BlendedOverlays.Length + 1];
        bakedTexture.TextureFilenames[0] = compositeTexture.Base;

        for (int i = 0; i < compositeTexture.BlendedOverlays.Length; i++)
        {
            BlendedOverlayTexture blendedOverlay = compositeTexture.BlendedOverlays[i];
            bakedTexture.TextureFilenames[i + 1] = blendedOverlay.Base;
            bakedTexture.BakedName.Path +=
                CompositeTexture.OverlaysSeparator +
                ((int)blendedOverlay.BlendMode).ToString() +
                CompositeTexture.BlendmodeSeparator +
                blendedOverlay.Base.ToString();
        }
    }

    private static void ApplyRotation(CompositeTexture compositeTexture, BakedCompositeTexture bakedTexture)
    {
        if (compositeTexture.Rotation == 0) return;

        bool isValidRotation = compositeTexture.Rotation is ValidRotation90 or ValidRotation180 or ValidRotation270;
        if (!isValidRotation)
        {
            throw new Exception(
                $"Texture definition {compositeTexture.Base} has a rotation thats not 0, 90, 180 or 270. " +
                "These are the only allowed values!");
        }

        bakedTexture.BakedName.Path += RotationSuffix + compositeTexture.Rotation;
    }

    private static void ApplyAlpha(CompositeTexture compositeTexture, BakedCompositeTexture bakedTexture)
    {
        if (compositeTexture.Alpha == MaxAlphaValue) return;

        bool isValidAlpha = compositeTexture.Alpha >= MinAlphaValue && compositeTexture.Alpha <= MaxAlphaValue;
        if (!isValidAlpha)
        {
            throw new Exception(
                $"Texture definition {compositeTexture.Base} has a alpha value outside the 0..255 range.");
        }

        bakedTexture.BakedName.Path += CompositeTexture.AlphaSeparator + compositeTexture.Alpha;
    }

    private static void BakeAlternates(IAssetManager assetManager, CompositeTexture compositeTexture, BakedCompositeTexture bakedTexture)
    {
        if (compositeTexture.Alternates == null) return;

        bakedTexture.BakedVariants = new BakedCompositeTexture[compositeTexture.Alternates.Length + 1];
        bakedTexture.BakedVariants[0] = bakedTexture;

        for (int i = 0; i < compositeTexture.Alternates.Length; i++)
        {
            bakedTexture.BakedVariants[i + 1] = Bake(assetManager, compositeTexture.Alternates[i]);
        }
    }

    private static void BakeTiles(IAssetManager assetManager, CompositeTexture compositeTexture, BakedCompositeTexture bakedTexture)
    {
        if (compositeTexture.Tiles == null) return;

        List<BakedCompositeTexture> bakedTiles = new();

        foreach (CompositeTexture tile in compositeTexture.Tiles)
        {
            if (tile.Base.EndsWithWildCard)
            {
                bakedTiles.AddRange(BakeWildcardTiles(assetManager, compositeTexture, tile));
            }
            else
            {
                bakedTiles.Add(BakeSingleTile(assetManager, compositeTexture, tile));
            }
        }

        bakedTexture.BakedTiles = bakedTiles.ToArray();
    }

    private static IEnumerable<BakedCompositeTexture> BakeWildcardTiles(IAssetManager assetManager, CompositeTexture compositeTexture, CompositeTexture tile)
    {
        CompositeTexture.wildcardsCache ??= new Dictionary<AssetLocation, List<IAsset>>();

        string pathWithoutWildcard = compositeTexture.Base.Path[..^1];
        List<IAsset> matchingAssets = CompositeTexture.wildcardsCache[compositeTexture.Base] =
            assetManager.GetManyInCategory(TexturesCategory, pathWithoutWildcard, compositeTexture.Base.Domain);

        int prefixLength = TexturesPrefixPath.Length + pathWithoutWildcard.Length + 1;
        List<IAsset> sortedAssets = matchingAssets
            .OrderBy(asset => asset.Location.Path[prefixLength..].RemoveFileEnding().ToInt())
            .ToList();

        foreach (IAsset asset in sortedAssets)
        {
            AssetLocation newLocation = asset.Location.CloneWithoutPrefixAndEnding(TexturesPrefixPath.Length);
            CompositeTexture wildcardTile = new(newLocation)
            {
                Rotation = compositeTexture.Rotation,
                Alpha = compositeTexture.Alpha,
                BlendedOverlays = compositeTexture.BlendedOverlays
            };

            BakedCompositeTexture bakedTile = Bake(assetManager, wildcardTile);
            bakedTile.TilesWidth = compositeTexture.TilesWidth;
            yield return bakedTile;
        }
    }

    private static BakedCompositeTexture BakeSingleTile(IAssetManager assetManager, CompositeTexture compositeTexture, CompositeTexture tile)
    {
        tile.BlendedOverlays = compositeTexture.BlendedOverlays;
        BakedCompositeTexture bakedTile = Bake(assetManager, tile);
        bakedTile.TilesWidth = compositeTexture.TilesWidth;
        return bakedTile;
    }

    private static BakedBitmap CreateSolidColorBitmap(int width, int height, int color)
    {
        int[] pixels = new int[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        BakedBitmap bitmap = new BakedBitmap()
        {
            Width = width,
            Height = height,
            TexturePixels = pixels
        };

        return bitmap;
    }
}

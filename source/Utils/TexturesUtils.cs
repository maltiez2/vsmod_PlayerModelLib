using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace PlayerModelLib;

public static class TexturesUtils
{
    /*public static BakedCompositeTexture Bake(this CompositeTexture ct, IAssetManager assetManager)
    {
        BakedCompositeTexture bct = new BakedCompositeTexture();

        ct.WildCardNoFiles = null;

        if (ct.Base.EndsWithWildCard)
        {
            if (CompositeTexture.wildcardsCache == null) CompositeTexture.wildcardsCache = new Dictionary<AssetLocation, List<IAsset>>();
            if (!CompositeTexture.wildcardsCache.TryGetValue(ct.Base, out List<IAsset> assets)) // Saves baking the same basic texture over and over
            {
                assets = CompositeTexture.wildcardsCache[ct.Base] = assetManager.GetManyInCategory("textures", ct.Base.Path.Substring(0, ct.Base.Path.Length - 1), ct.Base.Domain);
            }
            if (assets.Count == 0)
            {
                ct.WildCardNoFiles = ct.Base;
                ct.Base = new AssetLocation("unknown");
            }
            else
            if (assets.Count == 1)
            {
                ct.Base = assets[0].Location.CloneWithoutPrefixAndEnding("textures/".Length);
            }
            else
            {
                int origLength = (ct.Alternates == null ? 0 : ct.Alternates.Length);
                CompositeTexture[] alternates = new CompositeTexture[origLength + assets.Count - 1];
                if (ct.Alternates != null)
                {
                    Array.Copy(ct.Alternates, alternates, ct.Alternates.Length);
                }

                if (CompositeTexture.basicTexturesCache == null) CompositeTexture.basicTexturesCache = new Dictionary<AssetLocation, CompositeTexture>(); // Initialiser needed because it's ThreadStatic
                for (int i = 0; i < assets.Count; i++)
                {
                    IAsset asset = assets[i];
                    AssetLocation newLocation = asset.Location.CloneWithoutPrefixAndEnding("textures/".Length);

                    if (i == 0)
                    {
                        ct.Base = newLocation;
                    }
                    else
                    {
                        CompositeTexture act;
                        if (ct.Rotation == 0 && ct.Alpha == 255)
                        {
                            if (!CompositeTexture.basicTexturesCache.TryGetValue(newLocation, out act)) // Saves baking the same basic texture over and over
                            {
                                act = CompositeTexture.basicTexturesCache[newLocation] = new CompositeTexture(newLocation);
                            }
                        }
                        else
                        {
                            act = new CompositeTexture(newLocation);
                            act.Rotation = ct.Rotation;
                            act.Alpha = ct.Alpha;
                        }
                        alternates[origLength + i - 1] = act;
                    }
                }

                ct.Alternates = alternates;
            }
        }

        bct.BakedName = ct.Base.Clone();


        if (ct.BlendedOverlays != null)
        {
            bct.TextureFilenames = new AssetLocation[ct.BlendedOverlays.Length + 1];
            bct.TextureFilenames[0] = ct.Base;

            for (int i = 0; i < ct.BlendedOverlays.Length; i++)
            {
                BlendedOverlayTexture bov = ct.BlendedOverlays[i];
                bct.TextureFilenames[i + 1] = bov.Base;
                bct.BakedName.Path += CompositeTexture.OverlaysSeparator + ((int)bov.BlendMode).ToString() + CompositeTexture.BlendmodeSeparator + bov.Base.ToString();
            }
        }
        else
        {
            bct.TextureFilenames = new AssetLocation[] { ct.Base };
        }

        if (ct.Rotation != 0)
        {
            if (ct.Rotation != 90 && ct.Rotation != 180 && ct.Rotation != 270)
            {
                throw new Exception("Texture definition " + ct.Base + " has a rotation thats not 0, 90, 180 or 270. These are the only allowed values!");
            }
            bct.BakedName.Path += "@" + ct.Rotation;
        }

        if (ct.Alpha != 255)
        {
            if (ct.Alpha < 0 || ct.Alpha > 255)
            {
                throw new Exception("Texture definition " + ct.Base + " has a alpha value outside the 0..255 range.");
            }

            bct.BakedName.Path += "" + CompositeTexture.AlphaSeparator + ct.Alpha;
        }

        if (ct.Alternates != null)
        {
            bct.BakedVariants = new BakedCompositeTexture[ct.Alternates.Length + 1];
            bct.BakedVariants[0] = bct;
            for (int i = 0; i < ct.Alternates.Length; i++)
            {
                bct.BakedVariants[i + 1] = Bake(assetManager, ct.Alternates[i]);

            }
        }

        if (ct.Tiles != null)
        {
            List<BakedCompositeTexture> tiles = new List<BakedCompositeTexture>();

            for (int i = 0; i < ct.Tiles.Length; i++)
            {
                var tile = ct.Tiles[i];
                if (tile.Base.EndsWithWildCard)
                {
                    if (CompositeTexture.wildcardsCache == null) CompositeTexture.wildcardsCache = new Dictionary<AssetLocation, List<IAsset>>();

                    // Fix borked windows sorting (i.e. 1, 10, 11, 12, ....)
                    var basePath = ct.Base.Path.Substring(0, ct.Base.Path.Length - 1);
                    var assets = CompositeTexture.wildcardsCache[ct.Base] = assetManager.GetManyInCategory("textures", basePath, ct.Base.Domain);
                    var len = "textures".Length + basePath.Length + "/".Length;
                    var sortedassets = assets.OrderBy(asset => asset.Location.Path.Substring(len).RemoveFileEnding().ToInt()).ToList();

                    for (int j = 0; j < sortedassets.Count; j++)
                    {
                        IAsset asset = sortedassets[j];
                        AssetLocation newLocation = asset.Location.CloneWithoutPrefixAndEnding("textures/".Length);
                        var act = new CompositeTexture(newLocation);
                        act.Rotation = ct.Rotation;
                        act.Alpha = ct.Alpha;
                        act.BlendedOverlays = ct.BlendedOverlays;
                        var bt = Bake(assetManager, act);
                        bt.TilesWidth = ct.TilesWidth;
                        tiles.Add(bt);
                    }
                }
                else
                {
                    var act = ct.Tiles[i];
                    act.BlendedOverlays = ct.BlendedOverlays;
                    var bt = Bake(assetManager, act);
                    bt.TilesWidth = ct.TilesWidth;
                    tiles.Add(bt);
                }
            }

            bct.BakedTiles = tiles.ToArray();
        }

        return bct;
    }*/
}

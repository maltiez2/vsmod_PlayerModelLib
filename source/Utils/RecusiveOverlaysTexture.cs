using OpenTK.Mathematics;
using Vintagestory.API.Client;

namespace PlayerModelLib;

public class RecusiveOverlaysTexture
{
    public RecusiveOverlaysTexture(CompositeTexture texture, EnumTextureOverlayMode blendMode = EnumTextureOverlayMode.Normal)
    {
        Texture = texture;
        BlendMode = blendMode;
    }

    public RecusiveOverlaysTexture(CompositeTexture texture, List<RecusiveOverlaysTexture> overlays, EnumTextureOverlayMode blendMode = EnumTextureOverlayMode.Normal)
    {
        Texture = texture;
        BlendMode = blendMode;
        Overlays = overlays;
    }

    public CompositeTexture Texture { get; set; }
    public EnumTextureOverlayMode BlendMode { get; set; }
    public List<RecusiveOverlaysTexture> Overlays { get; set; } = [];
    public bool SolidColor { get; set; } = false;
    public bool Canvas { get; set; } = false;
    public string? SerializedCanvas { get; set; }
    public string? Color { get; set; }
    public Vector2i SizeOverride { get; set; } = Vector2i.Zero;
}

public class RecusiveOverlaysTextureWithTarget : RecusiveOverlaysTexture
{
    public RecusiveOverlaysTextureWithTarget(CompositeTexture texture, EnumTextureOverlayMode blendMode = EnumTextureOverlayMode.Normal) : base(texture, blendMode)
    {
    }

    public List<string> TargetCodes { get; set; } = [];
}
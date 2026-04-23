using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using OverhaulLib.Utils;

namespace PlayerModelLib;

public class CustomPlayerShapeRenderer : EntityPlayerShapeRenderer
{
    public CustomPlayerShapeRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
    {
    }

    public readonly ThreadSafeUInt TexturesAwaitingToBeAddedToAtlas = new(0);


    public override void TesselateShape()
    {
        if (PlayerModelModSystem.Settings.MultiThreadPayerShapeGeneration && entity.Api.Side == EnumAppSide.Client)
        {
            if (!_tesselating.Value)
            {
                _tesselating.SetTrue();
                TyronThreadPool.QueueTask(TesselateShapeOffThread, "CustomPlayerShapeRenderer");
            }
        }
        else
        {
            base.TesselateShape();
        }
    }

    public override void RenderToGui(float dt, double posX, double posY, double posZ, float yawDelta, float size)
    {
        try
        {
            base.RenderToGui(dt, posX, posY, posZ, yawDelta, size);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }

    private static readonly FieldInfo? _entityPlayerShapeRenderer_entityPlayer = typeof(EntityPlayerShapeRenderer).GetField("entityPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _entityPlayerShapeRenderer_watcherRegistered = typeof(EntityPlayerShapeRenderer).GetField("watcherRegistered", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _entityPlayerShapeRenderer_previfpMode = typeof(EntityPlayerShapeRenderer).GetField("previfpMode", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _entityPlayerShapeRenderer_ims = typeof(EntityShapeRenderer).GetField("ims", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _entityPlayerShapeRenderer_firstPersonMeshRef = typeof(EntityPlayerShapeRenderer).GetField("firstPersonMeshRef", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _entityPlayerShapeRenderer_thirdPersonMeshRef = typeof(EntityPlayerShapeRenderer).GetField("thirdPersonMeshRef", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo? _entityPlayerShapeRenderer_renderMode = typeof(EntityPlayerShapeRenderer).GetField("renderMode", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? _entityPlayerShapeRenderer_disposeMeshes = typeof(EntityPlayerShapeRenderer).GetMethod("disposeMeshes", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo? _entityPlayerShapeRenderer_determineRenderMode = typeof(EntityPlayerShapeRenderer).GetMethod("determineRenderMode", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo? _entityPlayerShapeRenderer_loadJointIdsRecursive = typeof(EntityPlayerShapeRenderer).GetMethod("loadJointIdsRecursive", BindingFlags.NonPublic | BindingFlags.Instance);

    private readonly ThreadSafeBool _tesselating = new(false);

    public virtual void TesselateShapeOffThread()
    {
        try
        {
            EntityPlayer? entityPlayer = (EntityPlayer?)_entityPlayerShapeRenderer_entityPlayer?.GetValue(this);
            bool watcherRegistered = (bool)(_entityPlayerShapeRenderer_watcherRegistered?.GetValue(this) ?? false);

            if (entityPlayer?.GetBehavior<EntityBehaviorPlayerInventory>()?.Inventory == null)
            {
                return;
            }

            defaultTexSource = GetTextureSource();
            CustomTesselateOffThread();
            if (watcherRegistered)
            {
                return;
            }

            _entityPlayerShapeRenderer_previfpMode?.SetValue(this, capi.Settings.Bool["immersiveFpMode"]);
            bool previfpMode = (bool)(_entityPlayerShapeRenderer_previfpMode?.GetValue(this) ?? false);

            if (IsSelf)
            {
                capi.Settings.Bool.AddWatcher("immersiveFpMode", delegate (bool on)
                {
                    entity.MarkShapeModified();
                    (entityPlayer.AnimManager as PlayerAnimationManager)?.OnIfpModeChanged(previfpMode, on);
                });
            }

            _entityPlayerShapeRenderer_watcherRegistered?.SetValue(this, true);
        }
        catch (Exception exception)
        {
            if (PlayerModelModSystem.Settings.LogOffThreadTesselationErrors)
            {
                string message = $"Error while tesselating player shape off-thread (please report in the lib thread on discord):\n{exception}";
                Log.Warn(entity.Api, this, message);
            }
            entity.MarkShapeModified();
        }
    }

    public virtual void CustomTesselateOffThread()
    {
        if (!IsSelf)
        {
            if (!loaded) return;

            _entityPlayerShapeRenderer_ims?.SetValue(this, entity.GetInterface<IMountable>());

            CustomEntityTesselateShapeOffThread(onMeshReady);
        }
        else
        {
            if (!loaded)
            {
                return;
            }

            CustomEntityTesselateShapeOffThread(delegate (MeshData meshData)
            {
                _entityPlayerShapeRenderer_disposeMeshes?.Invoke(this, []);
                if (!capi.IsShuttingDown && meshData.VerticesCount > 0)
                {
                    MeshData meshData2 = meshData.EmptyClone();
                    _entityPlayerShapeRenderer_thirdPersonMeshRef?.SetValue(this, capi.Render.UploadMultiTextureMesh(meshData));
                    _entityPlayerShapeRenderer_determineRenderMode?.Invoke(this, []);

                    RenderMode renderMode = (RenderMode)(_entityPlayerShapeRenderer_renderMode?.GetValue(this) ?? RenderMode.ThirdPerson);

                    if (renderMode == RenderMode.ImmersiveFirstPerson)
                    {
                        HashSet<int> skipJointIds = [];
                        _entityPlayerShapeRenderer_loadJointIdsRecursive?.Invoke(this, [entity.AnimManager.Animator.GetPosebyName("Neck"), skipJointIds]);
                        meshData2.AddMeshData(meshData, i => !skipJointIds.Contains(meshData.CustomInts.Values[i * 4]));
                    }
                    else
                    {
                        HashSet<int> includeJointIds = [];
                        _entityPlayerShapeRenderer_loadJointIdsRecursive?.Invoke(this, [entity.AnimManager.Animator.GetPosebyName("UpperArmL"), includeJointIds]);
                        _entityPlayerShapeRenderer_loadJointIdsRecursive?.Invoke(this, [entity.AnimManager.Animator.GetPosebyName("UpperArmR"), includeJointIds]);
                        meshData2.AddMeshData(meshData, i => includeJointIds.Contains(meshData.CustomInts.Values[i * 4]));
                    }

                    _entityPlayerShapeRenderer_firstPersonMeshRef?.SetValue(this, capi.Render.UploadMultiTextureMesh(meshData2));
                }
            });
        }
    }

    public virtual void CustomEntityTesselateShapeOffThread(Action<MeshData> onMeshDataReady, string[]? overrideSelectiveElements = null)
    {
        if (!loaded)
        {
            _tesselating.SetFalse();
            return;
        }

        CompositeShape compositeShape = OverrideCompositeShape ?? entity.Properties.Client.Shape;
        Shape entityShape = OverrideEntityShape ?? entity.Properties.Client.LoadedShapeForEntity;
        if (entityShape == null)
        {
            _tesselating.SetFalse();
            return;
        }

        entity.OnTesselation(ref entityShape, compositeShape.Base.ToString());

        while (TexturesAwaitingToBeAddedToAtlas.Value > 0)
        {
            Thread.Sleep(30);
        }

        defaultTexSource = GetTextureSource();
        string[] ovse = overrideSelectiveElements ?? OverrideSelectiveElements;

        MeshData meshdata;
        if (entity.Properties.Client.Shape.VoxelizeTexture)
        {
            int @int = entity.WatchedAttributes.GetInt("textureIndex");
            TextureAtlasPosition? atlasPos = defaultTexSource["all"];
            CompositeTexture firstTexture = entity.Properties.Client.FirstTexture;
            CompositeTexture[] alternates = firstTexture.Alternates;
            CompositeTexture texture = ((@int == 0) ? firstTexture : alternates[@int % alternates.Length]);
            meshdata = capi.Tesselator.VoxelizeTexture(texture, capi.EntityTextureAtlas.Size, atlasPos);
            for (int i = 0; i < meshdata.xyz.Length; i += 3)
            {
                meshdata.xyz[i] -= 0.125f;
                meshdata.xyz[i + 1] -= 0.5f;
                meshdata.xyz[i + 2] += 0.0625f;
            }
        }
        else
        {
            try
            {
                TesselationMetaData meta = new()
                {
                    QuantityElements = compositeShape.QuantityElements,
                    SelectiveElements = (ovse ?? compositeShape.SelectiveElements),
                    IgnoreElements = compositeShape.IgnoreElements,
                    TexSource = entity.GetBehavior<WearablesTesselatorBehavior>(),
                    WithJointIds = true,
                    WithDamageEffect = true,
                    TypeForLogging = "entity",
                    Rotation = new Vec3f(compositeShape.rotateX, compositeShape.rotateY, compositeShape.rotateZ)
                };
                capi.Tesselator.TesselateShape(meta, entityShape, out meshdata);
                meshdata.Translate(compositeShape.offsetX, compositeShape.offsetY, compositeShape.offsetZ);
            }
            catch (Exception e)
            {
                capi.World.Logger.Fatal("Failed tesselating entity {0} with id {1}. Entity will probably be invisible!.", entity.Code, entity.EntityId);
                capi.World.Logger.Fatal(e);
                _tesselating.SetFalse();
                return;
            }
        }

        capi.Event.EnqueueMainThreadTask(delegate
        {
            onMeshDataReady(meshdata);
            entity.OnTesselated();
        }, "uploadentitymesh");
        capi.TesselatorManager.ThreadDispose();
        _tesselating.SetFalse();
    }
}

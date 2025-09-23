using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

public class HeadControllerConfig
{
    public float HeadYOffsetFactor { get; set; } = 0.45f;
    public float HeadZOffsetFactor { get; set; } = 0.35f;
    public float NeckYOffsetFactor { get; set; } = 0.35f;
    public float NeckZOffsetFactor { get; set; } = 0.4f;
    public float UpperTorsoYOffsetFactor { get; set; } = 0.3f;
    public float UpperTorsoZOffsetFactor { get; set; } = 0.2f;
    public float LowerTorsoZOffsetFactor { get; set; } = 0.1f;
    public float UpperFootROffsetFactor { get; set; } = 0.1f;
    public float UpperFootLOffsetFactor { get; set; } = 0.1f;
    public float BodyFollowSpeedFactor { get; set; } = 1.0f;
    public float BodyFollowThresholdDeg { get; set; } = 69f;
    public float HeadYawLimitsDeg { get; set; } = 43f;
    public float HeadPitchLimitsDeg { get; set; } = 69f;
    public float HeadFollowEntityPitchFactor { get; set; } = 0.75f;
}

public class CustomModelHeadController : CustomPlayerHeadController
{
    public HeadControllerConfig Config { get; set; } = new();
    
    protected bool ConfigReceived = false;

    public CustomModelHeadController(IAnimationManager animator, EntityPlayer entity, Shape entityShape) : base(animator, entity, entityShape)
    {
        TryGetConfig();
    }

    public override void OnFrame(float dt)
    {
        TryGetConfig();

        base.OnFrame(dt);
    }

    public void ApplyConfig()
    {
        headYOffsetFactor = Config.HeadYOffsetFactor;
        headZOffsetFactor = Config.HeadZOffsetFactor;
        neckYOffsetFactor = Config.NeckYOffsetFactor;
        neckZOffsetFactor = Config.NeckZOffsetFactor;
        upperTorsoYOffsetFactor = Config.UpperTorsoYOffsetFactor;
        upperTorsoZOffsetFactor = Config.UpperTorsoZOffsetFactor;
        lowerTorsoZOffsetFactor = Config.LowerTorsoZOffsetFactor;
        upperFootROffsetFactor = Config.UpperFootROffsetFactor;
        upperFootLOffsetFactor = Config.UpperFootLOffsetFactor;
        bodyFollowSpeedFactor = Config.BodyFollowSpeedFactor;
        bodyFollowThresholdDeg = Config.BodyFollowThresholdDeg;
        headYawLimitsDeg = Config.HeadYawLimitsDeg;
        headPitchLimitsDeg = Config.HeadPitchLimitsDeg;
        headFollowEntityPitchFactor = Config.HeadFollowEntityPitchFactor;
    }

    protected void TryGetConfig()
    {
        /*if (ConfigReceived) return;
        HeadControllerConfig? config = entity.GetBehavior<PlayerSkinBehavior>()?.GetHeadControllerConfig();
        Config = config ?? Config;
        ConfigReceived = config != null;
        ApplyConfig();*/
    }
}


public class CustomPlayerHeadController : CustomEntityHeadController
{
    protected IClientPlayer? Player => EntityPlayer.Player as IClientPlayer;
    protected readonly EntityPlayer EntityPlayer;
    protected ICoreClientAPI ClientApi;

    protected bool TurnOpposite;
    protected bool RotateTpYawNow;

    protected float upperTorsoYOffsetFactor = 0.3f;
    protected float upperTorsoZOffsetFactor = 0.2f;
    protected float lowerTorsoZOffsetFactor = 0.1f;
    protected float upperFootROffsetFactor = 0.1f;
    protected float upperFootLOffsetFactor = 0.1f;

    protected float bodyFollowSpeedFactor = 1.0f;
    protected float bodyFollowThresholdDeg = 69f;
    protected float headYawLimitsDeg = 43f;
    protected float headPitchLimitsDeg = 69f;
    protected float headFollowEntityPitchFactor = 0.75f;

    protected readonly ElementPose upperTorsoPose;
    protected readonly ElementPose lowerTorsoPose;
    protected readonly ElementPose upperFootLPose;
    protected readonly ElementPose upperFootRPose;

    public CustomPlayerHeadController(IAnimationManager animator, EntityPlayer entity, Shape entityShape) : base(animator, entity, entityShape)
    {
        EntityPlayer = entity;
        ClientApi = entity.Api as ICoreClientAPI ?? throw new InvalidOperationException("PlayerHeadController have to be created client side");

        upperTorsoPose = GetPose("UpperTorso");
        lowerTorsoPose = GetPose("LowerTorso");
        upperFootRPose = GetPose("UpperFootR");
        upperFootLPose = GetPose("UpperFootL");
    }

    public override void OnFrame(float dt)
    {
        if (Player == null) return;

        upperTorsoPose.degOffY = 0;
        upperTorsoPose.degOffZ = 0;
        lowerTorsoPose.degOffZ = 0;
        upperFootRPose.degOffZ = 0;
        upperFootLPose.degOffZ = 0;

        if (!IsSelf())
        {
            base.OnFrame(dt);

            if (Entity.BodyYawServer == 0) // Why?
            {
                Entity.BodyYaw = Entity.Pos.Yaw;
            }

            return;
        }

        if (ClientApi.Input.MouseGrabbed)
        {
            AdjustAngles(dt);
        }

        base.OnFrame(dt);

        SetTorsoOffsets(dt);
    }

    protected virtual void AdjustAngles(float dt)
    {
        if (Player == null) return;

        EnumCameraMode cameraMode = Player.CameraMode;
        EnumMountAngleMode mountAngleMode = Player.Entity.MountedOn?.AngleMode ?? EnumMountAngleMode.Unaffected;
        bool bodyFollowExact = mountAngleMode == EnumMountAngleMode.Fixate || mountAngleMode == EnumMountAngleMode.FixateYaw || cameraMode == EnumCameraMode.Overhead || cameraMode == EnumCameraMode.FirstPerson;

        AdjustHeadAngles(cameraMode, dt);

        if (bodyFollowExact)
        {
            Entity.BodyYaw = Entity.Pos.Yaw;
        }
        else
        {
            AdjustBodyAngles(dt);
        }
    }

    protected virtual void AdjustHeadAngles(EnumCameraMode cameraMode, float dt) // @REFACTOR
    {
        float diff = GameMath.AngleRadDistance(Entity.BodyYaw, Entity.Pos.Yaw);

        if (Math.Abs(diff) > GameMath.PIHALF * 1.2f)
        {
            TurnOpposite = true;
        }

        if (TurnOpposite)
        {
            if (Math.Abs(diff) < GameMath.PIHALF * 0.9f)
            {
                TurnOpposite = false;
            }
            else
            {
                diff = 0;
            }
        }


        bool overheadLookAtMode = ClientApi.Settings.Bool["overheadLookAt"] && cameraMode == EnumCameraMode.Overhead; // overheadLookAt seems to be never set to true
        if (overheadLookAtMode)
        {
            // Code for head looking into camera that actually seems to be never utilized, will leave it be for now

            float dist = -GameMath.AngleRadDistance(ClientApi.Input.MouseYaw, Entity.Pos.Yaw);
            float targetHeadYaw = GameMath.PI + dist;
            float targetpitch = GameMath.Clamp(-Entity.Pos.Pitch - GameMath.PI + GameMath.TWOPI, -1, +0.8f);

            if (targetHeadYaw > GameMath.PI) targetHeadYaw -= GameMath.TWOPI;

            float pitchOffset = 0;

            if (targetHeadYaw < -1f || targetHeadYaw > 1f)
            {
                targetHeadYaw = 0;
                pitchOffset = (GameMath.Clamp((Entity.Pos.Pitch - GameMath.PI) * 0.75f, -1.2f, 1.2f) - Entity.Pos.HeadPitch) * dt * 6;
            }
            else
            {
                pitchOffset = (targetpitch - Entity.Pos.HeadPitch) * dt * 6;
            }

            Entity.Pos.HeadPitch += pitchOffset;
            Entity.Pos.HeadYaw += (targetHeadYaw - Entity.Pos.HeadYaw) * dt * 6;

            return;
        }


        Entity.Pos.HeadYaw += (diff - Entity.Pos.HeadYaw) * dt * 6;
        Entity.Pos.HeadYaw = GameMath.Clamp(Entity.Pos.HeadYaw, -headYawLimitsDeg * GameMath.DEG2RAD, headYawLimitsDeg * GameMath.DEG2RAD);
        Entity.Pos.HeadPitch = GameMath.Clamp(
            (Entity.Pos.Pitch - GameMath.PI) * headFollowEntityPitchFactor,
            -headPitchLimitsDeg * GameMath.DEG2RAD,
            headPitchLimitsDeg * GameMath.DEG2RAD);
    }

    protected virtual void AdjustBodyAngles(float dt)
    {
        if (!EntityPlayer.Alive || Player == null) return;

        bool isMoving = Player.Entity.Controls.TriesToMove || Player.Entity.ServerControls.TriesToMove;
        float threshold = isMoving ? 0.01f : bodyFollowThresholdDeg * GameMath.DEG2RAD;
        if (Entity.Controls.Gliding) threshold = 0;

        float yawDistance = GameMath.AngleRadDistance(Entity.BodyYaw, Entity.Pos.Yaw);

        if (Math.Abs(yawDistance) > threshold || RotateTpYawNow)
        {
            float speed = 0.05f + Math.Abs(yawDistance) * 3.5f * bodyFollowSpeedFactor;
            Entity.BodyYaw += GameMath.Clamp(yawDistance, -dt * speed, dt * speed);
            RotateTpYawNow = Math.Abs(yawDistance) > 0.01f;
        }
    }

    protected virtual void SetTorsoOffsets(float dt)
    {
        if (!IsSelfImmersiveFirstPerson()) return;

        (float yOffsetDeg, float zOffsetDeg) = GetOffsets(dt);

        upperTorsoPose.degOffZ = zOffsetDeg * upperTorsoYOffsetFactor;
        upperTorsoPose.degOffY = yOffsetDeg * upperTorsoZOffsetFactor;
        lowerTorsoPose.degOffZ = zOffsetDeg * lowerTorsoZOffsetFactor;
        upperFootRPose.degOffZ = -zOffsetDeg * upperFootROffsetFactor;
        upperFootLPose.degOffZ = -zOffsetDeg * upperFootLOffsetFactor;
    }

    protected bool IsSelf()
    {
        return ClientApi?.World.Player.PlayerUID == Player?.PlayerUID;
    }

    protected bool IsSelfImmersiveFirstPerson()
    {
        return IsSelf() && Player?.ImmersiveFpMode == true;
    }
}

public class CustomEntityHeadController : PlayerHeadController
{
    public float YawOffset { get => yawOffset; set => yawOffset = value; }
    public float PitchOffset { get => pitchOffset; set => pitchOffset = value; }

    protected readonly EntityAgent Entity;
    protected readonly IAnimationManager animationManager;
    protected readonly ElementPose headPose;
    protected readonly ElementPose neckPose;

    protected float headYOffsetFactor = 0.45f;
    protected float headZOffsetFactor = 0.35f;
    protected float neckYOffsetFactor = 0.35f;
    protected float neckZOffsetFactor = 0.4f;

    public CustomEntityHeadController(IAnimationManager animationManager, EntityAgent entity, Shape entityShape) : base(animationManager, entity as EntityPlayer, entityShape)
    {
        this.Entity = entity;
        this.animationManager = animationManager;

        headPose = GetPose("Head");
        neckPose = GetPose("Neck");
    }

    public override void OnFrame(float dt)
    {
        headPose.degOffY = 0;
        headPose.degOffZ = 0;
        neckPose.degOffZ = 0;

        if (Entity.Pos.HeadYaw == 0 && Entity.Pos.HeadPitch == 0) return;

        (float yOffsetDeg, float zOffsetDeg) = GetOffsets(dt);

        headPose.degOffY = yOffsetDeg * headYOffsetFactor;
        headPose.degOffZ = zOffsetDeg * headZOffsetFactor;
        neckPose.degOffY = yOffsetDeg * neckYOffsetFactor;
        neckPose.degOffZ = zOffsetDeg * neckZOffsetFactor;
    }

    protected virtual (float yOffsetDeg, float zOffsetDeg) GetOffsets(float dt) => ((Entity.Pos.HeadYaw + YawOffset) * GameMath.RAD2DEG, (Entity.Pos.HeadPitch + PitchOffset) * GameMath.RAD2DEG);

    protected ElementPose GetPose(string name) => animationManager.Animator.GetPosebyName(name) ?? throw new InvalidOperationException($"[Head Controller] Entity '{Entity.Code}' shape does not have '{name}' element.");
}

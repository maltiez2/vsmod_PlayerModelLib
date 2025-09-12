using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlayerModelLib;

public class CustomModelHeadController : PlayerHeadController
{
    protected EntityPlayer EntityPlayer;
    protected ICoreClientAPI Api;
    protected IClientPlayer? Player;

    public CustomModelHeadController(IAnimationManager animator, EntityPlayer entity, Shape entityShape) : base(animator, entity, entityShape)
    {
        EntityPlayer = entity;
        Player = EntityPlayer.Player as IClientPlayer;
        Api = EntityPlayer.Api as ICoreClientAPI ?? throw new Exception();
    }

    public override void OnFrame(float dt)
    {
        Player ??= EntityPlayer.Player as IClientPlayer;

        ICoreClientAPI? capi = entity.Api as ICoreClientAPI;
        bool isSelf = Api.World.Player.Entity.EntityId == entity.EntityId;

        if (!isSelf)
        {
            BaseOnFrame(dt);
            if (entity.BodyYawServer == 0)
                entity.BodyYaw = entity.Pos.Yaw;
            return;
        }

        float diff = GameMath.AngleRadDistance(entity.BodyYaw, entity.Pos.Yaw);

        if (Math.Abs(diff) > GameMath.PIHALF * 1.2f) turnOpposite = true;
        if (turnOpposite)
        {
            if (Math.Abs(diff) < GameMath.PIHALF * 0.9f) turnOpposite = false;
            else diff = 0;
        }

        EnumCameraMode cameraMode = (Player as IClientPlayer).CameraMode;

        bool overheadLookAtMode = capi.Settings.Bool["overheadLookAt"] && cameraMode == EnumCameraMode.Overhead;

        if (!overheadLookAtMode && capi.Input.MouseGrabbed)
        {
            entity.Pos.HeadYaw += (diff - entity.Pos.HeadYaw) * dt * 6;
            entity.Pos.HeadYaw = GameMath.Clamp(entity.Pos.HeadYaw, -0.75f, 0.75f);

            entity.Pos.HeadPitch = GameMath.Clamp((entity.Pos.Pitch - GameMath.PI) * 0.75f, -1.2f, 1.2f);
        }

        EnumMountAngleMode angleMode = EnumMountAngleMode.Unaffected;
        IMountableSeat mount = player.Entity.MountedOn;
        if (player.Entity.MountedOn != null)
        {
            angleMode = mount.AngleMode;
        }

        if (player?.Entity == null || angleMode == EnumMountAngleMode.Fixate || angleMode == EnumMountAngleMode.FixateYaw || cameraMode == EnumCameraMode.Overhead)
        {
            if (capi.Input.MouseGrabbed)
            {
                entity.BodyYaw = entity.Pos.Yaw;

                if (overheadLookAtMode)
                {
                    float dist = -GameMath.AngleRadDistance((entity.Api as ICoreClientAPI).Input.MouseYaw, entity.Pos.Yaw);
                    float targetHeadYaw = GameMath.PI + dist;
                    float targetpitch = GameMath.Clamp(-entity.Pos.Pitch - GameMath.PI + GameMath.TWOPI, -1, +0.8f);

                    if (targetHeadYaw > GameMath.PI) targetHeadYaw -= GameMath.TWOPI;

                    if (targetHeadYaw < -1f || targetHeadYaw > 1f)
                    {
                        targetHeadYaw = 0;

                        entity.Pos.HeadPitch += (GameMath.Clamp((entity.Pos.Pitch - GameMath.PI) * 0.75f, -1.2f, 1.2f) - entity.Pos.HeadPitch) * dt * 6;
                    }
                    else
                    {
                        entity.Pos.HeadPitch += (targetpitch - entity.Pos.HeadPitch) * dt * 6;
                    }

                    entity.Pos.HeadYaw += (targetHeadYaw - entity.Pos.HeadYaw) * dt * 6;
                }
            }

        }
        else
        {
            if (player?.Entity.Alive == true)
            {
                float yawDist = GameMath.AngleRadDistance(entity.BodyYaw, entity.Pos.Yaw);
                bool ismoving = player.Entity.Controls.TriesToMove || player.Entity.ServerControls.TriesToMove;

                bool attachedToClimbWall = false;

                float threshold = 1.2f - (ismoving ? 1.19f : 0) + (attachedToClimbWall ? 3 : 0);
                if (entity.Controls.Gliding) threshold = 0;

                if (player.PlayerUID == capi.World.Player.PlayerUID && !capi.Settings.Bool["immersiveFpMode"] && cameraMode != EnumCameraMode.FirstPerson)
                {
                    if (Math.Abs(yawDist) > threshold || rotateTpYawNow)
                    {
                        float speed = 0.05f + Math.Abs(yawDist) * 3.5f;
                        entity.BodyYaw += GameMath.Clamp(yawDist, -dt * speed, dt * speed);
                        rotateTpYawNow = Math.Abs(yawDist) > 0.01f;
                    }

                }
                else
                {
                    entity.BodyYaw = entity.Pos.Yaw;
                }
            }
        }

        BaseOnFrame(dt);
    }

    public virtual void BaseOnFrame(float dt)
    {
        HeadPose.degOffY = 0;
        HeadPose.degOffZ = 0;
        NeckPose.degOffZ = 0;
        UpperTorsoPose.degOffY = 0;
        UpperTorsoPose.degOffZ = 0;
        LowerTorsoPose.degOffZ = 0;
        UpperFootRPose.degOffZ = 0;
        UpperFootLPose.degOffZ = 0;

        if (entity.Pos.HeadYaw != 0 || entity.Pos.HeadPitch != 0)
        {
            float degoffy = (entity.Pos.HeadYaw + yawOffset) * GameMath.RAD2DEG;
            float degoffz = (entity.Pos.HeadPitch + pitchOffset) * GameMath.RAD2DEG;

            HeadPose.degOffY = degoffy * 0.45f;
            HeadPose.degOffZ = degoffz * 0.35f;

            NeckPose.degOffY = degoffy * 0.35f;
            NeckPose.degOffZ = degoffz * 0.4f;

            // Don't adjust torsoe if we are in normal fp mode
            ICoreClientAPI? capi = entity.World.Api as ICoreClientAPI;
            IPlayer? plr = (entity as EntityPlayer)?.Player;
            IPlayer? selfPlayer = capi?.World.Player.PlayerUID == plr?.PlayerUID ? plr : null;
            if (selfPlayer?.ImmersiveFpMode == true)
            {
                UpperTorsoPose.degOffZ = degoffz * 0.3f;
                UpperTorsoPose.degOffY = degoffy * 0.2f;

                float offz = degoffz * 0.1f;
                LowerTorsoPose.degOffZ = offz;
                UpperFootRPose.degOffZ = -offz;
                UpperFootLPose.degOffZ = -offz;
            }
        }
    }
}

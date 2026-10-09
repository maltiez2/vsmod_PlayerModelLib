using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace PlayerModelLib;

public class RandomIdleAnimationsBehavior : EntityBehavior
{
    public RandomIdleAnimationsBehavior(Entity entity) : base(entity)
    {

    }



    public TimeSpan TimeFromLastAnimation { get; set; } = TimeSpan.Zero;
    public string[] RandomIdleAnimations { get; set; } = [];
    public EntityBehaviorTiredness? TirednessBehavior { get; set; }
    public PlayerSkinBehavior? SkinBehavior { get; set; }
    public float AnimationChance { get; set; } = 0.0015f;
    public TimeSpan Cooldown { get; set; } = TimeSpan.Zero;



    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);

        string[] entityAnimations = properties.Attributes["randomIdleAnimations"].AsArray<string>([]).OfType<string>().ToArray() ?? [];

        RandomIdleAnimations = attributes["randomIdleAnimations"].AsArray<string>([]).OfType<string>().ToArray() ?? [];
        AnimationChance = attributes["animationChance"].AsFloat(AnimationChance);
        Cooldown = TimeSpan.FromSeconds(attributes["cooldown"].AsFloat(0));

        RandomIdleAnimations = RandomIdleAnimations.Concat(entityAnimations).ToArray();

        TirednessBehavior = entity.GetBehavior<EntityBehaviorTiredness>();
        SkinBehavior = entity.GetBehavior<PlayerSkinBehavior>();
    }

    public override void OnGameTick(float dt)
    {
        if (!entity.Alive) return;

        if (entity is not EntityAgent agent) return;

        bool triesToMove = agent.ServerControls.TriesToMove || agent.MountedOn != null;
        if (
            TimeFromLastAnimation >= Cooldown &&
            !triesToMove &&
            !agent.Controls.IsFlying &&
            !agent.Controls.Gliding &&
            agent.RightHandItemSlot?.Empty == true &&
            !agent.Swimming &&
            TirednessBehavior?.IsSleeping != true)
        {
            TimeFromLastAnimation += TimeSpan.FromSeconds(dt);
            if (agent.World.Rand.NextDouble() < AnimationChance)
            {
                PlayIdleAnimation();
                TimeFromLastAnimation = TimeSpan.Zero;
            }
        }
        else
        {
            TimeFromLastAnimation = TimeSpan.Zero;
        }
    }

    public override string PropertyName() => "idleanimations";



    protected virtual void PlayIdleAnimation()
    {
        string[] animations = GetIdleAnimations();

        if (animations.Length == 0) return;

        int randomIndex = entity.World.Rand.Next(animations.Length);

        entity.StartAnimation(animations[randomIndex]);
    }

    protected virtual string[] GetIdleAnimations()
    {
        if (SkinBehavior == null) return RandomIdleAnimations;

        if (SkinBehavior.CurrentModel.RandomIdleAnimations == null) return RandomIdleAnimations;

        return SkinBehavior.CurrentModel.RandomIdleAnimations;
    }
}
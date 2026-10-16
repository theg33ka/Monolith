using Content.Shared._Forge.HME;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._Forge.HME;

public sealed partial class HmeStasisBlossomVisualizerSystem : VisualizerSystem<HmeStasisBlossomVisualsComponent>
{
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private const string DeployAnimationKey = "hme-stasis-deploy";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HmeStasisBlossomVisualsComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnAnimationCompleted(Entity<HmeStasisBlossomVisualsComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != DeployAnimationKey || !args.Finished ||
            ent.Comp.LastState != HmeStasisBlossomVisualState.Deployed ||
            !TryComp(ent, out SpriteComponent? sprite))
            return;

        SpriteSystem.LayerSetRsiState((ent.Owner, sprite), "base", "active");
        SpriteSystem.LayerSetRsiState((ent.Owner, sprite), "screen", "active-screen");
        SpriteSystem.LayerSetAutoAnimated((ent.Owner, sprite), "base", true);
        SpriteSystem.LayerSetAutoAnimated((ent.Owner, sprite), "screen", true);
    }

    protected override void OnAppearanceChange(EntityUid uid, HmeStasisBlossomVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData(uid, HmeStasisBlossomVisuals.State,
                out HmeStasisBlossomVisualState state, args.Component) || component.LastState == state)
            return;

        component.LastState = state;
        _animation.Stop(uid, DeployAnimationKey);

        if (state != HmeStasisBlossomVisualState.Deployed)
        {
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), "base", "compact");
            SpriteSystem.LayerSetVisible((uid, args.Sprite), "screen", false);
            return;
        }

        SpriteSystem.LayerSetVisible((uid, args.Sprite), "screen", true);
        var animation = new Animation
        {
            Length = component.DeployAnimationDuration,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = "base",
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame("deploying", 0f),
                    },
                },
                new AnimationTrackSpriteFlick
                {
                    LayerKey = "screen",
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame("deploying-screen", 0f),
                    },
                },
            },
        };
        _animation.Play(uid, animation, DeployAnimationKey);
    }
}

using Content.Shared._Forge.HME;
using Robust.Client.GameObjects;

namespace Content.Client._Forge.HME;

public sealed partial class HmeStasisPatientVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HmeStasisPatientComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HmeStasisPatientComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<HmeStasisPatientComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<HmeStasisPatientComponent> ent, ref ComponentStartup args) => UpdateVisibility(ent);

    private void OnState(Entity<HmeStasisPatientComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateVisibility(ent);

    private void UpdateVisibility(Entity<HmeStasisPatientComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        if (ent.Comp.Hidden)
        {
            ent.Comp.OriginalVisibility ??= sprite.Visible;
            _sprite.SetVisible((ent.Owner, sprite), false);
        }
        else
        {
            RestoreVisibility(ent, sprite);
        }
    }

    private void OnShutdown(Entity<HmeStasisPatientComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            RestoreVisibility(ent, sprite);
    }

    private void RestoreVisibility(Entity<HmeStasisPatientComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.OriginalVisibility is not { } visible)
            return;

        _sprite.SetVisible((ent.Owner, sprite), visible);
        ent.Comp.OriginalVisibility = null;
    }
}

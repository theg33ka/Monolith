using System.Numerics;
using Content.Shared._Forge.ShipWeapons.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Shared._Forge.ShipWeapons.Systems;

public sealed class FixedMountFireArcSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FixedMountFireArcComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<FixedMountFireArcComponent> ent, ref ShotAttemptedEvent args)
    {
        var target = args.Used.Comp.ShootCoordinates;
        if (target == null)
            return;

        var origin = _transform.GetWorldPosition(ent.Owner);
        var targetPosition = _transform.ToMapCoordinates(target.Value).Position;
        var direction = targetPosition - origin;
        if (direction.LengthSquared() == 0)
            return;

        if (!IsWithinArc(ent.Owner, ent.Comp, direction, args.Used.Comp))
            args.Cancel();
    }

    public bool IsWithinArc(EntityUid uid, FixedMountFireArcComponent component, Vector2 direction, GunComponent? gun = null)
    {
        var xform = Transform(uid);
        var defaultDirection = gun?.DefaultDirection ?? new Vector2(0, -1);
        if (defaultDirection.LengthSquared() == 0)
            defaultDirection = new Vector2(0, -1);

        var localForward = component.ForwardOffset.RotateVec(Vector2.Normalize(defaultDirection));
        var forward = _transform.GetWorldRotation(xform).RotateVec(localForward);
        var aim = Vector2.Normalize(direction);
        return Vector2.Dot(Vector2.Normalize(forward), aim) >= Math.Cos(component.Arc.Theta / 2);
    }
}

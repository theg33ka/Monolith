using Content.Server._NF.Salvage;
using Content.Server.Body.Systems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Systems;
using Content.Shared._Forge.HME;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Forge.HME;

public sealed class HmeRescueBeaconSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private readonly HashSet<EntityUid> _activeBeacons = new();
    private readonly List<EntityUid> _updateBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HmeRescueBeaconComponent, ComponentInit>(OnBeaconInit);
        SubscribeLocalEvent<HmeRescueBeaconComponent, ComponentShutdown>(OnBeaconShutdown);
        SubscribeLocalEvent<HmeRescueBeaconComponent, ActivateInWorldEvent>(OnBeaconActivate);
        SubscribeLocalEvent<HmeRescueBeaconComponent, HmeSalvageMobCleanupAttemptEvent>(OnBeaconSalvageCleanup);
        SubscribeLocalEvent<SalvageExpeditionComponent, EntityTerminatingEvent>(OnBeaconSalvageGridTerminating, before: new[] { typeof(GridDeletionContainerSystem) });
        SubscribeLocalEvent<SalvageMobRestrictionsGridComponent, EntityTerminatingEvent>(OnBeaconRestrictedGridTerminating, before: new[] { typeof(GridDeletionContainerSystem) });
    }

    private void OnBeaconInit(Entity<HmeRescueBeaconComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.Armed && !ent.Comp.Spent)
            _activeBeacons.Add(ent.Owner);

        SetBeaconVisual(ent);
    }

    private void OnBeaconShutdown(Entity<HmeRescueBeaconComponent> ent, ref ComponentShutdown args)
    {
        _activeBeacons.Remove(ent.Owner);
    }

    private void OnBeaconActivate(Entity<HmeRescueBeaconComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Spent)
        {
            _popup.PopupEntity(Loc.GetString("hme-rescue-beacon-spent"), ent.Owner, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        ent.Comp.Armed = !ent.Comp.Armed;
        if (ent.Comp.Armed)
            _activeBeacons.Add(ent.Owner);
        else
            _activeBeacons.Remove(ent.Owner);

        Dirty(ent);
        SetBeaconVisual(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Armed ? "hme-rescue-beacon-armed" : "hme-rescue-beacon-disarmed"), ent.Owner, args.User);
        args.Handled = true;
    }

    private void OnBeaconSalvageGridTerminating(Entity<SalvageExpeditionComponent> ent, ref EntityTerminatingEvent args)
    {
        TryTriggerBeaconRescuesOnGrid(ent.Owner);
    }

    private void OnBeaconRestrictedGridTerminating(Entity<SalvageMobRestrictionsGridComponent> ent, ref EntityTerminatingEvent args)
    {
        TryTriggerBeaconRescuesOnGrid(ent.Owner);
    }

    private void TryTriggerBeaconRescuesOnGrid(EntityUid grid)
    {
        _updateBuffer.Clear();
        _updateBuffer.AddRange(_activeBeacons);

        foreach (var beacon in _updateBuffer)
        {
            if (!TryComp(beacon, out HmeRescueBeaconComponent? component) ||
                !component.Armed ||
                component.Spent)
            {
                _activeBeacons.Remove(beacon);
                continue;
            }

            if (!_inventory.TryGetContainingSlot(beacon, out var slot) ||
                (slot.SlotFlags & (SlotFlags.ARMBANDLEFT | SlotFlags.ARMBANDRIGHT)) == 0 ||
                !_inventory.TryGetContainingEntity(beacon, out var target) ||
                target is not { } targetUid ||
                !TryComp(targetUid, out TransformComponent? targetXform) ||
                targetXform.GridUid != grid)
            {
                continue;
            }

            var cleanupEv = new HmeSalvageMobCleanupAttemptEvent(grid, targetUid);
            RaiseLocalEvent(beacon, ref cleanupEv);
        }
    }

    private void OnBeaconSalvageCleanup(Entity<HmeRescueBeaconComponent> ent, ref HmeSalvageMobCleanupAttemptEvent args)
    {
        if (args.Handled || ent.Comp.Spent || !ent.Comp.Armed)
            return;

        if (!_inventory.TryGetContainingSlot(ent.Owner, out var slot))
            return;

        if ((slot.SlotFlags & (SlotFlags.ARMBANDLEFT | SlotFlags.ARMBANDRIGHT)) == 0)
            return;

        var target = args.Target;
        if (!Exists(target) || MetaData(target).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (!TryGetBeaconRescueDestination(ent.Comp, out var destination))
            return;

        if (!TryRemoveBeaconArm(target, slot.SlotFlags))
            return;

        ent.Comp.Armed = false;
        ent.Comp.Spent = true;
        _activeBeacons.Remove(ent.Owner);
        Dirty(ent);
        SetBeaconVisual(ent);

        _transform.SetMapCoordinates(target, destination);
        _damage.TryChangeDamage(target, ent.Comp.RescueCriticalDamage, ignoreResistances: true, interruptsDoAfters: false, canSever: false);

        if (HasComp<NFSalvageMobRestrictionsComponent>(target))
            RemComp<NFSalvageMobRestrictionsComponent>(target);

        var x = (int) MathF.Round(destination.Position.X);
        var y = (int) MathF.Round(destination.Position.Y);
        _radio.SendRadioMessage(ent.Owner, Loc.GetString("hme-rescue-beacon-radio", ("x", x), ("y", y)), ent.Comp.RadioChannel, ent.Owner);
        _emp.EmpPulse(destination, ent.Comp.EmpRange, ent.Comp.EmpEnergyConsumption, ent.Comp.EmpDuration, target);
        _explosion.QueueExplosion(destination,
            ExplosionSystem.DefaultExplosionPrototypeId,
            ent.Comp.ExplosionIntensity,
            ent.Comp.ExplosionSlope,
            ent.Comp.MaxTileIntensity,
            ent.Owner,
            canCreateVacuum: false);

        _inventory.DropSlotContents(target, slot.Name);
        args.Handled = true;
    }

    private bool TryGetBeaconRescueDestination(HmeRescueBeaconComponent component, out MapCoordinates destination)
    {
        destination = default;

        if (!_map.TryGetMap(_gameTicker.DefaultMap, out var mapUid) ||
            mapUid is not { } destinationMap ||
            MetaData(destinationMap).EntityLifeStage >= EntityLifeStage.Terminating)
        {
            return false;
        }

        var offset = _random.NextAngle().ToWorldVec() * (component.RescueDistance + _random.NextFloat(component.RescueJitter));
        destination = new MapCoordinates(offset, _gameTicker.DefaultMap);
        return true;
    }

    private void SetBeaconVisual(Entity<HmeRescueBeaconComponent> ent)
    {
        var state = ent.Comp.Spent
            ? HmeRescueBeaconVisualState.Spent
            : ent.Comp.Armed
                ? HmeRescueBeaconVisualState.Armed
                : HmeRescueBeaconVisualState.Idle;

        _appearance.SetData(ent.Owner, HmeRescueBeaconVisuals.State, state);
    }

    private bool TryRemoveBeaconArm(EntityUid target, SlotFlags slotFlags)
    {
        var symmetry = (slotFlags & SlotFlags.ARMBANDLEFT) != 0
            ? BodyPartSymmetry.Left
            : BodyPartSymmetry.Right;

        if (TryRemoveFirstPart(target, BodyPartType.Arm, symmetry))
            return true;

        return TryRemoveFirstPart(target, BodyPartType.Hand, symmetry);
    }

    private bool TryRemoveFirstPart(EntityUid target, BodyPartType partType, BodyPartSymmetry symmetry)
    {
        foreach (var (part, _) in _body.GetBodyChildrenOfType(target, partType, symmetry: symmetry))
        {
            var amputate = new AmputateAttemptEvent(part);
            RaiseLocalEvent(part, ref amputate);
            return !TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body == null;
        }

        return false;
    }
}

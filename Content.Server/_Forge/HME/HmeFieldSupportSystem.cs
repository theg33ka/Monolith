using System.Linq;
using System.Numerics;
using Content.Server._NF.Salvage;
using Content.Server.Body.Systems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Server.Radio.EntitySystems;
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Systems;
using Content.Shared._Forge.HME;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.PowerCell.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Forge.HME;

public sealed class HmeFieldSupportSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HmeSmartInfusionStandComponent, ComponentInit>(OnInfusionInit);
        SubscribeLocalEvent<HmeSmartInfusionStandComponent, ContainerIsInsertingAttemptEvent>(OnInfusionInsertAttempt);
        SubscribeLocalEvent<HmeStasisBlossomComponent, ComponentInit>(OnBlossomInit);
        SubscribeLocalEvent<HmeStasisBlossomComponent, MapInitEvent>(OnBlossomMapInit);
        SubscribeLocalEvent<HmeStasisBlossomComponent, AfterInteractEvent>(OnBlossomAfterInteract);
        SubscribeLocalEvent<HmeStasisBlossomComponent, InteractHandEvent>(OnBlossomInteractHand, before: new[] { typeof(SharedItemSystem) });
        SubscribeLocalEvent<HmeStasisBlossomComponent, UseInHandEvent>(OnBlossomUseInHand);
        SubscribeLocalEvent<HmeStasisBlossomComponent, ActivateInWorldEvent>(OnBlossomActivate);
        SubscribeLocalEvent<HmeStasisBlossomComponent, GetItemActionsEvent>(OnBlossomGetItemActions);
        SubscribeLocalEvent<HmeStasisBlossomComponent, HmeStasisBlossomToggleEvent>(OnBlossomToggleAction);
        SubscribeLocalEvent<HmeStasisBlossomComponent, HmeStasisBlossomDoAfterEvent>(OnBlossomDoAfter);
        SubscribeLocalEvent<HmeStasisBlossomComponent, ComponentShutdown>(OnBlossomShutdown);
        SubscribeLocalEvent<HmeStasisBlossomComponent, GetVerbsEvent<InteractionVerb>>(OnBlossomInteractionVerbs);
        SubscribeLocalEvent<HmeStasisBlossomComponent, ExaminedEvent>(OnBlossomExamined);
        SubscribeLocalEvent<HmeStasisBlossomComponent, GettingPickedUpAttemptEvent>(OnBlossomGettingPickedUpAttempt);
        SubscribeLocalEvent<HmeStasisBlossomComponent, PullAttemptEvent>(OnBlossomPullAttempt);
        SubscribeLocalEvent<StandingStateComponent, StandAttemptEvent>(OnStandingAttempt);
        SubscribeLocalEvent<HmeStasisBlossomCocoonComponent, ComponentShutdown>(OnCocoonShutdown);
        SubscribeLocalEvent<HmeRescueBeaconComponent, ComponentInit>(OnBeaconInit);
        SubscribeLocalEvent<HmeRescueBeaconComponent, ActivateInWorldEvent>(OnBeaconActivate);
        SubscribeLocalEvent<HmeRescueBeaconComponent, HmeSalvageMobCleanupAttemptEvent>(OnBeaconSalvageCleanup);
        SubscribeLocalEvent<SalvageExpeditionComponent, EntityTerminatingEvent>(OnBeaconSalvageGridTerminating, before: new[] { typeof(GridDeletionContainerSystem) });
        SubscribeLocalEvent<SalvageMobRestrictionsGridComponent, EntityTerminatingEvent>(OnBeaconRestrictedGridTerminating, before: new[] { typeof(GridDeletionContainerSystem) });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var infusionQuery = EntityQueryEnumerator<HmeSmartInfusionStandComponent, StorageComponent>();
        while (infusionQuery.MoveNext(out var uid, out var infusion, out var storage))
        {
            if (now < infusion.NextUpdate)
                continue;

            infusion.NextUpdate = now + infusion.UpdateInterval;
            UpdateInfusionStand(uid, infusion, storage);
        }

        var cocoonQuery = EntityQueryEnumerator<HmeStasisBlossomCocoonComponent>();
        while (cocoonQuery.MoveNext(out var uid, out var cocoon))
        {
            UpdateCocoon(uid, cocoon, now);
        }

        var blossomQuery = EntityQueryEnumerator<HmeStasisBlossomComponent>();
        while (blossomQuery.MoveNext(out var uid, out var blossom))
        {
            UpdateAttachedBlossom((uid, blossom), now);
        }
    }

    private void OnInfusionInit(Entity<HmeSmartInfusionStandComponent> ent, ref ComponentInit args)
    {
        SetInfusionVisual(ent.Owner, HmeInfusionStandVisualState.Empty);
    }

    private void OnInfusionInsertAttempt(Entity<HmeSmartInfusionStandComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!IsValidInfusionSource(ent.Comp, args.EntityUid))
            args.Cancel();
    }

    private bool IsValidInfusionSource(HmeSmartInfusionStandComponent component, EntityUid source)
    {
        if (!_solutions.TryGetDrainableSolution(source, out _, out var solution))
            return false;

        return solution.MaxVolume > FixedPoint2.Zero && solution.MaxVolume <= component.MaxSourceVolume;
    }

    private void UpdateInfusionStand(EntityUid uid, HmeSmartInfusionStandComponent component, StorageComponent storage)
    {
        var hasDrainableBeaker = false;
        foreach (var beaker in storage.Container.ContainedEntities)
        {
            if (_solutions.TryGetDrainableSolution(beaker, out _, out var solution) && solution.Volume > FixedPoint2.Zero)
            {
                hasDrainableBeaker = true;
                break;
            }
        }

        if (!hasDrainableBeaker)
        {
            SetInfusionVisual(uid, storage.Container.ContainedEntities.Count == 0
                ? HmeInfusionStandVisualState.Empty
                : HmeInfusionStandVisualState.Idle);
            return;
        }

        var coords = _transform.GetMapCoordinates(uid);
        foreach (var (target, damageable) in _lookup.GetEntitiesInRange<DamageableComponent>(coords, component.Range))
        {
            if (!ShouldInfusionTreat(target))
                continue;

            if (TryInjectBestMedication(storage, target, damageable, component))
            {
                SetInfusionVisual(uid, HmeInfusionStandVisualState.Active);
                return;
            }
        }

        SetInfusionVisual(uid, HmeInfusionStandVisualState.Filled);
    }

    private bool ShouldInfusionTreat(EntityUid target)
    {
        if (!TryComp<MobStateComponent>(target, out var mobState) || _mobState.IsDead(target, mobState))
            return false;

        return _mobState.IsCritical(target, mobState) || _standing.IsDown(target);
    }

    private bool TryInjectBestMedication(
        StorageComponent storage,
        EntityUid target,
        DamageableComponent damageable,
        HmeSmartInfusionStandComponent component)
    {
        if (!_solutions.TryGetInjectableSolution(target, out var targetSolutionEnt, out var targetSolution))
            return false;

        var remainingCapacity = targetSolution.AvailableVolume;
        if (remainingCapacity <= FixedPoint2.Zero)
            return false;

        foreach (var (damageType, damage) in GetPositiveDamageTypesDescending(damageable))
        {
            if (!component.DamageTypeReagents.TryGetValue(damageType, out var reagents))
                continue;

            foreach (var reagent in reagents)
            {
                var currentDose = targetSolution.GetTotalPrototypeQuantity(reagent);
                var dosageRoom = component.TransferAmount - currentDose;
                if (dosageRoom <= FixedPoint2.Zero)
                    continue;

                if (!TryFindStoredReagent(storage, reagent, out var sourceSolutionEnt, out var sourceSolution))
                    continue;

                var available = sourceSolution.GetTotalPrototypeQuantity(reagent);
                var amount = FixedPoint2.Min(available, FixedPoint2.Min(remainingCapacity, dosageRoom));
                if (amount <= FixedPoint2.Zero)
                    continue;

                if (!_solutions.RemoveReagent(sourceSolutionEnt, reagent, amount))
                    continue;

                if (_solutions.TryAddSolution(targetSolutionEnt.Value, new Solution(reagent, amount)))
                    return true;

                _solutions.TryAddSolution(sourceSolutionEnt, new Solution(reagent, amount));
            }
        }

        return false;
    }

    private static IEnumerable<KeyValuePair<string, FixedPoint2>> GetPositiveDamageTypesDescending(DamageableComponent damageable)
    {
        var sorted = new List<KeyValuePair<string, FixedPoint2>>();
        foreach (var damage in damageable.Damage.DamageDict)
        {
            if (damage.Value > FixedPoint2.Zero)
                sorted.Add(damage);
        }

        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
        return sorted;
    }

    private bool TryFindStoredReagent(
        StorageComponent storage,
        string reagent,
        out Entity<SolutionComponent> solutionEnt,
        out Solution solution)
    {
        foreach (var beaker in storage.Container.ContainedEntities)
        {
            if (!_solutions.TryGetDrainableSolution(beaker, out var beakerSolutionEnt, out var beakerSolution))
                continue;

            if (beakerSolution.GetTotalPrototypeQuantity(reagent) <= FixedPoint2.Zero)
                continue;

            solutionEnt = beakerSolutionEnt.Value;
            solution = beakerSolution;
            return true;
        }

        solutionEnt = default;
        solution = default!;
        return false;
    }

    private void SetInfusionVisual(EntityUid uid, HmeInfusionStandVisualState state)
    {
        _appearance.SetData(uid, HmeInfusionStandVisuals.State, state);
    }

    private void OnBlossomInit(Entity<HmeStasisBlossomComponent> ent, ref ComponentInit args)
    {
        SetBlossomVisual(ent.Owner, GetBlossomVisualState(ent.Comp));
    }

    private void OnBlossomMapInit(Entity<HmeStasisBlossomComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Deployed);
    }

    private void OnBlossomAfterInteract(Entity<HmeStasisBlossomComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        var target = args.Target.Value;
        if (ent.Comp.AttachedTarget != null || !HasComp<BodyComponent>(target))
            return;

        if (!CanAttachBlossomToTarget(target))
        {
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-target-not-down"), target, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.AttachDelay, new HmeStasisBlossomDoAfterEvent(), ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-attach-start"), target, args.User);
            args.Handled = true;
        }
    }

    private void OnBlossomInteractHand(Entity<HmeStasisBlossomComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.AttachedTarget == null)
            return;

        args.Handled = true;

        if (ent.Comp.Deployed)
        {
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-detach-deployed"), ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        DetachBlossom(ent, args.User);
    }

    private void OnBlossomUseInHand(Entity<HmeStasisBlossomComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryToggleBlossom(ent, args.User);
    }

    private void OnBlossomActivate(Entity<HmeStasisBlossomComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryToggleBlossom(ent, args.User);
    }

    private void OnBlossomGetItemActions(Entity<HmeStasisBlossomComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ent.Comp.ToggleActionEntity);
    }

    private void OnBlossomToggleAction(Entity<HmeStasisBlossomComponent> ent, ref HmeStasisBlossomToggleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryToggleBlossom(ent, args.Performer);
    }

    private void OnBlossomDoAfter(Entity<HmeStasisBlossomComponent> ent, ref HmeStasisBlossomDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null || ent.Comp.AttachedTarget != null)
            return;

        var target = args.Args.Target.Value;
        if (!Exists(target) || !CanAttachBlossomToTarget(target))
            return;

        if (!_hands.TryDrop(args.Args.User, ent.Owner, Transform(target).Coordinates, checkActionBlocker: false, doDropInteraction: false))
            return;

        AttachBlossom(ent, target);
        _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-attached"), target, args.Args.User);
        args.Handled = true;
    }

    private void OnBlossomShutdown(Entity<HmeStasisBlossomComponent> ent, ref ComponentShutdown args)
    {
        RestoreBlossomTransportAssist(ent);
        SetBlossomMetabolism(ent, false);
    }

    private void OnBlossomInteractionVerbs(Entity<HmeStasisBlossomComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.AttachedTarget != null)
        {
            var user = args.User;
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString(ent.Comp.Deployed ? "hme-stasis-blossom-verb-fold" : "hme-stasis-blossom-verb-deploy"),
                Act = () => TryToggleBlossom(ent, user),
                Priority = 1,
            });

            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("hme-stasis-blossom-verb-detach"),
                Disabled = ent.Comp.Deployed,
                Message = ent.Comp.Deployed ? Loc.GetString("hme-stasis-blossom-detach-deployed") : null,
                Act = () => DetachBlossom(ent, user),
            });
        }
    }

    private void OnBlossomExamined(Entity<HmeStasisBlossomComponent> ent, ref ExaminedEvent args)
    {
        var charge = 0f;
        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            charge = battery.CurrentCharge / battery.MaxCharge * 100f;

        args.PushMarkup(Loc.GetString("hme-stasis-blossom-battery-examine", ("charge", $"{charge:F0}")));
    }

    private void OnBlossomGettingPickedUpAttempt(Entity<HmeStasisBlossomComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.AttachedTarget != null)
            args.Cancel();
    }

    private void OnBlossomPullAttempt(Entity<HmeStasisBlossomComponent> ent, ref PullAttemptEvent args)
    {
        if (ent.Comp.AttachedTarget is not { } target || !Exists(target))
            return;

        args.Cancelled = true;

        if (args.PullerUid == target)
            return;

        _pulling.TryStartPull(args.PullerUid, target);
    }

    private void OnStandingAttempt(Entity<StandingStateComponent> ent, ref StandAttemptEvent args)
    {
        if (HasDeployedBlossomAttached(ent.Owner))
            args.Cancel();
    }

    private void UpdateAttachedBlossom(Entity<HmeStasisBlossomComponent> ent, TimeSpan now)
    {
        if (ent.Comp.AttachedTarget is not { } target || !Exists(target))
            return;

        if (!ent.Comp.Deployed)
            return;

        ForceBlossomTargetDown(target);
        ApplyBlossomTransportAssist(ent, target);

        if (now >= ent.Comp.NextPowerUse)
        {
            ent.Comp.NextPowerUse = now + ent.Comp.PowerUseInterval;
            if (!_powerCell.TryUseCharge(ent.Owner, ent.Comp.PowerUse))
            {
                FoldBlossom(ent);
                _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-power-empty"), ent.Owner, target, PopupType.SmallCaution);
                return;
            }
        }

        if (now < ent.Comp.NextHealTime)
            return;

        ent.Comp.NextHealTime = now + ent.Comp.HealInterval;
        _damage.TryChangeDamage(target, ent.Comp.StabilizingDamage, ignoreResistances: true, interruptsDoAfters: false, canSever: false);
    }

    private void AttachBlossom(Entity<HmeStasisBlossomComponent> ent, EntityUid target)
    {
        ent.Comp.AttachedTarget = target;
        ent.Comp.Deployed = false;
        ent.Comp.NextHealTime = _timing.CurTime;
        ent.Comp.NextPowerUse = _timing.CurTime + ent.Comp.PowerUseInterval;
        _transform.SetCoordinates(ent.Owner, new EntityCoordinates(target, Vector2.Zero));
        SetBlossomVisual(ent.Owner, HmeStasisBlossomVisualState.Attached);
        Dirty(ent);
    }

    private bool TryToggleBlossom(Entity<HmeStasisBlossomComponent> ent, EntityUid user)
    {
        if (ent.Comp.AttachedTarget is not { } target || !Exists(target))
        {
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-not-attached"), ent.Owner, user, PopupType.SmallCaution);
            return true;
        }

        if (ent.Comp.Deployed)
        {
            FoldBlossom(ent);
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-folded"), ent.Owner, user);
            return true;
        }

        if (!CanAttachBlossomToTarget(target))
        {
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-target-not-down"), target, user, PopupType.SmallCaution);
            return true;
        }

        if (!_powerCell.HasCharge(ent.Owner, ent.Comp.PowerUse, user: user))
            return true;

        ForceBlossomTargetDown(target);
        ent.Comp.Deployed = true;
        ent.Comp.NextHealTime = _timing.CurTime;
        ent.Comp.NextPowerUse = _timing.CurTime + ent.Comp.PowerUseInterval;
        SetBlossomMetabolism(ent, true);
        ApplyBlossomTransportAssist(ent, target);
        SetBlossomVisual(ent.Owner, HmeStasisBlossomVisualState.Deployed);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, true);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-deployed"), target, user);
        return true;
    }

    private void FoldBlossom(Entity<HmeStasisBlossomComponent> ent)
    {
        ent.Comp.Deployed = false;
        RestoreBlossomTransportAssist(ent);
        SetBlossomMetabolism(ent, false);
        SetBlossomVisual(ent.Owner, ent.Comp.AttachedTarget == null ? HmeStasisBlossomVisualState.Compact : HmeStasisBlossomVisualState.Attached);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, false);
        Dirty(ent);
    }

    private void DetachBlossom(Entity<HmeStasisBlossomComponent> ent, EntityUid user)
    {
        if (ent.Comp.Deployed || ent.Comp.AttachedTarget is not { } target)
            return;

        if (!_hands.CanPickupAnyHand(user, ent.Owner, checkActionBlocker: false))
        {
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-detach-hand-required"), ent.Owner, user, PopupType.SmallCaution);
            return;
        }

        var coords = _transform.GetMapCoordinates(ent.Owner);
        SetBlossomMetabolism(ent, false);
        ent.Comp.AttachedTarget = null;
        _transform.SetMapCoordinates(ent.Owner, coords);
        SetBlossomVisual(ent.Owner, HmeStasisBlossomVisualState.Compact);
        Dirty(ent);

        if (!_hands.TryPickupAnyHand(user, ent.Owner, checkActionBlocker: false, animate: false))
        {
            AttachBlossom(ent, target);
            return;
        }

        _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-detached"), ent.Owner, user);
    }

    private void SetBlossomMetabolism(Entity<HmeStasisBlossomComponent> ent, bool apply)
    {
        if (ent.Comp.AttachedTarget is not { } target || !Exists(target) || ent.Comp.MetabolismApplied == apply)
            return;

        var ev = new ApplyMetabolicMultiplierEvent(target, ent.Comp.MetabolicMultiplier, apply);
        RaiseLocalEvent(target, ref ev);
        ent.Comp.MetabolismApplied = apply;
    }

    private bool CanAttachBlossomToTarget(EntityUid target)
    {
        if (!HasComp<BodyComponent>(target))
            return false;

        if (_standing.IsDown(target))
            return true;

        return TryComp<MobStateComponent>(target, out var mobState) &&
               (_mobState.IsCritical(target, mobState) || _mobState.IsDead(target, mobState));
    }

    private void ForceBlossomTargetDown(EntityUid target)
    {
        if (TryComp<StandingStateComponent>(target, out var standing) &&
            standing.CurrentState == StandingState.GettingUp)
        {
            standing.CurrentState = StandingState.Lying;
            Dirty(target, standing);
        }

        _standing.Down(target, playSound: false, dropHeldItems: false, force: true, standingState: standing);
    }

    private bool HasDeployedBlossomAttached(EntityUid target)
    {
        var query = EntityQueryEnumerator<HmeStasisBlossomComponent>();
        while (query.MoveNext(out _, out var blossom))
        {
            if (blossom.Deployed && blossom.AttachedTarget == target)
                return true;
        }

        return false;
    }

    private void ApplyBlossomTransportAssist(Entity<HmeStasisBlossomComponent> ent, EntityUid target)
    {
        if (ent.Comp.TransportAdjustedTarget == target)
            return;

        RestoreBlossomTransportAssist(ent);

        if (!TryComp<FixturesComponent>(target, out var fixtures))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (fixture.Density <= 0f)
                continue;

            ent.Comp.OriginalFixtureDensities[id] = fixture.Density;
            var density = MathF.Max(fixture.Density * ent.Comp.TransportDensityScale, ent.Comp.MinTransportFixtureDensity);
            _physics.SetDensity(target, id, fixture, density, update: false, manager: fixtures);
        }

        if (ent.Comp.OriginalFixtureDensities.Count == 0)
            return;

        ent.Comp.TransportAdjustedTarget = target;
        _physics.ResetMassData(target, manager: fixtures);
        Dirty(ent);
    }

    private void RestoreBlossomTransportAssist(Entity<HmeStasisBlossomComponent> ent)
    {
        if (ent.Comp.TransportAdjustedTarget is not { } target)
        {
            ent.Comp.OriginalFixtureDensities.Clear();
            return;
        }

        if (Exists(target) && TryComp<FixturesComponent>(target, out var fixtures))
        {
            foreach (var (id, density) in ent.Comp.OriginalFixtureDensities)
            {
                if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                    continue;

                _physics.SetDensity(target, id, fixture, density, update: false, manager: fixtures);
            }

            _physics.ResetMassData(target, manager: fixtures);
        }

        ent.Comp.TransportAdjustedTarget = null;
        ent.Comp.OriginalFixtureDensities.Clear();
        Dirty(ent);
    }

    private void UpdateCocoon(EntityUid uid, HmeStasisBlossomCocoonComponent component, TimeSpan now)
    {
        if (component.Target is not { } target || !Exists(target) || _mobState.IsDead(target) || now >= component.EndTime)
        {
            EndCocoon((uid, component));
            return;
        }

        _transform.SetMapCoordinates(uid, _transform.GetMapCoordinates(target));

        if (now < component.NextHealTime)
            return;

        component.NextHealTime = now + component.HealInterval;
        _damage.TryChangeDamage(target, component.StabilizingDamage, ignoreResistances: true, interruptsDoAfters: false, canSever: false);
    }

    private void OnCocoonShutdown(Entity<HmeStasisBlossomCocoonComponent> ent, ref ComponentShutdown args)
    {
        SetCocoonMetabolism(ent, false);
    }

    private void EndCocoon(Entity<HmeStasisBlossomCocoonComponent> ent)
    {
        SetCocoonMetabolism(ent, false);
        QueueDel(ent.Owner);
    }

    private void SetCocoonMetabolism(Entity<HmeStasisBlossomCocoonComponent> ent, bool apply)
    {
        if (ent.Comp.Target is not { } target || !Exists(target) || ent.Comp.MetabolismApplied == apply)
            return;

        var ev = new ApplyMetabolicMultiplierEvent(target, ent.Comp.MetabolicMultiplier, apply);
        RaiseLocalEvent(target, ref ev);
        ent.Comp.MetabolismApplied = apply;
    }

    private void SetBlossomVisual(EntityUid uid, HmeStasisBlossomVisualState state)
    {
        _appearance.SetData(uid, HmeStasisBlossomVisuals.State, state);
    }

    private static HmeStasisBlossomVisualState GetBlossomVisualState(HmeStasisBlossomComponent component)
    {
        if (component.Deployed)
            return HmeStasisBlossomVisualState.Deployed;

        return component.AttachedTarget == null
            ? HmeStasisBlossomVisualState.Compact
            : HmeStasisBlossomVisualState.Attached;
    }

    private void OnBeaconInit(Entity<HmeRescueBeaconComponent> ent, ref ComponentInit args)
    {
        SetBeaconVisual(ent);
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
        var processed = new HashSet<EntityUid>();
        if (!TryComp<TransformComponent>(grid, out var gridXform))
            return;

        var children = new List<EntityUid>();
        var childEnumerator = gridXform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            children.Add(child);
        }

        foreach (var child in children)
        {
            TryTriggerBeaconRescueRecursive(child, grid, processed);
        }
    }

    private void TryTriggerBeaconRescueRecursive(EntityUid entity, EntityUid rootGrid, HashSet<EntityUid> processed)
    {
        if (entity == rootGrid || !Exists(entity) || !processed.Add(entity))
            return;

        if (TryTriggerBeaconRescue(entity, rootGrid))
            return;

        if (TryComp<ContainerManagerComponent>(entity, out var containerManager))
        {
            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities.ToArray())
                {
                    TryTriggerBeaconRescueRecursive(contained, rootGrid, processed);
                }
            }
        }

        if (!TryComp<TransformComponent>(entity, out var xform))
            return;

        var children = new List<EntityUid>();
        var childEnumerator = xform.ChildEnumerator;
        while (childEnumerator.MoveNext(out var child))
        {
            children.Add(child);
        }

        foreach (var child in children)
        {
            TryTriggerBeaconRescueRecursive(child, rootGrid, processed);
        }
    }

    private bool TryTriggerBeaconRescue(EntityUid target, EntityUid rootGrid)
    {
        var cleanupEv = new HmeSalvageMobCleanupAttemptEvent(rootGrid, target);
        foreach (var item in _inventory.GetHandOrInventoryEntities((target, null, null), SlotFlags.ARMBANDLEFT | SlotFlags.ARMBANDRIGHT))
        {
            RaiseLocalEvent(item, ref cleanupEv);
            if (cleanupEv.Handled)
                return true;
        }

        return false;
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
        if (!Exists(target))
            return;

        ent.Comp.Armed = false;
        ent.Comp.Spent = true;
        Dirty(ent);
        SetBeaconVisual(ent);

        if (!TryGetBeaconRescueDestination(ent.Comp, out var destination))
            return;

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
        TearBeaconArm(target, slot.SlotFlags);
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

    private void TearBeaconArm(EntityUid target, SlotFlags slotFlags)
    {
        var symmetry = (slotFlags & SlotFlags.ARMBANDLEFT) != 0
            ? BodyPartSymmetry.Left
            : BodyPartSymmetry.Right;

        if (TryRemoveFirstPart(target, BodyPartType.Arm, symmetry))
            return;

        TryRemoveFirstPart(target, BodyPartType.Hand, symmetry);
    }

    private bool TryRemoveFirstPart(EntityUid target, BodyPartType partType, BodyPartSymmetry symmetry)
    {
        foreach (var (part, _) in _body.GetBodyChildrenOfType(target, partType, symmetry: symmetry))
        {
            var amputate = new AmputateAttemptEvent(part);
            RaiseLocalEvent(part, ref amputate);
            QueueDel(part);
            return true;
        }

        return false;
    }
}

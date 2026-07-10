using System.Numerics;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Shared._Forge.HME;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.PowerCell.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Forge.HME;

public sealed class HmeStasisBlossomSystem : EntitySystem
{
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _deployedBlossoms = new();
    private readonly Dictionary<EntityUid, EntityUid> _blossomByTarget = new();
    private readonly List<EntityUid> _updateBuffer = new();
    private readonly List<string> _fixtureRemovalBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

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
        SubscribeLocalEvent<BodyComponent, ComponentShutdown>(OnTargetBodyShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _updateBuffer.Clear();
        _updateBuffer.AddRange(_deployedBlossoms);

        foreach (var uid in _updateBuffer)
        {
            if (!TryComp(uid, out HmeStasisBlossomComponent? blossom))
            {
                _deployedBlossoms.Remove(uid);
                continue;
            }

            UpdateAttachedBlossom((uid, blossom), now);
        }
    }

    private void OnBlossomInit(Entity<HmeStasisBlossomComponent> ent, ref ComponentInit args)
    {
        SetBlossomVisual(ent.Owner, GetBlossomVisualState(ent.Comp));
        if (ent.Comp.AttachedTarget is { } target)
            _blossomByTarget[target] = ent.Owner;
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
        ClearBlossomTarget(ent, fold: false);
    }

    private void OnTargetBodyShutdown(Entity<BodyComponent> ent, ref ComponentShutdown args)
    {
        if (!_blossomByTarget.TryGetValue(ent.Owner, out var blossomUid) ||
            !TryComp(blossomUid, out HmeStasisBlossomComponent? blossom))
        {
            _blossomByTarget.Remove(ent.Owner);
            return;
        }

        ClearBlossomTarget((blossomUid, blossom));
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
        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery) && battery.MaxCharge > 0f)
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
        if (!_blossomByTarget.TryGetValue(ent.Owner, out var blossomUid))
            return;

        if (!TryComp(blossomUid, out HmeStasisBlossomComponent? blossom))
        {
            _blossomByTarget.Remove(ent.Owner);
            return;
        }

        if (blossom.Deployed)
            args.Cancel();
    }

    private void UpdateAttachedBlossom(Entity<HmeStasisBlossomComponent> ent, TimeSpan now)
    {
        if (ent.Comp.AttachedTarget is not { } target || !Exists(target))
        {
            ClearBlossomTarget(ent);
            return;
        }

        if (!ent.Comp.Deployed)
        {
            _deployedBlossoms.Remove(ent.Owner);
            return;
        }

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
        ent.Comp.NextPowerUse = _timing.CurTime;
        _blossomByTarget[target] = ent.Owner;
        _transform.SetCoordinates(ent.Owner, new EntityCoordinates(target, Vector2.Zero));
        SetBlossomVisual(ent.Owner, HmeStasisBlossomVisualState.Attached);
        Dirty(ent);
    }

    private bool TryToggleBlossom(Entity<HmeStasisBlossomComponent> ent, EntityUid user)
    {
        if (ent.Comp.AttachedTarget is not { } target || !Exists(target))
        {
            ClearBlossomTarget(ent);
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-not-attached"), ent.Owner, user, PopupType.SmallCaution);
            return true;
        }

        if (ent.Comp.Deployed)
        {
            FoldBlossom(ent);
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-folded"), ent.Owner, user);
            return true;
        }

        if (!CanAttachBlossomToTarget(target, ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-target-not-down"), target, user, PopupType.SmallCaution);
            return true;
        }

        if (!_powerCell.HasCharge(ent.Owner, ent.Comp.PowerUse, user: user))
            return true;

        if (!_powerCell.TryUseCharge(ent.Owner, ent.Comp.PowerUse))
            return true;

        ForceBlossomTargetDown(target);
        ent.Comp.Deployed = true;
        ent.Comp.NextHealTime = _timing.CurTime;
        ent.Comp.NextPowerUse = _timing.CurTime + ent.Comp.PowerUseInterval;
        SetBlossomMetabolism(ent, true);
        ApplyBlossomTransportAssist(ent, target);
        SetBlossomVisual(ent.Owner, HmeStasisBlossomVisualState.Deployed);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, true);
        _deployedBlossoms.Add(ent.Owner);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("hme-stasis-blossom-deployed"), target, user);
        return true;
    }

    private void FoldBlossom(Entity<HmeStasisBlossomComponent> ent)
    {
        ent.Comp.Deployed = false;
        _deployedBlossoms.Remove(ent.Owner);
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
        RemoveTargetMapping(target, ent.Owner);
        ent.Comp.AttachedTarget = null;
        _deployedBlossoms.Remove(ent.Owner);
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

    private bool CanAttachBlossomToTarget(EntityUid target, EntityUid? blossom = null)
    {
        if (!HasComp<BodyComponent>(target))
            return false;

        if (_blossomByTarget.TryGetValue(target, out var attachedBlossom) && attachedBlossom != blossom)
        {
            if (Exists(attachedBlossom))
                return false;

            _blossomByTarget.Remove(target);
        }

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

    private void ClearBlossomTarget(Entity<HmeStasisBlossomComponent> ent, bool fold = true)
    {
        if (ent.Comp.AttachedTarget is { } target)
            RemoveTargetMapping(target, ent.Owner);

        RestoreBlossomTransportAssist(ent);
        SetBlossomMetabolism(ent, false);
        ent.Comp.AttachedTarget = null;
        ent.Comp.Deployed = false;
        _deployedBlossoms.Remove(ent.Owner);

        if (fold && Exists(ent.Owner))
        {
            SetBlossomVisual(ent.Owner, HmeStasisBlossomVisualState.Compact);
            _actions.SetToggled(ent.Comp.ToggleActionEntity, false);
        }

        Dirty(ent);
    }

    private void RemoveTargetMapping(EntityUid target, EntityUid blossom)
    {
        if (_blossomByTarget.TryGetValue(target, out var mapped) && mapped == blossom)
            _blossomByTarget.Remove(target);
    }

    private void ApplyBlossomTransportAssist(Entity<HmeStasisBlossomComponent> ent, EntityUid target)
    {
        if (ent.Comp.TransportAdjustedTarget != target)
        {
            RestoreBlossomTransportAssist(ent);
            ent.Comp.TransportAdjustedTarget = target;
        }

        if (!TryComp<FixturesComponent>(target, out var fixtures))
            return;

        _fixtureRemovalBuffer.Clear();
        foreach (var id in ent.Comp.TransportFixtureDensityChanges.Keys)
        {
            if (!fixtures.Fixtures.ContainsKey(id))
                _fixtureRemovalBuffer.Add(id);
        }

        foreach (var id in _fixtureRemovalBuffer)
        {
            ent.Comp.TransportFixtureDensityChanges.Remove(id);
        }

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (fixture.Density <= 0f)
                continue;

            if (ent.Comp.TransportFixtureDensityChanges.TryGetValue(id, out var existing) &&
                MathF.Abs(fixture.Density - existing.Applied) <= 0.001f)
            {
                continue;
            }

            var original = fixture.Density;
            var density = MathF.Max(original * ent.Comp.TransportDensityScale, ent.Comp.MinTransportFixtureDensity);
            ent.Comp.TransportFixtureDensityChanges[id] = new HmeFixtureDensityChange(original, density);
            _physics.SetDensity(target, id, fixture, density, update: false, manager: fixtures);
        }

        if (ent.Comp.TransportFixtureDensityChanges.Count == 0)
        {
            ent.Comp.TransportAdjustedTarget = null;
            return;
        }

        _physics.ResetMassData(target, manager: fixtures);
        Dirty(ent);
    }

    private void RestoreBlossomTransportAssist(Entity<HmeStasisBlossomComponent> ent)
    {
        if (ent.Comp.TransportAdjustedTarget is not { } target)
        {
            ent.Comp.TransportFixtureDensityChanges.Clear();
            return;
        }

        if (Exists(target) && TryComp<FixturesComponent>(target, out var fixtures))
        {
            foreach (var (id, change) in ent.Comp.TransportFixtureDensityChanges)
            {
                if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                    continue;

                if (MathF.Abs(fixture.Density - change.Applied) > 0.001f)
                    continue;

                _physics.SetDensity(target, id, fixture, change.Original, update: false, manager: fixtures);
            }

            _physics.ResetMassData(target, manager: fixtures);
        }

        ent.Comp.TransportAdjustedTarget = null;
        ent.Comp.TransportFixtureDensityChanges.Clear();
        Dirty(ent);
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
}

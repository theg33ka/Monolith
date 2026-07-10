using System.Numerics;
using Content.Shared._Forge.HME;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Forge.HME;

public sealed class HmeSmartInfusionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _activeStands = new();
    private readonly List<EntityUid> _updateBuffer = new();
    private readonly Dictionary<string, List<StoredReagentSource>> _reagentSources = new();
    private readonly List<KeyValuePair<string, FixedPoint2>> _damageBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HmeSmartInfusionStandComponent, ComponentInit>(OnInfusionInit);
        SubscribeLocalEvent<HmeSmartInfusionStandComponent, MapInitEvent>(OnInfusionMapInit);
        SubscribeLocalEvent<HmeSmartInfusionStandComponent, ComponentShutdown>(OnInfusionShutdown);
        SubscribeLocalEvent<HmeSmartInfusionStandComponent, ContainerIsInsertingAttemptEvent>(OnInfusionInsertAttempt);
        SubscribeLocalEvent<HmeSmartInfusionStandComponent, EntInsertedIntoContainerMessage>(OnInfusionContainerModified);
        SubscribeLocalEvent<HmeSmartInfusionStandComponent, EntRemovedFromContainerMessage>(OnInfusionContainerModified);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _updateBuffer.Clear();
        _updateBuffer.AddRange(_activeStands);

        foreach (var uid in _updateBuffer)
        {
            if (!TryComp(uid, out HmeSmartInfusionStandComponent? infusion) ||
                !TryComp(uid, out StorageComponent? storage))
            {
                _activeStands.Remove(uid);
                continue;
            }

            if (now < infusion.NextUpdate)
                continue;

            infusion.NextUpdate = now + infusion.UpdateInterval;
            UpdateInfusionStand(uid, infusion, storage);
        }
    }

    private void OnInfusionInit(Entity<HmeSmartInfusionStandComponent> ent, ref ComponentInit args)
    {
        SetInfusionVisual(ent.Owner, HmeInfusionStandVisualState.Empty);
    }

    private void OnInfusionMapInit(Entity<HmeSmartInfusionStandComponent> ent, ref MapInitEvent args)
    {
        RefreshInfusionActiveState(ent);
    }

    private void OnInfusionShutdown(Entity<HmeSmartInfusionStandComponent> ent, ref ComponentShutdown args)
    {
        _activeStands.Remove(ent.Owner);
    }

    private void OnInfusionInsertAttempt(Entity<HmeSmartInfusionStandComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!IsValidInfusionSource(ent.Comp, args.EntityUid))
            args.Cancel();
    }

    private void OnInfusionContainerModified(Entity<HmeSmartInfusionStandComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            RefreshInfusionActiveState(ent);
    }

    private void OnInfusionContainerModified(Entity<HmeSmartInfusionStandComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            RefreshInfusionActiveState(ent);
    }

    private void RefreshInfusionActiveState(Entity<HmeSmartInfusionStandComponent> ent)
    {
        if (!TryComp(ent.Owner, out StorageComponent? storage))
        {
            _activeStands.Remove(ent.Owner);
            SetInfusionVisual(ent.Owner, HmeInfusionStandVisualState.Empty);
            return;
        }

        if (HasAnyInfusionSource(ent.Comp, storage))
        {
            _activeStands.Add(ent.Owner);
            return;
        }

        _activeStands.Remove(ent.Owner);
        SetInfusionVisual(ent.Owner, storage.Container.ContainedEntities.Count == 0
            ? HmeInfusionStandVisualState.Empty
            : HmeInfusionStandVisualState.Idle);
    }

    private bool IsValidInfusionSource(HmeSmartInfusionStandComponent component, EntityUid source)
    {
        if (!_solutions.TryGetDrainableSolution(source, out _, out var solution))
            return false;

        return solution.MaxVolume > FixedPoint2.Zero && solution.MaxVolume <= component.MaxSourceVolume;
    }

    private bool HasAnyInfusionSource(HmeSmartInfusionStandComponent component, StorageComponent storage)
    {
        foreach (var beaker in storage.Container.ContainedEntities)
        {
            if (IsValidInfusionSource(component, beaker))
                return true;
        }

        return false;
    }

    private void UpdateInfusionStand(EntityUid uid, HmeSmartInfusionStandComponent component, StorageComponent storage)
    {
        var hasDrainableReagent = BuildStoredReagentIndex(storage);
        if (!hasDrainableReagent)
        {
            SetInfusionVisual(uid, storage.Container.ContainedEntities.Count == 0
                ? HmeInfusionStandVisualState.Empty
                : HmeInfusionStandVisualState.Idle);
            return;
        }

        if (TryFindBestInfusionTarget(uid, component, out var target, out var damageable) &&
            TryInjectBestMedication(target, damageable, component))
        {
            SetInfusionVisual(uid, HmeInfusionStandVisualState.Active);
            return;
        }

        SetInfusionVisual(uid, HmeInfusionStandVisualState.Filled);
    }

    private bool TryFindBestInfusionTarget(
        EntityUid stand,
        HmeSmartInfusionStandComponent component,
        out EntityUid target,
        out DamageableComponent damageable)
    {
        target = default;
        damageable = default!;

        var coords = _transform.GetMapCoordinates(stand);
        var bestCritical = false;
        var bestDamage = FixedPoint2.Zero;
        var bestDistance = float.MaxValue;

        foreach (var (candidate, candidateDamageable) in _lookup.GetEntitiesInRange<DamageableComponent>(coords, component.Range))
        {
            if (!TryGetInfusionPriority(candidate, candidateDamageable, component, coords.Position, out var critical, out var damage, out var distance))
                continue;

            if (target.IsValid() && !IsBetterInfusionTarget(candidate, critical, damage, distance, target, bestCritical, bestDamage, bestDistance))
                continue;

            target = candidate;
            damageable = candidateDamageable;
            bestCritical = critical;
            bestDamage = damage;
            bestDistance = distance;
        }

        return target.IsValid();
    }

    private bool TryGetInfusionPriority(
        EntityUid target,
        DamageableComponent damageable,
        HmeSmartInfusionStandComponent component,
        Vector2 standPosition,
        out bool critical,
        out FixedPoint2 treatableDamage,
        out float distance)
    {
        critical = false;
        treatableDamage = FixedPoint2.Zero;
        distance = 0f;

        if (!TryComp<MobStateComponent>(target, out var mobState) || _mobState.IsDead(target, mobState))
            return false;

        critical = _mobState.IsCritical(target, mobState);
        if (!critical && !_standing.IsDown(target))
            return false;

        foreach (var damage in damageable.Damage.DamageDict)
        {
            if (damage.Value > FixedPoint2.Zero && component.DamageTypeReagents.ContainsKey(damage.Key))
                treatableDamage += damage.Value;
        }

        if (treatableDamage <= FixedPoint2.Zero)
            return false;

        distance = Vector2.DistanceSquared(standPosition, _transform.GetMapCoordinates(target).Position);
        return true;
    }

    private static bool IsBetterInfusionTarget(
        EntityUid candidate,
        bool critical,
        FixedPoint2 damage,
        float distance,
        EntityUid current,
        bool currentCritical,
        FixedPoint2 currentDamage,
        float currentDistance)
    {
        if (critical != currentCritical)
            return critical;

        var damageCompare = damage.CompareTo(currentDamage);
        if (damageCompare != 0)
            return damageCompare > 0;

        var distanceCompare = distance.CompareTo(currentDistance);
        if (distanceCompare != 0)
            return distanceCompare < 0;

        return candidate.Id < current.Id;
    }

    private bool TryInjectBestMedication(
        EntityUid target,
        DamageableComponent damageable,
        HmeSmartInfusionStandComponent component)
    {
        if (!_solutions.TryGetInjectableSolution(target, out var targetSolutionEnt, out var targetSolution))
            return false;

        var remainingCapacity = targetSolution.AvailableVolume;
        if (remainingCapacity <= FixedPoint2.Zero)
            return false;

        GetPositiveDamageTypesDescending(damageable);
        foreach (var (damageType, _) in _damageBuffer)
        {
            if (!component.DamageTypeReagents.TryGetValue(damageType, out var reagents))
                continue;

            foreach (var reagent in reagents)
            {
                var currentDose = targetSolution.GetTotalPrototypeQuantity(reagent);
                var dosageRoom = component.TransferAmount - currentDose;
                if (dosageRoom <= FixedPoint2.Zero)
                    continue;

                if (!_reagentSources.TryGetValue(reagent, out var sources))
                    continue;

                foreach (var source in sources)
                {
                    var available = source.Solution.GetTotalPrototypeQuantity(reagent);
                    var amount = FixedPoint2.Min(available, FixedPoint2.Min(remainingCapacity, dosageRoom));
                    if (amount <= FixedPoint2.Zero)
                        continue;

                    if (!_solutions.RemoveReagent(source.SolutionEnt, reagent, amount))
                        continue;

                    if (_solutions.TryAddSolution(targetSolutionEnt.Value, new Solution(reagent, amount)))
                        return true;

                    _solutions.TryAddSolution(source.SolutionEnt, new Solution(reagent, amount));
                }
            }
        }

        return false;
    }

    private bool BuildStoredReagentIndex(StorageComponent storage)
    {
        _reagentSources.Clear();
        var hasDrainableReagent = false;

        foreach (var beaker in storage.Container.ContainedEntities)
        {
            if (!_solutions.TryGetDrainableSolution(beaker, out var solutionEnt, out var solution) ||
                solution.Volume <= FixedPoint2.Zero)
            {
                continue;
            }

            hasDrainableReagent = true;
            foreach (var reagent in solution.Contents)
            {
                if (reagent.Quantity <= FixedPoint2.Zero)
                    continue;

                var reagentId = reagent.Reagent.Prototype;
                if (!_reagentSources.TryGetValue(reagentId, out var sources))
                {
                    sources = new List<StoredReagentSource>();
                    _reagentSources.Add(reagentId, sources);
                }

                sources.Add(new StoredReagentSource(solutionEnt.Value, solution));
            }
        }

        return hasDrainableReagent;
    }

    private void GetPositiveDamageTypesDescending(DamageableComponent damageable)
    {
        _damageBuffer.Clear();
        foreach (var damage in damageable.Damage.DamageDict)
        {
            if (damage.Value > FixedPoint2.Zero)
                _damageBuffer.Add(damage);
        }

        _damageBuffer.Sort((a, b) => b.Value.CompareTo(a.Value));
    }

    private void SetInfusionVisual(EntityUid uid, HmeInfusionStandVisualState state)
    {
        _appearance.SetData(uid, HmeInfusionStandVisuals.State, state);
    }

    private readonly record struct StoredReagentSource(Entity<SolutionComponent> SolutionEnt, Solution Solution);
}

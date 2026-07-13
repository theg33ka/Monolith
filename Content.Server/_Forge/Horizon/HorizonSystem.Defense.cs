using System.Linq;
using System.Numerics;
using Content.Server._Forge.Horizon.Components;
using Content.Server._Forge.Horizon.Domain;
using Content.Server._Mono.NPC.HTN.Operators;
using Content.Server.NPC.HTN;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Components;
using Content.Shared.Damage;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Map;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    private readonly HashSet<EntityUid> _defenseExecutors = new();
    private TimeSpan _nextDefenseCheck;

    private void InitializeDefense()
    {
        SubscribeLocalEvent<HorizonObjectComponent, DamageChangedEvent>(OnHorizonObjectDamaged);
        SubscribeLocalEvent<HorizonDefenseExecutorComponent, ComponentStartup>(OnDefenseExecutorStartup);
        SubscribeLocalEvent<HorizonDefenseExecutorComponent, ComponentShutdown>(OnDefenseExecutorShutdown);
        SubscribeLocalEvent<HorizonDefenseExecutorComponent, SteeringDoneEvent>(OnDefenseSteeringDone);
    }

    private void ResetDefenseState()
    {
        _defenseExecutors.Clear();
        _nextDefenseCheck = default;
    }

    private void OnDefenseExecutorStartup(Entity<HorizonDefenseExecutorComponent> ent, ref ComponentStartup args)
    {
        _defenseExecutors.Add(ent.Owner);
    }

    private void OnDefenseExecutorShutdown(Entity<HorizonDefenseExecutorComponent> ent, ref ComponentShutdown args)
    {
        _defenseExecutors.Remove(ent.Owner);
        if (ent.Comp.OrderId is { } orderId && State.Orders.TryGetValue(orderId, out var order) &&
            order.Status == HorizonOrderStatus.Active)
        {
            order.Status = HorizonOrderStatus.Failed;
            order.FailureReason = "AMZ executor was destroyed";
        }

        if (State.Incidents.TryGetValue(ent.Comp.IncidentKey, out var incident))
            incident.ResponseOrdered = false;
    }

    private void OnDefenseSteeringDone(Entity<HorizonDefenseExecutorComponent> ent, ref SteeringDoneEvent args)
    {
        if (!ent.Comp.Busy)
            return;

        if (ent.Comp.Returning)
        {
            CompleteDefenseOrder(ent);
            return;
        }

        ent.Comp.ReturnAt = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonDefenseHoldSeconds)));
    }

    private void OnHorizonObjectDamaged(Entity<HorizonObjectComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is null || args.Origin is not { } origin ||
            origin == ent.Owner || HasComp<HorizonObjectComponent>(origin) ||
            !State.Objects.TryGetValue(ent.Owner, out var target))
        {
            return;
        }

        var damage = args.DamageDelta.GetTotal().Float();
        if (damage <= 0f)
            return;

        var organization = GetOrganizationKey(origin);
        var key = HorizonDefensePolicy.IncidentKey(organization, target.ObjectId);
        if (!State.Incidents.TryGetValue(key, out var incident))
        {
            var maxIncidents = Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonMaxIncidents));
            if (State.Incidents.Count >= maxIncidents)
            {
                var oldest = State.Incidents.Values.OrderBy(value => value.LastSeen).First();
                State.Incidents.Remove(oldest.Key);
            }

            incident = new HorizonIncident
            {
                Key = key,
                Organization = organization,
                Target = ent.Owner,
                Origin = origin,
                Position = target.WorldPosition,
                FirstSeen = _timing.CurTime,
                LastSeen = _timing.CurTime,
            };
            State.Incidents.Add(key, incident);
        }

        incident.Origin = origin;
        incident.Target = ent.Owner;
        incident.Position = target.WorldPosition;
        incident.Damage += damage;
        incident.LastSeen = _timing.CurTime;
        incident.ResponseOrdered = false;
        UpdateDamageRelation(organization, damage);
        TryDispatchDefense(incident);
    }

    private string GetOrganizationKey(EntityUid origin)
    {
        if (TryComp<NpcFactionMemberComponent>(origin, out var faction) && faction.Factions.Count > 0)
            return faction.Factions.OrderBy(value => value.ToString(), StringComparer.OrdinalIgnoreCase).First().ToString();

        var xform = Transform(origin);
        return xform.GridUid is { } grid ? $"grid-{grid}" : $"entity-{origin}";
    }

    private void UpdateDamageRelation(string organization, float damage)
    {
        if (!State.Relations.TryGetValue(organization, out var relation))
        {
            if (State.Relations.Count >= Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonMaxRelations)))
                return;

            relation = new HorizonRelation { Organization = organization };
            State.Relations.Add(organization, relation);
        }

        relation.Damage = Math.Clamp(relation.Damage + (int) MathF.Ceiling(damage), 0, 1000000);
        relation.Iff = HorizonDefensePolicy.IffForDamage(relation.Damage);
    }

    private void ProcessPendingIncidents()
    {
        var limit = Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonWorkItemsPerTick));
        foreach (var incident in State.Incidents.Values
                     .Where(value => !value.ResponseOrdered)
                     .OrderByDescending(value => value.LastSeen)
                     .Take(limit))
        {
            TryDispatchDefense(incident);
        }
    }

    private bool TryDispatchDefense(HorizonIncident incident)
    {
        if (incident.ResponseOrdered || State.Phase is HorizonDeploymentPhase.Dormant or HorizonDeploymentPhase.Destroyed)
            return false;

        var activeCount = _defenseExecutors.Count(uid =>
            !Deleted(uid) && TryComp<HorizonDefenseExecutorComponent>(uid, out var executor) && executor.Busy);
        if (activeCount >= Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonMaxDefenseUnits)))
            return false;

        if (incident.Origin is not { } origin || Deleted(origin))
            return false;

        var originTransform = Transform(origin);
        var originPosition = _transform.GetWorldPosition(originTransform);
        if (!HorizonDefensePolicy.CanChase(
                incident.Position,
                originPosition,
                _configuration.GetCVar(ForgeCVars.HorizonDefenseChaseRadius)))
        {
            return false;
        }

        foreach (var uid in _defenseExecutors)
        {
            if (Deleted(uid) || !TryComp<HorizonDefenseExecutorComponent>(uid, out var executor) || executor.Busy ||
                !TryComp<HTNComponent>(uid, out var htn) || !State.Objects.TryGetValue(uid, out var record))
            {
                continue;
            }

            var rawCost = Math.Max(0, _configuration.GetCVar(ForgeCVars.HorizonDefenseRawCost));
            var energyCost = Math.Max(0, _configuration.GetCVar(ForgeCVars.HorizonDefenseEnergyCost));
            if (!HorizonEconomy.TrySpend(State.Ledger, rawCost, 0, energyCost))
                return false;

            if (!TryCreateOrder(HorizonOrderType.DefendObject, string.Empty, incident.Target,
                    new EntityCoordinates(origin, Vector2.Zero), out var orderId))
            {
                HorizonEconomy.Refund(State.Ledger, rawCost, 0, energyCost,
                    _configuration.GetCVar(ForgeCVars.HorizonResourceCap));
                return false;
            }

            SetOrderStatus(orderId, HorizonOrderStatus.Active);
            var order = State.Orders[orderId];
            order.Executor = uid;
            order.Deadline = _timing.CurTime + TimeSpan.FromSeconds(
                Math.Max(5f, _configuration.GetCVar(ForgeCVars.HorizonDefenseResponseSeconds)));
            executor.OrderId = orderId;
            executor.IncidentKey = incident.Key;
            executor.HomeMap = record.MapId;
            executor.HomePosition = record.WorldPosition;
            executor.ReturnAt = order.Deadline;
            executor.Returning = false;
            executor.Busy = true;
            incident.ResponseOrdered = true;
            htn.Blackboard.SetValue("Target", _transform.ToCoordinates(new MapCoordinates(originPosition, originTransform.MapID)));
            htn.Blackboard.SetValue("TargetRotation", Angle.Zero);
            _npc.WakeNPC(uid, htn);
            AnnounceOnce($"defense-{incident.Key}",
                Loc.GetString("horizon-announcement-defense-dispatched", ("target", State.Objects[incident.Target!.Value].ObjectId)));
            return true;
        }

        return false;
    }

    private void UpdateDefense()
    {
        if (!_roundInitialized || _timing.CurTime < _nextDefenseCheck || State.Phase == HorizonDeploymentPhase.Destroyed)
            return;

        _nextDefenseCheck = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(0.5f, _configuration.GetCVar(ForgeCVars.HorizonOrderCheckInterval)));
        foreach (var uid in _defenseExecutors.ToArray())
        {
            if (Deleted(uid) || !TryComp<HorizonDefenseExecutorComponent>(uid, out var executor) || !executor.Busy ||
                _timing.CurTime < executor.ReturnAt)
            {
                continue;
            }

            if (executor.Returning)
            {
                CompleteDefenseOrder((uid, executor));
                continue;
            }

            SendDefenseHome(uid, executor);
        }
    }

    private void SendDefenseHome(EntityUid uid, HorizonDefenseExecutorComponent executor)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
        {
            CompleteDefenseOrder((uid, executor));
            return;
        }

        executor.Returning = true;
        executor.ReturnAt = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(5f, _configuration.GetCVar(ForgeCVars.HorizonDefenseResponseSeconds)));
        htn.Blackboard.SetValue("Target", _transform.ToCoordinates(new MapCoordinates(executor.HomePosition, executor.HomeMap)));
        htn.Blackboard.SetValue("TargetRotation", Angle.Zero);
        _npc.WakeNPC(uid, htn);
    }

    private void CompleteDefenseOrder(Entity<HorizonDefenseExecutorComponent> ent)
    {
        if (ent.Comp.OrderId is { } orderId)
            SetOrderStatus(orderId, HorizonOrderStatus.Complete);
        ent.Comp.OrderId = null;
        ent.Comp.IncidentKey = string.Empty;
        ent.Comp.Returning = false;
        ent.Comp.Busy = false;
    }

    private void OnRegisteredObjectDestroyed(EntityUid uid)
    {
        var incident = State.Incidents.Values
            .Where(value => value.Target == uid)
            .OrderByDescending(value => value.LastSeen)
            .FirstOrDefault();
        if (incident is not null)
            incident.DestroyedObjects++;
    }
}

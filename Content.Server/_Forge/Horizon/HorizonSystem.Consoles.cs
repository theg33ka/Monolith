using System.Linq;
using System.Text;
using Content.Server._Forge.Horizon.Domain;
using Content.Server.Stack;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _horizonConsoles = new();
    private readonly Dictionary<EntityUid, EntityUid> _consoleActors = new();
    private TimeSpan _nextConsoleRefresh;

    private void InitializeConsoles()
    {
        SubscribeLocalEvent<HorizonConsoleComponent, ComponentStartup>(OnConsoleStartup);
        SubscribeLocalEvent<HorizonConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<HorizonConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
        SubscribeLocalEvent<HorizonConsoleComponent, HorizonConsoleRefreshMessage>(OnConsoleRefresh);
        SubscribeLocalEvent<HorizonConsoleComponent, HorizonConsoleContributeMessage>(OnConsoleContributeMessage);
        SubscribeLocalEvent<HorizonConsoleComponent, HorizonConsoleHandoffMessage>(OnConsoleHandoff);
        SubscribeLocalEvent<HorizonConsoleComponent, AfterInteractEvent>(OnConsoleAfterInteract);
    }

    private void ResetConsoleState()
    {
        _horizonConsoles.Clear();
        _consoleActors.Clear();
        _nextConsoleRefresh = default;
    }

    private void OnConsoleStartup(Entity<HorizonConsoleComponent> ent, ref ComponentStartup args)
    {
        _horizonConsoles.Add(ent.Owner);
    }

    private void OnConsoleShutdown(Entity<HorizonConsoleComponent> ent, ref ComponentShutdown args)
    {
        _horizonConsoles.Remove(ent.Owner);
        _consoleActors.Remove(ent.Owner);
    }

    private void OnConsoleOpened(Entity<HorizonConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        _consoleActors[ent.Owner] = args.Actor;
        UpdateConsoleUi(ent, args.Actor);
    }

    private void OnConsoleRefresh(Entity<HorizonConsoleComponent> ent, ref HorizonConsoleRefreshMessage args)
    {
        _consoleActors[ent.Owner] = args.Actor;
        UpdateConsoleUi(ent, args.Actor);
    }

    private void OnConsoleContributeMessage(Entity<HorizonConsoleComponent> ent, ref HorizonConsoleContributeMessage args)
    {
        _consoleActors[ent.Owner] = args.Actor;
        UpdateConsoleUi(ent, args.Actor);
    }

    private void OnConsoleHandoff(Entity<HorizonConsoleComponent> ent, ref HorizonConsoleHandoffMessage args)
    {
        _consoleActors[ent.Owner] = args.Actor;
        var result = HandoffWanderingAi(args.Actor);
        _popup.PopupEntity(result, ent.Owner, args.Actor, PopupType.Medium);
        UpdateConsoleUi(ent, args.Actor);
    }

    private void OnConsoleAfterInteract(Entity<HorizonConsoleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !ent.Comp.AcceptsResources || !args.Used.IsValid() ||
            !TryComp<StackComponent>(args.Used, out var stack) ||
            (ent.Comp.AcceptedStacks.Count > 0 && !ent.Comp.AcceptedStacks.Contains(stack.StackTypeId)))
        {
            return;
        }

        var used = args.Used;
        var units = Math.Min(
            stack.Count,
            Math.Max(1, _configuration.GetCVar(ForgeCVars.HorizonMaxContributionUnits)));
        if (units <= 0)
            return;

        var organization = GetOrganizationKey(args.User);
        var relation = GetOrCreateRelation(organization);
        if (relation is null)
            return;

        _stack.SetCount(used, stack.Count - units, stack);
        relation.Contribution = Math.Clamp(relation.Contribution + units, 0, 1000000);
        relation.Access = HorizonRelationPolicy.AccessFor(relation.Contribution, relation.Damage);
        State.Ledger.Raw = Math.Clamp(
            State.Ledger.Raw + units,
            0,
            _configuration.GetCVar(ForgeCVars.HorizonResourceCap));
        args.Handled = true;
        _consoleActors[ent.Owner] = args.User;
        _popup.PopupEntity(
            Loc.GetString("horizon-console-contribution-accepted", ("units", units), ("access", relation.Access)),
            ent.Owner,
            args.User,
            PopupType.Medium);
        UpdateAllConsoleUis();
    }

    private HorizonRelation? GetOrCreateRelation(string organization)
    {
        if (State.Relations.TryGetValue(organization, out var relation))
            return relation;

        if (State.Relations.Count >= Math.Clamp(_configuration.GetCVar(ForgeCVars.HorizonMaxRelations), 1, 256))
            return null;

        relation = new HorizonRelation { Organization = organization };
        State.Relations.Add(organization, relation);
        return relation;
    }

    private void UpdateConsoles()
    {
        if (!_roundInitialized || _timing.CurTime < _nextConsoleRefresh)
            return;

        _nextConsoleRefresh = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonUiRefreshInterval)));
        UpdateAllConsoleUis();
    }

    private void UpdateAllConsoleUis()
    {
        foreach (var uid in _horizonConsoles.ToArray())
        {
            if (Deleted(uid) || !TryComp<HorizonConsoleComponent>(uid, out var component) ||
                !_userInterface.IsUiOpen(uid, HorizonConsoleUiKey.Key))
            {
                continue;
            }

            UpdateConsoleUi((uid, component), _consoleActors.GetValueOrDefault(uid));
        }
    }

    private void UpdateConsoleUi(Entity<HorizonConsoleComponent> ent, EntityUid? actor)
    {
        var organization = actor is { } user && !Deleted(user) ? GetOrganizationKey(user) : "anonymous";
        var relation = State.Relations.GetValueOrDefault(organization) ?? new HorizonRelation { Organization = organization };
        var state = new HorizonConsoleBoundUserInterfaceState(
            State.Phase,
            State.ActiveCluster,
            organization,
            relation.Access,
            relation.Iff,
            relation.Contribution,
            relation.Damage,
            State.Ledger.Raw,
            State.Ledger.Components,
            State.Ledger.Energy,
            State.Objects.Count,
            State.Orders.Count,
            State.Incidents.Count,
            ent.Comp.AcceptsResources,
            BuildNetworkNeed(),
            BuildDiagnostics(relation.Access),
            actor is { } handoffActor && CanWanderingAiHandoff(handoffActor),
            IsWanderingCarrierControlled(),
            actor is { } viewer ? BuildWanderingAiBrief(viewer) : string.Empty);
        _userInterface.SetUiState(ent.Owner, HorizonConsoleUiKey.Key, state);
    }

    private string BuildNetworkNeed()
    {
        var pending = State.Orders.Values
            .Where(order => order.Status == HorizonOrderStatus.Queued && !string.IsNullOrWhiteSpace(order.ProjectId))
            .OrderBy(order => order.CreatedAt)
            .FirstOrDefault();
        return pending is null
            ? "No pending construction request."
            : $"Pending project {pending.ProjectId}; reserve {State.Ledger.Raw}/{State.Ledger.Components}/{State.Ledger.Energy}.";
    }

    private string BuildDiagnostics(HorizonAccessTier access)
    {
        if (access == HorizonAccessTier.Basic)
            return "Detailed diagnostics require Operator access.";

        var output = new StringBuilder(1024);
        output.AppendLine("OBJECTS");
        foreach (var obj in State.Objects.Values.Take(64).OrderBy(value => value.ObjectId).Take(24))
            output.AppendLine($"{obj.ObjectId} {obj.Kind} {(obj.Active ? "online" : "offline")} d{obj.BranchDepth}");

        output.AppendLine("ORDERS");
        foreach (var order in State.Orders.Values.OrderByDescending(value => value.CreatedAt).Take(12))
            output.AppendLine($"{order.Type} {order.Status} {order.ProjectId}");

        output.AppendLine("INCIDENTS");
        foreach (var incident in State.Incidents.Values.OrderByDescending(value => value.LastSeen).Take(12))
            output.AppendLine($"{incident.Organization} dmg={incident.Damage:F0} lost={incident.DestroyedObjects}");

        if (access >= HorizonAccessTier.Partner)
        {
            output.AppendLine("RELATIONS");
            foreach (var relation in State.Relations.Values.OrderBy(value => value.Organization).Take(12))
                output.AppendLine($"{relation.Organization} {relation.Access}/{relation.Iff} +{relation.Contribution} -{relation.Damage}");
        }

        return output.ToString().TrimEnd();
    }

    private string BuildWanderingAiBrief(EntityUid viewer)
    {
        var authorized = State.WanderingAi == viewer ||
                         State.WanderingCarrier == viewer && IsWanderingCarrierControlled();
        if (!authorized || State.WanderingAi is not { } ai ||
            !TryComp<Components.HorizonWanderingAiComponent>(ai, out var component))
        {
            return string.Empty;
        }

        return $"{Loc.GetString("horizon-wandering-ai-identity")}\n" +
               $"{Loc.GetString("horizon-wandering-ai-directives-summary")}\n\n" +
               $"{Loc.GetString("horizon-wandering-ai-goal")}: {Loc.GetString(component.Goal)}\n" +
               $"{Loc.GetString("horizon-wandering-ai-context")}: {Loc.GetString(component.Context)}\n" +
               $"{Loc.GetString("horizon-wandering-ai-permissions")}: {Loc.GetString(component.Permissions)}";
    }

    private void SpawnHorizonConsoles(EntityUid grid)
    {
        Spawn("HorizonCommunicationConsole", new EntityCoordinates(grid, new System.Numerics.Vector2(1f, 0f)));
        Spawn("HorizonResourceTerminal", new EntityCoordinates(grid, new System.Numerics.Vector2(-1f, 0f)));
    }
}

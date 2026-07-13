using Content.Server._CorvaxNext.Silicons.Borgs;
using Content.Server._Forge.Horizon.Components;
using Content.Server._Forge.Horizon.Domain;
using Content.Shared._CorvaxNext.Silicons.Borgs.Components;
using Content.Shared.Mind;
using Content.Shared.Silicons.StationAi;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly AiRemoteControlSystem _aiRemote = default!;

    private void InitializeWanderingAi()
    {
        SubscribeLocalEvent<HorizonWanderingAiComponent, ComponentStartup>(OnWanderingAiStartup);
        SubscribeLocalEvent<HorizonWanderingAiComponent, ComponentShutdown>(OnWanderingAiShutdown);
        SubscribeLocalEvent<HorizonWanderingCarrierComponent, ComponentStartup>(OnWanderingCarrierStartup);
        SubscribeLocalEvent<HorizonWanderingCarrierComponent, ComponentShutdown>(OnWanderingCarrierShutdown);
    }

    private void ResetWanderingAiState()
    {
        State.WanderingAi = null;
        State.WanderingCarrier = null;
    }

    private void OnWanderingAiStartup(Entity<HorizonWanderingAiComponent> ent, ref ComponentStartup args)
    {
        if (State.WanderingAi is not { } current || !Exists(current))
            State.WanderingAi = ent.Owner;
    }

    private void OnWanderingAiShutdown(Entity<HorizonWanderingAiComponent> ent, ref ComponentShutdown args)
    {
        if (State.WanderingAi == ent.Owner)
            State.WanderingAi = null;
    }

    private void OnWanderingCarrierStartup(Entity<HorizonWanderingCarrierComponent> ent, ref ComponentStartup args)
    {
        if (State.WanderingCarrier is not { } current || !Exists(current))
            State.WanderingCarrier = ent.Owner;
    }

    private void OnWanderingCarrierShutdown(Entity<HorizonWanderingCarrierComponent> ent, ref ComponentShutdown args)
    {
        if (State.WanderingCarrier == ent.Owner)
            State.WanderingCarrier = null;
    }

    public bool CanWanderingAiHandoff(EntityUid actor)
    {
        if (State.WanderingAi is not { } ai || State.WanderingCarrier is not { } carrier)
            return false;

        var aiAvailable = Exists(ai) && TryComp<StationAiHeldComponent>(ai, out _);
        var carrierAvailable = Exists(carrier) && TryComp<AiRemoteControllerComponent>(carrier, out var remote);
        return HorizonWanderingAiPolicy.CanHandoff(
            State.Phase,
            actor == ai,
            aiAvailable,
            carrierAvailable,
            aiAvailable && _mind.TryGetMind(ai, out _, out _),
            carrierAvailable && _mind.TryGetMind(carrier, out _, out _),
            carrierAvailable && remote!.AiHolder is null && remote.LinkedMind is null,
            aiAvailable && _stationAi.TryGetCore(ai, out _));
    }

    public bool IsWanderingCarrierControlled()
    {
        return State.WanderingCarrier is { } carrier && Exists(carrier) &&
               TryComp<AiRemoteControllerComponent>(carrier, out var remote) &&
               remote.AiHolder == State.WanderingAi && remote.LinkedMind is not null;
    }

    public string HandoffWanderingAi(EntityUid actor)
    {
        if (!CanWanderingAiHandoff(actor) ||
            State.WanderingAi is not { } ai ||
            State.WanderingCarrier is not { } carrier ||
            !_mind.TryGetMind(ai, out var mindId, out _) ||
            !TryComp<StationAiHeldComponent>(ai, out var held) ||
            !TryComp<AiRemoteControllerComponent>(carrier, out var remote) ||
            !_stationAi.TryGetCore(ai, out var core))
        {
            return "Wandering AI handoff denied by Horizon safeguards.";
        }

        _mind.ControlMob(ai, carrier);
        remote.AiHolder = ai;
        remote.LinkedMind = mindId;
        held.CurrentConnectedEntity = carrier;
        _stationAi.SwitchRemoteEntityMode(core, false);
        AnnounceOnce("wandering-ai-handoff", Loc.GetString("horizon-announcement-ai-handoff"));
        UpdateAllConsoleUis();
        return "Wandering AI transferred to the designated AMU-05 carrier.";
    }

    public string ReturnWanderingAi()
    {
        if (State.WanderingCarrier is not { } carrier || !Exists(carrier) ||
            !TryComp<AiRemoteControllerComponent>(carrier, out var remote) ||
            remote.AiHolder != State.WanderingAi || remote.LinkedMind is null)
        {
            return "Wandering AI is not controlling the Horizon carrier.";
        }

        _aiRemote.ReturnMindIntoAi(carrier);
        AnnounceOnce("wandering-ai-return", Loc.GetString("horizon-announcement-ai-return"));
        UpdateAllConsoleUis();
        return "Wandering AI returned to the O-01 core.";
    }

    private EntityUid CreateWanderingCarrier(EntityUid grid)
    {
        if (State.WanderingCarrier is { } existing && Exists(existing))
            return existing;

        return Spawn("HorizonWanderingCarrier", new EntityCoordinates(grid, System.Numerics.Vector2.Zero));
    }
}

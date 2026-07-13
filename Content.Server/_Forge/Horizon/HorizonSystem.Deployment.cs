using System.Linq;
using System.Numerics;
using Content.Server._Forge.Horizon.Domain;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Shared._Forge.CCVar;
using Content.Shared._Forge.Horizon;
using Content.Shared._Forge.Horizon.Components;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Forge.Horizon;

public sealed partial class HorizonSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<string, TimeSpan> _announcementTimes = new(StringComparer.OrdinalIgnoreCase);
    private TimeSpan _nextProximityCheck;
    private bool _roundInitialized;

    private void InitializeDeployment()
    {
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        SetupRound();
    }

    private void ResetDeploymentState()
    {
        _roundInitialized = false;
        _announcementTimes.Clear();
        _nextProximityCheck = default;
    }

    public string SetupRound()
    {
        if (_roundInitialized)
            return "Horizon round deployment is already initialized.";

        if (!_configuration.GetCVar(ForgeCVars.HorizonEnabled))
            return "Horizon is disabled by forge.horizon.enabled.";

        State.Reset(_configuration.GetCVar(ForgeCVars.HorizonMaxWorkQueue));
        _announcementTimes.Clear();
        _roundInitialized = true;
        State.RoundStartedAt = _timing.CurTime;
        State.AutoActivationAt = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonAutoActivationSeconds)));
        _nextProximityCheck = _timing.CurTime;

        var count = Math.Clamp(_configuration.GetCVar(ForgeCVars.HorizonRtrCount), 6, 8);
        var minDistance = Math.Max(1000f, _configuration.GetCVar(ForgeCVars.HorizonRtrMinDistance));
        var maxDistance = Math.Max(minDistance, _configuration.GetCVar(ForgeCVars.HorizonRtrMaxDistance));
        var angleOffset = (float) _random.NextAngle().Theta;

        for (var index = 0; index < count; index++)
        {
            var angle = angleOffset + MathF.Tau * index / count;
            var distance = _random.NextFloat(minDistance, maxDistance);
            var position = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            var rtr = Spawn("HorizonRTR", new MapCoordinates(position, _ticker.DefaultMap));
            RenameObject(rtr, $"RTR-{index + 1:D2}");
        }

        return $"Spawned {count} dormant RTR objects on map {_ticker.DefaultMap}.";
    }

    private void UpdateDeployment()
    {
        if (!_roundInitialized || State.Phase == HorizonDeploymentPhase.Destroyed)
            return;

        var now = _timing.CurTime;
        if (State.Phase == HorizonDeploymentPhase.Dormant)
        {
            if (now >= State.AutoActivationAt)
            {
                BeginActivation(null, automatic: true);
                return;
            }

            if (now >= _nextProximityCheck)
            {
                _nextProximityCheck = now + TimeSpan.FromSeconds(
                    Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonProximityCheckInterval)));
                TryProximityActivation();
            }
        }

        if (State.Phase == HorizonDeploymentPhase.Waking &&
            State.WakeCompletesAt is { } completesAt &&
            now >= completesAt)
        {
            CompleteWake();
        }
    }

    private void TryProximityActivation()
    {
        var proximitySquared = MathF.Pow(Math.Max(1f, _configuration.GetCVar(ForgeCVars.HorizonProximityDistance)), 2);
        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } player || Deleted(player))
                continue;

            var playerTransform = Transform(player);
            var playerPosition = _transform.GetWorldPosition(playerTransform);
            foreach (var rtr in State.Objects.Values)
            {
                if (rtr.Kind != HorizonObjectKind.Rtr || !rtr.Dormant || rtr.MapId != playerTransform.MapID)
                    continue;

                if (Vector2.DistanceSquared(playerPosition, rtr.WorldPosition) > proximitySquared)
                    continue;

                BeginActivation(rtr.Entity, automatic: false);
                return;
            }
        }
    }

    public string BeginActivation(EntityUid? requestedRtr, bool automatic)
    {
        if (!_roundInitialized)
            return "Horizon round deployment is not initialized.";

        if (State.Phase != HorizonDeploymentPhase.Dormant)
            return $"Horizon already has an active cluster ({State.Phase}).";

        var dormant = State.Objects.Values
            .Where(obj => obj.Kind == HorizonObjectKind.Rtr && obj.Dormant && !Deleted(obj.Entity))
            .Select(obj => obj.Entity)
            .ToList();
        if (dormant.Count < 2)
            return "At least two intact dormant RTR objects are required.";

        var primary = requestedRtr is { } requested && dormant.Contains(requested)
            ? requested
            : dormant.OrderBy(uid => State.Objects[uid].WorldPosition.LengthSquared()).First();
        var neighbor = HorizonDeploymentPlanner.FindNearestNeighbor(primary, dormant, uid => State.Objects[uid].WorldPosition);
        if (neighbor is null)
            return "No intact neighboring RTR was found.";

        State.PrimaryRtr = primary;
        State.NeighborRtr = neighbor.Value;
        State.ActiveCluster = "HZ-01";
        State.Phase = HorizonDeploymentPhase.Waking;
        State.WakeCompletesAt = _timing.CurTime + TimeSpan.FromSeconds(
            Math.Max(0f, _configuration.GetCVar(ForgeCVars.HorizonWakeDelaySeconds)));
        SetObjectActivation(primary, true, false, State.ActiveCluster);
        SetObjectActivation(neighbor.Value, true, false, State.ActiveCluster);

        AnnounceOnce(
            "wake",
            Loc.GetString(automatic ? "horizon-announcement-auto-wake" : "horizon-announcement-proximity-wake"));
        return $"Activated {primary} with nearest neighbor {neighbor.Value}; cluster={State.ActiveCluster}.";
    }

    private void CompleteWake()
    {
        if (State.Phase != HorizonDeploymentPhase.Waking)
            return;

        State.WakeCompletesAt = null;
        State.Phase = HorizonDeploymentPhase.Deploying;
        AnnounceOnce("link", Loc.GetString("horizon-announcement-link-established"));
        OnInitialClusterReady();
    }

    partial void OnInitialClusterReady();

    private void AnnounceOnce(string key, string message)
    {
        var now = _timing.CurTime;
        var cooldown = TimeSpan.FromSeconds(Math.Max(0f, _configuration.GetCVar(ForgeCVars.HorizonAnnouncementCooldown)));
        if (_announcementTimes.TryGetValue(key, out var last) && now - last < cooldown)
            return;

        _announcementTimes[key] = now;
        _chat.DispatchGlobalAnnouncement(
            message,
            Loc.GetString("horizon-announcement-title"),
            playSound: false,
            colorOverride: Color.FromHex("#35c9c2"));
    }

    private bool RenameObject(EntityUid uid, string objectId)
    {
        if (!State.Objects.TryGetValue(uid, out var record))
            return false;

        State.ObjectsById.Remove(record.ObjectId);
        record.ObjectId = objectId;
        State.ObjectsById[objectId] = uid;
        if (TryComp<HorizonObjectComponent>(uid, out var component))
            component.ObjectId = objectId;
        return true;
    }

    public void DestroyNetwork(string reason)
    {
        State.Phase = HorizonDeploymentPhase.Destroyed;
        State.MatureNetwork = false;
        State.WorkQueue.Clear();
        foreach (var order in State.Orders.Values)
        {
            if (order.Status is HorizonOrderStatus.Queued or HorizonOrderStatus.Active)
            {
                order.Status = HorizonOrderStatus.Cancelled;
                order.FailureReason = reason;
            }
        }

        AnnounceOnce("destroyed", Loc.GetString("horizon-announcement-network-destroyed"));
    }
}

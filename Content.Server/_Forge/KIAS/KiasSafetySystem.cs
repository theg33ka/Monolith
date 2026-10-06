using System.Linq;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Shared._Forge.KIAS;
using Content.Shared.Anomaly.Components;
using Content.Shared.Atmos.Monitor;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Forge.KIAS;

public sealed class KiasSafetySystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private KiasCrewSystem _crew = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private KiasDisplaySystem _display = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AnomalyStabilityChangedEvent>(OnAnomaly);
        SubscribeLocalEvent<AtmosAlarmEvent>(OnAtmos);
        SubscribeLocalEvent<KiasAnomalyGrowthEvent>(OnGrowth);
        SubscribeLocalEvent<KiasAtmosStateChangedEvent>(OnAtmosState);
    }

    private void OnAnomaly(ref AnomalyStabilityChangedEvent args)
    {
        if (Transform(args.Anomaly).GridUid is not { } grid || !_kias.ActiveGrids.Contains(grid)
            || !TryComp<AnomalyComponent>(args.Anomaly, out var anomaly)
            || args.PreviousStability is not { } previous || previous > anomaly.GrowthThreshold || args.Stability <= anomaly.GrowthThreshold
            || !_crew.HasCoverage(grid, args.Anomaly, KiasScannerModules.Spectral))
            return;
        var ev = new KiasAnomalyGrowthEvent(grid, args.Anomaly, Location(grid, args.Anomaly));
        RaiseLocalEvent(grid, ref ev, true);
    }

    private void OnAtmos(AtmosAlarmEvent args)
    {
        if (args.Source is not { } source || !HasComp<AirAlarmComponent>(source)
            || Transform(source).GridUid is not { } grid || !_kias.HasRole(grid, KiasDeviceRole.Atmosphere))
            return;
        var ev = new KiasAtmosStateChangedEvent(grid, source, args.AlarmType, Location(grid, source));
        RaiseLocalEvent(grid, ref ev, true);
    }

    private void OnGrowth(ref KiasAnomalyGrowthEvent args)
    {
        Publish(args.Grid, Loc.GetString("kias-anomaly-growth", ("location", args.Location)), true);
    }

    private void OnAtmosState(ref KiasAtmosStateChangedEvent args)
    {
        if (args.State == AtmosAlarmType.Danger)
            Publish(args.Grid, Loc.GetString("kias-atmos-danger", ("location", args.Location)), true);
    }

    public string Location(EntityUid grid, EntityUid source)
    {
        if (TryComp<KiasDeviceComponent>(source, out var device) && !string.IsNullOrWhiteSpace(device.Room))
            return device.Room;
        if (_crew.RoomLabel(grid, source) is { } room)
            return room;
        var local = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(source)).Position;
        if (TryComp<MapGridComponent>(grid, out var map))
            local -= map.LocalAABB.Center;
        if (local.LengthSquared() <= 4)
            return Loc.GetString("kias-sector-center");
        var sector = local.Y >= 0 ? "fore" : "aft";
        sector += local.X < 0 ? "-port" : "-starboard";
        return Loc.GetString($"kias-sector-{sector}");
    }

    public void Publish(EntityUid grid, string message, bool warning = false, bool announce = true, EntityUid? speaker = null, string group = "")
    {
        if (!_kias.ActiveGrids.Contains(grid) || !TryComp<KiasGridComponent>(grid, out var runtime))
            return;
        if (_kias.HasRole(grid, KiasDeviceRole.Recorder))
        {
            if (runtime.Log.Count >= 64)
                runtime.Log.Dequeue();
            runtime.Log.Enqueue($"[{_timing.CurTime:hh\\:mm\\:ss}] {message}");
            foreach (var uid in runtime.Online)
            {
                if (!_kias.IsOnline(uid) || !TryComp<KiasRecorderComponent>(uid, out var recorder))
                    continue;
                while (recorder.Entries.Count >= 64)
                    recorder.Entries.RemoveAt(0);
                recorder.Entries.Add(runtime.Log.Last());
            }
        }
        if (announce)
        {
            var ev = new KiasAnnouncementEvent(grid, message, warning, speaker, group);
            RaiseLocalEvent(grid, ref ev, true);
        }
        _display.RefreshOpen(grid);
    }
}

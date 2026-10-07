using System.Linq;
using Content.Shared._Forge.KIAS;
using Robust.Shared.Timing;

namespace Content.Server._Forge.KIAS;

public sealed class KiasRecorderSystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private IGameTiming _timing = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<KiasRecorderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KiasTopologyChangedEvent>(OnTopology);
        SubscribeLocalEvent<KiasProtocolFiredEvent>(OnProtocol);
    }

    private void OnProtocol(ref KiasProtocolFiredEvent args)
    {
        if (!_kias.HasRole(args.Grid, KiasDeviceRole.Recorder)) return;
        var preset = Loc.TryGetString($"kias-preset-{args.PresetId}", out var title) ? title : (args.Index + 1).ToString();
        var critical = args.Trigger is KiasTrigger.CrewCritical or KiasTrigger.CrewDead or KiasTrigger.VesselCritical
            or KiasTrigger.Fire or KiasTrigger.AtmosDanger or KiasTrigger.Boarding;
        var text = $"[{_timing.CurTime:hh\\:mm\\:ss}] {(critical ? "[!] " : "")}[P] {preset}: {Loc.GetString($"kias-trigger-{args.Trigger.ToString().ToLowerInvariant()}")} / {args.Source}";
        foreach (var uid in Comp<KiasGridComponent>(args.Grid).Online)
        {
            if (!_kias.IsOnline(uid) || !TryComp<KiasRecorderComponent>(uid, out var recorder)) continue;
            while (recorder.ProtocolEvents.Count >= 64) recorder.ProtocolEvents.RemoveAt(0);
            recorder.ProtocolEvents.Add(text);
        }
    }

    private void OnMapInit(Entity<KiasRecorderComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Entries.Count > 64)
            ent.Comp.Entries.RemoveRange(0, ent.Comp.Entries.Count - 64);
        if (ent.Comp.ProtocolEvents.Count > 64) ent.Comp.ProtocolEvents.RemoveRange(0, ent.Comp.ProtocolEvents.Count - 64);
        for (var i = 0; i < ent.Comp.Entries.Count; i++)
            ent.Comp.Entries[i] = ent.Comp.Entries[i][..Math.Min(ent.Comp.Entries[i].Length, 512)];
        for (var i = 0; i < ent.Comp.ProtocolEvents.Count; i++)
            ent.Comp.ProtocolEvents[i] = ent.Comp.ProtocolEvents[i][..Math.Min(ent.Comp.ProtocolEvents[i].Length, 512)];
    }

    private void OnTopology(ref KiasTopologyChangedEvent args)
    {
        if (!TryComp<KiasGridComponent>(args.Grid, out var runtime) || runtime.Log.Count != 0)
            return;
        foreach (var uid in runtime.Online.OrderBy(uid => uid.Id))
        {
            if (!TryComp<KiasRecorderComponent>(uid, out var recorder))
                continue;
            foreach (var entry in recorder.Entries.TakeLast(64))
                runtime.Log.Enqueue(entry);
            if (runtime.Log.Count > 0)
                break;
        }
    }
}

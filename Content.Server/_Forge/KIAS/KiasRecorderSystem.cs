using System.Linq;
using Content.Shared._Forge.KIAS;

namespace Content.Server._Forge.KIAS;

public sealed class KiasRecorderSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<KiasRecorderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<KiasTopologyChangedEvent>(OnTopology);
    }

    private void OnMapInit(Entity<KiasRecorderComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Entries.Count > 64)
            ent.Comp.Entries.RemoveRange(0, ent.Comp.Entries.Count - 64);
        for (var i = 0; i < ent.Comp.Entries.Count; i++)
            ent.Comp.Entries[i] = ent.Comp.Entries[i][..Math.Min(ent.Comp.Entries[i].Length, 512)];
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

using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Forge.KIAS;
using Content.Shared._Mono.Company;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Forge.KIAS;

public sealed class KiasNavigationSystem : EntitySystem
{
    [Dependency] private KiasSystem _kias = default!;
    [Dependency] private KiasSafetySystem _notifications = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedShuttleSystem _shuttles = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FTLCompletedEvent>(OnFtl, after: new[] { typeof(FTLAntiCollisionSystem) });
        SubscribeLocalEvent<KiasAutopilotArrivedEvent>(OnAutopilot);
        SubscribeLocalEvent<KiasBluespaceDisturbanceEvent>(OnContact);
    }

    private void OnFtl(ref FTLCompletedEvent args)
    {
        if (_kias.ActiveGrids.Count == 0 || !TryComp<TransformComponent>(args.Entity, out var xform))
            return;
        var arrival = _transform.GetMapCoordinates(args.Entity, xform);
        var announced = new HashSet<EntityUid>();
        foreach (var horizon in _lookup.GetEntitiesInRange<KiasHorizonComponent>(arrival, 2000f))
        {
            if (!_kias.IsOnline(horizon) || Transform(horizon).GridUid is not { } grid || grid == args.Entity
                || !_kias.HasRole(grid, KiasDeviceRole.Navigation) || announced.Contains(grid))
                continue;
            var position = _transform.GetMapCoordinates(horizon);
            if (position.MapId != arrival.MapId)
                continue;
            var offset = arrival.Position - position.Position;
            var range = Math.Clamp(horizon.Comp.Range, 0, 2000f);
            if (offset.LengthSquared() > range * range)
                continue;
            announced.Add(grid);
            var ev = new KiasBluespaceDisturbanceEvent(grid, args.Entity, offset.Length(), (Angle.FromWorldVec(offset).Degrees + 360) % 360, Classify(grid, args.Entity));
            RaiseLocalEvent(grid, ref ev, true);
        }
    }

    public KiasContactDisposition Classify(EntityUid ownGrid, EntityUid contact)
    {
        if (_shuttles.GetIFFLabel(contact) == null)
            return KiasContactDisposition.Unknown;
        if (TryComp<CompanyComponent>(ownGrid, out var ownCompany) && TryComp<CompanyComponent>(contact, out var company)
            && company.CompanyName != "None" && company.CompanyName == ownCompany.CompanyName)
            return KiasContactDisposition.Friendly;
        if (TryComp<ShuttleFactionComponent>(ownGrid, out var ownFaction) && TryComp<ShuttleFactionComponent>(contact, out var faction))
        {
            if (!string.IsNullOrWhiteSpace(faction.Faction) && faction.Faction != "None" && faction.Faction == ownFaction.Faction)
                return KiasContactDisposition.Friendly;
            if (_prototypes.HasIndex<NpcFactionPrototype>(faction.Faction) && _prototypes.HasIndex<NpcFactionPrototype>(ownFaction.Faction)
                && _factions.IsFactionHostile(faction.Faction, ownFaction.Faction))
                return KiasContactDisposition.Hostile;
        }
        return HasComp<IFFComponent>(contact) ? KiasContactDisposition.Neutral : KiasContactDisposition.Unknown;
    }

    private void OnContact(ref KiasBluespaceDisturbanceEvent args)
    {
        if (!_kias.HasRole(args.Grid, KiasDeviceRole.Navigation))
            return;
        _notifications.Publish(args.Grid, Loc.GetString("kias-contact",
            ("disposition", Loc.GetString($"kias-contact-{args.Disposition.ToString().ToLowerInvariant()}")),
            ("range", MathF.Round(args.Distance / 1000f, 1)), ("bearing", Math.Round(args.Bearing))),
            args.Disposition is KiasContactDisposition.Unknown or KiasContactDisposition.Hostile);
    }

    private void OnAutopilot(ref KiasAutopilotArrivedEvent args)
    {
        if (_kias.HasRole(args.Grid, KiasDeviceRole.Navigation))
            _notifications.Publish(args.Grid, Loc.GetString("kias-autopilot-arrived"));
    }
}

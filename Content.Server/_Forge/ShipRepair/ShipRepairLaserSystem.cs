using System.Globalization;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Server._Forge.ShipRepair.Components;
using Content.Server._Mono.Radar;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared._Crescent.ShipShields;
using Content.Shared._Forge.ShipRepair.Components;
using Content.Shared._Forge.ShipWeapons.Components;
using Content.Shared._Mono.Radar;
using Content.Shared._Mono.ShipRepair;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Examine;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Forge.ShipRepair;

public sealed partial class ShipRepairLaserSystem : EntitySystem
{
    private const float MinWorkDelay = 0.05f;
    private static readonly Vector2i[] CardinalOffsets =
    [
        Vector2i.Up,
        Vector2i.Down,
        Vector2i.Left,
        Vector2i.Right,
    ];

    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipRepairLaserComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<ShipRepairLaserBeamComponent, HitscanRaycastFiredEvent>(OnBeamHit, after: [typeof(HitscanReflectSystem)]);
        SubscribeLocalEvent<ShipRepairLaserComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ShipRepairLaserComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdateRadarEffects(now);

        var query = EntityQueryEnumerator<ShipRepairLaserComponent>();
        while (query.MoveNext(out var uid, out var laser))
        {
            if (laser.ActiveGrid == null)
                continue;

            if (now >= laser.ActiveUntil)
            {
                StopRepair((uid, laser));
                continue;
            }

            if (!TryComp<LimitedChargesComponent>(uid, out var charges))
            {
                StopRepair((uid, laser));
                continue;
            }

            if (TryComp<BatteryComponent>(uid, out var battery) &&
                !_battery.TryUseCharge(uid, laser.PowerUsePerSecond * frameTime, battery))
            {
                StopRepair((uid, laser));
                continue;
            }

            ProcessWork(uid, laser, charges, now);
        }
    }

    private void OnShotAttempted(Entity<ShipRepairLaserComponent> ent, ref ShotAttemptedEvent args)
    {
        if (TryComp<LimitedChargesComponent>(ent.Owner, out var charges) && charges.Charges > 0)
            return;

        _audio.PlayPredicted(args.Used.Comp.SoundEmpty, ent.Owner, args.User);
        args.Cancel();
    }

    private void OnBeamHit(Entity<ShipRepairLaserBeamComponent> beam, ref HitscanRaycastFiredEvent args)
    {
        if (args.Canceled ||
            args.HitEntity is not { } hit ||
            !TryComp<ShipRepairLaserComponent>(args.Gun, out var laser))
        {
            return;
        }

        if (HasComp<ShipShieldComponent>(hit) || HasComp<ShipShieldedComponent>(hit))
            return;

        if (!TryGetTargetGrid(hit, out var targetGridNullable) ||
            targetGridNullable is not { } targetGrid ||
            IsOwnGrid(args.Gun, targetGrid) ||
            IsShieldedGrid(targetGrid) ||
            !TryComp<ShipRepairDataComponent>(targetGrid, out _))
        {
            return;
        }

        if (TryComp<ShipRepairRestrictComponent>(targetGrid, out var restrict) &&
            _whitelist.IsWhitelistFail(restrict.ToolWhitelist, args.Gun))
        {
            return;
        }

        if (_charges.HasInsufficientCharges(args.Gun, 1))
            return;

        var origin = GetHitLocalPosition(targetGrid, args.FromCoordinates, args.ShotDirection, args.DistanceTried);
        var now = _timing.CurTime;

        if (laser.ActiveGrid != targetGrid)
            laser.CurrentWork = null;

        laser.ActiveGrid = targetGrid;
        laser.ActiveOrigin = origin;
        laser.ActiveUntil = now + TimeSpan.FromSeconds(laser.ActiveDuration);
    }

    private void OnGetVerbs(Entity<ShipRepairLaserComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("ship-repair-laser-verb-print-receipt"),
            Priority = 1,
            Act = () => TryPrintReceipt(ent, user),
        });
    }

    private void OnExamined(Entity<ShipRepairLaserComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<LimitedChargesComponent>(ent, out var charges))
            return;

        args.PushMarkup(Loc.GetString("ship-repair-laser-examine-matter",
            ("matter", charges.Charges),
            ("maxMatter", charges.MaxCharges)));
    }

    private void ProcessWork(EntityUid laserUid, ShipRepairLaserComponent laser, LimitedChargesComponent charges, TimeSpan now)
    {
        if (laser.ActiveGrid is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var gridComp) ||
            !TryComp<ShipRepairDataComponent>(grid, out var repairData))
        {
            StopRepair((laserUid, laser));
            return;
        }

        if (laser.CurrentWork == null)
        {
            if (!TryFindNextRepair(laserUid, grid, gridComp, repairData, laser.ActiveOrigin, laser, out var work, charges.Charges))
            {
                TryPrintReceipt((laserUid, laser), grid, null, false);
                StopRepair((laserUid, laser));
                return;
            }

            if (_charges.HasInsufficientCharges(laserUid, work.Cost, charges))
            {
                StopRepair((laserUid, laser));
                return;
            }

            PrintPendingReceiptOnGridChange((laserUid, laser), grid);
            _charges.UseCharges(laserUid, work.Cost, charges);
            laser.LastTargetGrid = grid;
            RecordMatterSpent(grid, work.Cost);
            StartOrRefreshRadarEffect((laserUid, laser), grid, work.LocalPosition, now);
            work.FinishAt = now + TimeSpan.FromSeconds(MathF.Max(MinWorkDelay, work.Delay));
            laser.CurrentWork = work;
            return;
        }

        if (now < laser.CurrentWork.FinishAt)
            return;

        var current = laser.CurrentWork;
        laser.CurrentWork = null;

        if (!TryApplyRepair(laserUid, grid, gridComp, repairData, current))
            return;

        laser.LastTargetGrid = grid;
        if (IsFullyRepaired((laserUid, laser), grid))
        {
            TryPrintReceipt((laserUid, laser), grid, null, false);
            StopRepair((laserUid, laser));
        }
    }

    private void RecordMatterSpent(EntityUid grid, int amount)
    {
        var ledger = EnsureComp<ShipRepairLaserLedgerComponent>(grid);
        ledger.MatterSpent += amount;
        ledger.LastRepairTime = _gameTicker.RoundDuration();
    }

    private bool TryFindNextRepair(
        EntityUid laserUid,
        EntityUid grid,
        MapGridComponent gridComp,
        ShipRepairDataComponent repairData,
        Vector2 origin,
        ShipRepairLaserComponent laser,
        [NotNullWhen(true)] out ShipRepairLaserWork? work,
        int? maxCost = null)
    {
        work = null;
        var bestDistance = float.PositiveInfinity;

        foreach (var (chunkPos, chunk) in repairData.Chunks)
        {
            if (laser.EnableTileRepair)
                TryFindTileCandidate(grid, gridComp, repairData, chunkPos, chunk, origin, laser, maxCost, ref work, ref bestDistance);

            if (laser.EnableEntityRepair)
                TryFindEntityCandidate(laserUid, repairData, chunkPos, chunk, origin, laser, maxCost, ref work, ref bestDistance);
        }

        return work != null;
    }

    private void TryFindTileCandidate(
        EntityUid grid,
        MapGridComponent gridComp,
        ShipRepairDataComponent repairData,
        Vector2i chunkPos,
        ShipRepairChunk chunk,
        Vector2 origin,
        ShipRepairLaserComponent laser,
        int? maxCost,
        ref ShipRepairLaserWork? best,
        ref float bestDistance)
    {
        if (maxCost != null && laser.TileRepairCost > maxCost)
            return;

        var chunkBase = chunkPos * repairData.ChunkSize;
        for (var y = 0; y < repairData.ChunkSize; y++)
        {
            for (var x = 0; x < repairData.ChunkSize; x++)
            {
                var stored = chunk.Tiles[x + y * repairData.ChunkSize];
                if (stored == Tile.Empty.TypeId)
                    continue;

                var indices = chunkBase + new Vector2i(x, y);
                if (!CanRepairTile(grid, gridComp, indices, stored))
                    continue;

                var localPosition = _map.TileCenterToVector(grid, gridComp, indices);
                var distance = Vector2.DistanceSquared(localPosition, origin);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = new ShipRepairLaserWork
                {
                    Indices = indices,
                    LocalPosition = localPosition,
                    Delay = laser.TileRepairTime * laser.RepairTimeMultiplier,
                    Cost = laser.TileRepairCost,
                };
            }
        }
    }

    private void TryFindEntityCandidate(
        EntityUid laserUid,
        ShipRepairDataComponent repairData,
        Vector2i chunkPos,
        ShipRepairChunk chunk,
        Vector2 origin,
        ShipRepairLaserComponent laser,
        int? maxCost,
        ref ShipRepairLaserWork? best,
        ref float bestDistance)
    {
        var chunkBase = chunkPos * repairData.ChunkSize;
        foreach (var (repairId, spec) in chunk.Entities)
        {
            if (!TryGetEntityRepairable(laserUid, repairData, spec, out var repairable) ||
                !EntityNeedsRepair(spec))
            {
                continue;
            }

            if (maxCost != null && repairable.RepairCost > maxCost)
                continue;

            var distance = Vector2.DistanceSquared(spec.LocalPosition, origin);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = new ShipRepairLaserWork
            {
                Indices = chunkBase,
                RepairId = repairId,
                LocalPosition = spec.LocalPosition,
                Delay = repairable.RepairTime * laser.RepairTimeMultiplier,
                Cost = repairable.RepairCost,
            };
        }
    }

    private bool TryApplyRepair(
        EntityUid laserUid,
        EntityUid grid,
        MapGridComponent gridComp,
        ShipRepairDataComponent repairData,
        ShipRepairLaserWork work)
    {
        if (work.RepairId != null)
            return TryRepairEntity(laserUid, grid, repairData, work.Indices, work.RepairId.Value);

        if (!TryGetChunk(repairData, work.Indices, out var chunk))
            return false;

        var relative = GetRelativeIndices(work.Indices, repairData.ChunkSize);
        var stored = chunk.Tiles[relative.X + relative.Y * repairData.ChunkSize];
        if (!CanRepairTile(grid, gridComp, work.Indices, stored))
            return false;

        _map.SetTile(grid, gridComp, work.Indices, new Tile(stored));
        return true;
    }

    private bool TryRepairEntity(EntityUid laserUid, EntityUid grid, ShipRepairDataComponent repairData, Vector2i indices, int repairId)
    {
        if (!TryGetChunk(repairData, indices, out var chunk) ||
            !chunk.Entities.TryGetValue(repairId, out var spec) ||
            !TryGetEntityRepairable(laserUid, repairData, spec, out _))
        {
            return false;
        }

        var original = spec.OriginalEntity == null ? (EntityUid?) null : GetEntity(spec.OriginalEntity.Value);
        if (original != null && !TerminatingOrDeleted(original.Value))
        {
            var ev = new ShipRepairReinstateQueryEvent(true);
            RaiseLocalEvent(original.Value, ref ev);

            if (ev.Handled)
            {
                if (!ev.Repairable)
                    return false;
            }
            else if (Transform(original.Value).GridUid != null)
            {
                return false;
            }
            else
            {
                QueueDel(original.Value);
            }
        }

        var prototype = repairData.EntityPalette[spec.ProtoIndex];
        var spawned = Spawn(prototype, new EntityCoordinates(grid, spec.LocalPosition));
        _transform.SetLocalRotation(spawned, spec.Rotation);

        spec.OriginalEntity = GetNetEntity(spawned);
        RaiseNetworkEvent(new RepairEntityMessage(GetNetEntity(grid), indices, repairId, spec));
        return true;
    }

    private bool TryGetEntityRepairable(
        EntityUid laserUid,
        ShipRepairDataComponent repairData,
        ShipRepairEntitySpecifier spec,
        [NotNullWhen(true)] out ShipRepairableComponent? repairable)
    {
        repairable = null;

        if (spec.ProtoIndex < 0 ||
            spec.ProtoIndex >= repairData.EntityPalette.Count ||
            !_prototype.TryIndex(repairData.EntityPalette[spec.ProtoIndex], out EntityPrototype? prototype) ||
            !prototype.TryGetComponent<ShipRepairableComponent>(out repairable, Factory))
        {
            return false;
        }

        return !prototype.TryGetComponent<ShipRepairableRestrictComponent>(out var restrict, Factory) ||
               !_whitelist.IsWhitelistFail(restrict.ToolWhitelist, laserUid);
    }

    private bool EntityNeedsRepair(ShipRepairEntitySpecifier spec)
    {
        var original = spec.OriginalEntity == null ? (EntityUid?) null : GetEntity(spec.OriginalEntity.Value);
        if (original == null || TerminatingOrDeleted(original.Value))
            return true;

        var ev = new ShipRepairReinstateQueryEvent(true);
        RaiseLocalEvent(original.Value, ref ev);
        if (ev.Handled)
            return ev.Repairable;

        return Transform(original.Value).GridUid == null;
    }

    private bool TileNeedsRepair(EntityUid grid, MapGridComponent gridComp, Vector2i indices, int storedTile)
    {
        return !_map.TryGetTileRef(grid, gridComp, indices, out var tileRef) ||
               tileRef.Tile.TypeId != storedTile;
    }

    private bool CanRepairTile(EntityUid grid, MapGridComponent gridComp, Vector2i indices, int storedTile)
    {
        if (storedTile == Tile.Empty.TypeId ||
            !TileNeedsRepair(grid, gridComp, indices, storedTile))
        {
            return false;
        }

        if (_map.TryGetTileRef(grid, gridComp, indices, out var current) && !current.Tile.IsEmpty)
            return true;

        return HasCardinalTileNeighbor(grid, gridComp, indices);
    }

    private bool HasCardinalTileNeighbor(EntityUid grid, MapGridComponent gridComp, Vector2i indices)
    {
        foreach (var offset in CardinalOffsets)
        {
            if (_map.TryGetTileRef(grid, gridComp, indices + offset, out var neighbor) &&
                !neighbor.Tile.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateRadarEffects(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ShipRepairLaserRadarEffectComponent, RadarBlipComponent>();
        while (query.MoveNext(out var uid, out var effect, out var blip))
        {
            if (now >= effect.EndTime)
            {
                QueueDel(uid);
                continue;
            }

            var phase = (MathF.Sin((float) now.TotalSeconds * MathF.Tau * 0.75f) + 1f) * 0.5f;
            var scale = MathHelper.Lerp(effect.MinScale, effect.MaxScale, phase);

            blip.Config.Bounds = new Box2(-scale, -scale, scale, scale);
            blip.Config.Color = Color.InterpolateBetween(effect.ColorA, effect.ColorB, phase);
            blip.Config.Shape = phase > 0.5f ? RadarBlipShape.Ring : RadarBlipShape.Diamond;
        }
    }

    private void StartOrRefreshRadarEffect(
        Entity<ShipRepairLaserComponent> laser,
        EntityUid grid,
        Vector2 localPosition,
        TimeSpan now)
    {
        if (laser.Comp.RadarRepairEffectDuration <= 0f)
            return;

        var duration = TimeSpan.FromSeconds(laser.Comp.RadarRepairEffectDuration);
        var effectUid = laser.Comp.ActiveRadarEffect;
        if (effectUid == null || !Exists(effectUid.Value))
        {
            effectUid = Spawn(null, new EntityCoordinates(grid, localPosition));
            laser.Comp.ActiveRadarEffect = effectUid;
        }
        else
        {
            _transform.SetCoordinates(effectUid.Value, new EntityCoordinates(grid, localPosition));
        }

        EnsureComp<PhysicsComponent>(effectUid.Value);

        var blip = EnsureComp<RadarBlipComponent>(effectUid.Value);
        blip.VisibleFromOtherGrids = true;
        blip.RequireNoGrid = false;
        blip.Enabled = true;
        blip.MaxDistance = 8192f;
        blip.Config.RespectZoom = true;
        blip.Config.Rotate = false;

        var effect = EnsureComp<ShipRepairLaserRadarEffectComponent>(effectUid.Value);
        effect.EndTime = now + duration;
        effect.MinScale = laser.Comp.RadarRepairEffectMinScale;
        effect.MaxScale = laser.Comp.RadarRepairEffectMaxScale;
        effect.ColorA = laser.Comp.RadarRepairEffectColorA;
        effect.ColorB = laser.Comp.RadarRepairEffectColorB;
    }

    private void ClearRadarEffect(ShipRepairLaserComponent laser)
    {
        if (laser.ActiveRadarEffect is { } effect && Exists(effect))
            QueueDel(effect);

        laser.ActiveRadarEffect = null;
    }

    private bool TryGetTargetGrid(EntityUid hit, [NotNullWhen(true)] out EntityUid? grid)
    {
        grid = null;

        if (TryComp<MapGridComponent>(hit, out _))
        {
            grid = hit;
            return true;
        }

        grid = Transform(hit).GridUid;
        return grid != null;
    }

    private bool IsOwnGrid(EntityUid laserUid, EntityUid targetGrid)
    {
        if (_transform.GetGrid(laserUid) == targetGrid)
            return true;

        return TryComp<ShipWeaponHomeGridComponent>(laserUid, out var homeGrid) &&
               homeGrid.HomeGrid == targetGrid;
    }

    private bool IsShieldedGrid(EntityUid targetGrid)
    {
        if (HasComp<ShipShieldedComponent>(targetGrid))
            return true;

        return TryComp<ShipShieldGridStateComponent>(targetGrid, out var gridState) && gridState.Online;
    }

    private Vector2 GetHitLocalPosition(EntityUid grid, EntityCoordinates from, Vector2 direction, float distance)
    {
        var fromMap = _transform.ToMapCoordinates(from);
        var hitWorld = fromMap.Position + direction * distance;
        var gridWorld = _transform.GetWorldPosition(grid);
        var gridRotation = _transform.GetWorldRotation(grid);
        return (-gridRotation).RotateVec(hitWorld - gridWorld);
    }

    private void PrintPendingReceiptOnGridChange(Entity<ShipRepairLaserComponent> laser, EntityUid nextGrid)
    {
        if (laser.Comp.LastTargetGrid is not { } previousGrid ||
            previousGrid == nextGrid ||
            !Exists(previousGrid))
        {
            return;
        }

        TryPrintReceipt(laser, previousGrid, null, false);
    }

    private void TryPrintReceipt(Entity<ShipRepairLaserComponent> laser, EntityUid user)
    {
        if (laser.Comp.LastTargetGrid is not { } grid || !Exists(grid))
        {
            _popup.PopupEntity(Loc.GetString("ship-repair-laser-receipt-no-grid"), laser, user, PopupType.SmallCaution);
            return;
        }

        TryPrintReceipt(laser, grid, user, true);
    }

    private bool TryPrintReceipt(
        Entity<ShipRepairLaserComponent> laser,
        EntityUid grid,
        EntityUid? user,
        bool showPopup)
    {
        if (!TryComp<ShipRepairLaserLedgerComponent>(grid, out var ledger) || ledger.MatterSpent <= 0)
        {
            if (showPopup && user != null)
                _popup.PopupEntity(Loc.GetString("ship-repair-laser-receipt-empty"), laser, user.Value, PopupType.SmallCaution);

            return false;
        }

        var fullyRepaired = IsFullyRepaired(laser, grid);
        var header = Loc.GetString(fullyRepaired
            ? "ship-repair-laser-receipt-header-full"
            : "ship-repair-laser-receipt-header-partial");
        var time = _gameTicker.RoundDuration().ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
        var matter = ledger.MatterSpent.ToString("N0", CultureInfo.InvariantCulture);
        var content = Loc.GetString("ship-repair-laser-receipt-content",
            ("header", header),
            ("ship", Name(grid)),
            ("time", time),
            ("matter", matter));

        var paperUid = Spawn(laser.Comp.ReceiptPrototype, Transform(laser).Coordinates);
        if (TryComp<PaperComponent>(paperUid, out var paper))
        {
            _paper.SetContent((paperUid, paper), content);
            paper.EditingDisabled = true;
            Dirty(paperUid, paper);
        }

        _audio.PlayPvs(laser.Comp.ReceiptPrintSound, laser.Owner);
        ResetReceiptLedger(grid, ledger);

        if (showPopup && user != null)
            _popup.PopupEntity(Loc.GetString("ship-repair-laser-receipt-printed"), laser, user.Value);

        return true;
    }

    private void ResetReceiptLedger(EntityUid grid, ShipRepairLaserLedgerComponent ledger)
    {
        ledger.MatterSpent = 0;
        ledger.LastRepairTime = null;
        Dirty(grid, ledger);
    }

    private bool IsFullyRepaired(Entity<ShipRepairLaserComponent> laser, EntityUid grid)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp) ||
            !TryComp<ShipRepairDataComponent>(grid, out var repairData))
        {
            return false;
        }

        return !TryFindNextRepair(laser.Owner, grid, gridComp, repairData, Vector2.Zero, laser.Comp, out _);
    }

    private void StopRepair(Entity<ShipRepairLaserComponent> laser)
    {
        ClearRadarEffect(laser.Comp);
        laser.Comp.ActiveGrid = null;
        laser.Comp.CurrentWork = null;
        laser.Comp.ActiveUntil = TimeSpan.Zero;
    }

    private Vector2i GetRepairChunkIndices(Vector2i gridIndices, int chunkSize)
    {
        var xCoord = gridIndices.X < 0 ? 1 - chunkSize + gridIndices.X : gridIndices.X;
        var yCoord = gridIndices.Y < 0 ? 1 - chunkSize + gridIndices.Y : gridIndices.Y;
        return new Vector2i(xCoord / chunkSize, yCoord / chunkSize);
    }

    private Vector2i GetRelativeIndices(Vector2i gridIndices, int chunkSize)
    {
        var x = MathHelper.Mod(gridIndices.X, chunkSize);
        var y = MathHelper.Mod(gridIndices.Y, chunkSize);
        return new Vector2i(x, y);
    }

    private bool TryGetChunk(ShipRepairDataComponent data, Vector2i gridIndices, [NotNullWhen(true)] out ShipRepairChunk? chunk)
    {
        var chunkIndices = GetRepairChunkIndices(gridIndices, data.ChunkSize);
        return data.Chunks.TryGetValue(chunkIndices, out chunk);
    }
}

using Content.Shared._Forge.HME;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Forge.HME;

public sealed class HmeTriageVisorSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly List<HmeTriageVisorComponent> _activeVisors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HmeTriageVisorComponent, GotEquippedEvent>(OnVisorEquipped);
        SubscribeLocalEvent<HmeTriageVisorComponent, GotUnequippedEvent>(OnVisorUnequipped);
        SubscribeLocalEvent<HmeTriageVisorComponent, ComponentStartup>(OnVisorStartup);
        SubscribeLocalEvent<HmeTriageVisorComponent, ComponentRemove>(OnVisorRemove);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<MobStateComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnVisorEquipped(Entity<HmeTriageVisorComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.EYES) != 0)
            RefreshActiveVisors();
    }

    private void OnVisorUnequipped(Entity<HmeTriageVisorComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.EYES) != 0)
            RefreshActiveVisors();
    }

    private void OnVisorStartup(Entity<HmeTriageVisorComponent> ent, ref ComponentStartup args)
    {
        RefreshActiveVisors();
    }

    private void OnVisorRemove(Entity<HmeTriageVisorComponent> ent, ref ComponentRemove args)
    {
        RefreshActiveVisors();
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        RefreshActiveVisors();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _activeVisors.Clear();
    }

    private void OnGetStatusIcons(Entity<MobStateComponent> ent, ref GetStatusIconsEvent args)
    {
        RefreshActiveVisors();

        if (_activeVisors.Count == 0)
            return;

        if (!TryComp<DamageableComponent>(ent.Owner, out var damageable))
            return;

        foreach (var visor in _activeVisors)
        {
            if (!CanShowForContainer(damageable, visor) ||
                !TryGetDominantDamageIcon(damageable, visor, out var icon))
            {
                continue;
            }

            args.StatusIcons.Add(icon);
            return;
        }
    }

    private void RefreshActiveVisors()
    {
        _activeVisors.Clear();

        if (_player.LocalSession?.AttachedEntity is not { } viewer || !Exists(viewer))
            return;

        var slots = _inventory.GetSlotEnumerator(viewer, SlotFlags.EYES);
        while (slots.NextItem(out var item))
        {
            if (TryComp<HmeTriageVisorComponent>(item, out var visor))
                _activeVisors.Add(visor);
        }
    }

    private static bool CanShowForContainer(DamageableComponent damageable, HmeTriageVisorComponent visor)
    {
        return damageable.DamageContainerID is { } container &&
               visor.DamageContainers.Contains(container);
    }

    private bool TryGetDominantDamageIcon(
        DamageableComponent damageable,
        HmeTriageVisorComponent visor,
        out HealthIconPrototype icon)
    {
        var highest = FixedPoint2.Zero;
        ProtoId<HealthIconPrototype>? highestIcon = null;

        foreach (var (group, damage) in damageable.Damage.GetDamagePerGroup(_prototype))
        {
            if (damage <= highest || !visor.DamageGroupIcons.TryGetValue(group, out var iconId))
                continue;

            highest = damage;
            highestIcon = iconId;
        }

        if (highestIcon != null && _prototype.TryIndex(highestIcon.Value, out var indexedIcon))
        {
            icon = indexedIcon;
            return true;
        }

        icon = default!;
        return false;
    }
}

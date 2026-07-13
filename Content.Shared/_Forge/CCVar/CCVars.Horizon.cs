using Robust.Shared.Configuration;

namespace Content.Shared._Forge.CCVar;

public sealed partial class ForgeCVars
{
    public static readonly CVarDef<bool> HorizonEnabled =
        CVarDef.Create("forge.horizon.enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonRtrCount =
        CVarDef.Create("forge.horizon.rtr_count", 7, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonRtrMinDistance =
        CVarDef.Create("forge.horizon.rtr_min_distance", 20000f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonRtrMaxDistance =
        CVarDef.Create("forge.horizon.rtr_max_distance", 40000f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonProximityDistance =
        CVarDef.Create("forge.horizon.proximity_distance", 1000f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonProximityCheckInterval =
        CVarDef.Create("forge.horizon.proximity_check_interval", 5f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonAutoActivationSeconds =
        CVarDef.Create("forge.horizon.auto_activation_seconds", 5400f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonWakeDelaySeconds =
        CVarDef.Create("forge.horizon.wake_delay_seconds", 30f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonStrategicInterval =
        CVarDef.Create("forge.horizon.strategic_interval", 180f, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonResourceCap =
        CVarDef.Create("forge.horizon.resource_cap", 100000, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonWorkItemsPerTick =
        CVarDef.Create("forge.horizon.work_items_per_tick", 4, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonMaxWorkQueue =
        CVarDef.Create("forge.horizon.max_work_queue", 64, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonMaxOrders =
        CVarDef.Create("forge.horizon.max_orders", 24, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonMaxIncidents =
        CVarDef.Create("forge.horizon.max_incidents", 32, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonSpatialCandidateCount =
        CVarDef.Create("forge.horizon.spatial_candidate_count", 16, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonSpatialObjectLimit =
        CVarDef.Create("forge.horizon.spatial_object_limit", 64, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonBubbleRadius =
        CVarDef.Create("forge.horizon.bubble_radius", 15000f, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonMaxBranchDepth =
        CVarDef.Create("forge.horizon.max_branch_depth", 3, CVar.SERVERONLY);

    public static readonly CVarDef<int> HorizonMaxDefenseUnits =
        CVarDef.Create("forge.horizon.max_defense_units", 2, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonDefenseChaseRadius =
        CVarDef.Create("forge.horizon.defense_chase_radius", 2500f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonUiRefreshInterval =
        CVarDef.Create("forge.horizon.ui_refresh_interval", 15f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonAnnouncementCooldown =
        CVarDef.Create("forge.horizon.announcement_cooldown", 120f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonAmsMoveTimeout =
        CVarDef.Create("forge.horizon.ams_move_timeout", 600f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonDeploySeconds =
        CVarDef.Create("forge.horizon.deploy_seconds", 300f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonRespawnDelay =
        CVarDef.Create("forge.horizon.respawn_delay", 60f, CVar.SERVERONLY);

    public static readonly CVarDef<float> HorizonOrderCheckInterval =
        CVarDef.Create("forge.horizon.order_check_interval", 1f, CVar.SERVERONLY);
}

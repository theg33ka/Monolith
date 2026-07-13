using Content.Shared._Forge.Horizon;

namespace Content.Server._Forge.Horizon.Domain;

public static class HorizonWanderingAiPolicy
{
    public static bool CanHandoff(
        HorizonDeploymentPhase phase,
        bool actorIsDesignatedAi,
        bool aiAvailable,
        bool carrierAvailable,
        bool aiHasMind,
        bool carrierHasMind,
        bool remoteAvailable,
        bool aiHasCore)
    {
        return phase == HorizonDeploymentPhase.Operational &&
               actorIsDesignatedAi &&
               aiAvailable &&
               carrierAvailable &&
               aiHasMind &&
               !carrierHasMind &&
               remoteAvailable &&
               aiHasCore;
    }
}

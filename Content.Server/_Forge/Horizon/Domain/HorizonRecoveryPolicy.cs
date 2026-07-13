namespace Content.Server._Forge.Horizon.Domain;

public enum HorizonRecoveryAction : byte
{
    RetryAms,
    RelocateCluster,
    TerminateCycle,
}

public static class HorizonRecoveryPolicy
{
    public static HorizonRecoveryAction Select(int attemptsUsed, bool emergencyClusterUsed)
    {
        if (attemptsUsed < 3)
            return HorizonRecoveryAction.RetryAms;

        return emergencyClusterUsed
            ? HorizonRecoveryAction.TerminateCycle
            : HorizonRecoveryAction.RelocateCluster;
    }
}

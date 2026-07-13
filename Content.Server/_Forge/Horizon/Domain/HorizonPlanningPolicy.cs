using System.Linq;
using Content.Shared._Forge.Horizon;

namespace Content.Server._Forge.Horizon.Domain;

public readonly record struct HorizonProjectCandidate(
    string ProjectId,
    HorizonObjectKind Kind,
    int Priority,
    int DesiredCount,
    int MaxCount,
    int RawCost,
    int ComponentCost,
    int EnergyCost);

public static class HorizonPlanningPolicy
{
    public static HorizonProjectCandidate? SelectNext(
        IEnumerable<HorizonProjectCandidate> candidates,
        IReadOnlyDictionary<HorizonObjectKind, int> counts,
        HorizonLedger ledger)
    {
        return candidates
            .Where(candidate => IsStrategicBuildKind(candidate.Kind))
            .Where(candidate => counts.GetValueOrDefault(candidate.Kind) < Math.Max(0, candidate.DesiredCount))
            .Where(candidate => counts.GetValueOrDefault(candidate.Kind) < Math.Max(0, candidate.MaxCount))
            .Where(candidate => ledger.Raw >= candidate.RawCost &&
                                ledger.Components >= candidate.ComponentCost &&
                                ledger.Energy >= candidate.EnergyCost)
            .OrderBy(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.ProjectId, StringComparer.OrdinalIgnoreCase)
            .Cast<HorizonProjectCandidate?>()
            .FirstOrDefault();
    }

    public static bool IsStrategicBuildKind(HorizonObjectKind kind)
    {
        return kind is HorizonObjectKind.Energy or
            HorizonObjectKind.Relay or
            HorizonObjectKind.Mining or
            HorizonObjectKind.Production or
            HorizonObjectKind.Defense or
            HorizonObjectKind.Amz or
            HorizonObjectKind.Technical or
            HorizonObjectKind.Salvage or
            HorizonObjectKind.Carrier;
    }
}

using System.Diagnostics;

namespace Content.Server._Forge.KIAS;

public enum KiasPhase
{
    CrewUpdate, CrewCoverage, CrewScan, CrewPeople, CrewFauna, CrewRegistration,
    RuntimeUpdate, RuntimeBoot, RuntimeTimers, RuntimeEvents, RuntimeWork, RuntimeCommands,
    IntegrationReconcile, IntegrationControl, RoomGeometry, IoEmit,
}

public sealed class KiasPhaseMetrics
{
    public long Calls, TimestampTicks, AllocatedBytes, MaxTimestampTicks;
    public readonly long[] DurationBuckets = new long[32];
    public long Items, MaxQueue;
}

public readonly struct KiasPhaseMeasurement : IDisposable
{
    private readonly KiasPhaseMetrics? _metric;
    private readonly long _started, _bytes;

    public KiasPhaseMeasurement(KiasSystem system, KiasPhase phase)
    {
        _metric = system.MeasureUpdates ? system.Phases[(int) phase] : null;
        _started = _metric != null ? Stopwatch.GetTimestamp() : 0;
        _bytes = _metric != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
    }

    public void Dispose()
    {
        if (_metric == null) return;
        var elapsed = Stopwatch.GetTimestamp() - _started;
        _metric.Calls++;
        _metric.TimestampTicks += elapsed;
        _metric.AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - _bytes;
        _metric.MaxTimestampTicks = Math.Max(_metric.MaxTimestampTicks, elapsed);
        var bucket = 0;
        for (var value = elapsed; value > 1 && bucket < 31; value >>= 1) bucket++;
        _metric.DurationBuckets[bucket]++;
    }
}

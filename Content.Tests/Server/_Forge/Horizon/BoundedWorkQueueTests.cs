using System.Collections.Generic;
using Content.Server._Forge.Horizon.Domain;
using NUnit.Framework;

namespace Content.Tests.Server._Forge.Horizon;

[TestFixture]
public sealed class BoundedWorkQueueTests
{
    [Test]
    public void QueueRejectsBeyondCapacityAndDrainsBoundedBatch()
    {
        var queue = new BoundedWorkQueue<int>(2);
        var handled = new List<int>();

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryEnqueue(1), Is.True);
            Assert.That(queue.TryEnqueue(2), Is.True);
            Assert.That(queue.TryEnqueue(3), Is.False);
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.Rejected, Is.EqualTo(1));
        });

        var drained = queue.Drain(1, handled.Add);

        Assert.Multiple(() =>
        {
            Assert.That(drained, Is.EqualTo(1));
            Assert.That(handled, Is.EqualTo(new[] { 1 }));
            Assert.That(queue.Count, Is.EqualTo(1));
        });
    }
}

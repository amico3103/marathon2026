using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Tests;

public sealed class CleanupScopeTests
{
    [Test]
    [Category("fixture-cleanup")]
    public async Task AFailedCleanupDoesNotPreventOtherResourcesBeingReleased()
    {
        var events = new List<string>();
        var scope = new CleanupScope();
        scope.TrackAction(() => { events.Add("first"); return Task.CompletedTask; });
        scope.TrackAction(() => { events.Add("failed"); return Task.FromException(new InvalidOperationException("Controlled cleanup failure")); });
        scope.TrackAction(() => { events.Add("last"); return Task.CompletedTask; });
        var reported = false;
        try { await scope.DisposeAsync(); }
        catch (AggregateException error) { reported = error.InnerExceptions.Count == 1 && error.InnerExceptions[0] is InvalidOperationException; }
        await Assert.That(reported).IsTrue();
        await Assert.That(events.SequenceEqual(["last", "failed", "first"])).IsTrue();
        await scope.DisposeAsync();
        await Assert.That(events.Count).IsEqualTo(3);
    }
}

namespace SalesArena.Communications.Outbound.Tests;

internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _utcNow = start;

    public void Advance(TimeSpan delta) => _utcNow += delta;

    public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

using System.Net;
using System.Text;
using FluentAssertions;
using SalesArena.Orchestrator.BellStream;
using Xunit;

namespace SalesArena.Orchestrator.Tests;

/// <summary>
/// Pins the Bell Stream primitives: bus pub/sub, sliding-window rate limit,
/// Slack + Discord formatting, coordinator fan-out, post sanitization.
/// </summary>
public sealed class BellStreamTests
{
    private static readonly BellEvent SampleBell = new(
        Kind: BellKind.DealClosed,
        ContestId: "Tuesday",
        Persona: "Roma",
        LeadId: "L-0001",
        ValueUsd: 48_000m,
        Headline: "closed Yatzee Pharmaceutical",
        OccurredAtUtc: new DateTimeOffset(2026, 5, 18, 14, 30, 0, TimeSpan.Zero));

    // ---- bus -------------------------------------------------------------

    [Fact]
    public async Task InMemoryBellEventBus_fires_BellRang_for_every_publish()
    {
        var bus = new InMemoryBellEventBus();
        var received = new List<BellEvent>();
        bus.BellRang += (_, e) => received.Add(e);

        await bus.PublishAsync(SampleBell);
        await bus.PublishAsync(SampleBell with { LeadId = "L-0002" });

        received.Should().HaveCount(2);
        received[0].LeadId.Should().Be("L-0001");
        received[1].LeadId.Should().Be("L-0002");
    }

    // ---- rate limiter ----------------------------------------------------

    [Fact]
    public void RateLimiter_allows_up_to_cap_per_window()
    {
        var clock = new FakeTime(new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero));
        var limiter = new BellRateLimiter(maxPerWindow: 3, time: clock);

        limiter.TryAcquire().Should().BeTrue();   // 1
        limiter.TryAcquire().Should().BeTrue();   // 2
        limiter.TryAcquire().Should().BeTrue();   // 3
        limiter.TryAcquire().Should().BeFalse();  // 4 — blocked

        limiter.CurrentCount.Should().Be(3);
    }

    [Fact]
    public void RateLimiter_expires_oldest_entries_when_window_passes()
    {
        var clock = new FakeTime(new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero));
        var limiter = new BellRateLimiter(maxPerWindow: 2, window: TimeSpan.FromMinutes(1), time: clock);

        limiter.TryAcquire().Should().BeTrue();
        limiter.TryAcquire().Should().BeTrue();
        limiter.TryAcquire().Should().BeFalse(); // at cap

        clock.Advance(TimeSpan.FromMinutes(2)); // window expired

        limiter.TryAcquire().Should().BeTrue();   // slots cleared
        limiter.CurrentCount.Should().Be(1);
    }

    [Fact]
    public void RateLimiter_with_zero_cap_disables_limiting()
    {
        var limiter = new BellRateLimiter(maxPerWindow: 0);
        limiter.IsDisabled.Should().BeTrue();
        for (var i = 0; i < 100; i++) limiter.TryAcquire().Should().BeTrue();
    }

    // ---- formatting ------------------------------------------------------

    [Fact]
    public void Slack_format_uses_mrkdwn_and_includes_revenue()
    {
        var msg = SlackWebhookPoster.FormatHeadline(SampleBell);
        msg.Should().Contain(":bell:");
        msg.Should().Contain("*Roma*");
        msg.Should().Contain("closed Yatzee Pharmaceutical");
        msg.Should().Contain("*$48,000*");
    }

    [Fact]
    public void Slack_format_picks_persona_glyph_per_BellKind()
    {
        SlackWebhookPoster.FormatHeadline(SampleBell with { Kind = BellKind.CadillacPromotion }).Should().Contain(":car:");
        SlackWebhookPoster.FormatHeadline(SampleBell with { Kind = BellKind.GlengarryDrip }).Should().Contain(":gem:");
        SlackWebhookPoster.FormatHeadline(SampleBell with { Kind = BellKind.Ceremonial }).Should().Contain(":mega:");
    }

    [Fact]
    public void Discord_format_uses_emoji_and_double_asterisk_bold()
    {
        var msg = DiscordWebhookPoster.FormatHeadline(SampleBell);
        msg.Should().Contain("🔔");
        msg.Should().Contain("**Roma**");
        msg.Should().Contain("**$48,000**");
    }

    [Fact]
    public void Format_omits_revenue_when_value_is_null()
    {
        var noValueBell = SampleBell with { ValueUsd = null, Kind = BellKind.Ceremonial };
        SlackWebhookPoster.FormatHeadline(noValueBell).Should().NotContain("$");
        DiscordWebhookPoster.FormatHeadline(noValueBell).Should().NotContain("$");
    }

    // ---- webhook posters -------------------------------------------------

    [Fact]
    public async Task SlackWebhookPoster_returns_false_when_no_URL_configured()
    {
        var poster = new SlackWebhookPoster(new HttpClient(new StubHttpHandler()), webhookUrl: null);
        poster.IsConfigured.Should().BeFalse();
        (await poster.PostAsync(SampleBell)).Should().BeFalse();
    }

    [Fact]
    public async Task SlackWebhookPoster_posts_json_and_returns_true_on_2xx()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK);
        var poster = new SlackWebhookPoster(new HttpClient(handler), webhookUrl: "https://hooks.example/x");

        var result = await poster.PostAsync(SampleBell);

        result.Should().BeTrue();
        handler.LastRequestUri.Should().Be("https://hooks.example/x");
        handler.LastRequestBody.Should().Contain(":bell:");
        handler.LastRequestBody.Should().Contain("Roma");
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task SlackWebhookPoster_returns_false_when_server_replies_500()
    {
        var handler = new StubHttpHandler(HttpStatusCode.InternalServerError);
        var poster = new SlackWebhookPoster(new HttpClient(handler), webhookUrl: "https://hooks.example/x");
        (await poster.PostAsync(SampleBell)).Should().BeFalse();
    }

    [Fact]
    public async Task DiscordWebhookPoster_includes_username_override()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK);
        var poster = new DiscordWebhookPoster(new HttpClient(handler), "https://discord.example/x", "MitchAndMurray");

        await poster.PostAsync(SampleBell);

        handler.LastRequestBody.Should().Contain("MitchAndMurray");
        // System.Text.Json's web defaults escape non-ASCII to \uXXXX form.
        // The bell emoji 🔔 (U+1F514) serializes as the surrogate pair 🔔.
        handler.LastRequestBody.Should().Contain("\\uD83D\\uDD14");
        handler.LastRequestBody.Should().Contain("**Roma**");
    }

    [Fact]
    public async Task WebhookPoster_swallows_transport_exceptions_and_returns_false()
    {
        var handler = new StubHttpHandler(throwOnSend: new HttpRequestException("network is down"));
        var poster = new SlackWebhookPoster(new HttpClient(handler), webhookUrl: "https://hooks.example/x");
        (await poster.PostAsync(SampleBell)).Should().BeFalse();
    }

    // ---- coordinator -----------------------------------------------------

    [Fact]
    public async Task Coordinator_fans_a_bell_to_every_configured_poster()
    {
        var bus = new InMemoryBellEventBus();
        var slackHandler = new StubHttpHandler();
        var discordHandler = new StubHttpHandler();
        var slack = new SlackWebhookPoster(new HttpClient(slackHandler), "https://slack.example/x");
        var discord = new DiscordWebhookPoster(new HttpClient(discordHandler), "https://discord.example/x");

        await using var coord = new BellStreamCoordinator(bus, new IBellWebhookPoster[] { slack, discord }, new BellRateLimiter(maxPerWindow: 10));

        var dispatched = await coord.DispatchAsync(SampleBell);

        dispatched.Should().Be(2);
        slackHandler.RequestCount.Should().Be(1);
        discordHandler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Coordinator_skips_unconfigured_posters()
    {
        var bus = new InMemoryBellEventBus();
        var handler = new StubHttpHandler();
        var slack = new SlackWebhookPoster(new HttpClient(handler), "https://slack.example/x");
        var unconfigured = new DiscordWebhookPoster(new HttpClient(new StubHttpHandler()), webhookUrl: null);

        await using var coord = new BellStreamCoordinator(bus, new IBellWebhookPoster[] { slack, unconfigured }, new BellRateLimiter(maxPerWindow: 10));

        var dispatched = await coord.DispatchAsync(SampleBell);
        dispatched.Should().Be(1);
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Coordinator_subscribed_to_bus_dispatches_on_publish()
    {
        var bus = new InMemoryBellEventBus();
        var handler = new StubHttpHandler();
        var slack = new SlackWebhookPoster(new HttpClient(handler), "https://slack.example/x");
        await using var coord = new BellStreamCoordinator(bus, new[] { slack }, new BellRateLimiter(maxPerWindow: 10));

        await bus.PublishAsync(SampleBell);

        // Give the fire-and-forget async-void handler a moment to complete.
        await Task.Delay(50);

        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Coordinator_respects_rate_limit_and_drops_excess_bells()
    {
        var clock = new FakeTime(new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero));
        var bus = new InMemoryBellEventBus();
        var handler = new StubHttpHandler();
        var slack = new SlackWebhookPoster(new HttpClient(handler), "https://slack.example/x");
        var limiter = new BellRateLimiter(maxPerWindow: 2, window: TimeSpan.FromMinutes(1), time: clock);
        await using var coord = new BellStreamCoordinator(bus, new[] { slack }, limiter);

        await bus.PublishAsync(SampleBell);
        await bus.PublishAsync(SampleBell with { LeadId = "L-2" });
        await bus.PublishAsync(SampleBell with { LeadId = "L-3" }); // over cap

        await Task.Delay(100);

        handler.RequestCount.Should().Be(2);
        coord.RateLimitedDropCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_unsubscribes_from_bus()
    {
        var bus = new InMemoryBellEventBus();
        var handler = new StubHttpHandler();
        var slack = new SlackWebhookPoster(new HttpClient(handler), "https://slack.example/x");
        var coord = new BellStreamCoordinator(bus, new[] { slack }, new BellRateLimiter(maxPerWindow: 10));

        await coord.DisposeAsync();

        await bus.PublishAsync(SampleBell);
        await Task.Delay(50);

        // After dispose, no more dispatches even on a bell publish.
        handler.RequestCount.Should().Be(0);
    }

    // ---- test doubles ---------------------------------------------------

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly Exception? _throwOnSend;

        public StubHttpHandler(HttpStatusCode status = HttpStatusCode.OK, Exception? throwOnSend = null)
        {
            _status = status;
            _throwOnSend = throwOnSend;
        }

        public int RequestCount { get; private set; }
        public string? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri?.ToString();
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            if (_throwOnSend is not null) throw _throwOnSend;
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTime(DateTimeOffset start) => _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}

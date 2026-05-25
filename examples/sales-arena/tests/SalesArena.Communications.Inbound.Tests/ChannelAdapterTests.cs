using FluentAssertions;
using SalesArena.Communications.Inbound;
using Xunit;

namespace SalesArena.Communications.Inbound.Tests;

public sealed class ChannelAdapterTests
{
    [Theory]
    [InlineData(typeof(LocalEmailInboundAdapter), "email", "email-inbox.json")]
    [InlineData(typeof(LocalSmsInboundAdapter), "sms", "sms-inbox.json")]
    [InlineData(typeof(LocalWebFormInboundAdapter), "webform", "webform-inbox.json")]
    [InlineData(typeof(LocalChatInboundAdapter), "chat", "chat-inbox.json")]
    public async Task Local_adapters_load_fixture_messages(Type adapterType, string channel, string file)
    {
        var path = Path.Combine(InboundTestHost.FixtureRoot, file);
        var adapter = (IInboundChannelAdapter)Activator.CreateInstance(adapterType, path)!;
        adapter.Channel.Should().Be(channel);

        var messages = await adapter.FetchPendingAsync();
        messages.Should().NotBeEmpty();
        messages.Should().OnlyContain(m => m.Channel == channel);
    }
}

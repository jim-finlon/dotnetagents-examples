// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;
using System.Text.Json;
using SalesArena.Cli.Commands;
using DotNetAgents.Core.PublicExamples;
using Xunit;

namespace SalesArena.Cli.Tests;

public sealed class SmokeRunnerTests
{
    [Fact]
    public async Task RunAsync_runs_teaser_simulation_and_outputs_passed_envelope()
    {
        // Intercept stdout to capture the JSON envelope
        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            var exitCode = await SmokeRunner.RunAsync();

            exitCode.Should().Be(0);

            var outputJson = sw.ToString().Trim();
            outputJson.Should().NotBeEmpty();

            // Deserialize and validate the envelope
            var envelope = PublicExampleResultEnvelopeJson.Deserialize(outputJson);
            envelope.Should().NotBeNull();
            envelope.ExampleId.Should().Be("sales-arena");
            envelope.SchemaVersion.Should().Be(PublicExampleResultEnvelopeContract.SchemaVersion);
            envelope.LocalValidation.IsPassed.Should().BeTrue();
            envelope.LocalValidation.Checks.Should().HaveCount(5);
            envelope.SelfReportedMetrics.Should().ContainKey("totalRevenueUsd").WhoseValue.Should().Be(73000m);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

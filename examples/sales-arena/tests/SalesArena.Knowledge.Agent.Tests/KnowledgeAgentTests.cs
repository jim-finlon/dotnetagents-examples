using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using SalesArena.Knowledge.Agent;
using Xunit;

namespace SalesArena.Knowledge.Agent.Tests;

public class KnowledgeAgentTests
{
    private static InMemoryKnowledgeSource Source(params (string id, string heading, string text)[] chunks)
        => new(chunks.Select(c => new KnowledgeChunk(c.id, c.id.Split('#')[0], c.heading, c.text)).ToArray());

    [Fact]
    public async System.Threading.Tasks.Task HappyPath_ReturnsTopRanked()
    {
        var agent = new KnowledgeAgent(Source(
            ("a.md#chunk-0", "Pricing", "We offer Pro tier monthly billing with calibration windows."),
            ("b.md#chunk-0", "Personas", "Levene closes through repeated outreach and re-engagement."),
            ("c.md#chunk-0", "Objections", "Price too high — respond with calibration value framing.")));

        var answer = await agent.AnswerAsync(new KnowledgeQuery("calibration", TopN: 5));

        answer.Hits.Should().HaveCount(2);
        answer.CitedSources.Should().Contain("a.md");
        answer.CitedSources.Should().Contain("c.md");
        answer.Hits[0].Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async System.Threading.Tasks.Task Ranking_HigherFrequency_Wins()
    {
        var agent = new KnowledgeAgent(Source(
            ("a.md#chunk-0", "Pricing", "calibration calibration calibration value framing."),
            ("b.md#chunk-0", "Personas", "calibration only mentioned once here.")));

        var answer = await agent.AnswerAsync(new KnowledgeQuery("calibration"));

        answer.Hits[0].ChunkId.Should().Be("a.md#chunk-0");
        answer.Hits[1].ChunkId.Should().Be("b.md#chunk-0");
        answer.Hits[0].Score.Should().BeGreaterThan(answer.Hits[1].Score);
    }

    [Fact]
    public async System.Threading.Tasks.Task StopWords_AreFiltered()
    {
        var agent = new KnowledgeAgent(Source(
            ("a.md#chunk-0", "x", "the and or but is are")));

        var answer = await agent.AnswerAsync(new KnowledgeQuery("the and or"));

        answer.Hits.Should().BeEmpty();
        answer.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("stop-words");
    }

    [Fact]
    public async System.Threading.Tasks.Task NoMatch_EmitsDiagnostic()
    {
        var agent = new KnowledgeAgent(Source(("a.md#chunk-0", "x", "calibration")));
        var answer = await agent.AnswerAsync(new KnowledgeQuery("nonexistent-term"));
        answer.Hits.Should().BeEmpty();
        answer.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task BlankQuery_Throws()
    {
        var agent = new KnowledgeAgent(Source(("a.md#chunk-0", "x", "y")));
        Func<System.Threading.Tasks.Task> act = () => agent.AnswerAsync(new KnowledgeQuery("   "));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async System.Threading.Tasks.Task TopN_LimitsResults()
    {
        var chunks = Enumerable.Range(0, 5)
            .Select(i => ($"c{i}.md#chunk-0", "x", "calibration"))
            .ToArray();
        var agent = new KnowledgeAgent(Source(chunks));

        var answer = await agent.AnswerAsync(new KnowledgeQuery("calibration", TopN: 2));

        answer.Hits.Should().HaveCount(2);
    }

    [Fact]
    public async System.Threading.Tasks.Task MultiKeyword_SumsScoresAcrossTerms()
    {
        var agent = new KnowledgeAgent(Source(
            ("a.md#chunk-0", "x", "calibration alone in this chunk."),
            ("b.md#chunk-0", "x", "calibration plus framing both here.")));

        var answer = await agent.AnswerAsync(new KnowledgeQuery("calibration framing"));

        answer.Hits[0].ChunkId.Should().Be("b.md#chunk-0");
    }

    [Fact]
    public void FileSystemSource_MissingDir_Throws()
    {
        Action act = () => _ = new FileSystemKnowledgeSource("/definitely/not/here/xyz");
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public async System.Threading.Tasks.Task FileSystemSource_RoundtripsMarkdown()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "sa-knowledge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllText(Path.Combine(tmp, "pricing.md"),
                "# Pricing\nWe sell calibration tooling.\n## Tiers\nPro and Enterprise.\n");

            var src = new FileSystemKnowledgeSource(tmp);
            var agent = new KnowledgeAgent(src);

            var answer = await agent.AnswerAsync(new KnowledgeQuery("calibration"));

            answer.Hits.Should().NotBeEmpty();
            answer.CitedSources.Should().Contain("pricing.md");
            answer.Hits[0].ChunkId.Should().StartWith("pricing.md#chunk-");
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task StableTieBreak_ByChunkIdOrdinal()
    {
        var agent = new KnowledgeAgent(Source(
            ("b.md#chunk-0", "x", "calibration"),
            ("a.md#chunk-0", "x", "calibration")));

        var answer = await agent.AnswerAsync(new KnowledgeQuery("calibration"));

        answer.Hits[0].ChunkId.Should().Be("a.md#chunk-0");
        answer.Hits[1].ChunkId.Should().Be("b.md#chunk-0");
    }
}

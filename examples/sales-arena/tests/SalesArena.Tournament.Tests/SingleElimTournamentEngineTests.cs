using FluentAssertions;
using SalesArena.Tournament;
using Xunit;

namespace SalesArena.Tournament.Tests;

public sealed class SingleElimTournamentEngineTests
{
    private static SingleElimTournamentEngine BuildEngine(
        IReadOnlyDictionary<string, double>? ratings = null,
        TournamentOptions? options = null)
    {
        var elo = new FixedEloRating(ratings ?? new Dictionary<string, double>(StringComparer.Ordinal));
        return new SingleElimTournamentEngine(elo, options);
    }

    [Fact]
    public void CreateBracket_seeds_personas_by_descending_ELO()
    {
        var engine = BuildEngine(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["roma"] = 1700,
            ["levene"] = 1600,
            ["moss"] = 1500,
            ["aaronow"] = 1400,
        });

        var bracket = engine.CreateBracket(new[] { "moss", "aaronow", "roma", "levene" }, "s-1");

        var allSlots = bracket.Rounds[0].Matches.SelectMany(m => new[] { m.A, m.B }).Where(s => !s.IsBye);
        var seedOrder = allSlots.OrderBy(s => s.Seed).Select(s => s.Persona).ToArray();
        seedOrder.Should().BeEquivalentTo(new[] { "roma", "levene", "moss", "aaronow" },
            opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Top_seed_plays_lowest_seed_in_round_1()
    {
        var engine = BuildEngine(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["s1"] = 1800,
            ["s2"] = 1700,
            ["s3"] = 1600,
            ["s4"] = 1500,
        });

        var bracket = engine.CreateBracket(new[] { "s1", "s2", "s3", "s4" }, "s-1");
        var round1 = bracket.Rounds[0].Matches;

        // Top seed vs bottom seed in one match; 2-vs-3 in the other.
        var matchWithS1 = round1.Single(m => m.A.Persona == "s1" || m.B.Persona == "s1");
        var s1Opp = matchWithS1.A.Persona == "s1" ? matchWithS1.B.Persona : matchWithS1.A.Persona;
        s1Opp.Should().Be("s4");

        var matchWithS2 = round1.Single(m => m.A.Persona == "s2" || m.B.Persona == "s2");
        var s2Opp = matchWithS2.A.Persona == "s2" ? matchWithS2.B.Persona : matchWithS2.A.Persona;
        s2Opp.Should().Be("s3");
    }

    [Fact]
    public void Power_of_two_bracket_has_no_byes()
    {
        var engine = BuildEngine();
        var bracket = engine.CreateBracket(new[] { "a", "b", "c", "d" }, "s-1");
        bracket.Rounds[0].Matches.SelectMany(m => new[] { m.A, m.B }).Where(s => s.IsBye).Should().BeEmpty();
    }

    [Fact]
    public void Three_persona_bracket_assigns_one_bye_to_top_seed()
    {
        var engine = BuildEngine(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["top"] = 1800,
            ["mid"] = 1600,
            ["low"] = 1400,
        });

        var bracket = engine.CreateBracket(new[] { "top", "mid", "low" }, "s-1");
        var round1 = bracket.Rounds[0].Matches;
        round1.Should().HaveCount(2, "bracket-of-4 with 1 bye = 2 round-1 matches");

        // Top-seed match: top vs bye → bye-walkover; top advances.
        var topMatch = round1.Single(m => m.A.Persona == "top" || m.B.Persona == "top");
        (topMatch.A.IsBye || topMatch.B.IsBye).Should().BeTrue();
        topMatch.Winner.Should().Be("top");
        topMatch.CompletedAtUtc.Should().NotBeNull();

        var contestedMatch = round1.Single(m => !m.A.IsBye && !m.B.IsBye);
        new[] { contestedMatch.A.Persona, contestedMatch.B.Persona }.Should().BeEquivalentTo(new[] { "mid", "low" });
        contestedMatch.Winner.Should().BeNull();
    }

    [Fact]
    public void Five_persona_bracket_produces_8_slot_layout_with_3_byes()
    {
        var engine = BuildEngine(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["s1"] = 1900, ["s2"] = 1800, ["s3"] = 1700, ["s4"] = 1600, ["s5"] = 1500,
        });

        var bracket = engine.CreateBracket(new[] { "s1", "s2", "s3", "s4", "s5" }, "s-1");
        var slots = bracket.Rounds[0].Matches.SelectMany(m => new[] { m.A, m.B }).ToArray();
        slots.Should().HaveCount(8);
        slots.Count(s => s.IsBye).Should().Be(3);

        // Top 3 seeds get byes (TopSeedsGetByes=true default; byes filled into
        // the trailing seed positions of an 8-slot bracket, which pair against
        // the top seeds in round 1).
        var byeMatches = bracket.Rounds[0].Matches.Where(m => m.A.IsBye || m.B.IsBye).ToArray();
        var byeAdvancers = byeMatches.Select(m => m.Winner).ToArray();
        byeAdvancers.Should().BeEquivalentTo(new[] { "s1", "s2", "s3" });
    }

    [Fact]
    public async Task RunToCompletion_top_seed_wins_every_match_crowns_top_seed()
    {
        var engine = BuildEngine(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["alpha"] = 1800, ["bravo"] = 1700, ["charlie"] = 1600, ["delta"] = 1500,
        });
        var bracket = engine.CreateBracket(new[] { "alpha", "bravo", "charlie", "delta" }, "s-1");

        // Stub: top seed always wins. Ordering set in code is by ELO desc.
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["alpha"] = 4, ["bravo"] = 3, ["charlie"] = 2, ["delta"] = 1,
        };
        var runner = new StubRoundRunner((a, b) => ranks[a] > ranks[b] ? a : b);

        var done = await engine.RunToCompletionAsync(bracket, runner);
        done.Status.Should().Be(BracketStatus.Completed);
        done.Champion.Should().Be("alpha");
        done.Rounds.Should().HaveCount(2);
        done.Rounds[^1].Matches.Should().ContainSingle();
    }

    [Fact]
    public async Task Underdog_can_run_table_when_runner_picks_them()
    {
        var engine = BuildEngine(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["alpha"] = 1800, ["bravo"] = 1700, ["charlie"] = 1600, ["delta"] = 1500,
        });
        var bracket = engine.CreateBracket(new[] { "alpha", "bravo", "charlie", "delta" }, "s-1");

        // delta wins every match.
        var runner = new StubRoundRunner((a, b) => a == "delta" ? a : (b == "delta" ? b : a));
        var done = await engine.RunToCompletionAsync(bracket, runner);

        done.Champion.Should().Be("delta");
    }

    [Fact]
    public void ApplyRoundResult_records_winner_and_completion_time()
    {
        var engine = BuildEngine();
        var bracket = engine.CreateBracket(new[] { "a", "b" }, "s-1");

        var match = bracket.Rounds[0].Matches[0];
        var result = new RoundResult(bracket.Id, 1, match.Position, "a", "b", "test");
        var updated = engine.ApplyRoundResult(bracket, result);

        var doneMatch = updated.Rounds[0].Matches[0];
        doneMatch.Winner.Should().Be("a");
        doneMatch.CompletedAtUtc.Should().NotBeNull();
        updated.Status.Should().Be(BracketStatus.Completed);
        updated.Champion.Should().Be("a");
    }

    [Fact]
    public void Partial_bracket_recovery_resumes_from_last_committed_state()
    {
        var engine = BuildEngine(new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["alpha"] = 1800, ["bravo"] = 1700, ["charlie"] = 1600, ["delta"] = 1500,
        });
        var bracket = engine.CreateBracket(new[] { "alpha", "bravo", "charlie", "delta" }, "s-1");

        // Manually drive round 1 in two steps to simulate partial commit.
        var round1 = bracket.Rounds[0].Matches.ToArray();
        bracket = engine.ApplyRoundResult(bracket, new RoundResult(bracket.Id, 1, round1[0].Position,
            round1[0].A.Persona!, round1[0].B.Persona!, null));

        bracket.Status.Should().Be(BracketStatus.InProgress);
        bracket.Rounds.Should().HaveCount(1, "round 2 isn't built until round 1 finishes");

        // Resume the second match.
        bracket = engine.ApplyRoundResult(bracket, new RoundResult(bracket.Id, 1, round1[1].Position,
            round1[1].A.Persona!, round1[1].B.Persona!, null));

        bracket.Rounds.Should().HaveCount(2, "round 2 should auto-materialize when round 1 finishes");
    }

    [Fact]
    public void ApplyRoundResult_rejects_unknown_match()
    {
        var engine = BuildEngine();
        var bracket = engine.CreateBracket(new[] { "a", "b" }, "s-1");

        Action act = () => engine.ApplyRoundResult(bracket,
            new RoundResult(bracket.Id, 1, MatchPosition: 99, "a", "b", null));
        act.Should().Throw<TournamentException>().Which.Code.Should().Be(TournamentErrorCode.UnknownMatch);
    }

    [Fact]
    public void ApplyRoundResult_rejects_unknown_round()
    {
        var engine = BuildEngine();
        var bracket = engine.CreateBracket(new[] { "a", "b" }, "s-1");

        Action act = () => engine.ApplyRoundResult(bracket,
            new RoundResult(bracket.Id, RoundNumber: 99, MatchPosition: 0, "a", "b", null));
        act.Should().Throw<TournamentException>().Which.Code.Should().Be(TournamentErrorCode.UnknownRound);
    }

    [Fact]
    public void ApplyRoundResult_rejects_winner_not_in_match()
    {
        var engine = BuildEngine();
        var bracket = engine.CreateBracket(new[] { "a", "b" }, "s-1");

        Action act = () => engine.ApplyRoundResult(bracket,
            new RoundResult(bracket.Id, 1, 0, Winner: "ghost", Loser: "a", null));
        act.Should().Throw<TournamentException>().Which.Code.Should().Be(TournamentErrorCode.WinnerNotInMatch);
    }

    [Fact]
    public void ApplyRoundResult_rejects_already_decided_match()
    {
        var engine = BuildEngine();
        var bracket = engine.CreateBracket(new[] { "a", "b" }, "s-1");
        bracket = engine.ApplyRoundResult(bracket, new RoundResult(bracket.Id, 1, 0, "a", "b", null));

        Action act = () => engine.ApplyRoundResult(bracket, new RoundResult(bracket.Id, 1, 0, "b", "a", null));
        act.Should().Throw<TournamentException>().Which.Code.Should().Be(TournamentErrorCode.BracketAlreadyComplete);
    }

    [Fact]
    public void CreateBracket_rejects_fewer_than_two_personas()
    {
        var engine = BuildEngine();
        Action act = () => engine.CreateBracket(new[] { "lonely" }, "s-1");
        act.Should().Throw<TournamentException>().Which.Code.Should().Be(TournamentErrorCode.NotEnoughPersonas);
    }

    [Fact]
    public void CreateBracket_rejects_more_than_MaxPersonas()
    {
        var engine = BuildEngine(options: new TournamentOptions { MaxPersonas = 4 });
        Action act = () => engine.CreateBracket(new[] { "a", "b", "c", "d", "e" }, "s-1");
        act.Should().Throw<TournamentException>().Which.Code.Should().Be(TournamentErrorCode.TooManyPersonas);
    }

    [Fact]
    public void CreateBracket_rejects_duplicate_entrants()
    {
        var engine = BuildEngine();
        Action act = () => engine.CreateBracket(new[] { "a", "a", "b", "c" }, "s-1");
        act.Should().Throw<TournamentException>().Which.Code.Should().Be(TournamentErrorCode.DuplicatePersona);
    }

    [Fact]
    public void Two_persona_bracket_runs_a_single_round()
    {
        var engine = BuildEngine();
        var bracket = engine.CreateBracket(new[] { "a", "b" }, "s-1");
        bracket.Rounds.Should().HaveCount(1);
        bracket.Rounds[0].Matches.Should().HaveCount(1);
    }

    [Fact]
    public void NextPowerOfTwo_matches_canonical_values()
    {
        SingleElimTournamentEngine.NextPowerOfTwo(2).Should().Be(2);
        SingleElimTournamentEngine.NextPowerOfTwo(3).Should().Be(4);
        SingleElimTournamentEngine.NextPowerOfTwo(5).Should().Be(8);
        SingleElimTournamentEngine.NextPowerOfTwo(8).Should().Be(8);
    }

    [Fact]
    public void SeedPositions_size_8_matches_classic_tennis_layout()
    {
        // Slot ordering must place 1-vs-8, 4-vs-5, 3-vs-6, 2-vs-7 in round 1
        // (the standard layout where 1 + 2 only meet in the final).
        var positions = SingleElimTournamentEngine.SeedPositions(8);
        positions.Should().HaveCount(8);

        // Verify pair structure: seeds 1+8 share matchIdx 0, 4+5 share idx 1,
        // 3+6 share idx 2, 2+7 share idx 3.
        int MatchIdxOfSeed(int seed) => positions[seed - 1] / 2;
        MatchIdxOfSeed(1).Should().Be(MatchIdxOfSeed(8));
        MatchIdxOfSeed(4).Should().Be(MatchIdxOfSeed(5));
        MatchIdxOfSeed(3).Should().Be(MatchIdxOfSeed(6));
        MatchIdxOfSeed(2).Should().Be(MatchIdxOfSeed(7));
        // Top + 2nd seed don't share a round-1 match.
        MatchIdxOfSeed(1).Should().NotBe(MatchIdxOfSeed(2));
    }
}

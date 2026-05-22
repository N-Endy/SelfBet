using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Application.Services;
using SelfBet.Domain.Entities;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.UseCases;

public sealed class RunEngine(
    IFootballDataProvider footballDataProvider,
    ISportyBetFixtureCache fixtureCache,
    IFeatureBuilder featureBuilder,
    IPredictionService predictionService,
    ISlipOptimizer slipOptimizer,
    IBankrollService bankrollService,
    IStrategyConfigRepository strategyConfigRepository,
    IRunRepository runRepository,
    ISlipRepository slipRepository,
    IPlacementRepository placementRepository,
    IAutomationGateway automationGateway,
    ISafetyGate safetyGate,
    IAuditService auditService,
    IEmailNotifier emailNotifier,
    ITeamStrengthService teamStrengthService,
    ILogger<RunEngine> logger)
{
    public async Task<RunOutcome> ExecuteAsync(string trigger, CancellationToken cancellationToken)
    {
        var run = new Run { Trigger = trigger, Status = RunStatus.Running };
        await runRepository.SaveAsync(run, cancellationToken);
        await auditService.LogAsync("run.started", $"Run triggered by {trigger}", new { run.Id }, cancellationToken);

        try
        {
            var config = await strategyConfigRepository.GetAsync(cancellationToken);
            var bankroll = await bankrollService.GetCurrentAsync(cancellationToken);
            var stakePerSlip = bankrollService.ComputeStakePerSlip(bankroll.Balance, config);

            var lookahead = Math.Clamp(config.FixtureLookaheadHours, 12, 96);
            var snapshot = await fixtureCache.GetSnapshotAsync(cancellationToken);
            var fixtures = await footballDataProvider.GetUpcomingFixturesAsync(
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(lookahead),
                cancellationToken);
            run.FixturesEvaluated = fixtures.Count;
            var placementFixtures = snapshot.PlacementFixtures;

            var filteredFixtures = fixtures
                .Where(f => config.EnabledLeagues.Count == 0 ||
                            config.EnabledLeagues.Any(l =>
                                f.League.Contains(l, StringComparison.OrdinalIgnoreCase) ||
                                l.Contains(f.League, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            logger.LogInformation("RunEngine: {Total} fixtures, {Filtered} after league filter.",
                fixtures.Count, filteredFixtures.Count);

            var allowedMarkets = config.AllowedMarkets.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var trimmedFixtures = filteredFixtures
                .Select(f => new FixtureOddsDto
                {
                    FixtureId = f.FixtureId,
                    League = f.League,
                    HomeTeam = f.HomeTeam,
                    AwayTeam = f.AwayTeam,
                    KickoffUtc = f.KickoffUtc,
                    HomeStats = f.HomeStats,
                    AwayStats = f.AwayStats,
                    Markets = f.Markets
                        .Where(m => allowedMarkets.Count == 0 || allowedMarkets.Contains(m.Market))
                        .ToList()
                })
                .Where(f => f.Markets.Count > 0)
                .ToList();

            var features = featureBuilder.Build(trimmedFixtures);
            var enriched = await EnrichWithTeamStrengthsAsync(trimmedFixtures, features, cancellationToken);
            var candidates = BuildCandidates(trimmedFixtures, enriched);
            run.CandidatesGenerated = candidates.Count;

            logger.LogInformation("RunEngine: {F} fixtures with markets, {C} candidates.",
                trimmedFixtures.Count, candidates.Count);

            var build = slipOptimizer.Build(candidates, config, run.Id, stakePerSlip);
            run.SlipsBuilt = build.Slips.Count(s => s.Status == SlipStatus.Ready);

            var dixonCount = candidates.Count(c => c.PredictionSource == "DixonColes");
            var readyLegs = build.Slips.Where(s => s.Status == SlipStatus.Ready).SelectMany(s => s.Legs).ToList();
            logger.LogInformation(
                "RunEngine: {Ready}/{Total} slips ready. Dixon-Coles candidates {Dixon}/{Cand}. " +
                "Ready legs: avg edge {Edge:P1}, leg counts [{Counts}].",
                run.SlipsBuilt, build.Slips.Count,
                dixonCount, candidates.Count,
                readyLegs.Count == 0 ? 0 : readyLegs.Average(l => l.Edge),
                string.Join(", ", build.Slips.Where(s => s.Status == SlipStatus.Ready).Select(s => s.Legs.Count)));

            await slipRepository.SaveAsync(build.Slips, cancellationToken);

            var outcome = new RunOutcome { Run = run, Slips = build.Slips };
            var gate = safetyGate.Evaluate(outcome);

            switch (gate)
            {
                case SafetyGateOutcome.Block:
                    run.Status = RunStatus.Failed;
                    run.Message = "Safety gate blocked the run.";
                    break;

                case SafetyGateOutcome.HoldForConfirmation:
                    run.Status = RunStatus.RequiresConfirmation;
                    run.Message = "Safety gate flagged this run; manual confirmation required.";
                    foreach (var slip in build.Slips.Where(s => s.Status == SlipStatus.Ready))
                    {
                        slip.Status = SlipStatus.AwaitingConfirmation;
                        await slipRepository.UpdateAsync(slip, cancellationToken);
                    }
                    break;

                case SafetyGateOutcome.Pass:
                default:
                    await PlaceReadySlipsAsync(build.Slips, config, placementFixtures, cancellationToken);
                    run.Status = build.Slips.Any(s => s.Status == SlipStatus.Failed)
                        ? RunStatus.RequiresConfirmation
                        : RunStatus.Completed;
                    run.Message = build.Notes ?? "Slips generated successfully.";
                    break;
            }

            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            await runRepository.SaveAsync(run, cancellationToken);
            await auditService.LogAsync("run.completed", run.Message ?? "Run completed.", new { run.Id, run.Status }, cancellationToken);

            // Send email notification
            await SendRunEmailAsync(build.Slips, bankroll.Balance, cancellationToken);

            return new RunOutcome { Run = run, Slips = build.Slips };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run {RunId} failed", run.Id);
            run.Status = RunStatus.Failed;
            run.Message = ex.Message;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            await runRepository.SaveAsync(run, cancellationToken);
            await auditService.LogAsync("run.failed", ex.Message, new { run.Id }, cancellationToken);
            return new RunOutcome { Run = run, Slips = [] };
        }
    }

    private async Task<IReadOnlyList<FeatureVector>> EnrichWithTeamStrengthsAsync(
        IReadOnlyList<FixtureOddsDto> fixtures,
        IReadOnlyList<FeatureVector> features,
        CancellationToken ct)
    {
        var fixtureLookup = fixtures.ToDictionary(f => f.FixtureId, f => f);
        var expectationCache = new Dictionary<string, FixtureExpectation?>();
        var uniqueFixtures = fixtureLookup.Values.ToList();
        using var enrichGate = new SemaphoreSlim(8);

        await Task.WhenAll(uniqueFixtures.Select(async fx =>
        {
            await enrichGate.WaitAsync(ct);
            try
            {
                try
                {
                    var expectation = await teamStrengthService.GetFixtureExpectationAsync(
                        fx.League, fx.HomeTeam, fx.AwayTeam, ct);
                    lock (expectationCache) { expectationCache[fx.FixtureId] = expectation; }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Team strength lookup failed for {Fixture}", fx.FixtureId);
                    lock (expectationCache) { expectationCache[fx.FixtureId] = null; }
                }
            }
            finally
            {
                enrichGate.Release();
            }
        }));

        var enriched = new List<FeatureVector>(features.Count);

        foreach (var f in features)
        {
            FixtureExpectation? expectation = null;
            if (fixtureLookup.TryGetValue(f.FixtureId, out _))
                expectationCache.TryGetValue(f.FixtureId, out expectation);

            enriched.Add(new FeatureVector
            {
                FixtureId = f.FixtureId,
                Market = f.Market,
                Outcome = f.Outcome,
                MarketImpliedProbability = f.MarketImpliedProbability,
                AttackStrengthDelta = f.AttackStrengthDelta,
                FormDelta = f.FormDelta,
                RestDaysDelta = f.RestDaysDelta,
                InjuriesPenalty = f.InjuriesPenalty,
                MarketDispersion = f.MarketDispersion,
                League = f.League,
                HomeTeam = f.HomeTeam,
                AwayTeam = f.AwayTeam,
                FixtureExpectation = expectation
            });
        }

        var totalFixtures = fixtureLookup.Count;
        var fittedFixtures = expectationCache.Values.Count(e => e is not null);
        logger.LogInformation(
            "RunEngine: Dixon-Coles fits available for {Fitted}/{Total} fixtures ({Pct:P0}). " +
            "Remaining fixtures will use bookmaker-derived fallback.",
            fittedFixtures, totalFixtures,
            totalFixtures == 0 ? 0 : (double)fittedFixtures / totalFixtures);

        return enriched;
    }

    private List<CandidateBet> BuildCandidates(
        IReadOnlyList<FixtureOddsDto> fixtures,
        IReadOnlyList<FeatureVector> features)
    {
        var fixtureLookup = fixtures.ToDictionary(f => f.FixtureId, f => f);
        var matchByFixture = fixtures.ToDictionary(
            f => f.FixtureId,
            f => new Match
            {
                ProviderFixtureId = f.FixtureId,
                League = f.League,
                HomeTeam = f.HomeTeam,
                AwayTeam = f.AwayTeam,
                KickoffUtc = f.KickoffUtc
            });

        var candidates = new List<CandidateBet>();
        foreach (var feature in features)
        {
            if (!fixtureLookup.TryGetValue(feature.FixtureId, out var fixture)) continue;

            var market = fixture.Markets.FirstOrDefault(m =>
                string.Equals(m.Market, feature.Market, StringComparison.OrdinalIgnoreCase));
            var outcome = market?.Outcomes.FirstOrDefault(o =>
                string.Equals(o.Outcome, feature.Outcome, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    MarketOutcomeNormalizer.NormalizeOutcome(feature.Market, o.Outcome),
                    feature.Outcome,
                    StringComparison.OrdinalIgnoreCase));

            if (outcome is null) continue;

            var probability = predictionService.Predict(feature);
            var source = feature.FixtureExpectation is null ? "BookmakerFallback" : "DixonColes";
            candidates.Add(new CandidateBet
            {
                Match = matchByFixture[feature.FixtureId],
                Market = feature.Market,
                Outcome = feature.Outcome,
                Odds = outcome.Odds,
                ModelProbability = probability,
                PredictionSource = source,
                HomeSampleSize = feature.FixtureExpectation?.HomeSampleSize,
                AwaySampleSize = feature.FixtureExpectation?.AwaySampleSize
            });
        }

        return candidates;
    }

    private async Task PlaceReadySlipsAsync(
        IReadOnlyList<Slip> slips,
        StrategyConfig config,
        IReadOnlyList<SportyBetPlacementFixture>? placementFixtures,
        CancellationToken cancellationToken)
    {
        var ready = slips.Where(s => s.Status == SlipStatus.Ready).ToList();
        if (ready.Count == 0) return;

        async Task PlaceOneAsync(Slip slip)
        {
            try
            {
                var attempt = await automationGateway.PlaceSlipAsync(slip, cancellationToken, placementFixtures);
                await placementRepository.SaveAsync(attempt, cancellationToken);

                if (attempt.Success)
                {
                    slip.BookingCode = attempt.BookingCode;
                    slip.BookingUrl = attempt.BookingUrl;

                    if (!string.IsNullOrEmpty(attempt.ExternalTicketId))
                    {
                        slip.Status = SlipStatus.Placed;
                        slip.ExternalTicketId = attempt.ExternalTicketId;
                        slip.PlacedAtUtc = attempt.AttemptedAtUtc;
                    }
                    else
                    {
                        slip.Status = SlipStatus.AwaitingConfirmation;
                    }
                }
                else
                {
                    slip.Status = config.RequireConfirmationOnRisk
                        ? SlipStatus.AwaitingConfirmation
                        : SlipStatus.Failed;
                    slip.FailureReason = attempt.Error;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to place slip {SlipId}", slip.Id);
                slip.Status = SlipStatus.Failed;
                slip.FailureReason = ex.Message;
            }

            await slipRepository.UpdateAsync(slip, cancellationToken);
        }

        if (ready.Count == 1)
        {
            await PlaceOneAsync(ready[0]);
            return;
        }

        await Task.WhenAll(ready.Select(PlaceOneAsync));
    }

    private async Task SendRunEmailAsync(
        IReadOnlyList<Slip> slips,
        decimal balance,
        CancellationToken ct)
    {
        try
        {
            var readySlips = slips.Where(s =>
                s.Status is SlipStatus.Ready or SlipStatus.AwaitingConfirmation or SlipStatus.Placed).ToList();
            if (readySlips.Count == 0) return;

            var summary = new RunSummaryEmail
            {
                RunDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Balance = balance,
                SlipCount = readySlips.Count,
                Slips = readySlips.Select(s => new SlipEmailSummary
                {
                    Sequence = s.Sequence,
                    TotalOdds = s.TotalOdds,
                    Stake = s.Stake,
                    PotentialReturn = s.PotentialReturn,
                    BookingCode = s.BookingCode,
                    BookingUrl = s.BookingUrl,
                    Legs = s.Legs.Select(l => new LegEmailSummary
                    {
                        MatchTitle = l.MatchTitle,
                        League = l.League,
                        Market = l.Market,
                        Outcome = l.Outcome,
                        Odds = l.Odds
                    }).ToList()
                }).ToList()
            };

            await emailNotifier.SendRunSummaryAsync(summary, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send run email notification");
        }
    }
}

using SelfBet.Application.Models;

namespace SelfBet.Application.Abstractions;

public interface IFeatureBuilder
{
    IReadOnlyList<FeatureVector> Build(IReadOnlyList<FixtureOddsDto> fixtures);
}

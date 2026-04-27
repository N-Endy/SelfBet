using SelfBet.Application.Models;

namespace SelfBet.Application.Abstractions;

public interface IPredictionService
{
    decimal Predict(FeatureVector vector);
}

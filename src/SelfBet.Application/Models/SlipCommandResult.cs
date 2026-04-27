namespace SelfBet.Application.Models;

/// <summary>Outcome of manual place/cancel operations for API responses and dashboard messages.</summary>
public sealed class SlipCommandResult
{
    public bool Ok { get; init; }
    public string? Message { get; init; }

    public static SlipCommandResult Success(string? message = null) => new() { Ok = true, Message = message };

    public static SlipCommandResult Failed(string message) => new() { Ok = false, Message = message };
}

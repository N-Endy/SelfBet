using Npgsql;

namespace SelfBet.Infrastructure.Persistence;

public static class ResilientPostgresConnectionString
{
    public static string Build(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Connection string is empty.", nameof(raw));
        }

        var builder = new NpgsqlConnectionStringBuilder(raw)
        {
            Timeout = Math.Max(new NpgsqlConnectionStringBuilder(raw).Timeout, 60)
        };

        if (builder.CommandTimeout < 120)
        {
            builder.CommandTimeout = 120;
        }

        if (builder.KeepAlive == 0)
        {
            builder.KeepAlive = 30;
        }

        if (builder.ChannelBinding == ChannelBinding.Require)
        {
            builder.ChannelBinding = ChannelBinding.Prefer;
        }

        return builder.ConnectionString;
    }
}

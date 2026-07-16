using StackExchange.Redis;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Redis-backed <see cref="IOAuthStateStore"/>. Follows the same pattern as the other transient
/// stores (pending registrations, password-reset tokens).
/// </summary>
public class RedisOAuthStateStore : IOAuthStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IDatabase _database;

    public RedisOAuthStateStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<string> CreateAsync(CancellationToken cancellationToken)
    {
        var state = Guid.NewGuid().ToString("N");

        await _database.StringSetAsync(GetKey(state), "1", Ttl);

        return state;
    }

    public async Task<bool> ConsumeAsync(string state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        // KeyDelete returns true only when the key existed — verify + single-use consume in one op.
        return await _database.KeyDeleteAsync(GetKey(state));
    }

    private static string GetKey(string state) => $"oauth:state:{state}";
}

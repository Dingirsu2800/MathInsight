using System.Text.Json;
using StackExchange.Redis;

namespace MathInsight.Modules.Identity_Access.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IPendingRegistrationStore"/>. Redis is the sole
/// store for pending registrations (BR-04, BR-14); losing it only loses in-flight registrations.
/// </summary>
public class RedisPendingRegistrationStore : IPendingRegistrationStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IDatabase _database;

    public RedisPendingRegistrationStore(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<string> SaveAsync(PendingRegistration payload, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var json = JsonSerializer.Serialize(payload);

        await _database.StringSetAsync(GetKey(token), json, Ttl);

        return token;
    }

    public async Task<PendingRegistration?> GetAsync(string token, CancellationToken cancellationToken)
    {
        var json = await _database.StringGetAsync(GetKey(token));

        if (!json.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<PendingRegistration>(json.ToString());
    }

    public async Task DeleteAsync(string token, CancellationToken cancellationToken)
    {
        await _database.KeyDeleteAsync(GetKey(token));
    }

    private static string GetKey(string token) => $"pending:register:{token}";
}

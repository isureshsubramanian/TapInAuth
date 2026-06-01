using TapInAuth.Stores;
using TapInAuth.Tokens;

namespace TapInAuth.Core.Tests.Services.Fakes;

public sealed class FakeRecoveryCodeStore : IRecoveryCodeStore
{
    public List<RecoveryCode> Saved { get; } = new();

    public Task SaveBatchAsync(IReadOnlyList<RecoveryCode> codes, CancellationToken cancellationToken = default)
    {
        Saved.AddRange(codes);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RecoveryCode>> ListActiveAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RecoveryCode> active = Saved
            .Where(c => c.TenantId == tenant.Id && c.UserId == userId && c.ConsumedAt is null)
            .ToList();
        return Task.FromResult(active);
    }

    public Task MarkConsumedAsync(TenantContext tenant, Guid codeId, DateTimeOffset consumedAt, CancellationToken cancellationToken = default)
    {
        var c = Saved.FirstOrDefault(x => x.Id == codeId && x.TenantId == tenant.Id);
        if (c is not null)
        {
            c.ConsumedAt = consumedAt;
        }
        return Task.CompletedTask;
    }

    public Task<int> DeleteAllForUserAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        var removed = Saved.RemoveAll(c => c.TenantId == tenant.Id && c.UserId == userId);
        return Task.FromResult(removed);
    }

    public Task<int> CountActiveAsync(TenantContext tenant, Guid userId, CancellationToken cancellationToken = default)
    {
        var n = Saved.Count(c => c.TenantId == tenant.Id && c.UserId == userId && c.ConsumedAt is null);
        return Task.FromResult(n);
    }
}

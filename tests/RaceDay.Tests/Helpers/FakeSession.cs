using Microsoft.AspNetCore.Http;

namespace RaceDay.Tests.Helpers;

/// <summary>
/// In-memory ISession used to simulate a logged-in (or logged-out) user in controller tests,
/// without needing a real web server or distributed cache.
/// </summary>
public class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken token = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, out byte[] value)
    {
        var found = _store.TryGetValue(key, out var v);
        value = v ?? Array.Empty<byte>();
        return found;
    }
}

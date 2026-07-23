using System.Text.Json;
using System.Text.Json.Serialization;
using GithubScout.Core.Abstractions;
using GithubScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Store;

/// <summary>
/// Stores the watchlist as a plain JSON file that can be hand-edited by the user and is also
/// read/written by the web dashboard's add/remove endpoints, so both stay in sync automatically.
/// </summary>
public sealed class JsonFileWatchlistStore : IWatchlistStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly ILogger<JsonFileWatchlistStore> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonFileWatchlistStore(string filePath, ILogger<JsonFileWatchlistStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WatchItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadAllUnlockedAsync(cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<WatchItem> AddAsync(WatchItem item, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllUnlockedAsync(cancellationToken)).ToList();
            var existingIndex = items.FindIndex(i => i.Id == item.Id);
            if (existingIndex >= 0)
            {
                items[existingIndex] = item;
            }
            else
            {
                items.Add(item);
            }

            await WriteAllUnlockedAsync(items, cancellationToken);
            return item;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllUnlockedAsync(cancellationToken)).ToList();
            var removed = items.RemoveAll(i => i.Id == id) > 0;
            if (removed)
            {
                await WriteAllUnlockedAsync(items, cancellationToken);
            }

            return removed;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<IReadOnlyList<WatchItem>> ReadAllUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<WatchItem>();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var items = await JsonSerializer.DeserializeAsync<List<WatchItem>>(stream, SerializerOptions, cancellationToken);
            return items ?? new List<WatchItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse watchlist file at {Path}; treating it as empty", _filePath);
            return Array.Empty<WatchItem>();
        }
    }

    private async Task WriteAllUnlockedAsync(IReadOnlyList<WatchItem> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, items, SerializerOptions, cancellationToken);
    }
}

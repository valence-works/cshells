using System.Text.Json;
using CShells.Configuration;
using FluentStorage.Blobs;

namespace CShells.Providers.FluentStorage;

/// <summary>
/// Provides shell settings from blob storage using FluentStorage.
/// Each blob in the specified path represents a shell configuration in JSON format.
/// </summary>
public class FluentStorageShellSettingsProvider : IShellSettingsProvider
{
    private readonly IBlobStorage _blobStorage;
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentStorageShellSettingsProvider"/> class.
    /// </summary>
    /// <param name="blobStorage">The blob storage instance to read shell configurations from.</param>
    /// <param name="path">The path/prefix within the blob storage where shell JSON files are located. If null, reads from root.</param>
    /// <param name="jsonOptions">Optional JSON serialization options. If null, uses default options with case-insensitive property names.</param>
    public FluentStorageShellSettingsProvider(
        IBlobStorage blobStorage,
        string? path = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        Guard.Against.Null(blobStorage);

        _blobStorage = blobStorage;
        _path = path ?? string.Empty;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new FeatureEntryListJsonConverter() }
        };
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ShellSettings>> GetShellSettingsAsync(CancellationToken cancellationToken = default)
    {
        // List all blobs in the specified path
        var blobs = await _blobStorage.ListAsync(
            new()
            {
                FolderPath = string.IsNullOrEmpty(_path) ? null : _path,
                Recurse = false,
                FilePrefix = null
            },
            cancellationToken);

        if (blobs == null || !blobs.Any())
        {
            return [];
        }

        var shellSettings = new List<ShellSettings>();

        foreach (var blob in blobs.Where(b => b.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                // Read the blob content
                await using var stream = await _blobStorage.OpenReadAsync(blob.FullPath, cancellationToken);

                // Deserialize to ShellConfig
                var shellConfig = await JsonSerializer.DeserializeAsync<ShellConfig>(stream, _jsonOptions, cancellationToken);

                if (shellConfig == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize shell configuration from '{blob.Name}'.");
                }

                // Convert to ShellSettings
                var settings = ShellSettingsFactory.Create(shellConfig);
                shellSettings.Add(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading shell configuration from blob '{blob.Name}': {ex.Message}", ex);
            }
        }

        return shellSettings;
    }
}

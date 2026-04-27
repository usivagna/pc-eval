using System.Text.Json;
using PCEval.Models;

namespace PCEval.Services;

public class OemCatalogService : IOemCatalogService
{
    private const string AssetFile = "oem-catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private IReadOnlyList<OemSystem>? _cached;

    public async Task<IReadOnlyList<OemSystem>> LoadAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(AssetFile);
            var list = await JsonSerializer.DeserializeAsync<List<OemSystem>>(stream, JsonOptions, ct).ConfigureAwait(false);
            _cached = list ?? new List<OemSystem>();
        }
        catch (FileNotFoundException)
        {
            _cached = Array.Empty<OemSystem>();
        }

        return _cached;
    }
}

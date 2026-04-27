using PCEval.Models;

namespace PCEval.Services;

public interface IOemCatalogService
{
    Task<IReadOnlyList<OemSystem>> LoadAsync(CancellationToken ct = default);
}

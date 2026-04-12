using PCEval.Models;

namespace PCEval.Services;

/// <summary>Interface for platform-specific display information collection.</summary>
public interface IDisplayService
{
    /// <summary>
    /// Return display information for all connected monitors.
    /// The first entry is the primary display.
    /// </summary>
    Task<List<DisplayInfo>> GetAllDisplaysInfoAsync();
}

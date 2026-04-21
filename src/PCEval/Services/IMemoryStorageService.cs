using PCEval.Models;

namespace PCEval.Services;

/// <summary>Interface for platform-specific memory and storage information collection.</summary>
public interface IMemoryStorageService
{
    /// <summary>Return memory and storage information for the current system.</summary>
    Task<MemoryStorageInfo> GetMemoryStorageInfoAsync();
}

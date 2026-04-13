using PCEval.Models;

namespace PCEval.Services;

/// <summary>Interface for platform-specific processor information collection.</summary>
public interface IProcessorService
{
    /// <summary>Return processor information for the active CPU/SoC.</summary>
    Task<ProcessorInfo> GetProcessorInfoAsync();
}

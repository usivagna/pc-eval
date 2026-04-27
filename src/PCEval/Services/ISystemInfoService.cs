using PCEval.Models;

namespace PCEval.Services;

public interface ISystemInfoService
{
    Task<SystemInfo> GetSystemInfoAsync();
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCEval.Models;
using PCEval.Services;
using DisplayInfo = PCEval.Models.DisplayInfo;

namespace PCEval.ViewModels;

/// <summary>
/// ViewModel for the Mac Lineup page — compares the user's PC point-by-point
/// against every Mac in the current Apple lineup.
/// </summary>
public partial class MacLineupViewModel : ObservableObject
{
    private readonly IProcessorService _processorService;
    private readonly IDisplayService _displayService;
    private readonly ISystemInfoService _systemInfoService;

    public ObservableCollection<MacComparisonResult> Comparisons { get; } = [];

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _userCpuLabel = "Detecting…";
    [ObservableProperty] private string _userDisplayLabel = "Detecting…";
    [ObservableProperty] private string _userMemoryLabel = "Detecting…";
    [ObservableProperty] private string _userStorageLabel = "Detecting…";
    [ObservableProperty] private string _userGpuLabel = "Detecting…";
    [ObservableProperty] private string _diagonalInput = "";
    [ObservableProperty] private string _summary = "";

    private ProcessorInfo? _processor;
    private DisplayInfo? _display;
    private SystemInfo? _system;

    public MacLineupViewModel(
        IProcessorService processorService,
        IDisplayService displayService,
        ISystemInfoService systemInfoService)
    {
        _processorService  = processorService;
        _displayService    = displayService;
        _systemInfoService = systemInfoService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _processor = await _processorService.GetProcessorInfoAsync();
            var displays = await _displayService.GetAllDisplaysInfoAsync();
            _display = displays.FirstOrDefault();
            _system  = await _systemInfoService.GetSystemInfoAsync();

            UserCpuLabel = _processor?.CpuModel ?? "Unknown CPU";

            if (_display is not null && _display.ResolutionWidth.HasValue && _display.ResolutionHeight.HasValue)
            {
                UserDisplayLabel = $"{_display.ResolutionWidth}×{_display.ResolutionHeight}"
                                   + (_display.RefreshRate.HasValue ? $" @ {_display.RefreshRate:0} Hz" : "");
            }
            else
            {
                UserDisplayLabel = "Unknown display";
            }

            // Memory / storage / GPU summaries for the "Your PC" card
            UserMemoryLabel  = FormatMemory(_system);
            UserStorageLabel = FormatStorage(_system);
            UserGpuLabel     = FormatGpu(_system);

            // If user already typed a diagonal, apply it before computing
            ApplyDiagonal();

            Rebuild();
        }
        catch (Exception ex)
        {
            Summary = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnDiagonalInputChanged(string value)
    {
        ApplyDiagonal();
        Rebuild();
    }

    private void ApplyDiagonal()
    {
        if (_display is null) return;
        if (double.TryParse(DiagonalInput, out double d) && d > 0)
            _display.DiagonalInches = d;
    }

    private void Rebuild()
    {
        Comparisons.Clear();
        var results = MacLineupLogic.BuildLineupComparison(_processor, _display, _system);
        foreach (var r in results) Comparisons.Add(r);

        int beats = results.Count(r => r.Verdict == "Beats");
        int matches = results.Count(r => r.Verdict == "Matches");
        int trails = results.Count(r => r.Verdict == "Trails");
        int mixed = results.Count(r => r.Verdict == "Mixed");
        Summary = $"Across {results.Count} Mac configs: beats {beats}, matches {matches}, mixed {mixed}, trails {trails}.";
    }

    private static string FormatMemory(SystemInfo? s)
    {
        if (s?.TotalRamGb is not double gb || gb <= 0) return "Unknown";
        var label = $"{gb:0.#} GB";
        if (!string.IsNullOrEmpty(s.RamType)) label += $" {s.RamType}";
        if (s.RamSpeedMhz is int sp && sp > 0) label += $"-{sp}";
        return label;
    }

    private static string FormatStorage(SystemInfo? s)
    {
        if (s?.PrimaryStorageGb is not double gb || gb <= 0) return "Unknown";
        var label = $"{gb:0} GB";
        if (!string.IsNullOrEmpty(s.PrimaryStorageType)) label += $" {s.PrimaryStorageType}";
        if (s.TotalStorageGb is double total && total > gb)
            label += $" (total {total:0} GB)";
        return label;
    }

    private static string FormatGpu(SystemInfo? s)
    {
        if (string.IsNullOrEmpty(s?.PrimaryGpuName)) return "Unknown";
        var label = s.PrimaryGpuName!;
        if (s.PrimaryGpuVramGb is double v && v > 0) label += $" · {v:0.#} GB VRAM";
        return label;
    }
}

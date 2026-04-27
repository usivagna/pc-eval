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

    public ObservableCollection<MacComparisonResult> Comparisons { get; } = [];

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _userCpuLabel = "Detecting…";
    [ObservableProperty] private string _userDisplayLabel = "Detecting…";
    [ObservableProperty] private string _diagonalInput = "";
    [ObservableProperty] private string _summary = "";

    private ProcessorInfo? _processor;
    private DisplayInfo? _display;

    public MacLineupViewModel(IProcessorService processorService, IDisplayService displayService)
    {
        _processorService = processorService;
        _displayService   = displayService;
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
        var results = MacLineupLogic.BuildLineupComparison(_processor, _display);
        foreach (var r in results) Comparisons.Add(r);

        int beats = results.Count(r => r.Verdict == "Beats");
        int matches = results.Count(r => r.Verdict == "Matches");
        int trails = results.Count(r => r.Verdict == "Trails");
        int mixed = results.Count(r => r.Verdict == "Mixed");
        Summary = $"Across {results.Count} Mac configs: beats {beats}, matches {matches}, mixed {mixed}, trails {trails}.";
    }
}

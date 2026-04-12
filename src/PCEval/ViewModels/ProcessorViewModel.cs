using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCEval.Models;
using PCEval.Services;

namespace PCEval.ViewModels;

/// <summary>ViewModel for the Processor evaluation page.</summary>
public partial class ProcessorViewModel : ObservableObject
{
    private readonly IProcessorService _processorService;

    public ObservableCollection<ProcessorScorecardRow> DimensionRows { get; } = [];
    public ObservableCollection<ProcessorScorecardRow> SummaryRows   { get; } = [];

    [ObservableProperty] private bool   _isLoading = true;
    [ObservableProperty] private string _cpuModel   = "—";
    [ObservableProperty] private string _tierLabel  = "—";
    [ObservableProperty] private string _vendor     = "—";
    [ObservableProperty] private string _architecture = "—";
    [ObservableProperty] private string _cores      = "—";
    [ObservableProperty] private string _threads    = "—";
    [ObservableProperty] private string _maxFreq    = "—";
    [ObservableProperty] private string _typicalTdp = "—";
    [ObservableProperty] private string _overallGrade = "—";
    [ObservableProperty] private string _overallVerdict = "—";
    [ObservableProperty] private Color  _overallColor = Colors.Gray;

    public ProcessorViewModel(IProcessorService processorService)
    {
        _processorService = processorService;
    }

    [RelayCommand]
    public async Task LoadProcessorAsync()
    {
        IsLoading = true;
        try
        {
            var info = await _processorService.GetProcessorInfoAsync();
            PopulateDetectedInfo(info);

            var rows = ProcessorLogic.BuildScorecard(info);
            DimensionRows.Clear();
            SummaryRows.Clear();
            foreach (var row in rows)
            {
                if (row.IsDimensionRow)
                    DimensionRows.Add(row);
                else
                    SummaryRows.Add(row);
            }

            var (grade, color, verdict) = ProcessorLogic.OverallGrade(rows);
            OverallGrade   = grade;
            OverallColor   = color;
            OverallVerdict = verdict;
        }
        catch (Exception ex)
        {
            OverallVerdict = $"Error loading processor info: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateDetectedInfo(ProcessorInfo info)
    {
        var tier = info.Tier;

        CpuModel      = info.CpuModel ?? "Unknown";
        TierLabel     = tier?.Label   ?? "Unrecognised";
        Vendor        = info.Vendor   ?? tier?.Vendor ?? "Unknown";
        Architecture  = info.Architecture ?? tier?.Arch ?? "Unknown";
        MaxFreq       = info.MaxFreqMhz.HasValue ? $"{info.MaxFreqMhz.Value:F0} MHz" : "Unknown";
        TypicalTdp    = tier?.TypicalTdp.HasValue == true ? $"{tier.TypicalTdp} W" : "Unknown";

        if (info.PerformanceCores.HasValue && info.EfficiencyCores.HasValue)
            Cores = $"{info.PerformanceCores}P + {info.EfficiencyCores}E";
        else if (info.TotalCores.HasValue)
            Cores = info.TotalCores.Value.ToString();
        else
            Cores = "Unknown";

        Threads = info.TotalThreads.HasValue ? info.TotalThreads.Value.ToString() : "Unknown";
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCEval.Models;
using PCEval.Services;

namespace PCEval.ViewModels;

/// <summary>ViewModel for the Memory and Storage evaluation page.</summary>
public partial class MemoryStorageViewModel : ObservableObject
{
    private readonly IMemoryStorageService _service;

    public ObservableCollection<MemoryStorageScorecardRow> DimensionRows { get; } = [];
    public ObservableCollection<MemoryStorageScorecardRow> InfoRows      { get; } = [];

    [ObservableProperty] private bool   _isLoading     = true;
    [ObservableProperty] private string _totalRam      = "—";
    [ObservableProperty] private string _ramType       = "—";
    [ObservableProperty] private string _ramSpeed      = "—";
    [ObservableProperty] private string _ramSlots      = "—";
    [ObservableProperty] private string _primaryStorage = "—";
    [ObservableProperty] private string _storageType   = "—";
    [ObservableProperty] private string _storageModel  = "—";
    [ObservableProperty] private string _totalStorage  = "—";
    [ObservableProperty] private string _overallGrade  = "—";
    [ObservableProperty] private string _overallVerdict = "—";
    [ObservableProperty] private Color  _overallColor  = Colors.Gray;

    public MemoryStorageViewModel(IMemoryStorageService service)
    {
        _service = service;
    }

    [RelayCommand]
    public async Task LoadMemoryStorageAsync()
    {
        IsLoading = true;
        try
        {
            var info = await _service.GetMemoryStorageInfoAsync();
            PopulateDetectedInfo(info);

            var rows = MemoryStorageLogic.BuildScorecard(info);
            DimensionRows.Clear();
            InfoRows.Clear();
            foreach (var row in rows)
            {
                if (row.IsDimensionRow)
                    DimensionRows.Add(row);
                else
                    InfoRows.Add(row);
            }

            var (grade, color, verdict) = MemoryStorageLogic.OverallGrade(rows);
            OverallGrade   = grade;
            OverallColor   = color;
            OverallVerdict = verdict;
        }
        catch (Exception ex)
        {
            OverallVerdict = $"Error loading memory/storage info: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateDetectedInfo(MemoryStorageInfo info)
    {
        TotalRam = info.TotalRamGb.HasValue
            ? $"{info.TotalRamGb.Value:F1} GB"
            : "Unknown";

        RamType = !string.IsNullOrEmpty(info.RamType) ? info.RamType : "Unknown";

        RamSpeed = info.RamSpeedMhz.HasValue
            ? $"{info.RamSpeedMhz.Value} MHz"
            : "Unknown";

        if (info.RamSlots.HasValue && info.RamSlotsUsed.HasValue)
            RamSlots = $"{info.RamSlotsUsed} / {info.RamSlots} used";
        else if (info.RamSlotsUsed.HasValue)
            RamSlots = $"{info.RamSlotsUsed} module(s)";
        else
            RamSlots = "Unknown";

        PrimaryStorage = info.PrimaryStorageGb.HasValue
            ? FormatStorageGb(info.PrimaryStorageGb.Value)
            : "Unknown";

        StorageType  = !string.IsNullOrEmpty(info.StorageType)  ? info.StorageType  : "Unknown";
        StorageModel = !string.IsNullOrEmpty(info.StorageModel) ? info.StorageModel : "Unknown";

        TotalStorage = info.TotalStorageGb.HasValue
            ? FormatStorageGb(info.TotalStorageGb.Value)
            : "Unknown";
    }

    private static string FormatStorageGb(double gb) =>
        gb >= 1024 ? $"{gb / 1024:F1} TB" : $"{gb:F0} GB";
}

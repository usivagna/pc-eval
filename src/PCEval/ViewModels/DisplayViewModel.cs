using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCEval.Models;
using PCEval.Services;
using DisplayInfo = PCEval.Models.DisplayInfo;

namespace PCEval.ViewModels;

/// <summary>ViewModel for the Display evaluation page.</summary>
public partial class DisplayViewModel : ObservableObject
{
    private readonly IDisplayService _displayService;

    // ── Collections ───────────────────────────────────────────────────────
    public ObservableCollection<string> MonitorNames { get; } = [];
    public ObservableCollection<DisplayScorecardRow> ScorecardRows { get; } = [];

    public static IReadOnlyList<(string Label, int Inches)> ViewingDistances { get; } =
    [
        ("10 in  – phone held close",     10),
        ("12 in  – tablet",               12),
        ("15 in  – laptop",               15),
        ("18 in  – laptop / small monitor", 18),
        ("20 in  – desktop monitor",      20),
        ("24 in  – large desktop monitor", 24),
        ("30 in  – large/TV monitor",     30),
    ];

    public IReadOnlyList<string> ViewingDistanceLabels { get; } =
        ViewingDistances.Select(d => d.Label).ToList();

    // ── Observable properties ─────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _hasMultipleMonitors;
    [ObservableProperty] private int  _selectedMonitorIndex;
    [ObservableProperty] private int  _selectedDistanceIndex = 3; // 18 in default

    [ObservableProperty] private string _resolution    = "—";
    [ObservableProperty] private string _refreshRate   = "—";
    [ObservableProperty] private string _adaptiveSync  = "—";
    [ObservableProperty] private string _dciP3Gamut    = "—";
    [ObservableProperty] private string _srgbGamut     = "—";
    [ObservableProperty] private string _hdrTier       = "—";
    [ObservableProperty] private string _panelType     = "—";
    [ObservableProperty] private string _colorProfile  = "—";

    [ObservableProperty] private string _diagonalInput = "";
    [ObservableProperty] private string _overallGrade  = "—";
    [ObservableProperty] private string _overallDesc   = "Enter screen diagonal to get your score";
    [ObservableProperty] private Color  _overallColor  = Colors.Gray;

    // ── Private state ─────────────────────────────────────────────────────
    private List<DisplayInfo> _allDisplays = [];
    private DisplayInfo _currentDisplay = new();

    public DisplayViewModel(IDisplayService displayService)
    {
        _displayService = displayService;
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadDisplaysAsync()
    {
        IsLoading = true;
        try
        {
            _allDisplays = await _displayService.GetAllDisplaysInfoAsync();
            if (_allDisplays.Count == 0) _allDisplays = [new DisplayInfo()];

            MonitorNames.Clear();
            for (int i = 0; i < _allDisplays.Count; i++)
            {
                var d    = _allDisplays[i];
                string n = d.MonitorName ?? d.ManufacturerName ?? d.ManufacturerId
                           ?? $"Display {i + 1}";
                MonitorNames.Add(n);
            }

            HasMultipleMonitors  = _allDisplays.Count > 1;
            SelectedMonitorIndex = 0;
            SelectMonitor(0);
        }
        catch (Exception ex)
        {
            OverallDesc = $"Error loading display info: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedMonitorIndexChanged(int value) => SelectMonitor(value);
    partial void OnSelectedDistanceIndexChanged(int value) => UpdateScores();
    partial void OnDiagonalInputChanged(string value) => UpdateScores();

    private void SelectMonitor(int index)
    {
        if (index < 0 || index >= _allDisplays.Count) return;
        _currentDisplay = _allDisplays[index];

        // Auto-fill diagonal from EDID if available
        if (_currentDisplay.DiagonalInches.HasValue && string.IsNullOrEmpty(DiagonalInput))
            DiagonalInput = _currentDisplay.DiagonalInches.Value.ToString("F1");

        RefreshDetectedInfo();
        UpdateScores();
    }

    private void RefreshDetectedInfo()
    {
        var inf = _currentDisplay;
        var (w, h) = GetEffectiveResolution(inf);

        Resolution   = w > 0 && h > 0 ? $"{w} × {h} px" : "Unknown";
        RefreshRate  = inf.RefreshRate.HasValue ? $"{inf.RefreshRate.Value:F0} Hz" : "Unknown";
        AdaptiveSync = inf.AdaptiveSync == true
            ? (inf.AdaptiveSyncRange ?? "Yes")
            : "Not detected";
        DciP3Gamut  = inf.GamutP3Pct.HasValue   ? $"{inf.GamutP3Pct.Value:F1}%"   : "Unknown";
        SrgbGamut   = inf.GamutSrgbPct.HasValue  ? $"{inf.GamutSrgbPct.Value:F1}%" : "Unknown";
        HdrTier     = inf.HdrTier ?? "Not detected";
        PanelType   = inf.PanelType ?? "Unknown";
        ColorProfile = inf.IccProfileName ?? "Not detected";
    }

    /// <summary>
    /// Returns the effective (w, h) for a display.
    /// Falls back to <see cref="DeviceDisplay.MainDisplayInfo"/> when OS/EDID
    /// detection doesn't provide a resolution — mirrors the Python
    /// <c>winfo_screenwidth()</c> fallback in display_eval.py.
    /// </summary>
    private static (int w, int h) GetEffectiveResolution(Models.DisplayInfo inf)
    {
        int w = inf.ResolutionWidth  ?? 0;
        int h = inf.ResolutionHeight ?? 0;
        if (w > 0 && h > 0) return (w, h);
        try
        {
            var screen = DeviceDisplay.MainDisplayInfo;
            return ((int)screen.Width, (int)screen.Height);
        }
        catch { return (w, h); }
    }

    private void UpdateScores()
    {
        var inf     = _currentDisplay;
        int distIdx = Math.Clamp(SelectedDistanceIndex, 0, ViewingDistances.Count - 1);
        double dist = ViewingDistances[distIdx].Inches;

        // Use the same effective resolution as RefreshDetectedInfo (with fallback)
        var (w, h) = GetEffectiveResolution(inf);

        // Parse diagonal
        double diagonal = 0;
        if (!string.IsNullOrWhiteSpace(DiagonalInput))
            double.TryParse(DiagonalInput, out diagonal);

        var (rows, grade, desc, color) = DisplayLogic.BuildScorecard(
            inf,
            w > 0 ? w : inf.ResolutionWidth,
            h > 0 ? h : inf.ResolutionHeight,
            diagonal,
            dist);

        ScorecardRows.Clear();
        foreach (var row in rows) ScorecardRows.Add(row);

        OverallGrade = grade;
        OverallDesc  = desc;
        OverallColor = color;
    }
}

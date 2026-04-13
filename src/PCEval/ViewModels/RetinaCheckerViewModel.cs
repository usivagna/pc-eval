using CommunityToolkit.Mvvm.ComponentModel;
using PCEval.Services;

namespace PCEval.ViewModels;

/// <summary>ViewModel for the Retina Display Checker page.</summary>
public partial class RetinaCheckerViewModel : ObservableObject
{
    // Viewing distances (label, inches) – same as Python version
    public static IReadOnlyList<(string Label, int Inches)> ViewingDistances { get; } =
    [
        ("10 in  – phone held close",      10),
        ("12 in  – tablet",                12),
        ("15 in  – laptop",                15),
        ("18 in  – laptop / small monitor", 18),
        ("20 in  – desktop monitor",       20),
        ("24 in  – large desktop monitor", 24),
        ("30 in  – large/TV monitor",      30),
    ];

    public IReadOnlyList<string> ViewingDistanceLabels { get; } =
        ViewingDistances.Select(d => d.Label).ToList();

    [ObservableProperty] private string _detectedResolution = "—";
    [ObservableProperty] private string _diagonalInput      = "";
    [ObservableProperty] private string _ppiDisplay         = "—";
    [ObservableProperty] private string _minPpiDisplay      = "—";
    [ObservableProperty] private int    _selectedDistanceIndex = 3; // 18 in default
    [ObservableProperty] private string _resultText         = "Enter screen diagonal to see result";
    [ObservableProperty] private string _explanationText    = "";
    [ObservableProperty] private Color  _resultColor        = Colors.Gray;

    // Screen resolution detected by MAUI DeviceDisplay API
    private int _screenW;
    private int _screenH;

    public RetinaCheckerViewModel()
    {
        DetectScreenResolution();
        UpdateResult();
    }

    private void DetectScreenResolution()
    {
        try
        {
            var screen = DeviceDisplay.MainDisplayInfo;
            _screenW = (int)screen.Width;
            _screenH = (int)screen.Height;
            DetectedResolution = $"{_screenW} × {_screenH} px";
        }
        catch
        {
            _screenW = 1920;
            _screenH = 1080;
            DetectedResolution = "Resolution unavailable";
        }
    }

    partial void OnSelectedDistanceIndexChanged(int value) => UpdateResult();
    partial void OnDiagonalInputChanged(string value) => UpdateResult();

    private void UpdateResult()
    {
        int idx      = Math.Clamp(SelectedDistanceIndex, 0, ViewingDistances.Count - 1);
        double dist  = ViewingDistances[idx].Inches;
        double minPpi = DisplayLogic.RetinaMinPpi(dist);
        MinPpiDisplay = $"{minPpi:F1} PPI";

        string raw = DiagonalInput.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            PpiDisplay    = "—";
            ResultText    = "Enter screen diagonal to see result";
            ExplanationText = "";
            ResultColor   = Colors.Gray;
            return;
        }

        if (!double.TryParse(raw, out double diagonal) || diagonal <= 0 ||
            _screenW <= 0 || _screenH <= 0)
        {
            PpiDisplay    = "Invalid input";
            ResultText    = "Please enter a valid diagonal size.";
            ExplanationText = "";
            ResultColor   = Color.FromArgb("#cc0000");
            return;
        }

        double ppi = DisplayLogic.CalculatePpi(_screenW, _screenH, diagonal);
        PpiDisplay = $"{ppi:F1} PPI";

        if (ppi >= minPpi)
        {
            ResultText = "✓  Retina Display";
            ResultColor = Color.FromArgb("#007a00");
            ExplanationText =
                $"Your display's pixel density ({ppi:F1} PPI) is above the " +
                $"retina threshold ({minPpi:F1} PPI) for a viewing distance " +
                $"of {dist} inches. Individual pixels are not " +
                $"distinguishable to the human eye at this distance.";
        }
        else
        {
            ResultText = "✗  Not a Retina Display";
            ResultColor = Color.FromArgb("#cc0000");
            ExplanationText =
                $"Your display's pixel density ({ppi:F1} PPI) is below the " +
                $"retina threshold ({minPpi:F1} PPI) for a viewing distance " +
                $"of {dist} inches. Individual pixels may be visible " +
                $"to the human eye at this distance.";
        }
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCEval.Models;
using PCEval.Services;

namespace PCEval.ViewModels;

public partial class OemCompareViewModel : ObservableObject
{
    private readonly IOemCatalogService _catalog;

    [ObservableProperty] private ObservableCollection<OemSystem> allSystems = new();
    [ObservableProperty] private OemSystem? slot1;
    [ObservableProperty] private OemSystem? slot2;
    [ObservableProperty] private OemSystem? slot3;
    [ObservableProperty] private ObservableCollection<SpecGroup> specGroups = new();
    [ObservableProperty] private bool isLoading = true;

    public OemCompareViewModel(IOemCatalogService catalog)
    {
        _catalog = catalog;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var list = await _catalog.LoadAsync();
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            AllSystems.Clear();
            foreach (var s in list) AllSystems.Add(s);
            Slot1 = list.Count > 0 ? list[0] : null;
            Slot2 = list.Count > 1 ? list[1] : null;
            Slot3 = list.Count > 2 ? list[2] : null;
            IsLoading = false;
        });
    }

    partial void OnSlot1Changed(OemSystem? value) => RebuildSpecGroups();
    partial void OnSlot2Changed(OemSystem? value) => RebuildSpecGroups();
    partial void OnSlot3Changed(OemSystem? value) => RebuildSpecGroups();

    /// <summary>
    /// How a numeric spec ranks: higher value is better (e.g. RAM, benchmarks)
    /// or lower value is better (e.g. weight, price). <see cref="None"/>
    /// disables winner highlighting.
    /// </summary>
    private enum Ranking { None, HigherBetter, LowerBetter }

    private void RebuildSpecGroups()
    {
        var slots = new[] { Slot1, Slot2, Slot3 };
        if (slots.All(s => s is null))
        {
            SpecGroups = new ObservableCollection<SpecGroup>();
            return;
        }

        SpecRow Row(string label, Func<OemSystem, string> f)
        {
            var v = slots.Select(s => s is null ? "—" : f(s)).ToArray();
            return new SpecRow(label, v[0], v[1], v[2], -1);
        }

        SpecRow Num(string label, Func<OemSystem, double> extract, Func<double, string> format, Ranking rank)
        {
            var raw = slots.Select(s => s is null ? (double?)null : extract(s)).ToArray();
            var disp = slots.Select((s, i) => s is null ? "—" : format(raw[i]!.Value)).ToArray();

            int winner = -1;
            if (rank != Ranking.None)
            {
                // Treat zero/missing as absent so we don't crown a slot whose
                // benchmark just isn't populated.
                var present = raw
                    .Select((v, i) => (v, i))
                    .Where(t => t.v.HasValue && t.v.Value > 0)
                    .ToArray();

                if (present.Length >= 2)
                {
                    var sorted = rank == Ranking.HigherBetter
                        ? present.OrderByDescending(t => t.v!.Value).ToArray()
                        : present.OrderBy(t => t.v!.Value).ToArray();

                    if (sorted[0].v!.Value != sorted[1].v!.Value)
                        winner = sorted[0].i;
                }
            }

            return new SpecRow(label, disp[0], disp[1], disp[2], winner);
        }

        var groups = new List<SpecGroup>
        {
            new("Chip", "\uE950", new[]
            {
                Row("Processor",        s => s.ChipModel),
                Num("Cores",            s => s.ChipCores,                 n => $"{n:0}",         Ranking.HigherBetter),
                Num("Threads",          s => s.ChipThreads,               n => $"{n:0}",         Ranking.HigherBetter),
                Num("Boost clock",      s => s.ChipBoostGhz,              n => $"{n:0.0} GHz",   Ranking.HigherBetter),
                Num("Base clock",       s => s.ChipBaseGhz,               n => $"{n:0.0} GHz",   Ranking.HigherBetter),
                Num("TDP",              s => s.ChipTdpWatts,              n => $"{n:0} W",       Ranking.LowerBetter),
                Num("Cinebench R23",    s => s.CinebenchR23Multi,         n => n > 0 ? n.ToString("N0") : "—", Ranking.HigherBetter),
            }),
            new("Memory", "\uE88E", new[]
            {
                Num("Capacity",         s => s.MemoryGb,                  n => $"{n:0} GB",      Ranking.HigherBetter),
                Row("Type",             s => s.MemoryType),
                Num("Speed",            s => s.MemorySpeedMhz,            n => $"{n:N0} MT/s",   Ranking.HigherBetter),
                Row("User-upgradable",  s => s.MemoryUpgradable ? "Yes" : "No"),
            }),
            new("Storage", "\uEDA2", new[]
            {
                Num("Capacity",         s => s.StorageGb,
                                        n => n >= 1024 ? $"{n / 1024:0.#} TB" : $"{n:0} GB",     Ranking.HigherBetter),
                Row("Interface",        s => s.StorageInterface),
                Num("Read",             s => s.StorageReadMBps,           n => $"{n:N0} MB/s",   Ranking.HigherBetter),
                Num("Write",            s => s.StorageWriteMBps,          n => $"{n:N0} MB/s",   Ranking.HigherBetter),
            }),
            new("Graphics", "\uE7F4", new[]
            {
                Row("GPU",              s => s.GpuModel),
                Num("VRAM",             s => s.GpuVramGb,
                                        n => n > 0 ? $"{n:0} GB" : "Shared",                     Ranking.HigherBetter),
                Num("3DMark Time Spy",  s => s.Gpu3DMarkTimeSpy,
                                        n => n > 0 ? n.ToString("N0") : "—",                    Ranking.HigherBetter),
            }),
            new("Display", "\uE7F4", new[]
            {
                Num("Size",             s => s.DisplaySizeInches,         n => $"{n:0.0}\"",     Ranking.None),
                Row("Resolution",       s => s.DisplayResolution),
                Row("Panel",            s => s.DisplayPanel),
                Num("Refresh rate",     s => s.DisplayRefreshHz,          n => $"{n:0} Hz",      Ranking.HigherBetter),
                Num("Brightness",       s => s.DisplayBrightnessNits,     n => $"{n:0} nits",    Ranking.HigherBetter),
                Row("Color",            s => s.DisplayColorGamut),
                Num("Pixel density",    s => s.DisplayPpi,                n => $"{n:0} ppi",     Ranking.HigherBetter),
            }),
            new("Battery", "\uEBA6", new[]
            {
                Num("Capacity",         s => s.BatteryWh,                 n => $"{n:0} Wh",      Ranking.HigherBetter),
                Num("Claimed runtime",  s => s.BatteryClaimedHours,       n => $"up to {n:0} hr",Ranking.HigherBetter),
            }),
            new("Ports & wireless", "\uE839", new[]
            {
                Num("USB-A",            s => s.UsbA,                      n => $"{n:0}",         Ranking.HigherBetter),
                Num("USB-C",            s => s.UsbC,                      n => $"{n:0}",         Ranking.HigherBetter),
                Num("Thunderbolt",      s => s.Thunderbolt,
                                        n => n > 0 ? $"{n:0} × TB" : "None",                     Ranking.HigherBetter),
                Row("HDMI",             s => s.Hdmi ? "Yes" : "No"),
                Row("SD reader",        s => s.SdReader ? "Yes" : "No"),
                Row("Wi-Fi",            s => s.WifiVersion),
                Row("Bluetooth",        s => s.BluetoothVersion),
            }),
            new("Camera & audio", "\uE722", new[]
            {
                Row("Webcam",           s => s.Webcam),
                Row("Windows Hello (IR)", s => s.WindowsHelloIr ? "Yes" : "No"),
                Num("Speakers",         s => s.Speakers,                  n => $"{n:0}",         Ranking.HigherBetter),
                Num("Microphones",      s => s.Mics,                      n => $"{n:0}",         Ranking.HigherBetter),
            }),
            new("Physical", "\uE7C1", new[]
            {
                Num("Weight",           s => s.WeightKg,
                                        n => $"{n:0.00} kg ({n * 2.205:0.0} lb)",                 Ranking.LowerBetter),
                Row("Dimensions",       s => s.Dimensions),
                Row("Form factor",      s => s.FormFactor),
            }),
            new("Price & OS", "\uE8C7", new[]
            {
                Num("Starting price",   s => (double)s.StartingPriceUsd,  n => $"${n:N0}",       Ranking.LowerBetter),
                Row("Operating system", s => s.OperatingSystem),
            }),
        };

        SpecGroups = new ObservableCollection<SpecGroup>(groups);
    }

    [RelayCommand] private Task OpenSlot1Async() => OpenAsync(Slot1);
    [RelayCommand] private Task OpenSlot2Async() => OpenAsync(Slot2);
    [RelayCommand] private Task OpenSlot3Async() => OpenAsync(Slot3);

    private static Task OpenAsync(OemSystem? s) =>
        s is { PcPartPickerUrl: { Length: > 0 } url } ? Launcher.OpenAsync(url) : Task.CompletedTask;
}

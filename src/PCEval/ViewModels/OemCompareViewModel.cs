using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCEval.Models;
using PCEval.Services;

namespace PCEval.ViewModels;

public partial class OemCompareViewModel : ObservableObject
{
    private readonly IOemCatalogService _catalog;

    [ObservableProperty]
    private ObservableCollection<OemSystem> allSystems = new();

    [ObservableProperty]
    private OemSystem? slot1;

    [ObservableProperty]
    private OemSystem? slot2;

    [ObservableProperty]
    private ObservableCollection<SpecGroup> specGroups = new();

    [ObservableProperty]
    private bool isLoading = true;

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
            IsLoading = false;
        });
    }

    partial void OnSlot1Changed(OemSystem? value) => RebuildSpecGroups();
    partial void OnSlot2Changed(OemSystem? value) => RebuildSpecGroups();

    private void RebuildSpecGroups()
    {
        var a = Slot1;
        var b = Slot2;
        var groups = new List<SpecGroup>();

        if (a is null && b is null)
        {
            SpecGroups = new ObservableCollection<SpecGroup>();
            return;
        }

        // Helper closures
        string V(Func<OemSystem, string> f, OemSystem? s) => s is null ? "—" : f(s);
        string Vd(Func<OemSystem, string> f, OemSystem? s) => s is null ? "—" : f(s);

        SpecRow R(string label, Func<OemSystem, string> f) =>
            new(label, V(f, a), V(f, b));

        groups.Add(new SpecGroup("Chip", "\uE950", new[]
        {
            R("Processor", s => s.ChipModel),
            R("Cores / Threads", s => $"{s.ChipCores} / {s.ChipThreads}"),
            R("Clock", s => $"{s.ChipBaseGhz:0.0} – {s.ChipBoostGhz:0.0} GHz"),
            R("TDP", s => $"{s.ChipTdpWatts} W"),
            R("Cinebench R23 multi", s => s.CinebenchR23Multi > 0 ? s.CinebenchR23Multi.ToString("N0") : "—"),
        }));

        groups.Add(new SpecGroup("Memory", "\uE88E", new[]
        {
            R("Capacity", s => $"{s.MemoryGb} GB"),
            R("Type", s => s.MemoryType),
            R("Speed", s => $"{s.MemorySpeedMhz:N0} MT/s"),
            R("User-upgradable", s => s.MemoryUpgradable ? "Yes" : "No"),
        }));

        groups.Add(new SpecGroup("Storage", "\uEDA2", new[]
        {
            R("Capacity", s => s.StorageGb >= 1024 ? $"{s.StorageGb / 1024.0:0.#} TB" : $"{s.StorageGb} GB"),
            R("Interface", s => s.StorageInterface),
            R("Read", s => $"{s.StorageReadMBps:N0} MB/s"),
            R("Write", s => $"{s.StorageWriteMBps:N0} MB/s"),
        }));

        groups.Add(new SpecGroup("Graphics", "\uE7F4", new[]
        {
            R("GPU", s => s.GpuModel),
            R("VRAM", s => s.GpuVramGb > 0 ? $"{s.GpuVramGb} GB" : "Shared"),
            R("3DMark Time Spy", s => s.Gpu3DMarkTimeSpy > 0 ? s.Gpu3DMarkTimeSpy.ToString("N0") : "—"),
        }));

        groups.Add(new SpecGroup("Display", "\uE7F4", new[]
        {
            R("Size", s => $"{s.DisplaySizeInches:0.0}\""),
            R("Resolution", s => s.DisplayResolution),
            R("Panel", s => s.DisplayPanel),
            R("Refresh rate", s => $"{s.DisplayRefreshHz} Hz"),
            R("Brightness", s => $"{s.DisplayBrightnessNits} nits"),
            R("Color", s => s.DisplayColorGamut),
            R("Pixel density", s => $"{s.DisplayPpi} ppi"),
        }));

        groups.Add(new SpecGroup("Battery", "\uEBA6", new[]
        {
            R("Capacity", s => $"{s.BatteryWh} Wh"),
            R("Claimed runtime", s => $"up to {s.BatteryClaimedHours} hr"),
        }));

        groups.Add(new SpecGroup("Ports & wireless", "\uE839", new[]
        {
            R("USB-A", s => s.UsbA.ToString()),
            R("USB-C", s => s.UsbC.ToString()),
            R("Thunderbolt", s => s.Thunderbolt > 0 ? $"{s.Thunderbolt} × TB" : "None"),
            R("HDMI", s => s.Hdmi ? "Yes" : "No"),
            R("SD reader", s => s.SdReader ? "Yes" : "No"),
            R("Wi-Fi", s => s.WifiVersion),
            R("Bluetooth", s => s.BluetoothVersion),
        }));

        groups.Add(new SpecGroup("Camera & audio", "\uE722", new[]
        {
            R("Webcam", s => s.Webcam),
            R("Windows Hello (IR)", s => s.WindowsHelloIr ? "Yes" : "No"),
            R("Speakers", s => $"{s.Speakers}"),
            R("Microphones", s => $"{s.Mics}"),
        }));

        groups.Add(new SpecGroup("Physical", "\uE7C1", new[]
        {
            R("Weight", s => $"{s.WeightKg:0.00} kg ({s.WeightKg * 2.205:0.0} lb)"),
            R("Dimensions", s => s.Dimensions),
            R("Form factor", s => s.FormFactor),
        }));

        groups.Add(new SpecGroup("Price & OS", "\uE8C7", new[]
        {
            R("Starting price", s => $"${s.StartingPriceUsd:N0}"),
            R("Operating system", s => s.OperatingSystem),
        }));

        SpecGroups = new ObservableCollection<SpecGroup>(groups);
    }

    [RelayCommand]
    private async Task OpenSlot1Async()
    {
        if (Slot1 is { PcPartPickerUrl: { Length: > 0 } url })
            await Launcher.OpenAsync(url);
    }

    [RelayCommand]
    private async Task OpenSlot2Async()
    {
        if (Slot2 is { PcPartPickerUrl: { Length: > 0 } url })
            await Launcher.OpenAsync(url);
    }
}

namespace PCEval.Models;

/// <summary>
/// A Windows OEM system in the comparison catalog. Mirrors the shape of an
/// apple.com/mac/compare entry but populated from PC Part Picker–style specs.
/// All numeric "score" fields are best-effort approximations from public
/// reviews and benchmark databases; treat them as rough relative indicators.
/// </summary>
public class OemSystem
{
    public string Id { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string FormFactor { get; set; } = "Laptop";
    public decimal StartingPriceUsd { get; set; }
    public string PcPartPickerUrl { get; set; } = "";
    public string OperatingSystem { get; set; } = "Windows 11 Home";

    // Chip
    public string ChipModel { get; set; } = "";
    public int ChipCores { get; set; }
    public int ChipThreads { get; set; }
    public double ChipBaseGhz { get; set; }
    public double ChipBoostGhz { get; set; }
    public int ChipTdpWatts { get; set; }
    public int CinebenchR23Multi { get; set; }

    // Memory
    public int MemoryGb { get; set; }
    public string MemoryType { get; set; } = "";
    public int MemorySpeedMhz { get; set; }
    public bool MemoryUpgradable { get; set; }

    // Storage
    public int StorageGb { get; set; }
    public string StorageInterface { get; set; } = "";
    public int StorageReadMBps { get; set; }
    public int StorageWriteMBps { get; set; }

    // Graphics
    public string GpuModel { get; set; } = "";
    public int GpuVramGb { get; set; }
    public int Gpu3DMarkTimeSpy { get; set; }

    // Display
    public double DisplaySizeInches { get; set; }
    public string DisplayResolution { get; set; } = "";
    public string DisplayPanel { get; set; } = "";
    public int DisplayRefreshHz { get; set; }
    public int DisplayBrightnessNits { get; set; }
    public string DisplayColorGamut { get; set; } = "";
    public int DisplayPpi { get; set; }

    // Battery
    public int BatteryWh { get; set; }
    public int BatteryClaimedHours { get; set; }

    // Connectivity
    public int UsbA { get; set; }
    public int UsbC { get; set; }
    public int Thunderbolt { get; set; }
    public bool Hdmi { get; set; }
    public bool SdReader { get; set; }
    public string WifiVersion { get; set; } = "";
    public string BluetoothVersion { get; set; } = "";

    // Camera & audio
    public string Webcam { get; set; } = "";
    public bool WindowsHelloIr { get; set; }
    public int Speakers { get; set; }
    public int Mics { get; set; }

    // Physical
    public double WeightKg { get; set; }
    public string Dimensions { get; set; } = "";

    public string DisplayName => $"{Brand} {Model}";
}

/// <summary>One row inside a spec section: a label plus a value per slot.</summary>
public record SpecRow(string Label, string ValueA, string ValueB);

/// <summary>A group of related spec rows (e.g. "Chip", "Memory").</summary>
public record SpecGroup(string Title, string Glyph, IReadOnlyList<SpecRow> Rows);

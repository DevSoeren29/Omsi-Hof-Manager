using System.Text.Json;

namespace OmsiHofManager.Services;

public sealed class AppSettings
{
    public string? VehiclesPath { get; set; }
    public List<string> SelectedBuses { get; set; } = [];
    public List<string> DeselectedHofs { get; set; } = [];
    public bool OverwriteExisting { get; set; } = true;
    public bool BackupBeforeOverwrite { get; set; } = true;
    public double WindowWidth { get; set; } = 1240;
    public double WindowHeight { get; set; } = 780;
    public bool WindowMaximized { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new();
        }
        catch (Exception)
        {
            // Beschädigte Einstellungen -> Standardwerte.
        }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // Einstellungen sind nicht kritisch.
        }
    }
}

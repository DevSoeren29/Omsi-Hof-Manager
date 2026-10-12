using System.Security.Principal;

namespace OmsiHofManager.Services;

public static class AppPaths
{
    /// <summary>
    /// Datenordner. Liegt eine Datei "portable.txt" neben der EXE, werden alle Daten
    /// im Unterordner "Daten" neben der EXE gespeichert (portabler Modus).
    /// </summary>
    public static string DataFolder { get; } = ResolveDataFolder();

    public static string LibraryFolder => Path.Combine(DataFolder, "Hofdateien");
    public static string BackupFolder => Path.Combine(DataFolder, "Backups");
    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");

    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    private static string ResolveDataFolder()
    {
        var exeDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeDir, "portable.txt")))
            return Path.Combine(exeDir, "Daten");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OmsiHofManager");
    }
}

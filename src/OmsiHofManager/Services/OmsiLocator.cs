using System.Text.RegularExpressions;
using Microsoft.Win32;
using OmsiHofManager.Models;

namespace OmsiHofManager.Services;

public static partial class OmsiLocator
{
    private const string OmsiFolderName = "OMSI 2";

    /// <summary>Sucht den Vehicles-Ordner von OMSI 2 über Steam (Registry + libraryfolders.vdf).</summary>
    public static string? FindVehiclesFolder()
    {
        var steamRoots = new List<string>();

        AddIfNotEmpty(steamRoots, Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string);
        AddIfNotEmpty(steamRoots, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string);
        AddIfNotEmpty(steamRoots, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string);
        steamRoots.Add(@"C:\Program Files (x86)\Steam");
        steamRoots.Add(@"C:\Program Files\Steam");

        var libraries = new List<string>();
        foreach (var root in steamRoots.Select(NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            libraries.Add(root);
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;
            try
            {
                foreach (Match m in LibraryPathRegex().Matches(File.ReadAllText(vdf)))
                    libraries.Add(NormalizePath(m.Groups[1].Value.Replace(@"\\", @"\")));
            }
            catch (Exception)
            {
                // ignorieren
            }
        }

        foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var vehicles = Path.Combine(lib, "steamapps", "common", OmsiFolderName, "Vehicles");
            if (Directory.Exists(vehicles))
                return GetActualCasing(vehicles);
        }

        // Retail-/Nicht-Steam-Installationen
        foreach (var candidate in new[] { @"C:\OMSI 2", @"D:\OMSI 2", @"C:\Games\OMSI 2", @"D:\Games\OMSI 2" })
        {
            var vehicles = Path.Combine(candidate, "Vehicles");
            if (Directory.Exists(vehicles))
                return vehicles;
        }

        return null;
    }

    /// <summary>Akzeptiert den OMSI-2-Hauptordner oder direkt den Vehicles-Ordner.</summary>
    public static string? NormalizeVehiclesPath(string selected)
    {
        if (!Directory.Exists(selected))
            return null;
        var sub = Path.Combine(selected, "Vehicles");
        if (Directory.Exists(sub))
            return sub;
        if (string.Equals(Path.GetFileName(selected.TrimEnd('\\')), "Vehicles", StringComparison.OrdinalIgnoreCase))
            return selected;
        // Ordner mit Fahrzeug-Unterordnern ebenfalls akzeptieren
        return ScanBuses(selected).Count > 0 ? selected : null;
    }

    /// <summary>Findet alle Fahrzeugordner mit .bus-Dateien (bis zu zwei Ebenen tief).</summary>
    public static List<BusFolder> ScanBuses(string vehiclesPath)
    {
        var result = new List<BusFolder>();
        if (!Directory.Exists(vehiclesPath))
            return result;

        foreach (var dir in SafeGetDirectories(vehiclesPath))
        {
            if (TryCreateBus(vehiclesPath, dir) is { } bus)
            {
                result.Add(bus);
                continue;
            }

            foreach (var sub in SafeGetDirectories(dir))
            {
                if (TryCreateBus(vehiclesPath, sub) is { } subBus)
                    result.Add(subBus);
            }
        }

        result.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.FolderName, b.FolderName));
        return result;
    }

    private static BusFolder? TryCreateBus(string vehiclesPath, string dir)
    {
        try
        {
            var models = Directory.EnumerateFiles(dir, "*.bus", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (models.Count == 0)
                return null;

            var bus = new BusFolder
            {
                FolderName = Path.GetRelativePath(vehiclesPath, dir),
                FullPath = dir,
                Models = models,
            };
            bus.RefreshInstalledHofs();
            return bus;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string[] SafeGetDirectories(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch (Exception) { return []; }
    }

    private static void AddIfNotEmpty(List<string> list, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            list.Add(value);
    }

    private static string NormalizePath(string path) => path.Replace('/', '\\').TrimEnd('\\');

    /// <summary>Steam speichert Pfade teils kleingeschrieben – echte Schreibweise vom Dateisystem holen.</summary>
    private static string GetActualCasing(string path)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            if (dir.Parent is null)
                return dir.FullName.ToUpperInvariant();
            var parent = GetActualCasing(dir.Parent.FullName);
            var match = new DirectoryInfo(parent).GetDirectories(dir.Name).FirstOrDefault();
            return match?.FullName ?? Path.Combine(parent, dir.Name);
        }
        catch (Exception)
        {
            return path;
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}

using OmsiHofManager.Models;

namespace OmsiHofManager.Services;

public sealed record DistributionResult(
    int Copied,
    int Overwritten,
    int Unchanged,
    int Skipped,
    IReadOnlyList<string> Errors,
    bool AccessDenied,
    string? BackupFolder);

public static class Distributor
{
    /// <summary>Kopiert die Hofdateien in jeden der angegebenen Busordner.</summary>
    public static DistributionResult Distribute(
        IReadOnlyList<HofFile> hofs,
        IReadOnlyList<BusFolder> buses,
        bool overwrite,
        bool backup,
        IProgress<double>? progress = null)
    {
        int copied = 0, overwritten = 0, unchanged = 0, skipped = 0;
        var errors = new List<string>();
        var accessDenied = false;
        string? backupRoot = null;

        var total = Math.Max(1, hofs.Count * buses.Count);
        var done = 0;

        foreach (var bus in buses)
        {
            foreach (var hof in hofs)
            {
                var target = Path.Combine(bus.FullPath, hof.FileName);
                try
                {
                    if (!File.Exists(target))
                    {
                        File.Copy(hof.FullPath, target);
                        copied++;
                    }
                    else if (HofParser.FilesEqual(hof.FullPath, target))
                    {
                        unchanged++;
                    }
                    else if (!overwrite)
                    {
                        skipped++;
                    }
                    else
                    {
                        if (backup)
                        {
                            backupRoot ??= Path.Combine(AppPaths.BackupFolder, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                            var backupDir = Path.Combine(backupRoot, bus.FolderName);
                            Directory.CreateDirectory(backupDir);
                            File.Copy(target, Path.Combine(backupDir, hof.FileName), overwrite: true);
                        }

                        File.SetAttributes(target, FileAttributes.Normal);
                        File.Copy(hof.FullPath, target, overwrite: true);
                        overwritten++;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    accessDenied = true;
                    errors.Add($"{bus.FolderName}\\{hof.FileName}: Zugriff verweigert");
                }
                catch (Exception ex)
                {
                    errors.Add($"{bus.FolderName}\\{hof.FileName}: {ex.Message}");
                }

                progress?.Report(++done / (double)total);
            }
        }

        return new DistributionResult(copied, overwritten, unchanged, skipped, errors, accessDenied, backupRoot);
    }
}

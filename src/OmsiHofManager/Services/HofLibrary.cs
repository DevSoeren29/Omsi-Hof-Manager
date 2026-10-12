using OmsiHofManager.Models;

namespace OmsiHofManager.Services;

public sealed record ImportPlan(List<string> New, List<string> Conflicting, int Identical);

/// <summary>Die eigene Sammlung von Hofdateien (ein Ordner im Datenverzeichnis der App).</summary>
public sealed class HofLibrary
{
    public string Folder { get; }

    public HofLibrary(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
    }

    public List<HofFile> Load()
    {
        Directory.CreateDirectory(Folder);
        return Directory.EnumerateFiles(Folder, "*.hof", SearchOption.TopDirectoryOnly)
            .Select(HofFile.FromPath)
            .OrderBy(h => h.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Prüft, welche Quelldateien neu sind, welche identisch schon existieren und welche
    /// unter gleichem Namen mit anderem Inhalt vorhanden sind. Bei doppelten Dateinamen gewinnt die erste.
    /// </summary>
    public ImportPlan Analyze(IEnumerable<string> sources)
    {
        var newFiles = new List<string>();
        var conflicts = new List<string>();
        var identical = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var name = Path.GetFileName(source);
            if (!seen.Add(name))
                continue;

            var target = Path.Combine(Folder, name);
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                identical++;
                continue;
            }

            if (!File.Exists(target))
                newFiles.Add(source);
            else if (HofParser.FilesEqual(source, target))
                identical++;
            else
                conflicts.Add(source);
        }

        return new ImportPlan(newFiles, conflicts, identical);
    }

    public void CopyIn(IEnumerable<string> sources)
    {
        Directory.CreateDirectory(Folder);
        foreach (var source in sources)
        {
            var target = Path.Combine(Folder, Path.GetFileName(source));
            if (File.Exists(target))
                File.SetAttributes(target, FileAttributes.Normal);
            File.Copy(source, target, overwrite: true);
            File.SetAttributes(target, FileAttributes.Normal);
        }
    }
}

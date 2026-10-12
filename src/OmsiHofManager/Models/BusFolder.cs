using OmsiHofManager.ViewModels;

namespace OmsiHofManager.Models;

/// <summary>Ein Fahrzeugordner unter "OMSI 2\Vehicles", der mindestens eine .bus-Datei enthält.</summary>
public sealed class BusFolder : ObservableObject
{
    private bool _isSelected;
    private int _missingCount;
    private HashSet<string> _installedHofs = new(StringComparer.OrdinalIgnoreCase);

    public required string FolderName { get; init; }
    public required string FullPath { get; init; }
    public required IReadOnlyList<string> Models { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public IReadOnlySet<string> InstalledHofs => _installedHofs;

    public int HofCount => _installedHofs.Count;

    public string Info =>
        $"{Models.Count} {(Models.Count == 1 ? "Modell" : "Modelle")}  ·  {HofCount} {(HofCount == 1 ? "Hofdatei" : "Hofdateien")}";

    public string ModelsToolTip => "Modelle:\n" + string.Join("\n", Models);

    /// <summary>Anzahl der aktuell ausgewählten Bibliotheks-Hofdateien, die in diesem Bus fehlen.</summary>
    public int MissingCount
    {
        get => _missingCount;
        set
        {
            if (SetProperty(ref _missingCount, value))
            {
                OnPropertyChanged(nameof(HasMissing));
                OnPropertyChanged(nameof(MissingText));
            }
        }
    }

    public bool HasMissing => MissingCount > 0;

    private bool _showBadge;

    /// <summary>Badge nur anzeigen, wenn überhaupt Hofdateien ausgewählt sind.</summary>
    public bool ShowBadge
    {
        get => _showBadge;
        set => SetProperty(ref _showBadge, value);
    }

    public string MissingText => MissingCount > 0 ? $"{MissingCount} fehlen" : "aktuell";

    public void RefreshInstalledHofs()
    {
        try
        {
            _installedHofs = Directory.EnumerateFiles(FullPath, "*.hof", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            _installedHofs = new(StringComparer.OrdinalIgnoreCase);
        }

        OnPropertyChanged(nameof(InstalledHofs));
        OnPropertyChanged(nameof(HofCount));
        OnPropertyChanged(nameof(Info));
    }
}

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using OmsiHofManager.Models;
using OmsiHofManager.Services;

namespace OmsiHofManager.ViewModels;

public enum StatusSeverity { Info, Success, Warning, Error }

public sealed class MainViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly HofLibrary _library;

    private string? _vehiclesPath;
    private string _hofFilter = "";
    private string _busFilter = "";
    private bool _overwriteExisting;
    private bool _backupBeforeOverwrite;
    private bool _isBusy;
    private double _progress;
    private string _statusMessage = "";
    private StatusSeverity _statusSeverity;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        _library = new HofLibrary(AppPaths.LibraryFolder);
        _overwriteExisting = settings.OverwriteExisting;
        _backupBeforeOverwrite = settings.BackupBeforeOverwrite;

        HofsView = CollectionViewSource.GetDefaultView(Hofs);
        HofsView.Filter = o => o is HofFile h && Matches(_hofFilter, h.Title, h.FileName);
        BusesView = CollectionViewSource.GetDefaultView(Buses);
        BusesView.Filter = o => o is BusFolder b && Matches(_busFilter, [b.FolderName, .. b.Models]);

        AddHofsCommand = new RelayCommand(AddHofs, () => !IsBusy);
        AddHofFolderCommand = new RelayCommand(AddHofFolder, () => !IsBusy);
        ImportFromGameCommand = new RelayCommand(ImportFromGame, () => !IsBusy && Buses.Count > 0);
        RemoveHofsCommand = new RelayCommand(p => RemoveHofs(p as IList), _ => !IsBusy);
        OpenLibraryFolderCommand = new RelayCommand(() => OpenInExplorer(_library.Folder));

        SelectAllHofsCommand = new RelayCommand(() => SetSelection(HofsView, true));
        SelectNoHofsCommand = new RelayCommand(() => SetSelection(HofsView, false));
        SelectAllBusesCommand = new RelayCommand(() => SetSelection(BusesView, true));
        SelectNoBusesCommand = new RelayCommand(() => SetSelection(BusesView, false));
        SelectMissingBusesCommand = new RelayCommand(SelectBusesWithMissing);

        BrowseVehiclesCommand = new RelayCommand(BrowseVehicles, () => !IsBusy);
        AutoDetectCommand = new RelayCommand(AutoDetect, () => !IsBusy);
        RescanCommand = new RelayCommand(() => ScanBuses(), () => !IsBusy && HasVehiclesPath);

        DistributeCommand = new RelayCommand(async () => await DistributeAsync(), () => !IsBusy);
        RemoveFromBusesCommand = new RelayCommand(RemoveFromBuses, () => !IsBusy);
        DismissStatusCommand = new RelayCommand(() => StatusMessage = "");
    }

    #region Properties

    public ObservableCollection<HofFile> Hofs { get; } = [];
    public ObservableCollection<BusFolder> Buses { get; } = [];
    public ICollectionView HofsView { get; }
    public ICollectionView BusesView { get; }

    public string LibraryFolder => _library.Folder;

    public string? VehiclesPath
    {
        get => _vehiclesPath;
        private set
        {
            if (SetProperty(ref _vehiclesPath, value))
                OnPropertyChanged(nameof(HasVehiclesPath));
        }
    }

    public bool HasVehiclesPath => !string.IsNullOrEmpty(VehiclesPath) && Directory.Exists(VehiclesPath);

    public string HofFilter
    {
        get => _hofFilter;
        set
        {
            if (SetProperty(ref _hofFilter, value))
                HofsView.Refresh();
        }
    }

    public string BusFilter
    {
        get => _busFilter;
        set
        {
            if (SetProperty(ref _busFilter, value))
                BusesView.Refresh();
        }
    }

    public bool OverwriteExisting
    {
        get => _overwriteExisting;
        set => SetProperty(ref _overwriteExisting, value);
    }

    public bool BackupBeforeOverwrite
    {
        get => _backupBeforeOverwrite;
        set => SetProperty(ref _backupBeforeOverwrite, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
                OnPropertyChanged(nameof(HasStatus));
        }
    }

    public StatusSeverity StatusSeverity
    {
        get => _statusSeverity;
        private set => SetProperty(ref _statusSeverity, value);
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public int SelectedHofCount => Hofs.Count(h => h.IsSelected);
    public int SelectedBusCount => Buses.Count(b => b.IsSelected);

    public string HofHeader => $"{SelectedHofCount} von {Hofs.Count} ausgewählt";
    public string BusHeader => $"{SelectedBusCount} von {Buses.Count} ausgewählt";

    public string SummaryText =>
        $"{SelectedHofCount} {(SelectedHofCount == 1 ? "Hofdatei" : "Hofdateien")}  →  {SelectedBusCount} {(SelectedBusCount == 1 ? "Bus" : "Busse")}";

    #endregion

    #region Commands

    public ICommand AddHofsCommand { get; }
    public ICommand AddHofFolderCommand { get; }
    public ICommand ImportFromGameCommand { get; }
    public ICommand RemoveHofsCommand { get; }
    public ICommand OpenLibraryFolderCommand { get; }
    public ICommand SelectAllHofsCommand { get; }
    public ICommand SelectNoHofsCommand { get; }
    public ICommand SelectAllBusesCommand { get; }
    public ICommand SelectNoBusesCommand { get; }
    public ICommand SelectMissingBusesCommand { get; }
    public ICommand BrowseVehiclesCommand { get; }
    public ICommand AutoDetectCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand DistributeCommand { get; }
    public ICommand RemoveFromBusesCommand { get; }
    public ICommand DismissStatusCommand { get; }

    #endregion

    #region Startup / Settings

    public void Initialize()
    {
        ReloadLibrary(new HashSet<string>(_settings.DeselectedHofs, StringComparer.OrdinalIgnoreCase));

        var path = _settings.VehiclesPath;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            path = OmsiLocator.FindVehiclesFolder();

        VehiclesPath = path;
        if (HasVehiclesPath)
        {
            ScanBuses(new HashSet<string>(_settings.SelectedBuses, StringComparer.OrdinalIgnoreCase));
            if (Hofs.Count == 0)
                SetStatus("Willkommen! Füge Hofdateien per Drag & Drop, über „Hinzufügen“ oder „Aus OMSI importieren“ zu deiner Bibliothek hinzu.", StatusSeverity.Info);
        }
        else
        {
            SetStatus("OMSI 2 wurde nicht automatisch gefunden. Bitte den OMSI-2- oder Vehicles-Ordner über „Durchsuchen“ auswählen.", StatusSeverity.Warning);
        }
    }

    public void SaveSettings()
    {
        _settings.VehiclesPath = VehiclesPath;
        _settings.OverwriteExisting = OverwriteExisting;
        _settings.BackupBeforeOverwrite = BackupBeforeOverwrite;
        _settings.SelectedBuses = Buses.Where(b => b.IsSelected).Select(b => b.FolderName).ToList();
        _settings.DeselectedHofs = Hofs.Where(h => !h.IsSelected).Select(h => h.FileName).ToList();
        _settings.Save();
    }

    #endregion

    #region Library

    private void ReloadLibrary(ISet<string>? deselected = null)
    {
        deselected ??= Hofs.Where(h => !h.IsSelected).Select(h => h.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var h in Hofs)
            h.PropertyChanged -= OnItemPropertyChanged;
        Hofs.Clear();

        foreach (var h in _library.Load())
        {
            h.IsSelected = !deselected.Contains(h.FileName);
            h.PropertyChanged += OnItemPropertyChanged;
            Hofs.Add(h);
        }

        UpdateCounts();
    }

    private void AddHofs()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Hofdateien hinzufügen",
            Filter = "OMSI-Hofdateien (*.hof)|*.hof|Alle Dateien (*.*)|*.*",
            Multiselect = true,
        };
        if (dialog.ShowDialog() == true)
            ImportIntoLibrary(dialog.FileNames);
    }

    private void AddHofFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit Hofdateien auswählen (inkl. Unterordner)" };
        if (dialog.ShowDialog() == true)
            ImportPaths([dialog.FolderName]);
    }

    /// <summary>Für Drag &amp; Drop: Dateien und Ordner (rekursiv) importieren.</summary>
    public void ImportPaths(IEnumerable<string> paths)
    {
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                try { files.AddRange(Directory.EnumerateFiles(path, "*.hof", SearchOption.AllDirectories)); }
                catch (Exception ex) { SetStatus($"Ordner konnte nicht gelesen werden: {ex.Message}", StatusSeverity.Error); }
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
        }
        ImportIntoLibrary(files);
    }

    private void ImportFromGame()
    {
        // Alle Hofdateien aus allen Bussen einsammeln; bei gleichem Namen gewinnt die neueste Version.
        var all = Buses
            .SelectMany(b => b.InstalledHofs.Select(name => Path.Combine(b.FullPath, name)))
            .Where(File.Exists)
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(File.GetLastWriteTime).First())
            .ToList();

        if (all.Count == 0)
        {
            SetStatus("In den gefundenen Bussen sind keine Hofdateien vorhanden.", StatusSeverity.Warning);
            return;
        }

        ImportIntoLibrary(all);
    }

    private void ImportIntoLibrary(IEnumerable<string> files)
    {
        var list = files.Where(f => f.EndsWith(".hof", StringComparison.OrdinalIgnoreCase)).ToList();
        if (list.Count == 0)
        {
            SetStatus("Keine .hof-Dateien gefunden.", StatusSeverity.Warning);
            return;
        }

        ImportPlan plan;
        try
        {
            plan = _library.Analyze(list);
        }
        catch (Exception ex)
        {
            SetStatus($"Fehler beim Lesen der Dateien: {ex.Message}", StatusSeverity.Error);
            return;
        }

        var toCopy = new List<string>(plan.New);
        var replaced = 0;
        if (plan.Conflicting.Count > 0)
        {
            var names = string.Join("\n", plan.Conflicting.Take(12).Select(f => "•  " + Path.GetFileName(f)));
            if (plan.Conflicting.Count > 12)
                names += $"\n…und {plan.Conflicting.Count - 12} weitere";

            var answer = MessageBox.Show(
                $"{plan.Conflicting.Count} Hofdatei(en) gibt es bereits in der Bibliothek, aber mit anderem Inhalt:\n\n{names}\n\n" +
                "Sollen die vorhandenen Versionen ersetzt werden?\n\n(Ja = ersetzen, Nein = behalten, Abbrechen = nichts importieren)",
                "Hofdateien ersetzen?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel)
                return;
            if (answer == MessageBoxResult.Yes)
            {
                toCopy.AddRange(plan.Conflicting);
                replaced = plan.Conflicting.Count;
            }
        }

        try
        {
            _library.CopyIn(toCopy);
        }
        catch (Exception ex)
        {
            SetStatus($"Fehler beim Kopieren in die Bibliothek: {ex.Message}", StatusSeverity.Error);
            return;
        }

        ReloadLibrary();
        SetStatus(
            $"Bibliothek aktualisiert: {plan.New.Count} neu, {replaced} ersetzt, {plan.Identical} bereits vorhanden.",
            plan.New.Count + replaced > 0 ? StatusSeverity.Success : StatusSeverity.Info);
    }

    private void RemoveHofs(IList? highlighted)
    {
        var toRemove = highlighted?.OfType<HofFile>().ToList() ?? [];
        if (toRemove.Count == 0)
        {
            SetStatus("Markiere zuerst die Hofdateien in der Liste (anklicken, Strg/Umschalt für mehrere), die entfernt werden sollen.", StatusSeverity.Info);
            return;
        }

        var answer = MessageBox.Show(
            $"{toRemove.Count} Hofdatei(en) aus der Bibliothek entfernen?\n\nDie Dateien werden in den Papierkorb verschoben. In den Bussen installierte Kopien bleiben erhalten.",
            "Aus Bibliothek entfernen", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return;

        var ok = RecycleBin.Send(toRemove.Select(h => h.FullPath).ToList());
        ReloadLibrary();
        SetStatus(ok
                ? $"{toRemove.Count} Hofdatei(en) aus der Bibliothek entfernt (Papierkorb)."
                : "Einige Dateien konnten nicht entfernt werden.",
            ok ? StatusSeverity.Success : StatusSeverity.Warning);
    }

    #endregion

    #region Buses

    private void BrowseVehicles()
    {
        var dialog = new OpenFolderDialog { Title = "OMSI-2-Ordner oder dessen Vehicles-Ordner auswählen" };
        if (HasVehiclesPath)
            dialog.InitialDirectory = VehiclesPath;

        if (dialog.ShowDialog() != true)
            return;

        var normalized = OmsiLocator.NormalizeVehiclesPath(dialog.FolderName);
        if (normalized is null)
        {
            SetStatus("In diesem Ordner wurden keine OMSI-Fahrzeuge gefunden. Bitte den OMSI-2-Ordner oder „OMSI 2\\Vehicles“ wählen.", StatusSeverity.Error);
            return;
        }

        VehiclesPath = normalized;
        ScanBuses();
    }

    private void AutoDetect()
    {
        var found = OmsiLocator.FindVehiclesFolder();
        if (found is null)
        {
            SetStatus("OMSI 2 konnte nicht automatisch gefunden werden. Bitte über „Durchsuchen“ auswählen.", StatusSeverity.Warning);
            return;
        }
        VehiclesPath = found;
        ScanBuses();
    }

    private void ScanBuses(ISet<string>? selected = null)
    {
        if (!HasVehiclesPath)
            return;

        selected ??= Buses.Where(b => b.IsSelected).Select(b => b.FolderName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var b in Buses)
            b.PropertyChanged -= OnItemPropertyChanged;
        Buses.Clear();

        foreach (var bus in OmsiLocator.ScanBuses(VehiclesPath!))
        {
            bus.IsSelected = selected.Contains(bus.FolderName);
            bus.PropertyChanged += OnItemPropertyChanged;
            Buses.Add(bus);
        }

        UpdateCounts();
        CommandManager.InvalidateRequerySuggested();
        SetStatus(Buses.Count > 0
                ? $"{Buses.Count} Busse in „{VehiclesPath}“ gefunden."
                : "Im gewählten Ordner wurden keine Busse (.bus-Dateien) gefunden.",
            Buses.Count > 0 ? StatusSeverity.Info : StatusSeverity.Warning);
    }

    private void SelectBusesWithMissing()
    {
        foreach (var bus in BusesView.OfType<BusFolder>())
            bus.IsSelected = bus.HasMissing;
    }

    public void OpenBusFolder(BusFolder bus) => OpenInExplorer(bus.FullPath);

    public void ShowHofInExplorer(HofFile hof)
    {
        try { Process.Start("explorer.exe", $"/select,\"{hof.FullPath}\""); }
        catch (Exception ex) { SetStatus(ex.Message, StatusSeverity.Error); }
    }

    #endregion

    #region Distribute / Remove

    private async Task DistributeAsync()
    {
        var hofs = Hofs.Where(h => h.IsSelected).ToList();
        var buses = Buses.Where(b => b.IsSelected).ToList();

        if (hofs.Count == 0 || buses.Count == 0)
        {
            SetStatus("Wähle mindestens eine Hofdatei und einen Bus aus (Häkchen setzen).", StatusSeverity.Warning);
            return;
        }

        IsBusy = true;
        Progress = 0;
        SetStatus($"Verteile {hofs.Count} Hofdatei(en) auf {buses.Count} Bus(se)…", StatusSeverity.Info);

        var progress = new Progress<double>(p => Progress = p * 100);
        var overwrite = OverwriteExisting;
        var backup = BackupBeforeOverwrite;

        DistributionResult result;
        try
        {
            result = await Task.Run(() => Distributor.Distribute(hofs, buses, overwrite, backup, progress));
        }
        finally
        {
            foreach (var bus in buses)
                bus.RefreshInstalledHofs();
            UpdateCounts();
            IsBusy = false;
        }

        var parts = new List<string> { $"{result.Copied} neu kopiert" };
        if (result.Overwritten > 0) parts.Add($"{result.Overwritten} aktualisiert");
        if (result.Unchanged > 0) parts.Add($"{result.Unchanged} bereits aktuell");
        if (result.Skipped > 0) parts.Add($"{result.Skipped} übersprungen (Überschreiben aus)");
        if (result.Errors.Count > 0) parts.Add($"{result.Errors.Count} Fehler");
        var summary = "Fertig: " + string.Join(", ", parts) + ".";
        if (result.BackupFolder is not null)
            summary += " Sicherung angelegt.";

        if (result.Errors.Count == 0)
        {
            SetStatus(summary, StatusSeverity.Success);
            return;
        }

        SetStatus(summary, StatusSeverity.Error);
        if (result.AccessDenied)
        {
            OfferElevation();
        }
        else
        {
            var details = string.Join("\n", result.Errors.Take(15));
            if (result.Errors.Count > 15)
                details += $"\n…und {result.Errors.Count - 15} weitere";
            MessageBox.Show(details, "Fehler beim Verteilen", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveFromBuses()
    {
        var hofs = Hofs.Where(h => h.IsSelected).ToList();
        var buses = Buses.Where(b => b.IsSelected).ToList();
        if (hofs.Count == 0 || buses.Count == 0)
        {
            SetStatus("Wähle mindestens eine Hofdatei und einen Bus aus.", StatusSeverity.Warning);
            return;
        }

        var targets = buses
            .SelectMany(b => hofs.Select(h => Path.Combine(b.FullPath, h.FileName)))
            .Where(File.Exists)
            .ToList();

        if (targets.Count == 0)
        {
            SetStatus("Die ausgewählten Hofdateien sind in den ausgewählten Bussen nicht vorhanden.", StatusSeverity.Info);
            return;
        }

        var answer = MessageBox.Show(
            $"{targets.Count} Hofdatei(en) aus {buses.Count} Bus(sen) entfernen?\n\nDie Dateien werden in den Papierkorb verschoben.",
            "Aus Bussen entfernen", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
            return;

        var ok = RecycleBin.Send(targets);
        foreach (var bus in buses)
            bus.RefreshInstalledHofs();
        UpdateCounts();

        if (ok)
        {
            SetStatus($"{targets.Count} Hofdatei(en) aus den Bussen entfernt (Papierkorb).", StatusSeverity.Success);
        }
        else
        {
            SetStatus("Einige Hofdateien konnten nicht entfernt werden.", StatusSeverity.Error);
            OfferElevation();
        }
    }

    private void OfferElevation()
    {
        if (AppPaths.IsElevated)
        {
            MessageBox.Show(
                "Zugriff verweigert, obwohl das Programm Administratorrechte hat.\n\nLäuft OMSI 2 gerade oder sind die Dateien schreibgeschützt?",
                "Zugriff verweigert", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show(
            "Für den OMSI-Ordner fehlen Schreibrechte (typisch unter „C:\\Program Files (x86)“).\n\nSoll das Programm jetzt als Administrator neu gestartet werden?",
            "Administratorrechte erforderlich", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
            return;

        SaveSettings();
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" });
            Application.Current.Shutdown();
        }
        catch (Exception)
        {
            // UAC-Abfrage abgelehnt
        }
    }

    #endregion

    #region Helpers

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HofFile.IsSelected))
            UpdateCounts();
    }

    private void UpdateCounts()
    {
        var selectedHofs = Hofs.Where(h => h.IsSelected).Select(h => h.FileName).ToList();
        foreach (var bus in Buses)
        {
            bus.MissingCount = selectedHofs.Count(name => !bus.InstalledHofs.Contains(name));
            bus.ShowBadge = selectedHofs.Count > 0;
        }

        OnPropertyChanged(nameof(SelectedHofCount));
        OnPropertyChanged(nameof(SelectedBusCount));
        OnPropertyChanged(nameof(HofHeader));
        OnPropertyChanged(nameof(BusHeader));
        OnPropertyChanged(nameof(SummaryText));
    }

    private static void SetSelection(ICollectionView view, bool selected)
    {
        foreach (var item in view)
        {
            switch (item)
            {
                case HofFile h: h.IsSelected = selected; break;
                case BusFolder b: b.IsSelected = selected; break;
            }
        }
    }

    private static bool Matches(string filter, params string[] values)
        => string.IsNullOrWhiteSpace(filter)
           || values.Any(v => v.Contains(filter.Trim(), StringComparison.CurrentCultureIgnoreCase));

    private void OpenInExplorer(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, StatusSeverity.Error);
        }
    }

    public void SetStatus(string message, StatusSeverity severity)
    {
        StatusSeverity = severity;
        StatusMessage = message;
    }

    #endregion
}

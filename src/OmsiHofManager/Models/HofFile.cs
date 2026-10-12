using OmsiHofManager.Services;
using OmsiHofManager.ViewModels;

namespace OmsiHofManager.Models;

/// <summary>Eine Hofdatei (.hof) in der Bibliothek.</summary>
public sealed class HofFile : ObservableObject
{
    private bool _isSelected = true;

    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public string DisplayName { get; init; } = "";
    public long Size { get; init; }
    public DateTime Modified { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Title => string.IsNullOrWhiteSpace(DisplayName)
        ? Path.GetFileNameWithoutExtension(FileName)
        : DisplayName;

    public string Subtitle => $"{FileName}  ·  {FormatSize(Size)}  ·  {Modified:dd.MM.yyyy}";

    private static string FormatSize(long bytes) => bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024.0:0.#} KB";

    public static HofFile FromPath(string path)
    {
        var info = new FileInfo(path);
        return new HofFile
        {
            FileName = info.Name,
            FullPath = info.FullName,
            DisplayName = HofParser.ReadDisplayName(info.FullName) ?? "",
            Size = info.Length,
            Modified = info.LastWriteTime,
        };
    }
}

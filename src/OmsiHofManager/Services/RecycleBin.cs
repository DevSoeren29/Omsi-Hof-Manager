using System.Runtime.InteropServices;

namespace OmsiHofManager.Services;

/// <summary>Verschiebt Dateien in den Windows-Papierkorb statt sie endgültig zu löschen.</summary>
public static class RecycleBin
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    /// <returns>true, wenn alle Dateien verschoben wurden.</returns>
    public static bool Send(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0)
            return true;

        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // Liste ist durch \0 getrennt und mit doppeltem \0 abgeschlossen
            pFrom = string.Join('\0', paths.Select(Path.GetFullPath)) + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        return SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted && paths.All(p => !File.Exists(p));
    }
}

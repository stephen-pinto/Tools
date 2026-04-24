using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace DuplicateSnifferCli.Engine;

/// <summary>
/// Recursively scans a root directory and produces a flat list of <see cref="FileRecord"/> entries
/// matching the supplied include / exclude / extension / size filters.
/// </summary>
public class FlatIndexBuilder
{
    private readonly string _rootPath;
    private readonly IReadOnlyList<string> _includeFilters;
    private readonly IReadOnlyList<string> _excludeFilters;
    private readonly IReadOnlyList<string> _extensionFilters;
    private readonly long _minSize;
    private readonly long _maxSize;

    /// <summary>
    /// Creates a new index builder.
    /// </summary>
    /// <param name="rootPath">Root directory to scan.</param>
    /// <param name="includeFilters">Wildcard patterns; if non-empty a file name must match at least one.</param>
    /// <param name="excludeFilters">Wildcard patterns; files matching any are skipped.</param>
    /// <param name="extensionFilters">Extensions (with or without leading dot); if non-empty restricts results.</param>
    /// <param name="minSize">Minimum file size in bytes (inclusive). Use 0 for no minimum.</param>
    /// <param name="maxSize">Maximum file size in bytes (inclusive). Use <see cref="long.MaxValue"/> for no maximum.</param>
    public FlatIndexBuilder(
        string rootPath,
        IEnumerable<string>? includeFilters = null,
        IEnumerable<string>? excludeFilters = null,
        IEnumerable<string>? extensionFilters = null,
        long minSize = 0,
        long maxSize = long.MaxValue)
    {
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        _includeFilters = includeFilters?.ToList() ?? new List<string>();
        _excludeFilters = excludeFilters?.ToList() ?? new List<string>();
        _extensionFilters = NormalizeExtensions(extensionFilters);
        _minSize = minSize;
        _maxSize = maxSize;
    }

    private static IReadOnlyList<string> NormalizeExtensions(IEnumerable<string>? exts)
    {
        if (exts is null) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var e in exts)
        {
            if (string.IsNullOrWhiteSpace(e)) continue;
            list.Add(e.StartsWith('.') ? e : "." + e);
        }
        return list;
    }

    /// <summary>
    /// Walks the configured directory tree and returns matching files.
    /// </summary>
    /// <param name="onFileScanned">Optional callback invoked with the running included-file count.</param>
    /// <param name="cancellationToken">Token to cancel the scan early.</param>
    public List<FileRecord> BuildIndex(Action<int>? onFileScanned = null, CancellationToken cancellationToken = default)
    {
        var results = new List<FileRecord>();

        if (!Directory.Exists(_rootPath))
            return results;

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(_rootPath, "*", options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[scan] Failed to enumerate '{_rootPath}': {ex.Message}");
            return results;
        }

        int count = 0;
        foreach (var path in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo info;
            try
            {
                info = new FileInfo(path);
                if ((info.Attributes & FileAttributes.Directory) != 0) continue;
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[scan] Skipped '{path}': {ex.Message}");
                continue;
            }

            var name = info.Name;
            var ext = info.Extension;
            long size;
            try { size = info.Length; }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[scan] Skipped '{path}' (size): {ex.Message}");
                continue;
            }

            if (size < _minSize || size > _maxSize) continue;

            if (_extensionFilters.Count > 0 &&
                !_extensionFilters.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (_excludeFilters.Count > 0 &&
                _excludeFilters.Any(p => WildcardMatcher.Matches(p, name)))
                continue;

            if (_includeFilters.Count > 0 &&
                !_includeFilters.Any(p => WildcardMatcher.Matches(p, name)))
                continue;

            ulong fileId = 0;
            try { fileId = TryGetFileId(path); }
            catch { fileId = 0; }

            results.Add(new FileRecord
            {
                FullPath = path,
                Name = name,
                Extension = ext,
                Size = size,
                FileId = fileId,
            });

            count++;
            onFileScanned?.Invoke(count);
        }

        return results;
    }

    /// <summary>
    /// Attempts to retrieve a unique file identifier for hard-link detection.
    /// Returns 0 on failure or when not supported.
    /// </summary>
    private static ulong TryGetFileId(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return TryGetWindowsFileId(path);

        // On Unix, File.GetUnixFileMode etc. don't expose inode; best-effort fallback: 0.
        return 0;
    }

    private static ulong TryGetWindowsFileId(string path)
    {
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        const uint OPEN_EXISTING = 3;
        const uint FILE_SHARE_READ = 0x1;
        const uint FILE_SHARE_WRITE = 0x2;
        const uint FILE_SHARE_DELETE = 0x4;

        using var handle = CreateFileW(
            path,
            0, // no access required for metadata
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);

        if (handle.IsInvalid) return 0;

        if (!GetFileInformationByHandle(handle, out var info))
            return 0;

        return ((ulong)info.nFileIndexHigh << 32) | info.nFileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
}

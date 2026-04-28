namespace DuplicateSnifferCli.Engine;

/// <summary>
/// Represents metadata about a single file discovered during scanning.
/// </summary>
public class FileRecord
{
    /// <summary>Absolute path to the file.</summary>
    public required string FullPath { get; init; }

    /// <summary>File name without directory.</summary>
    public required string Name { get; init; }

    /// <summary>File extension (including the leading dot, or empty).</summary>
    public required string Extension { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long Size { get; init; }

    /// <summary>Whether this record is currently considered a candidate.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Hash of the first chunk of the file (XxHash128, 16 bytes).</summary>
    public byte[]? PartialHash { get; set; }

    /// <summary>Hash of the entire file (XxHash128, 16 bytes).</summary>
    public byte[]? FullHash { get; set; }

    /// <summary>
    /// Unique file identifier used for hard-link / inode detection.
    /// On Windows this corresponds to the FILE_ID_INFO / nFileIndex; on Unix it is the inode.
    /// Zero indicates "unknown".
    /// </summary>
    public ulong FileId { get; set; }
}

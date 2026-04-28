using System.Collections.Concurrent;
using System.IO.Hashing;
using Microsoft.Win32.SafeHandles;

namespace DuplicateSnifferCli.Engine;

/// <summary>
/// A set of files that share identical content (same full XxHash128).
/// </summary>
public class DuplicateGroup
{
    /// <summary>Full content hash shared by all files in this group.</summary>
    public required byte[] Hash { get; init; }

    /// <summary>Files belonging to this duplicate group.</summary>
    public required List<FileRecord> Files { get; init; }

    /// <summary>
    /// Bytes that could be reclaimed by keeping a single copy.
    /// </summary>
    public long WastedBytes => Files.Count > 1 ? (Files.Count - 1) * Files[0].Size : 0;
}

/// <summary>
/// Detects duplicate files using a multi-stage pipeline:
/// size bucketing → partial hash (first 64 KB) → full hash → hard-link filtering.
/// </summary>
public class DuplicateFinder
{
    private const int PartialHashBytes = 65536;
    private const int FullHashBufferSize = 81920;

    private readonly int _maxParallelism;

    /// <summary>
    /// Creates a new duplicate finder.
    /// </summary>
    /// <param name="maxParallelism">Maximum number of files hashed concurrently.</param>
    public DuplicateFinder(int maxParallelism = 4)
    {
        _maxParallelism = Math.Max(1, maxParallelism);
    }

    /// <summary>
    /// Runs the full duplicate-detection pipeline.
    /// </summary>
    /// <param name="files">Candidate files (typically produced by <see cref="FlatIndexBuilder"/>).</param>
    /// <param name="onSizeBucketProgress">Progress callback for the size-bucketing phase.</param>
    /// <param name="onPartialHashProgress">Progress callback for the partial-hash phase.</param>
    /// <param name="onFullHashProgress">Progress callback for the full-hash phase.</param>
    /// <param name="cancellationToken">Token to cancel the operation early.</param>
    public List<DuplicateGroup> FindDuplicates(
        List<FileRecord> files,
        Action<int, int>? onSizeBucketProgress = null,
        Action<int, int>? onPartialHashProgress = null,
        Action<int, int>? onFullHashProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (files is null) throw new ArgumentNullException(nameof(files));

        // ---- 1. Size prebucketing -----------------------------------------
        var sizeBuckets = new Dictionary<long, List<FileRecord>>();
        int total = files.Count;
        int processed = 0;
        foreach (var f in files)
        {
            if (!f.IsEnabled) { processed++; onSizeBucketProgress?.Invoke(processed, total); continue; }
            if (!sizeBuckets.TryGetValue(f.Size, out var list))
            {
                list = new List<FileRecord>();
                sizeBuckets[f.Size] = list;
            }
            list.Add(f);
            processed++;
            onSizeBucketProgress?.Invoke(processed, total);
        }

        var sizeCandidates = sizeBuckets
            .Where(kv => kv.Value.Count > 1)
            .SelectMany(kv => kv.Value)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        // ---- 2. Partial hash ---------------------------------------------
        int partialTotal = sizeCandidates.Count;
        int partialDone = 0;
        var po = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxParallelism,
            CancellationToken = cancellationToken,
        };

        Parallel.ForEach(sizeCandidates, po, file =>
        {
            try
            {
                file.PartialHash = ComputePartialHash(file.FullPath, file.Size);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[partial] {file.FullPath}: {ex.Message}");
                file.IsEnabled = false;
            }
            int done = Interlocked.Increment(ref partialDone);
            onPartialHashProgress?.Invoke(done, partialTotal);
        });

        var partialBuckets = new Dictionary<PartialKey, List<FileRecord>>();
        foreach (var f in sizeCandidates)
        {
            if (!f.IsEnabled || f.PartialHash is null) continue;
            var key = new PartialKey(f.Size, f.PartialHash);
            if (!partialBuckets.TryGetValue(key, out var list))
            {
                list = new List<FileRecord>();
                partialBuckets[key] = list;
            }
            list.Add(f);
        }

        var fullCandidates = partialBuckets
            .Where(kv => kv.Value.Count > 1)
            .SelectMany(kv => kv.Value)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        // ---- 3. Full hash -------------------------------------------------
        int fullTotal = fullCandidates.Count;
        int fullDone = 0;

        Parallel.ForEach(fullCandidates, po, file =>
        {
            try
            {
                file.FullHash = ComputeFullHash(file.FullPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[full] {file.FullPath}: {ex.Message}");
                file.IsEnabled = false;
            }
            int done = Interlocked.Increment(ref fullDone);
            onFullHashProgress?.Invoke(done, fullTotal);
        });

        // ---- 4. Group by full hash ---------------------------------------
        var groups = new Dictionary<HashKey, List<FileRecord>>();
        foreach (var f in fullCandidates)
        {
            if (!f.IsEnabled || f.FullHash is null) continue;
            var key = new HashKey(f.FullHash);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<FileRecord>();
                groups[key] = list;
            }
            list.Add(f);
        }

        var result = new List<DuplicateGroup>();
        foreach (var (key, list) in groups)
        {
            if (list.Count < 2) continue;

            // 5. Hard-link filter: skip if every file shares the same non-zero FileId.
            ulong firstId = list[0].FileId;
            if (firstId != 0 && list.All(x => x.FileId == firstId))
                continue;

            result.Add(new DuplicateGroup
            {
                Hash = key.Bytes,
                Files = list,
            });
        }

        return result;
    }

    private static byte[] ComputePartialHash(string path, long fileSize)
    {
        int toRead = (int)Math.Min(PartialHashBytes, fileSize);
        if (toRead <= 0) return XxHash128.Hash(ReadOnlySpan<byte>.Empty);

        using SafeFileHandle handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.None);

        byte[] buffer = new byte[toRead];
        int offset = 0;
        while (offset < toRead)
        {
            int n = RandomAccess.Read(handle, buffer.AsSpan(offset, toRead - offset), offset);
            if (n <= 0) break;
            offset += n;
        }

        return XxHash128.Hash(buffer.AsSpan(0, offset));
    }

    private static byte[] ComputeFullHash(string path)
    {
        var hasher = new XxHash128();
        using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: FullHashBufferSize,
            FileOptions.SequentialScan);

        byte[] buffer = new byte[FullHashBufferSize];
        int read;
        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            hasher.Append(buffer.AsSpan(0, read));
        }
        return hasher.GetHashAndReset();
    }

    private readonly struct PartialKey : IEquatable<PartialKey>
    {
        public readonly long Size;
        public readonly byte[] Hash;
        public PartialKey(long size, byte[] hash) { Size = size; Hash = hash; }

        public bool Equals(PartialKey other) =>
            Size == other.Size && Hash.AsSpan().SequenceEqual(other.Hash);
        public override bool Equals(object? obj) => obj is PartialKey k && Equals(k);
        public override int GetHashCode()
        {
            var h = new HashCode();
            h.Add(Size);
            h.AddBytes(Hash);
            return h.ToHashCode();
        }
    }

    private readonly struct HashKey : IEquatable<HashKey>
    {
        public readonly byte[] Bytes;
        public HashKey(byte[] bytes) { Bytes = bytes; }
        public bool Equals(HashKey other) => Bytes.AsSpan().SequenceEqual(other.Bytes);
        public override bool Equals(object? obj) => obj is HashKey k && Equals(k);
        public override int GetHashCode()
        {
            var h = new HashCode();
            h.AddBytes(Bytes);
            return h.ToHashCode();
        }
    }
}

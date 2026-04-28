using System.Text;
using System.Text.Json;
using DuplicateSnifferCli.Engine;
using Spectre.Console;

namespace DuplicateSnifferCli.Display;

/// <summary>
/// Renders scan results (duplicates, listings, size-filtered) to the console,
/// to plain-text files, or as JSON.
/// </summary>
public static class ResultsRenderer
{
    private static readonly Color[] GroupPalette =
    {
        Color.Cyan1,
        Color.HotPink,
        Color.Yellow,
        Color.SpringGreen2,
        Color.Orange1,
        Color.MediumPurple,
        Color.Aqua,
        Color.Red,
    };

    /// <summary>
    /// Renders duplicate groups to the console using a colorful tree widget.
    /// </summary>
    public static void RenderDuplicates(List<DuplicateGroup> groups, TimeSpan elapsed, int totalFilesScanned)
    {
        AnsiConsole.Write(new Rule("[bold magenta]🔁 Duplicate Groups[/]").LeftJustified());
        AnsiConsole.WriteLine();

        if (groups.Count == 0)
        {
            AnsiConsole.MarkupLine("[bold green]🎉 No duplicates found![/]");
        }
        else
        {
            var tree = new Tree("[bold yellow]📂 Duplicate Files[/]")
            {
                Style = Style.Parse("grey"),
            };

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var color = GroupPalette[i % GroupPalette.Length].ToMarkup();
                var hashShort = HashToShortHex(group.Hash);
                var size = group.Files.FirstOrDefault()?.Size ?? 0;

                var groupNode = tree.AddNode(
                    $"[{color}]🧬 Group #{i + 1}[/] " +
                    $"[grey](hash:[/] [bold {color}]{hashShort}[/][grey])[/] " +
                    $"[white]·[/] [bold]{group.Files.Count}[/] [grey]files[/] " +
                    $"[white]·[/] [bold]{Markup.Escape(FormatBytes(size))}[/] [grey]each[/] " +
                    $"[white]·[/] [bold red]💾 {Markup.Escape(FormatBytes(group.WastedBytes))}[/] [grey]wasted[/]");

                foreach (var file in group.Files)
                {
                    var disabledMark = file.IsEnabled ? string.Empty : " [red](disabled)[/]";
                    groupNode.AddNode(
                        $"[{color}]📄[/] [white]{Markup.Escape(file.FullPath)}[/] " +
                        $"[grey]({Markup.Escape(FormatBytes(file.Size))})[/]{disabledMark}");
                }
            }

            AnsiConsole.Write(tree);
        }

        AnsiConsole.WriteLine();
        RenderSummary(totalFilesScanned, groups, elapsed);
    }

    /// <summary>
    /// Renders a flat file listing as a table.
    /// </summary>
    public static void RenderFileList(List<FileRecord> files, TimeSpan elapsed)
    {
        AnsiConsole.Write(new Rule("[bold cyan]📄 File Listing[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = BuildFileTable(files);
        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        RenderSummary(files.Count, null, elapsed);
    }

    /// <summary>
    /// Renders files matching a size filter as a table.
    /// </summary>
    public static void RenderSizeFiltered(List<FileRecord> files, long? minSize, long? maxSize, TimeSpan elapsed)
    {
        var minStr = minSize.HasValue ? FormatBytes(minSize.Value) : "−∞";
        var maxStr = maxSize.HasValue ? FormatBytes(maxSize.Value) : "+∞";

        AnsiConsole.Write(new Rule(
            $"[bold yellow]📏 Size-Filtered Results[/] [grey]({Markup.Escape(minStr)} → {Markup.Escape(maxStr)})[/]")
            .LeftJustified());
        AnsiConsole.WriteLine();

        var table = BuildFileTable(files);
        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        RenderSummary(files.Count, null, elapsed);
    }

    /// <summary>
    /// Writes results to a plain-text file (no ANSI markup).
    /// </summary>
    public static void WriteToFile(string outputPath, List<DuplicateGroup>? groups, List<FileRecord>? files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DuplicateSniffer Results");
        sb.AppendLine("========================");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (groups is { Count: > 0 })
        {
            sb.AppendLine($"Duplicate Groups: {groups.Count}");
            sb.AppendLine(new string('-', 60));

            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                var size = g.Files.FirstOrDefault()?.Size ?? 0;
                sb.AppendLine($"Group #{i + 1}  hash={HashToShortHex(g.Hash)}  files={g.Files.Count}  size={FormatBytes(size)}  wasted={FormatBytes(g.WastedBytes)}");
                foreach (var f in g.Files)
                {
                    var marker = f.IsEnabled ? " " : "*";
                    sb.AppendLine($"  {marker} {f.FullPath} ({FormatBytes(f.Size)})");
                }
                sb.AppendLine();
            }

            var totalWasted = groups.Sum(g => g.WastedBytes);
            var totalDupFiles = groups.Sum(g => g.Files.Count);
            sb.AppendLine($"Total duplicate files: {totalDupFiles}");
            sb.AppendLine($"Total wasted space:    {FormatBytes(totalWasted)}");
        }
        else if (files is { Count: > 0 })
        {
            sb.AppendLine($"Files: {files.Count}");
            sb.AppendLine(new string('-', 60));
            foreach (var f in files)
            {
                sb.AppendLine($"{f.FullPath}\t{FormatBytes(f.Size)}");
            }
        }
        else
        {
            sb.AppendLine("(no results)");
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, sb.ToString());
        AnsiConsole.MarkupLine($"[green]✓[/] Wrote results to [bold]{Markup.Escape(outputPath)}[/]");
    }

    /// <summary>
    /// Writes results as JSON, either to file or to stdout.
    /// </summary>
    public static void WriteJson(List<DuplicateGroup>? groups, List<FileRecord>? files, string? outputPath = null)
    {
        var payload = new
        {
            generatedAt = DateTime.UtcNow,
            duplicateGroups = groups?.Select(g => new
            {
                hash = HashToHex(g.Hash),
                fileCount = g.Files.Count,
                wastedBytes = g.WastedBytes,
                files = g.Files.Select(f => new
                {
                    fullPath = f.FullPath,
                    name = f.Name,
                    extension = f.Extension,
                    size = f.Size,
                    isEnabled = f.IsEnabled,
                }),
            }),
            files = files?.Select(f => new
            {
                fullPath = f.FullPath,
                name = f.Name,
                extension = f.Extension,
                size = f.Size,
                isEnabled = f.IsEnabled,
            }),
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        var json = JsonSerializer.Serialize(payload, options);

        if (string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine(json);
        }
        else
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, json);
            AnsiConsole.MarkupLine($"[green]✓[/] Wrote JSON to [bold]{Markup.Escape(outputPath)}[/]");
        }
    }

    private static Table BuildFileTable(List<FileRecord> files)
    {
        var table = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold cyan]#[/]").RightAligned())
            .AddColumn(new TableColumn("[bold cyan]📄 Name[/]"))
            .AddColumn(new TableColumn("[bold cyan]📂 Path[/]"))
            .AddColumn(new TableColumn("[bold cyan]💾 Size[/]").RightAligned())
            .AddColumn(new TableColumn("[bold cyan]Ext[/]"));

        int idx = 1;
        foreach (var f in files)
        {
            table.AddRow(
                $"[grey]{idx++}[/]",
                $"[white]{Markup.Escape(f.Name ?? string.Empty)}[/]",
                $"[grey]{Markup.Escape(f.FullPath ?? string.Empty)}[/]",
                $"[bold yellow]{Markup.Escape(FormatBytes(f.Size))}[/]",
                $"[magenta]{Markup.Escape(f.Extension ?? string.Empty)}[/]");
        }

        return table;
    }

    private static void RenderSummary(int totalFilesScanned, List<DuplicateGroup>? groups, TimeSpan elapsed)
    {
        var dupGroups = groups?.Count ?? 0;
        var dupFiles = groups?.Sum(g => g.Files.Count) ?? 0;
        var wasted = groups?.Sum(g => g.WastedBytes) ?? 0;

        var content = new Markup(
            $"[bold green]📁 Total files scanned:[/] [white]{totalFilesScanned:N0}[/]\n" +
            $"[bold magenta]🔁 Duplicate groups:[/]   [white]{dupGroups:N0}[/]\n" +
            $"[bold yellow]📄 Duplicate files:[/]    [white]{dupFiles:N0}[/]\n" +
            $"[bold red]💾 Wasted space:[/]       [white]{Markup.Escape(FormatBytes(wasted))}[/]\n" +
            $"[bold cyan]⏱️  Time elapsed:[/]      [white]{elapsed.TotalSeconds:F2}s[/]");

        var panel = new Panel(content)
        {
            Header = new PanelHeader("[bold white on blue] 📊 Scan Summary [/]", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(2, 1, 2, 1),
        };
        panel.Expand = false;

        AnsiConsole.Write(panel);
    }

    private static string HashToHex(byte[]? hash)
    {
        if (hash is null || hash.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    private static string HashToShortHex(byte[]? hash)
    {
        var hex = HashToHex(hash);
        return hex.Length <= 12 ? hex : hex[..12] + "…";
    }

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., "1.5 MB").
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "-" + FormatBytes(-bytes);
        }

        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{(long)value} {units[unit]}"
            : $"{value:0.##} {units[unit]}";
    }
}

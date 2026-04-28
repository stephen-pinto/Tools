using System.ComponentModel;
using System.Diagnostics;
using DuplicateSnifferCli.Display;
using DuplicateSnifferCli.Engine;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DuplicateSnifferCli.Commands;

[Description("Scan a folder and find duplicate files or list files matching filters.")]
public sealed class SniffCommand : Command<SniffCommand.SniffSettings>
{
    private static readonly CancellationTokenSource _cts = new();
    private static int _handlerRegistered;

    /// <summary>
    /// Registers the Ctrl+C handler. Safe to call multiple times — only the first call takes effect.
    /// First Ctrl+C cancels gracefully; second Ctrl+C force-terminates.
    /// </summary>
    public static void RegisterCancelHandler()
    {
        if (Interlocked.CompareExchange(ref _handlerRegistered, 1, 0) == 0)
        {
            Console.CancelKeyPress += (_, e) =>
            {
                if (!_cts.IsCancellationRequested)
                {
                    // First Ctrl+C: cancel gracefully
                    e.Cancel = true;
                    _cts.Cancel();
                    AnsiConsole.MarkupLine("\n[yellow]⚠️  Ctrl+C received — cancelling... Press Ctrl+C again to force quit.[/]");
                }
                else
                {
                    // Second Ctrl+C: let the process die
                    AnsiConsole.MarkupLine("\n[red]🛑 Force quit.[/]");
                    e.Cancel = false;
                }
            };
        }
    }

    public sealed class SniffSettings : CommandSettings
    {
        [Description("Root folder to scan.")]
        [CommandOption("-r|--root <ROOT>")]
        public string Root { get; set; } = string.Empty;

        [Description("Include patterns (wildcards). Repeatable.")]
        [CommandOption("-f|--filter <PATTERN>")]
        public string[]? IncludeFilters { get; set; }

        [Description("Exclude patterns (wildcards). Repeatable.")]
        [CommandOption("-e|--exclude <PATTERN>")]
        public string[]? ExcludeFilters { get; set; }

        [Description("File extension filters (e.g. .jpg .png). Repeatable.")]
        [CommandOption("-t|--type <EXT>")]
        public string[]? ExtensionFilters { get; set; }

        [Description("Find duplicate files.")]
        [CommandOption("-d|--dup")]
        [DefaultValue(false)]
        public bool FindDuplicates { get; set; }

        [Description("Minimum file size in bytes.")]
        [CommandOption("-l|--min-size <BYTES>")]
        public long? MinSize { get; set; }

        [Description("Maximum file size in bytes.")]
        [CommandOption("-m|--max-size <BYTES>")]
        public long? MaxSize { get; set; }

        [Description("Output file path.")]
        [CommandOption("-o|--out <PATH>")]
        public string? OutputPath { get; set; }

        [Description("Degree of parallelism for hashing.")]
        [CommandOption("-p|--parallelism <N>")]
        [DefaultValue(4)]
        public int Parallelism { get; set; } = 4;

        [Description("Output format: text or json.")]
        [CommandOption("--format <FORMAT>")]
        [DefaultValue("text")]
        public string Format { get; set; } = "text";

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Root))
                return ValidationResult.Error("--root is required.");
            if (!Directory.Exists(Root))
                return ValidationResult.Error($"Root directory does not exist: {Root}");
            if (Parallelism <= 0)
                return ValidationResult.Error("--parallelism must be greater than 0.");
            var fmt = Format?.ToLowerInvariant();
            if (fmt != "text" && fmt != "json")
                return ValidationResult.Error("--format must be 'text' or 'json'.");
            if (MinSize.HasValue && MinSize.Value < 0)
                return ValidationResult.Error("--min-size must be >= 0.");
            if (MaxSize.HasValue && MaxSize.Value < 0)
                return ValidationResult.Error("--max-size must be >= 0.");
            if (MinSize.HasValue && MaxSize.HasValue && MinSize.Value > MaxSize.Value)
                return ValidationResult.Error("--min-size cannot exceed --max-size.");
            return ValidationResult.Success();
        }
    }

    protected override int Execute(CommandContext context, SniffSettings settings, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var token = linked.Token;

        HeaderRenderer.Render(settings.Root, "1.0.0");
        AnsiConsole.WriteLine();

        var sw = Stopwatch.StartNew();
        List<FileRecord> files = new();

        try
        {
            AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green bold"))
                .Start("📂 Scanning... 0 files found", ctx =>
                {
                    var builder = new FlatIndexBuilder(
                        settings.Root,
                        settings.IncludeFilters,
                        settings.ExcludeFilters,
                        settings.ExtensionFilters,
                        settings.MinSize ?? 0L,
                        settings.MaxSize ?? long.MaxValue);

                    files = builder.BuildIndex(count =>
                    {
                        ctx.Status($"📂 Scanning... {count:N0} files found");
                    }, token);
                });
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Operation cancelled by user.[/]");
            return 130;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error during scan:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }

        sw.Stop();
        var scanElapsed = sw.Elapsed;
        AnsiConsole.MarkupLine($"[green]✅ Indexed {files.Count:N0} files in {scanElapsed.TotalSeconds:F2}s[/]");
        AnsiConsole.WriteLine();

        if (token.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine("[yellow]⚠️  Operation cancelled by user.[/]");
            return 130;
        }

        var jsonMode = string.Equals(settings.Format, "json", StringComparison.OrdinalIgnoreCase);

        if (settings.FindDuplicates)
        {
            List<DuplicateGroup> duplicates = new();
            var dupSw = Stopwatch.StartNew();

            try
            {
                AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .AutoClear(false)
                    .HideCompleted(false)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn(),
                    })
                    .Start(ctx =>
                    {
                        var sizeTask = ctx.AddTask("🔢 Size bucketing", autoStart: true, maxValue: 1);
                        var partialTask = ctx.AddTask("🔐 Partial hashing", autoStart: false, maxValue: 1);
                        var fullTask = ctx.AddTask("🔒 Full hashing", autoStart: false, maxValue: 1);

                        var finder = new DuplicateFinder(settings.Parallelism);
                        duplicates = finder.FindDuplicates(
                            files,
                            (current, total) => UpdateTask(sizeTask, current, total),
                            (current, total) =>
                            {
                                if (!partialTask.IsStarted) partialTask.StartTask();
                                UpdateTask(partialTask, current, total);
                            },
                            (current, total) =>
                            {
                                if (!fullTask.IsStarted) fullTask.StartTask();
                                UpdateTask(fullTask, current, total);
                            },
                            token);

                        Complete(sizeTask);
                        Complete(partialTask);
                        Complete(fullTask);
                    });
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  Operation cancelled by user.[/]");
                return 130;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error during duplicate detection:[/] {Markup.Escape(ex.Message)}");
                return 2;
            }

            dupSw.Stop();

            if (token.IsCancellationRequested)
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  Operation cancelled by user.[/]");
                return 130;
            }

            ResultsRenderer.RenderDuplicates(duplicates, dupSw.Elapsed, files.Count);

            if (jsonMode)
            {
                ResultsRenderer.WriteJson(duplicates, null, settings.OutputPath);
            }
            else if (!string.IsNullOrWhiteSpace(settings.OutputPath))
            {
                ResultsRenderer.WriteToFile(settings.OutputPath!, duplicates, null);
            }
        }
        else if (settings.MinSize.HasValue || settings.MaxSize.HasValue)
        {
            ResultsRenderer.RenderSizeFiltered(files, settings.MinSize, settings.MaxSize, scanElapsed);

            if (jsonMode)
            {
                ResultsRenderer.WriteJson(null, files, settings.OutputPath);
            }
            else if (!string.IsNullOrWhiteSpace(settings.OutputPath))
            {
                ResultsRenderer.WriteToFile(settings.OutputPath!, null, files);
            }
        }
        else
        {
            ResultsRenderer.RenderFileList(files, scanElapsed);

            if (jsonMode)
            {
                ResultsRenderer.WriteJson(null, files, settings.OutputPath);
            }
            else if (!string.IsNullOrWhiteSpace(settings.OutputPath))
            {
                ResultsRenderer.WriteToFile(settings.OutputPath!, null, files);
            }
        }

        return 0;
    }

    private static void UpdateTask(ProgressTask task, int current, int total)
    {
        if (total <= 0)
        {
            task.MaxValue = 1;
            task.Value = 1;
            return;
        }
        task.MaxValue = total;
        task.Value = Math.Min(current, total);
    }

    private static void Complete(ProgressTask task)
    {
        if (task.MaxValue <= 0) task.MaxValue = 1;
        task.Value = task.MaxValue;
        if (!task.IsFinished) task.StopTask();
    }
}

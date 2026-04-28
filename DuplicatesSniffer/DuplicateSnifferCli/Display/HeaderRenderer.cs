using Spectre.Console;

namespace DuplicateSnifferCli.Display;

/// <summary>
/// Renders the colorful animated/stylized header for the Duplicate Sniffer CLI.
/// </summary>
public static class HeaderRenderer
{
    /// <summary>
    /// Renders the application header to the console.
    /// </summary>
    /// <param name="rootPath">The root path being scanned.</param>
    /// <param name="version">The application version string.</param>
    public static void Render(string rootPath, string version = "1.0.0")
    {
        AnsiConsole.WriteLine();

        var figlet = new FigletText("DuplicateSniffer")
            .Centered()
            .Color(Color.HotPink);
        AnsiConsole.Write(figlet);

        var rule = new Rule("[bold cyan]🐕  Sniffing out duplicate files...[/]")
        {
            Style = Style.Parse("purple"),
        };
        rule.Centered();
        AnsiConsole.Write(rule);

        var safeRoot = Markup.Escape(rootPath);
        var safeVersion = Markup.Escape(version);

        var content = new Markup(
            $"[bold yellow]🔍 Tagline:[/] [italic]Sniffing out duplicate files...[/]\n" +
            $"[bold green]📂 Root:[/]    [white]{safeRoot}[/]\n" +
            $"[bold magenta]📌 Version:[/] [white]v{safeVersion}[/]\n" +
            $"[grey]⌨️  Press [bold]Ctrl+C[/] to cancel at any time.[/]");

        var panel = new Panel(content)
        {
            Header = new PanelHeader("[bold blue] 🐾 Scan Configuration [/]", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1, 2, 1),
        };
        panel.Expand = false;

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }
}

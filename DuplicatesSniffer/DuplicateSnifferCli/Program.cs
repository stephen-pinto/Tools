using System.Runtime.InteropServices;
using System.Text;
using DuplicateSnifferCli.Commands;
using Spectre.Console.Cli;

// Enable UTF-8 output so emoji render correctly on Windows
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Enable Virtual Terminal Processing on Windows for ANSI escape sequences (animations, spinners, progress bars)
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    EnableWindowsVirtualTerminal();
}

// Register Ctrl+C handler early, before Spectre.Console.Cli can install its own
SniffCommand.RegisterCancelHandler();

var app = new CommandApp<SniffCommand>();
app.Configure(config =>
{
    config.SetApplicationName("dupe-sniffer");
    config.SetApplicationVersion("1.0.0");

    config.AddExample("--root", "C:\\Users\\photos", "--dup");
    config.AddExample("--root", ".", "--dup", "--type", ".jpg", ".png");
    config.AddExample("--root", "/data", "--exclude", "*.tmp", "--min-size", "1048576");
});

return app.Run(args);

static void EnableWindowsVirtualTerminal()
{
    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
    if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        return;

    if (!GetConsoleMode(handle, out uint mode))
        return;

    SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
}

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr GetStdHandle(int nStdHandle);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

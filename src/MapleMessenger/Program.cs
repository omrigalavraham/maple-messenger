using System.IO;
using System.Windows;
using MapleMessenger.UI;

namespace MapleMessenger;

internal static class Program
{
    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MapleMessenger", "error.log");

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--make-icon") { Ui.WriteIcon(args[1]); return; }

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) => { Log(e.Exception); e.Handled = true; };
        app.Run(new MainWindow(args));
    }

    private static void Log(Exception e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:u} {e}\n\n");
        }
        catch (IOException) { }
    }
}

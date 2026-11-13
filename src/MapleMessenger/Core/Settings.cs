using System.IO;
using System.Text.Json;

namespace MapleMessenger.Core;

internal sealed class Settings
{
    public const int MinInterval = 1;
    public const int MaxInterval = 600;
    public static readonly string[] Hotkeys = ["F8", "F9", "F10", "F11", "F12", "Pause"];

    public string[] Messages { get; set; } = ["", ""];
    public int IntervalSeconds { get; set; } = 10;
    public string Hotkey { get; set; } = "F9";
    /// <summary>Press Enter before typing, to open the chat box.</summary>
    public bool EnterFirst { get; set; } = true;
    /// <summary>Send the letters straight to the game's window, so it keeps typing while another window is in front.</summary>
    public bool Background { get; set; } = true;

    /// <summary>The interval counts from the start of one message to the start of the next,
    /// so "every 4 seconds" really is every 4 seconds, however long the typing took.</summary>
    /// <returns>When the next message may start. Never sooner than a short breath after this one ended.</returns>
    public static DateTime NextStart(DateTime started, DateTime ended, int intervalSeconds) =>
        new[] { started.AddSeconds(intervalSeconds), ended.AddMilliseconds(MinGapMs) }.Max();

    /// <summary>The least pause between two messages, so the chat box has closed before the next Enter.</summary>
    public const int MinGapMs = 300;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MapleMessenger", "settings.json");

    public Settings Normalize()
    {
        var m = Messages ?? [];
        Messages = [m.Length > 0 ? m[0] ?? "" : "", m.Length > 1 ? m[1] ?? "" : ""];
        IntervalSeconds = Math.Clamp(IntervalSeconds, MinInterval, MaxInterval);
        if (!Hotkeys.Contains(Hotkey)) Hotkey = "F9";
        return this;
    }

    public static int HotkeyVk(string name) => name switch
    {
        "F8" => 0x77, "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B, "Pause" => 0x13,
        _ => 0x78,
    };

    public static Settings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new()).Normalize();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { }
        return new Settings().Normalize();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }
}

using System.Text.Json;

namespace MapleMessenger.Core;

internal sealed class Settings
{
    public const int MinInterval = 3;
    public const int MaxInterval = 600;
    public static readonly string[] Hotkeys = ["F8", "F9", "F10", "F11", "F12", "Pause"];

    public string[] Messages { get; set; } = ["", ""];
    public int IntervalSeconds { get; set; } = 10;
    public string Hotkey { get; set; } = "F9";
    /// <summary>Press Enter before typing, to open the chat box.</summary>
    public bool EnterFirst { get; set; } = true;

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

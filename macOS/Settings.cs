using System;
using System.IO;
using System.Text.Json;

namespace AKCardScanner;

// Replaces the WinForms Properties.Settings; stored under ~/Library/Application Support/AKCardScanner
internal class Settings
{
    public int DefaultPort { get; set; } = 9876;
    public string Version { get; set; } = "";

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AKCardScanner");
    private static string FilePath => Path.Combine(Folder, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); }
        catch { }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AKCardScanner;

// One row of CardData/cards.json: one barcode, the card it belongs to, and what is known about it.
public class Card
{
    [JsonPropertyName("t")] public string Pile { get; set; }      // A, S or M
    [JsonPropertyName("n")] public string Number { get; set; }
    [JsonPropertyName("nm")] public string Name { get; set; }
    [JsonPropertyName("r")] public string Rarity { get; set; }
    [JsonPropertyName("c")] public string Code { get; set; }       // the barcode sent to Play!
    [JsonPropertyName("s")] public string Status { get; set; }
    [JsonPropertyName("v")] public string Version { get; set; }    // earliest English release, if known
    [JsonPropertyName("str")] public int? Strength { get; set; }
    [JsonPropertyName("hp")] public int? Health { get; set; }
    [JsonPropertyName("atk")] public string GoldAttack { get; set; }
    [JsonPropertyName("k")] public string Kind { get; set; }
    [JsonPropertyName("g")] public int? Guts { get; set; }
    [JsonPropertyName("te")] public int? Tech { get; set; }
    [JsonPropertyName("p")] public int? Power { get; set; }
    [JsonPropertyName("au")] public string Aura { get; set; }
    [JsonPropertyName("f")] public string Focus { get; set; }
    [JsonPropertyName("mp")] public int? MiraclePower { get; set; }
    [JsonPropertyName("e")] public string Effect { get; set; }

    public string Number_ => Number ?? "";

    public (string, string) StatLine() => Pile switch
    {
        "A" => (Strength is int s ? $"STR {s}" : "", Health is int h ? $"HP {h}" : ""),
        "S" => (Guts is int g ? $"G{g} T{Tech} P{Power}" : "", Focus ?? ""),
        _ => (MiraclePower is int m ? $"MP {m}" : "", Effect ?? "")
    };
}

public static class CardStore
{
    public static readonly string[] Versions =
        { "Ver 1", "Ver 2", "Ver 3", "Ver 4", "Ver 5", "Ver 6", "Evo 1", "Evo 2", "Evo 3", "Evo 4", "Evo 5", "Evo 6", "Evo 7", "Evo 8" };

    // A user copy in ~/Library/Application Support/AKCardScanner/cards.json wins over the bundled list,
    // so cards can be added without touching (and unsigning) the app bundle.
    public static List<Card> Load()
    {
        string user = Path.Combine(Settings.Folder, "cards.json");
        string path = File.Exists(user) ? user : Path.Combine(AppContext.BaseDirectory, "CardData", "cards.json");
        var cards = JsonSerializer.Deserialize<List<Card>>(File.ReadAllText(path)) ?? new List<Card>();
        return cards.Where(c => !string.IsNullOrEmpty(c.Code) && !string.IsNullOrEmpty(c.Pile)).ToList();
    }

    public static int VersionIndex(string v) => string.IsNullOrEmpty(v) ? 99 : Array.IndexOf(Versions, v);

    // Optional card art: CardArt/<number>.jpg|png in the Application Support folder.
    private static Dictionary<string, string> _art;
    public static string ArtPath(Card c)
    {
        if (_art == null)
        {
            _art = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string dir = Path.Combine(Settings.Folder, "CardArt");
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".jpg" or ".jpeg" or ".png" or ".webp") _art[Path.GetFileNameWithoutExtension(f)] = f;
                }
        }
        if (!string.IsNullOrEmpty(c.Number) && _art.TryGetValue(c.Number, out var p)) return p;
        return null;
    }
}

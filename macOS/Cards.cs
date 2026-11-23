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

    // Several barcodes can be printings of one card (same number and name, e.g. a Gold Ver 4 card and a
    // Normal Evo 6 reissue). The fan shows each card once, using its best printing for the chosen version.
    private static readonly string[] RarityOrder = { "Kaiser", "Ultra", "Gold", "Silver", "Bronze", "Normal" };
    private static readonly string[] StatusOrder = { "Tested", "In app", "Untested", "Conflict" };

    public static List<List<Card>> Group(List<Card> all)
    {
        // Miracle names may or may not carry the animal in brackets; other cards keep theirs (e.g. "Lion (Prototype)")
        string Stem(Card c) => new string(((c.Pile == "M" ? (c.Name ?? "").Split('(')[0] : c.Name) ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        // Animal and Strong cards are identified by name: the same card can carry different numbers in the
        // English and Japanese releases. Miracle names repeat across cards, so their number stays in the key.
        var groups = all.GroupBy(c => (c.Pile, c.Pile == "M" ? c.Number ?? "" : "", Stem(c))).Select(g => g.ToList()).ToList();
        foreach (var g in groups.Where(g => g.Count > 1 && g[0].Pile == "M"))
        {
            // Miracle names sometimes carry the animal in brackets; show the fuller name for every printing
            var name = g.Select(c => c.Name).OrderByDescending(n => n.Length).First();
            foreach (var c in g) c.Name = name;
        }
        return groups;
    }

    public static Card Best(IEnumerable<Card> printings)
    {
        int R(string r) { int i = Array.IndexOf(RarityOrder, r); return i < 0 ? 99 : i; }
        int S(string s) { int i = Array.IndexOf(StatusOrder, s); return i < 0 ? 99 : i; }
        int Stats(Card c) => new object[] { c.Strength, c.Health, c.Guts, c.MiraclePower, c.GoldAttack, c.Effect }.Count(o => o != null);
        return printings.OrderBy(c => R(c.Rarity)).ThenBy(c => S(c.Status)).ThenByDescending(Stats).FirstOrDefault();
    }

    // Optional card art in the Application Support folder's CardArt subfolder (jpg, png or webp).
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
        // Named by card, e.g. "A-greatwhiteshark.jpg" (pile, then the name in lower case without spaces or
        // punctuation), or by number, e.g. "A002.jpg". The name wins, because some numbers are reused.
        string name = ((c.Pile == "M" ? (c.Name ?? "").Split('(')[0] : c.Name) ?? "");
        string key = c.Pile + "-" + new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (_art.TryGetValue(key, out var p)) return p;
        if (!string.IsNullOrEmpty(c.Number) && _art.TryGetValue(c.Number, out p)) return p;
        return null;
    }
}

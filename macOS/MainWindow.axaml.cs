using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AKCardScanner;

public partial class MainWindow : Window
{
    private struct CardData
    {
        public string Number, Name, Type, Rarity, Code;
    }

    private readonly HttpClient client = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
    private readonly Settings settings = Settings.Load();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    private XElement animalCards, strongCards, miracleCards;
    private CardData animal, strong, miracle;
    private bool timerHalted;

    private string ServerUrl => $"http://127.0.0.1:{PortBox.Text}/sys147/barcode";

    public MainWindow()
    {
        InitializeComponent();

        // Card lists: a user copy in ~/Library/Application Support/AKCardScanner/CardData wins over the bundled one,
        // so cards can be added without modifying (and breaking the signature of) the .app bundle.
        string userData = Path.Combine(Settings.Folder, "CardData");
        string folder = File.Exists(Path.Combine(userData, "AnimalCards.xml")) ? userData : Path.Combine(AppContext.BaseDirectory, "CardData");
        animalCards = XElement.Load(Path.Combine(folder, "AnimalCards.xml"));
        strongCards = XElement.Load(Path.Combine(folder, "StrongCards.xml"));
        miracleCards = XElement.Load(Path.Combine(folder, "MiracleCards.xml"));
        AnimalCombo.ItemsSource = CardNames(animalCards);
        StrongCombo.ItemsSource = CardNames(strongCards);
        MiracleCombo.ItemsSource = CardNames(miracleCards);

        PortBox.Text = settings.DefaultPort.ToString();

        AnimalCombo.SelectionChanged += (_, _) => { animal = Select(AnimalCombo, animalCards, AnimalButton); };
        StrongCombo.SelectionChanged += (_, _) => { strong = Select(StrongCombo, strongCards, StrongButton); };
        MiracleCombo.SelectionChanged += (_, _) => { miracle = Select(MiracleCombo, miracleCards, MiracleButton); };

        AnimalButton.Click += async (_, _) => await SendCard(animal, "animal");
        StrongButton.Click += async (_, _) => await SendCard(strong, "strong");
        MiracleButton.Click += async (_, _) => await SendCard(miracle, "miracle");
        AllButton.Click += (_, _) => StartScanAll();
        CustomButton.Click += async (_, _) => await SendCustom();
        TestButton.Click += async (_, _) => await TestPort();

        CustomBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) CustomButton.IsEnabled = !string.IsNullOrEmpty(CustomBox.Text); };
        // Return in the custom box sends — also what keyboard-emulating USB barcode scanners do
        CustomBox.AddHandler(KeyDownEvent, async (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Return)
            {
                e.Handled = true;
                if (!string.IsNullOrEmpty(CustomBox.Text)) await SendCustom();
            }
        }, RoutingStrategies.Tunnel);
        PortBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) OnPortChanged(); };

        timer.Tick += Timer_Tick;
        Closing += (_, _) =>
        {
            if (int.TryParse(PortBox.Text, out int p)) { settings.DefaultPort = p; settings.Save(); }
        };
        Opened += (_, _) => CustomBox.Focus();
    }

    private static List<string> CardNames(XElement xml)
    {
        var list = new List<string>();
        foreach (var name in xml.Descendants("card").Select(c => c.Element("name")?.Value))
            if (name != null && !list.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                list.Add(name);
        list.Sort();
        return list;
    }

    private static CardData Lookup(string name, XElement stack)
    {
        var x = stack.Descendants("card").FirstOrDefault(c => c.Element("name")?.Value == name);
        return x == null ? default : new CardData
        {
            Number = x.Element("number")?.Value, Name = x.Element("name")?.Value,
            Type = x.Element("type")?.Value, Rarity = x.Element("rarity")?.Value, Code = x.Element("code")?.Value
        };
    }

    private CardData Select(ComboBox combo, XElement stack, Button button)
    {
        button.IsEnabled = combo.SelectedIndex != -1;
        var card = Lookup(combo.SelectedItem as string, stack);
        SetStatus($"Selected {card.Rarity} {card.Type} Card #{card.Number}: {card.Name}");
        AllButton.IsEnabled = AnimalCombo.SelectedIndex != -1 && StrongCombo.SelectedIndex != -1 && MiracleCombo.SelectedIndex != -1;
        return card;
    }

    private void SetStatus(string text) => StatusText.Text = string.IsNullOrEmpty(text) ? " " : text;

    private static string Describe(CardData c) => $"{c.Rarity} {c.Type} Card: {c.Name}";

    private async Task<bool> SendAsync(string data)
    {
        try
        {
            var resp = await client.PostAsync(ServerUrl, new StringContent(data, Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode) await resp.Content.ReadAsStringAsync();
            return true;
        }
        catch (Exception ex)
        {
            StopTimer();
            await MessageDialog.Show(this,
                "Could not connect to the Arcade I/O Server.\nPlease ensure the server is enabled in the emulator and the ports match.\n\nERROR:\n" + ex.Message,
                "Arcade I/O Server Not Found");
            return false;
        }
    }

    private async Task SendCard(CardData card, string kind)
    {
        if (card.Name == null) { SetStatus($"Invalid {kind} card selected!"); return; }
        IsEnabled = false;
        await SendAsync(card.Code);
        SetStatus($"Sent {Describe(card)}!");
        IsEnabled = true;
    }

    private async Task SendCustom()
    {
        IsEnabled = false;
        string text = CustomBox.Text;
        CustomBox.Text = "";
        SetStatus("Sent " + text);
        await SendAsync(text);
        IsEnabled = true;
        CustomBox.Focus();
    }

    private async Task TestPort()
    {
        IsEnabled = false;
        SetStatus("Pinging Play! emulator...");
        try
        {
            var resp = await client.PostAsync(ServerUrl, new StringContent("", Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
            {
                SetStatus("Response received!");
                await MessageDialog.Show(this, "The Arcade I/O Server is set up correctly and responding!", "Arcade I/O Server Found");
            }
            else SetStatus($"Server responded with {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            SetStatus("No response received!");
            await MessageDialog.Show(this,
                "Could not connect to the Arcade I/O Server.\nPlease ensure the server is enabled in the emulator and the ports match.\n\nERROR:\n" + ex.Message,
                "Arcade I/O Server Not Found");
        }
        IsEnabled = true;
    }

    private void OnPortChanged()
    {
        string text = PortBox.Text ?? "";
        string digits = new string(text.Where(char.IsDigit).ToArray());
        if (text != digits)
        {
            int caret = PortBox.CaretIndex - (text.Length - digits.Length);
            PortBox.Text = digits;
            PortBox.CaretIndex = Math.Max(0, caret);
        }
        TestButton.IsEnabled = digits.Length > 0;
    }

    // Same timing as the Windows app: 100 ms ticks, +2 per tick. Animal at 0, Strong at 80 (4 s, after the
    // game's animal animation), Miracle at 100 (5 s).
    private void StartScanAll()
    {
        if (animal.Name == null || strong.Name == null || miracle.Name == null || Progress.Value > 0) return;
        IsEnabled = false;
        timerHalted = false;
        AllButton.IsVisible = false;
        Progress.IsVisible = true;
        timer.Start();
    }

    private async void Timer_Tick(object sender, EventArgs e)
    {
        double v = Progress.Value;
        if (v < 100) Progress.Value = v + 2; else StopTimer();

        CardData? due = v switch { 0 => animal, 80 => strong, 100 => miracle, _ => null };
        if (due is CardData card && card.Name != null)
        {
            SetStatus($"Sent {Describe(card)}!");
            if (!await SendAsync(card.Code)) timerHalted = true;
        }
        if (v >= 100 && !timerHalted) SetStatus($"Sent {Describe(miracle)}!");
    }

    private void StopTimer()
    {
        timer.Stop();
        Progress.IsVisible = false;
        Progress.Value = 0;
        AllButton.IsVisible = true;
        IsEnabled = true;
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace AKCardScanner;

public class MainForm : Form
{
	public struct CardData
	{
		public string cardNumber;
		public string cardName;
		public string cardType;
		public string cardRarity;
		public string cardCode;
	}

	private HttpClient client;

    // Auto-Update Info
	private readonly string owner = "Gama-Tech";
	private readonly string repo = "AK-Card-Scanner"; 
	private readonly string currentVersion = "2.0.0";

    // Card Storage
	private XElement animalCards;
	private XElement strongCards;
	private XElement miracleCards;

    // Selected Cards
	private CardData selectedAnimalCard;
	private CardData selectedStrongCard;
	private CardData selectedMiracleCard;

    // Components
	private IContainer components;
	private TextBox textBox;
	private Button Submit;
	private ComboBox animalCardComboBox;
	private Button animalCardSubmitButton;
	private Button strongCardSubmitButton;
	private ComboBox strongCardComboBox;
	private Button miracleCardSubmitButton;
	private ComboBox miracleCardComboBox;
	private GroupBox groupBox1;
	private GroupBox groupBox2;
	private StatusStrip statusStrip1;
	private ToolStripStatusLabel statusStrip;
	private GroupBox groupBox3;
	private TextBox portTextBox;
	private Button testButton;
	private Label label1;
	private Button allCardSubmitButton;
	private ProgressBar progressBar1;
	private Timer timer1;

    private string serverUrl => $"http://127.0.0.1:{portTextBox.Text}/sys147/barcode";

    /// <summary>
    /// Checks if updates are available and gives the user the option to update if so.
    /// </summary>
    /// <returns></returns>
    public async Task CheckForUpdates()
    {
        string requestUri = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        statusStrip.Text = "Checking for updates...";

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
            client.Timeout = TimeSpan.FromSeconds(10);

            using var response = await client.GetAsync(requestUri);

            if (!response.IsSuccessStatusCode)
            {
                statusStrip.Text = $"Update check failed with status: {response.StatusCode}";
                return;
            }

            string value = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(value))
            {
                statusStrip.Text = "";
                return;
            }

            dynamic val = JsonConvert.DeserializeObject(value);
            string text = val.tag_name?.ToString() ?? string.Empty;

            // Find the installer exe in the release assets
            string downloadUrl = null;
            if (val.assets != null)
            {
                foreach (var asset in val.assets)
                {
                    string assetName = asset.name?.ToString() ?? string.Empty;
                    // Adjust this filter if a release could ever contain more than one .exe asset
                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.browser_download_url?.ToString();
                        break;
                    }
                }
            }

            if (currentVersion != text)
            {
                statusStrip.Text = "An update (v" + text + ") is available!";
                new UpdateForm(text, downloadUrl).ShowDialog();
            }
            else
            {
                statusStrip.Text = "Application is up to date!";
            }
        }
        catch (HttpRequestException ex)
        {
            // DNS resolution failures, connection refused, TLS errors, etc.
            statusStrip.Text = "Network error: Unable to check for updates.";
        }
        catch (TaskCanceledException)
        {
            // Timeouts and cancellations
            statusStrip.Text = "Update check timed out.";
        }
        catch (JsonException ex)
        {
            statusStrip.Text = "Failed to parse update information.";
        }
        catch (Exception ex)
        {
            statusStrip.Text = "An unexpected error occurred while checking for updates.";
        }
    }

    /// <summary>
    /// Searches for a card by name from a given stack and returns CardData.
    /// </summary>
    /// <param name="cardName">Name of the card</param>
    /// <param name="cardStack">Stack to search for the card</param>
    /// <returns></returns>
    public CardData GetCardInfoByName(string cardName, XElement cardStack)
	{
		CardData result = default(CardData);
		XElement xElement = cardStack.Descendants("card").FirstOrDefault((XElement c) => c.Element("name")?.Value == cardName);
		if (xElement != null)
		{
			result.cardNumber = xElement.Element("number")?.Value;
			result.cardName = xElement.Element("name")?.Value;
			result.cardType = xElement.Element("type")?.Value;
			result.cardRarity = xElement.Element("rarity")?.Value;
			result.cardCode = xElement.Element("code")?.Value;
		}
		return result;
	}

    /// <summary>
    /// Populate a given combobox with the contents of a given XML
    /// </summary>
    /// <param name="comboBox"></param>
    /// <param name="xml"></param>
	public void PopulateComboBoxFromXML(ComboBox comboBox, XElement xml)
	{
		List<string> list = new List<string>();
		foreach (XElement item in xml.Descendants("card"))
		{
			string cardName = item.Element("name")?.Value;
			if (cardName != null && !list.Any((string card) => string.Equals(cardName, card, StringComparison.OrdinalIgnoreCase)))
			{
				list.Add(cardName);
			}
		}
		list.Sort();
		foreach (string item2 in list)
		{
			comboBox.Items.Add(item2);
		}
	}

    /// <summary>
    /// Sends a given string to a given URL
    /// </summary>
    /// <param name="url">The URL of the server</param>
    /// <param name="data">The data to send</param>
    /// <returns></returns>
	private async Task SendDataToServerAsync(string url, string data)
	{
		try
		{
			StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
			HttpResponseMessage httpResponseMessage = await client.PostAsync(url, content);
			if (httpResponseMessage.IsSuccessStatusCode)
			{
				await httpResponseMessage.Content.ReadAsStringAsync();
			}
		}
		catch (Exception ex)
		{
            StopTimer();
            MessageBox.Show(this, "Could not connect to the Arcade I/O Server.\nPlease ensure the server is enabled in the emulator and the ports match. \n\nERROR:\n" + ex.Message, "Arcade I/O Server Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
	}

	private async Task PingServerAsync(string url, string data)
	{
		try
		{
			StringContent content = new StringContent(data, Encoding.UTF8, "application/json");
			if ((await client.PostAsync(url, content)).IsSuccessStatusCode)
			{
				statusStrip.Text = "Response received!";
                MessageBox.Show(this, "The Arcade I/O Server is set up correctly and responding!", "Arcade I/O Server Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
		}
		catch (Exception ex)
		{
			statusStrip.Text = "No response received!";
            MessageBox.Show(this, "Could not connect to the Arcade I/O Server.\nPlease ensure the server is enabled in the emulator and the ports match. \n\nERROR:\n" + ex.Message, "Arcade I/O Server Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);

		}
	}

	public MainForm()
	{
		InitializeComponent();
        
        // Load the card XML files and populate the combo boxes
		string cardDataFolder = Path.Combine(AppContext.BaseDirectory, "CardData");
		animalCards = XElement.Load(Path.Combine(cardDataFolder, "AnimalCards.xml"));
		strongCards = XElement.Load(Path.Combine(cardDataFolder, "StrongCards.xml"));
		miracleCards = XElement.Load(Path.Combine(cardDataFolder, "MiracleCards.xml"));
		PopulateComboBoxFromXML(animalCardComboBox, animalCards);
		PopulateComboBoxFromXML(strongCardComboBox, strongCards);
		PopulateComboBoxFromXML(miracleCardComboBox, miracleCards);
		client = new HttpClient(new HttpClientHandler{UseProxy = false});

        // Check for updates
        _ = CheckForUpdates();
	}

	private void Form1_Load(object sender, EventArgs e)
	{
        // Load the last used port
        portTextBox.Text = Properties.Settings.Default.DefaultPort.ToString();
    }

	private async void Submit_Click(object sender, EventArgs? e)
	{
		base.Enabled = false;
		string text = textBox.Text;
		textBox.Clear();
		textBox.Focus();
		statusStrip.Text = "Sent " + text;
		await SendDataToServerAsync(serverUrl, text);
		base.Enabled = true;
	}

	private void textBox_TextChanged(object sender, EventArgs e)
	{
		Submit.Enabled = textBox.Text.Length > 0;
	}

	private void animalCardComboBox_IndexChanged(object sender, EventArgs e)
	{
		animalCardSubmitButton.Enabled = animalCardComboBox.SelectedIndex != -1;
		selectedAnimalCard = GetCardInfoByName(animalCardComboBox.Text, animalCards);
		statusStrip.Text = $"Selected {selectedAnimalCard.cardRarity} {selectedAnimalCard.cardType} Card #{selectedAnimalCard.cardNumber}: {selectedAnimalCard.cardName}";
		allCardSubmitButton.Enabled = animalCardSubmitButton.Enabled && strongCardSubmitButton.Enabled && miracleCardSubmitButton.Enabled;
	}

	private void strongCardComboBox_IndexChanged(object sender, EventArgs e)
	{
		strongCardSubmitButton.Enabled = strongCardComboBox.SelectedIndex != -1;
		selectedStrongCard = GetCardInfoByName(strongCardComboBox.Text, strongCards);
		statusStrip.Text = $"Selected {selectedStrongCard.cardRarity} {selectedStrongCard.cardType} Card #{selectedStrongCard.cardNumber}: {selectedStrongCard.cardName}";
		allCardSubmitButton.Enabled = animalCardSubmitButton.Enabled && strongCardSubmitButton.Enabled && miracleCardSubmitButton.Enabled;
	}

	private void miracleCardComboBox_IndexChanged(object sender, EventArgs e)
	{
		miracleCardSubmitButton.Enabled = miracleCardComboBox.SelectedIndex != -1;
		selectedMiracleCard = GetCardInfoByName(miracleCardComboBox.Text, miracleCards);
		statusStrip.Text = $"Selected {selectedMiracleCard.cardRarity} {selectedMiracleCard.cardType} Card #{selectedMiracleCard.cardNumber}: {selectedMiracleCard.cardName}";
		allCardSubmitButton.Enabled = animalCardSubmitButton.Enabled && strongCardSubmitButton.Enabled && miracleCardSubmitButton.Enabled;
	}

	private async void animalCardSubmitButton_Click(object sender, EventArgs e)
	{
		if (selectedAnimalCard.cardName != null)
		{
			base.Enabled = false;
			await SendDataToServerAsync(serverUrl, selectedAnimalCard.cardCode);
			statusStrip.Text = $"Sent {selectedAnimalCard.cardRarity} {selectedAnimalCard.cardType} Card: {selectedAnimalCard.cardName}!";
		}
		else
		{
			statusStrip.Text = "Invalid animal card selected!";
		}
		base.Enabled = true;
	}

	private async void strongCardSubmitButton_Click(object sender, EventArgs e)
	{
		if (selectedStrongCard.cardName != null)
		{
			base.Enabled = false;
			await SendDataToServerAsync(serverUrl, selectedStrongCard.cardCode);
			statusStrip.Text = $"Sent {selectedStrongCard.cardRarity} {selectedStrongCard.cardType} Card: {selectedStrongCard.cardName}!";
		}
		else
		{
			statusStrip.Text = "Invalid strong card selected!";
		}
		base.Enabled = true;
	}

	private async void miracleCardSubmitButton_Click(object sender, EventArgs e)
	{
		if (selectedMiracleCard.cardName != null)
		{
			base.Enabled = false;
			await SendDataToServerAsync(serverUrl, selectedMiracleCard.cardCode);
			statusStrip.Text = $"Sent {selectedMiracleCard.cardRarity} {selectedMiracleCard.cardType} Card: {selectedMiracleCard.cardName}!";
		}
		else
		{
			statusStrip.Text = "Invalid miracle card selected!";
		}
		base.Enabled = true;
	}

	private async void testButton_Click(object sender, EventArgs e)
	{
		base.Enabled = false;
		statusStrip.Text = "Pinging Play! emulator...";
		await PingServerAsync(serverUrl, "");
		base.Enabled = true;
	}

    /// <summary>
    /// Triggered when the Port is changed.
    /// Strips non-digit characters from input.
    /// </summary>
    private void portTextBox_TextChanged(object sender, EventArgs e)
    {

        string digitsOnly = new string(portTextBox.Text.Where(char.IsDigit).ToArray());
        if (portTextBox.Text != digitsOnly)
        {
            int selectionStart = portTextBox.SelectionStart;
            int removedChars = portTextBox.Text.Length - digitsOnly.Length;
            portTextBox.Text = digitsOnly;
            portTextBox.SelectionStart = Math.Max(0, selectionStart - removedChars);
        }

        testButton.Enabled = portTextBox.Text.Length > 0;
    }

    /// <summary>
    /// If 3 cards are selected, start a timer and send them sequentially
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void allCardSubmitButton_Click(object sender, EventArgs e)
	{
        // Safety check
        if (selectedAnimalCard.cardName == null || selectedStrongCard.cardName == null || selectedMiracleCard.cardName == null || progressBar1.Value > 0)
            return;

        // Swap button with progress bar
		base.Enabled = false;
        progressBar1.Visible = true;
        timer1.Start();
    }

    // The timer ticks every 100ms (10 ticks per second).
    // Each tick increases the progress bar by 2/100 (20 per second).
    // When sending the Animal Card, the game shows a brief animation which prevents further cards from being scanned.
    // To account for this, we wait 4 seconds before continuing to send the Strong Card.
	private void timer1_Tick(object sender, EventArgs e)
	{
        // Send Animal Card instantly
		if (progressBar1.Value == 0 && selectedAnimalCard.cardName != null)
		{
			SendDataToServerAsync(serverUrl, selectedAnimalCard.cardCode);
			statusStrip.Text = $"Sent {selectedAnimalCard.cardRarity} {selectedAnimalCard.cardType} Card: {selectedAnimalCard.cardName}!";
		}
        // Send Strong Card after 4 seconds
		if (progressBar1.Value == 80 && selectedStrongCard.cardName != null)
		{
			SendDataToServerAsync(serverUrl, selectedStrongCard.cardCode);
			statusStrip.Text = $"Sent {selectedStrongCard.cardRarity} {selectedStrongCard.cardType} Card: {selectedStrongCard.cardName}!";
		}
        // Send Strong Card after 5 seconds
        if (progressBar1.Value == 100 && selectedMiracleCard.cardName != null)
		{
			SendDataToServerAsync(serverUrl, selectedMiracleCard.cardCode);
			statusStrip.Text = $"Sent {selectedMiracleCard.cardRarity} {selectedMiracleCard.cardType} Card: {selectedMiracleCard.cardName}!";
		}

        // Increment the progress bar if below 100, stop the timer otherwise.
        if (progressBar1.Value < 100)
		{
			progressBar1.Value += 2;
			return;
		}
        StopTimer();
    }

    /// <summary>
    /// Stops the timer and resets the progress bar, status strip & form accessibility.
    /// </summary>
    private void StopTimer()
    {
        timer1.Stop();
		progressBar1.Visible = false;
		progressBar1.Value = 0;
		statusStrip.Text = "";
		base.Enabled = true;
    }


    // Detect Keys.Return in the Custom Barcode box.
    // This is for easier entry, but also allows the use of USB barcode scanners that emulate keyboard inputs
    private void textBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Return)
		{
			if (textBox.Text.Length > 0)
			{
				Submit_Click(this, null);
			}
			e.Handled = true;
			e.SuppressKeyPress = true;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

    private void InitializeComponent()
    {
        components = new Container();
        ComponentResourceManager resources = new ComponentResourceManager(typeof(MainForm));
        textBox = new TextBox();
        Submit = new Button();
        animalCardComboBox = new ComboBox();
        animalCardSubmitButton = new Button();
        strongCardSubmitButton = new Button();
        strongCardComboBox = new ComboBox();
        miracleCardSubmitButton = new Button();
        miracleCardComboBox = new ComboBox();
        groupBox1 = new GroupBox();
        progressBar1 = new ProgressBar();
        label1 = new Label();
        allCardSubmitButton = new Button();
        groupBox2 = new GroupBox();
        statusStrip1 = new StatusStrip();
        statusStrip = new ToolStripStatusLabel();
        groupBox3 = new GroupBox();
        testButton = new Button();
        portTextBox = new TextBox();
        timer1 = new Timer(components);
        groupBox1.SuspendLayout();
        groupBox2.SuspendLayout();
        statusStrip1.SuspendLayout();
        groupBox3.SuspendLayout();
        SuspendLayout();
        // 
        // textBox
        // 
        textBox.Location = new Point(6, 22);
        textBox.MaxLength = 8;
        textBox.Name = "textBox";
        textBox.PlaceholderText = "Enter a custom barcode to send";
        textBox.Size = new Size(333, 23);
        textBox.TabIndex = 0;
        textBox.TextChanged += textBox_TextChanged;
        textBox.KeyDown += textBox_KeyDown;
        // 
        // Submit
        // 
        Submit.Enabled = false;
        Submit.Location = new Point(345, 22);
        Submit.Name = "Submit";
        Submit.Size = new Size(75, 23);
        Submit.TabIndex = 1;
        Submit.Text = "Scan Card";
        Submit.UseVisualStyleBackColor = true;
        Submit.Click += Submit_Click;
        // 
        // animalCardComboBox
        // 
        animalCardComboBox.FormattingEnabled = true;
        animalCardComboBox.Location = new Point(6, 22);
        animalCardComboBox.Name = "animalCardComboBox";
        animalCardComboBox.Size = new Size(333, 23);
        animalCardComboBox.TabIndex = 2;
        animalCardComboBox.Text = "Animal card";
        animalCardComboBox.SelectedIndexChanged += animalCardComboBox_IndexChanged;
        // 
        // animalCardSubmitButton
        // 
        animalCardSubmitButton.Enabled = false;
        animalCardSubmitButton.Location = new Point(345, 22);
        animalCardSubmitButton.Name = "animalCardSubmitButton";
        animalCardSubmitButton.Size = new Size(75, 23);
        animalCardSubmitButton.TabIndex = 3;
        animalCardSubmitButton.Text = "Scan Card";
        animalCardSubmitButton.UseVisualStyleBackColor = true;
        animalCardSubmitButton.Click += animalCardSubmitButton_Click;
        // 
        // strongCardSubmitButton
        // 
        strongCardSubmitButton.Enabled = false;
        strongCardSubmitButton.Location = new Point(345, 51);
        strongCardSubmitButton.Name = "strongCardSubmitButton";
        strongCardSubmitButton.Size = new Size(75, 23);
        strongCardSubmitButton.TabIndex = 5;
        strongCardSubmitButton.Text = "Scan Card";
        strongCardSubmitButton.UseVisualStyleBackColor = true;
        strongCardSubmitButton.Click += strongCardSubmitButton_Click;
        // 
        // strongCardComboBox
        // 
        strongCardComboBox.FormattingEnabled = true;
        strongCardComboBox.Location = new Point(6, 51);
        strongCardComboBox.Name = "strongCardComboBox";
        strongCardComboBox.Size = new Size(333, 23);
        strongCardComboBox.TabIndex = 4;
        strongCardComboBox.Text = "Strong card";
        strongCardComboBox.SelectedIndexChanged += strongCardComboBox_IndexChanged;
        // 
        // miracleCardSubmitButton
        // 
        miracleCardSubmitButton.Enabled = false;
        miracleCardSubmitButton.Location = new Point(345, 80);
        miracleCardSubmitButton.Name = "miracleCardSubmitButton";
        miracleCardSubmitButton.Size = new Size(75, 23);
        miracleCardSubmitButton.TabIndex = 7;
        miracleCardSubmitButton.Text = "Scan Card";
        miracleCardSubmitButton.UseVisualStyleBackColor = true;
        miracleCardSubmitButton.Click += miracleCardSubmitButton_Click;
        // 
        // miracleCardComboBox
        // 
        miracleCardComboBox.FormattingEnabled = true;
        miracleCardComboBox.Location = new Point(6, 80);
        miracleCardComboBox.Name = "miracleCardComboBox";
        miracleCardComboBox.Size = new Size(333, 23);
        miracleCardComboBox.TabIndex = 6;
        miracleCardComboBox.Text = "Miracle Card";
        miracleCardComboBox.SelectedIndexChanged += miracleCardComboBox_IndexChanged;
        // 
        // groupBox1
        // 
        groupBox1.Anchor = AnchorStyles.Top;
        groupBox1.Controls.Add(progressBar1);
        groupBox1.Controls.Add(label1);
        groupBox1.Controls.Add(allCardSubmitButton);
        groupBox1.Controls.Add(miracleCardComboBox);
        groupBox1.Controls.Add(animalCardSubmitButton);
        groupBox1.Controls.Add(strongCardSubmitButton);
        groupBox1.Controls.Add(animalCardComboBox);
        groupBox1.Controls.Add(miracleCardSubmitButton);
        groupBox1.Controls.Add(strongCardComboBox);
        groupBox1.Location = new Point(61, 12);
        groupBox1.Name = "groupBox1";
        groupBox1.Size = new Size(425, 143);
        groupBox1.TabIndex = 8;
        groupBox1.TabStop = false;
        groupBox1.Text = "Cards";
        // 
        // progressBar1
        // 
        progressBar1.Location = new Point(345, 109);
        progressBar1.Name = "progressBar1";
        progressBar1.Size = new Size(75, 23);
        progressBar1.Step = 1;
        progressBar1.TabIndex = 11;
        progressBar1.Visible = false;
        // 
        // label1
        // 
        label1.Font = new Font("Segoe UI", 9F);
        label1.Location = new Point(6, 109);
        label1.Name = "label1";
        label1.Size = new Size(333, 23);
        label1.TabIndex = 9;
        label1.Text = "If you have 3 cards selected you can send them all at once   ▶";
        label1.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // allCardSubmitButton
        // 
        allCardSubmitButton.Enabled = false;
        allCardSubmitButton.Location = new Point(345, 109);
        allCardSubmitButton.Name = "allCardSubmitButton";
        allCardSubmitButton.Size = new Size(75, 23);
        allCardSubmitButton.TabIndex = 8;
        allCardSubmitButton.Text = "Scan All";
        allCardSubmitButton.UseVisualStyleBackColor = true;
        allCardSubmitButton.Click += allCardSubmitButton_Click;
        // 
        // groupBox2
        // 
        groupBox2.Anchor = AnchorStyles.Top;
        groupBox2.Controls.Add(textBox);
        groupBox2.Controls.Add(Submit);
        groupBox2.Location = new Point(61, 161);
        groupBox2.Name = "groupBox2";
        groupBox2.Size = new Size(425, 55);
        groupBox2.TabIndex = 9;
        groupBox2.TabStop = false;
        groupBox2.Text = "Custom Barcode";
        // 
        // statusStrip1
        // 
        statusStrip1.Items.AddRange(new ToolStripItem[] { statusStrip });
        statusStrip1.Location = new Point(0, 294);
        statusStrip1.Name = "statusStrip1";
        statusStrip1.Size = new Size(547, 22);
        statusStrip1.SizingGrip = false;
        statusStrip1.TabIndex = 10;
        statusStrip1.Text = "statusStrip1";
        // 
        // statusStrip
        // 
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(0, 17);
        // 
        // groupBox3
        // 
        groupBox3.Anchor = AnchorStyles.Top;
        groupBox3.Controls.Add(testButton);
        groupBox3.Controls.Add(portTextBox);
        groupBox3.Location = new Point(61, 222);
        groupBox3.Name = "groupBox3";
        groupBox3.Size = new Size(159, 55);
        groupBox3.TabIndex = 10;
        groupBox3.TabStop = false;
        groupBox3.Text = "Server Port";
        // 
        // testButton
        // 
        testButton.Location = new Point(77, 22);
        testButton.Name = "testButton";
        testButton.Size = new Size(75, 23);
        testButton.TabIndex = 2;
        testButton.Text = "Test Port";
        testButton.UseVisualStyleBackColor = true;
        testButton.Click += testButton_Click;
        // 
        // portTextBox
        // 
        portTextBox.Location = new Point(6, 22);
        portTextBox.MaxLength = 5;
        portTextBox.Name = "portTextBox";
        portTextBox.PlaceholderText = "Port #";
        portTextBox.Size = new Size(65, 23);
        portTextBox.TabIndex = 0;
        portTextBox.Text = "9876";
        portTextBox.TextChanged += portTextBox_TextChanged;
        // 
        // timer1
        // 
        timer1.Tick += timer1_Tick;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = SystemColors.Control;
        ClientSize = new Size(547, 316);
        Controls.Add(groupBox3);
        Controls.Add(statusStrip1);
        Controls.Add(groupBox2);
        Controls.Add(groupBox1);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        Name = "MainForm";
        Text = "AK Card Scanner";
        FormClosed += MainFormClosed;
        Load += Form1_Load;
        groupBox1.ResumeLayout(false);
        groupBox2.ResumeLayout(false);
        groupBox2.PerformLayout();
        statusStrip1.ResumeLayout(false);
        statusStrip1.PerformLayout();
        groupBox3.ResumeLayout(false);
        groupBox3.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private void MainFormClosed(object sender, FormClosedEventArgs e)
    {
        if (int.TryParse(portTextBox.Text, out int port))
            Properties.Settings.Default.DefaultPort = port;

        Properties.Settings.Default.Save();
    }
}

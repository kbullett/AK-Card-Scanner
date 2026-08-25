using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AKCardScanner;

public class UpdateForm : Form
{
    private IContainer components;

    private Button button1;

    private Button button2;
    private ProgressBar downloadProgressBar;
    private Label label1;

    private string _downloadUrl;

    public UpdateForm()
    {
        InitializeComponent();
    }

    /// <summary>Creates the update dialog pre-populated with the new version and the installer's download URL.</summary>
    public UpdateForm(string newVersion, string downloadUrl) : this()
    {
        _downloadUrl = downloadUrl;
        if (!string.IsNullOrEmpty(newVersion))
            label1.Text = $"Version {newVersion} is available!";
    }

    private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e)
    {
    }

    private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
    {
    }

    private void button2_Click(object sender, EventArgs e)
    {
        Close();
    }

    private async void button1_Click(object sender, EventArgs e)
    {
        var modifiedFiles = CardDataIntegrityChecker.GetModifiedFiles();
        if (modifiedFiles.Count > 0)
        {
            var result = MessageBox.Show(this,
                "The following card data file(s) appear to have been edited:\n\n" +
                string.Join("\n", modifiedFiles) +
                "\n\nInstalling this update will overwrite them with the default versions. " +
                "Back up your changes first if you want to keep them.\n\nContinue anyway?",
                "Custom Card Data Detected",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.No)
            {
                return;
            }
        }

        if (string.IsNullOrEmpty(_downloadUrl))
        {
            MessageBox.Show(this, "No installer was found for the latest release.\nThe \"Releases\" page will be opened instead.", 
                "Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Gama-Tech/AK-Card-Scanner/releases",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,"Failed to open the GitHub releases page: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
            Close();
            return;
        }

        // Swap the buttons out for the progress bar (they share the same layout position).
        button1.Visible = false;
        button2.Visible = false;
        downloadProgressBar.Visible = true;
        downloadProgressBar.Value = 0;
        label1.Text = "Downloading update...";

        try
        {
            var downloader = new UpdateDownloader();
            var progress = new Progress<int>(percent =>
            {
                downloadProgressBar.Value = Math.Min(percent, 100);
                label1.Text = $"Downloading update... {percent}%";
            });

            string installerPath = await downloader.DownloadInstallerAsync(_downloadUrl, progress);

            // No percentage is available for the silent install step itself,
            // so switch to an indeterminate (marquee) progress bar.
            label1.Text = "Installing update...";
            downloadProgressBar.Style = ProgressBarStyle.Marquee;
            downloadProgressBar.MarqueeAnimationSpeed = 30;

            // Runs the installer with /S, waits for it to finish, then relaunches
            // this app from the same path it's currently running from. The app
            // exits inside this call once the install completes.
            await downloader.RunSilentInstallerAndRelaunchAsync(installerPath, Application.ExecutablePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to download the update: {ex.Message}\n\nYou can download it manually from the releases page instead.",
                "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

            // Restore the buttons so the user can retry or cancel.
            downloadProgressBar.Visible = false;
            button1.Visible = true;
            button2.Visible = true;
            label1.Text = "An update is available!";
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
        ComponentResourceManager resources = new ComponentResourceManager(typeof(UpdateForm));
        button1 = new Button();
        button2 = new Button();
        label1 = new Label();
        downloadProgressBar = new ProgressBar();
        SuspendLayout();
        // 
        // button1
        // 
        button1.Location = new Point(142, 40);
        button1.Name = "button1";
        button1.Size = new Size(104, 30);
        button1.TabIndex = 1;
        button1.Text = "Download";
        button1.UseVisualStyleBackColor = true;
        button1.Click += button1_Click;
        // 
        // button2
        // 
        button2.Location = new Point(12, 40);
        button2.Name = "button2";
        button2.Size = new Size(104, 30);
        button2.TabIndex = 2;
        button2.Text = "Cancel";
        button2.UseVisualStyleBackColor = true;
        button2.Click += button2_Click;
        // 
        // label1
        // 
        label1.Location = new Point(12, 9);
        label1.Name = "label1";
        label1.Size = new Size(234, 19);
        label1.TabIndex = 3;
        label1.Text = "An update is available!";
        label1.TextAlign = ContentAlignment.TopCenter;
        // 
        // downloadProgressBar
        // 
        downloadProgressBar.Location = new Point(12, 40);
        downloadProgressBar.Name = "downloadProgressBar";
        downloadProgressBar.Size = new Size(234, 30);
        downloadProgressBar.Step = 1;
        downloadProgressBar.TabIndex = 12;
        downloadProgressBar.Visible = false;
        // 
        // UpdateForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(258, 82);
        ControlBox = false;
        Controls.Add(downloadProgressBar);
        Controls.Add(label1);
        Controls.Add(button2);
        Controls.Add(button1);
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "UpdateForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Update Available!";
        ResumeLayout(false);
    }

}
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace PiratedLauncher
{
    public partial class Download : Form
    {
        private string currentProcess = null;
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public Game GameTo_Download { get; set; }
        private FileDownloader downloader = new FileDownloader();
        private FileExtractor extractor = new FileExtractor();

        public Download()
        {
            InitializeComponent();
        }

        public static bool IsValidUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (Uri.TryCreate(input, UriKind.Absolute, out Uri uriResult))
                return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;

            return false;
        }

        private async void Download_LoadAsync(object sender, EventArgs e)
        {
            string key1 = Settings.inputtedFirstKey;
            string key2 = Settings.inputtedSecondKey;
            string downloadUrl = GameTo_Download.DownloadUrl + $"?key1={key1}&key2={key2}";

            if (!IsValidUrl(downloadUrl))
            {
                MessageBox.Show("Invalid download URL.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (var wc = new WebClient())
                {
                    string jsonResponse = wc.DownloadString(downloadUrl);
                    JObject json = JObject.Parse(jsonResponse);
                    string actualUrl = json["result"]?.Value<string>();

                    if (!IsValidUrl(actualUrl))
                    {
                        MessageBox.Show("Invalid file URL received.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    Progress<(float progress, string speed)> progress = new Progress<(float progress, string speed)>(report =>
                    {
                        progressBar1.Value = (int)Math.Round(report.progress * 100);
                        progressLabel.Text = $"{(report.progress * 100):F2}% at {report.speed}";
                    });

                    // Start the file download with speed limit (e.g., 1 MB/s)
                    currentProcess = "downloading";
                    var (isSuccess, outputFileName) = await downloader.DownloadFileAsync(actualUrl, progress, speedLimitBytesPerSecond: 1024 * 1024);

                    if (isSuccess)
                    {
                        // Handle extraction if the file is an archive
                        if (outputFileName.EndsWith(".rar") || outputFileName.EndsWith(".zip") || outputFileName.EndsWith(".7z"))
                        {
                            currentProcess = "extracting";
                            statusLabel.Text = "Extracting...";

                            await Task.Run(() =>
                            {
                                if (extractor.ExtractArchive(outputFileName, GameTo_Download.Name + GameTo_Download.GameVersion))
                                {
                                    System.IO.File.Delete(outputFileName);
                                }
                            });
                        }

                        // Download and extract crack file if available
                        if (!string.IsNullOrEmpty(GameTo_Download.CrackUrl))
                        {
                            var (isSuccess2, outputFileName2) = await downloader.DownloadFileAsync(GameTo_Download.CrackUrl, null, speedLimitBytesPerSecond: 512 * 1024); // Crack download at 512 KB/s

                            if (isSuccess2 && (outputFileName2.EndsWith(".rar") || outputFileName2.EndsWith(".zip") || outputFileName2.EndsWith(".7z")))
                            {
                                await Task.Run(() =>
                                {
                                    if (extractor.ExtractArchive(outputFileName2, GameTo_Download.Name + "Crack"))
                                    {
                                        System.IO.File.Delete(outputFileName2);
                                    }
                                });
                            }
                        }
                        currentProcess = null;
                        MessageBox.Show("Successfully downloaded and extracted everything. Check the current folder.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // Reset settings
                        Settings.inputtedFirstKey = string.Empty;
                        Settings.inputtedSecondKey = string.Empty;
                        Settings.whichKey = 1;

                        // Show the main form
                        var mainForm = new Updater();
                        mainForm.Closed += (closedSender, closedEventArgs) => this.Close();
                        mainForm.Show();

                        this.Hide();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Download Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            if (currentProcess == null)
            {
                Environment.Exit(0);
            }
            DialogResult result = MessageBox.Show(
            $"Do you want to proceed? The game is still {currentProcess}.",
            "Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                downloader.PauseDownload();
                Task.Delay(500);
                Environment.Exit(0);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void pauseButton_Click(object sender, EventArgs e)
        {
            if (downloader != null)
            {
                if (downloader.IsPaused)
                {
                    downloader.ResumeDownload();
                    pauseButton.Text = "Pause";
                    statusLabel.Text = "Downloading";
                }
                else
                {
                    downloader.PauseDownload();
                    pauseButton.Text = "Resume";
                    statusLabel.Text = "Paused";
                }
            }
        }

        private void progressLabel_Click(object sender, EventArgs e)
        {

        }
    }
}

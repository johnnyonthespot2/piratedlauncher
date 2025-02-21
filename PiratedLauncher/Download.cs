using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace PiratedLauncher
{
    public partial class Download : Form
    {
        private Query query = new Query();
        private ProcessState currentProcess = ProcessState.None;
        private FileDownloader downloader = new FileDownloader();
        private FileExtractor extractor = new FileExtractor();

        public Game GameTo_Download { get; set; }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public Download()
        {
            InitializeComponent();
            query.Initialize();
        }

        public enum ProcessState
        {
            None,
            Downloading,
            Extracting,
            Completed
        }

        private async void Download_LoadAsync(object sender, EventArgs e)
        {
            try
            {
                string downloadUrl = $"{GameTo_Download.DownloadUrl}?key1={Settings.inputtedFirstKey}&key2={Settings.inputtedSecondKey}";

                if (!IsValidUrl(downloadUrl))
                {
                    ShowError("Invalid download URL.");
                    return;
                }

                string jsonResponse = await query.FetchDataAsync(downloadUrl);
                string actualUrl = JObject.Parse(jsonResponse)["result"]?.Value<string>();

                if (!IsValidUrl(actualUrl))
                {
                    ShowError("Invalid file URL received.");
                    return;
                }

                var progress = new Progress<(float progress, string speed)>(report =>
                {
                    progressBar1.Value = (int)(report.progress * 100);
                    progressLabel.Text = $"{report.progress * 100:F2}% at {report.speed}";
                });

                currentProcess = ProcessState.Downloading;
                var (isSuccess, outputFileName) = await downloader.DownloadFileAsync(actualUrl, progress, speedLimitBytesPerSecond: 1024 * 1024);

                if (isSuccess)
                {
                    await HandleExtraction(outputFileName);
                    await HandleCrackDownload();

                    MessageBox.Show("Download and extraction complete.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ResetSettingsAndShowMainForm();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
        }

        private async Task HandleExtraction(string filePath)
        {
            if (filePath.EndsWith(".rar") || filePath.EndsWith(".zip") || filePath.EndsWith(".7z"))
            {
                currentProcess = ProcessState.Extracting;
                statusLabel.Text = "Extracting...";

                var progress = new Progress<(float progress, string status)>(report =>
                {
                    if (progressBar1.InvokeRequired)
                    {
                        progressBar1.Invoke(new Action(() =>
                        {
                            progressBar1.Value = (int)(report.progress * 100);
                            progressLabel.Text = $"{report.progress * 100:F2}% - {report.status}";
                        }));
                    }
                    else
                    {
                        progressBar1.Value = (int)(report.progress * 100);
                        progressLabel.Text = $"{report.progress * 100:F2}% - {report.status}";
                    }
                });

                bool success = await Task.Run(() => extractor.ExtractArchive(filePath, Path.Combine(Directory.GetCurrentDirectory(), GameTo_Download.Name + GameTo_Download.GameVersion), progress));

                if (success)
                {
                    File.Delete(filePath);
                    statusLabel.Text = "Extraction complete";
                    progressLabel.Text = "100% - Done";
                }
                else
                {
                    MessageBox.Show("There was an error extracting the file.\nTry extracting manually with 7zip.\nExiting.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task HandleCrackDownload()
        {
            if (!string.IsNullOrEmpty(GameTo_Download.CrackUrl))
            {
                var (isSuccess, crackFileName) = await downloader.DownloadFileAsync(GameTo_Download.CrackUrl, null, speedLimitBytesPerSecond: 512 * 1024);

                if (isSuccess && (crackFileName.EndsWith(".rar") || crackFileName.EndsWith(".zip") || crackFileName.EndsWith(".7z")))
                {
                    currentProcess = ProcessState.Extracting;
                    statusLabel.Text = "Extracting crack...";

                    var progress = new Progress<(float progress, string status)>(report =>
                    {
                        if (progressBar1.InvokeRequired)
                        {
                            progressBar1.Invoke(new Action(() =>
                            {
                                progressBar1.Value = (int)(report.progress * 100);
                                progressLabel.Text = $"{report.progress * 100:F2}% - {report.status}";
                            }));
                        }
                        else
                        {
                            progressBar1.Value = (int)(report.progress * 100);
                            progressLabel.Text = $"{report.progress * 100:F2}% - {report.status}";
                        }
                    });

                    bool success = await Task.Run(() => extractor.ExtractArchive(crackFileName, Path.Combine(Directory.GetCurrentDirectory(), GameTo_Download.Name + "Crack"), progress));

                    if (success)
                    {
                        File.Delete(crackFileName);
                        statusLabel.Text = "Crack extraction complete";
                    }
                }
            }
        }

        private void ResetSettingsAndShowMainForm()
        {
            currentProcess = ProcessState.Completed;

            Settings.inputtedFirstKey = string.Empty;
            Settings.inputtedSecondKey = string.Empty;
            Settings.whichKey = 1;

            var mainForm = new Updater();
            mainForm.Closed += (closedSender, closedEventArgs) => this.Close();
            mainForm.Show();
            this.Hide();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool IsValidUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            return Uri.TryCreate(input, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (currentProcess == ProcessState.None)
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
            if (currentProcess == ProcessState.Downloading && downloader != null)
            {
                if (downloader.IsPaused)
                {
                    downloader.ResumeDownload();
                    pauseButton.Text = "Pause";
                    statusLabel.Text = "Downloading...";
                }
                else
                {
                    downloader.PauseDownload();
                    pauseButton.Text = "Resume";
                    statusLabel.Text = "Paused";
                }
            }
            else if (currentProcess == ProcessState.Extracting && extractor != null)
            {
                if (extractor.IsPaused)
                {
                    extractor.ResumeExtraction();
                    pauseButton.Text = "Pause";
                    statusLabel.Text = "Extracting...";
                }
                else
                {
                    extractor.PauseExtraction();
                    pauseButton.Text = "Resume";
                    statusLabel.Text = "Paused";
                }
            }
        }

        private void progressLabel_Click(object sender, EventArgs e) { }
    }
}
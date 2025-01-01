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
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public Game GameTo_Download { get; set; }

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
            string downloadUrl = GameTo_Download.DownloadUrl + $"?key1={key1}" + "&" + $"key2={key2}";

            if (IsValidUrl(downloadUrl))
            {
                WebClient wc = new WebClient();
                string jsonResponse = wc.DownloadString(downloadUrl);

                JObject json = JObject.Parse(jsonResponse);

                string actualUrl = json["result"].Value<string>();

                if (IsValidUrl(actualUrl))
                {
                    FileDownloader downloader = new FileDownloader();
                    FileExtractor extractor = new FileExtractor();

                    Progress<float> progress = new Progress<float>(percent =>
                    {
                        progressBar1.Value = (int)Math.Round(percent * 100);
                        progressLabel.Text = $"{(percent * 100):F2}% / 100%";
                    });

                    try
                    {
                        var (isSuccess, outputFileName) = await downloader.DownloadFileAsync(actualUrl, progress);
                        if (isSuccess)
                        {

                            if(outputFileName.Contains(".rar") | outputFileName.Contains(".zip") | outputFileName.Contains(".7z"))
                            {
                                progressLabel.Text = "Extracting...";

                                Thread thread = new Thread(() =>
                                {
                                    if (extractor.ExtractArchive(outputFileName, GameTo_Download.Name + GameTo_Download.GameVersion))
                                        System.IO.File.Delete(outputFileName);
                                });
                                thread.Start();
                            }

                            if (GameTo_Download.CrackUrl != string.Empty)
                            {
                                var (isSuccess2, outputFileName2) = await downloader.DownloadFileAsync(GameTo_Download.CrackUrl, null);
                                if (isSuccess2)
                                {
                                    if (outputFileName2.Contains(".rar") | outputFileName2.Contains(".zip") | outputFileName2.Contains(".7z"))
                                    {
                                        Thread thread2 = new Thread(() =>
                                        {
                                            if (extractor.ExtractArchive(outputFileName2, GameTo_Download.Name + "Crack"))
                                                System.IO.File.Delete(outputFileName2);
                                        });
                                        thread2.Start();
                                    }
                                }

                                MessageBox.Show("Successfully downloaded and extracted everything. Check the current folder.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                Settings.inputtedFirstKey = string.Empty;
                                Settings.inputtedSecondKey = string.Empty;
                                Settings.whichKey = 1;

                                var mainForm = new Updater();

                                mainForm.Closed += (closedSender, closedEventArgs) => this.Close();

                                mainForm.Show();

                                this.Hide();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        MessageBox.Show("Download canceled.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Download failed: {ex.Message}");
                    }
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
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
    }
}

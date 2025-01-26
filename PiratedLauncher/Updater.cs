using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace PiratedLauncher
{
    public partial class Updater : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        private Query query = new Query();

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();
       

        public Updater()
        {
            InitializeComponent();

            query.Initialize();
        }

        private async Task AddGamePanelToParentAsync(Panel parentPanel, Game game)
        {
            Panel gamePanel = await CreateGamePanelAsync(game);

            parentPanel.Controls.Add(gamePanel);
        }

        private async void Updater_Load(object sender, EventArgs e)
        {
            if(!Directory.Exists("x86") | !Directory.Exists("x64"))
            {
                MessageBox.Show("The x86 and x64 folders are missing.\nMake sure you extract everything from the launcher to a dedicated folder.\nExiting.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            if(Assembly.GetExecutingAssembly().Location.Contains(Environment.GetEnvironmentVariable("TEMP")))
            {
                MessageBox.Show("You are running the launcher without extracting.\nPlease extract it to a dedicated folder and try again.\nExiting.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            string apiVersion = string.Empty;
            string jsonResponse = string.Empty; //JObject does not like unassigned variables

            try
            {
                apiVersion = await query.FetchDataAsync(" launcher ver link here ");
                jsonResponse = await query.FetchDataAsync(" launcher api json here ");
            }
            catch (Exception ex)
            {
                Clipboard.SetText(ex.Message);
                MessageBox.Show("There was an error connecting to the API. Make sure you got an internet connection.\nTry a VPN before reporting this error on the discord server.\nExiting.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            try
            {
                Version currentVersion = new Version(Settings.launcherVersion);
                Version fetchedVersion = new Version(apiVersion.Trim());

                if (fetchedVersion > currentVersion)
                {
                    MessageBox.Show("There is a new launcher version available to download.\nCheck the discord!\nExiting.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Clipboard.SetText(ex.Message);
                MessageBox.Show("There was an error processing the version check. Error copied to clipboard, report this error on the discord.\nExiting.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            JObject deserializedObject = JObject.Parse(jsonResponse);

            List<Game> games = new List<Game>();

            foreach (var gameJson in deserializedObject["games"])
            {
                Game game = new Game
                {
                    Name = gameJson["name"].ToString(),
                    GameVersion = gameJson["version"].ToString(),
                    GameSize = gameJson["size"].ToString(),
                    Step1Url = gameJson["key_links"]["step1"].ToString(),
                    Step2Url = gameJson["key_links"]["step2"].ToString(),
                    DownloadUrl = gameJson["download_link"].ToString(),
                    ImageUrl = gameJson["image_url"].ToString(),
                    CrackUrl = gameJson["crack_link"].ToString()
                };
                games.Add(game);
            }

            flowLayoutPanel1.AutoScroll = true;
            flowLayoutPanel1.SuspendLayout();

            foreach (var game in games)
            {
                await AddGamePanelToParentAsync(flowLayoutPanel1, game);
            }

            flowLayoutPanel1.ResumeLayout();
        }

        private async Task<Panel> CreateGamePanelAsync(Game game)
        {
            Panel panel = new Panel
            {
                Width = 350,
                Height = 160,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(30, 30, 30),
                Margin = new Padding(10),
                Padding = new Padding(10)
            };

            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.None
            };
            panel.Controls.Add(contentPanel);

            PictureBox pictureBox = new PictureBox
            {
                Width = 120,
                Height = 120,
                Left = 10,
                Top = 10,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent
            };

            try
            {
                pictureBox.Image = await LoadImageFromUrlAsync(game.ImageUrl);
            }
            catch
            {
                pictureBox.Image = SystemIcons.Warning.ToBitmap();
            }
            contentPanel.Controls.Add(pictureBox);

            // Create individual labels instead of using FlowLayoutPanel
            Label nameLabel = new Label
            {
                Text = game.Name,
                Left = 140,
                Top = 10,
                MaximumSize = new Size(190, 0),
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            contentPanel.Controls.Add(nameLabel);

            Label versionLabel = new Label
            {
                Text = $"Version: {game.GameVersion}",
                Left = 140,
                Top = nameLabel.Bottom + 5,
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent
            };
            contentPanel.Controls.Add(versionLabel);

            Label gameSizeLabel = new Label
            {
                Text = $"Size: {game.GameSize}",
                Left = 140,
                Top = versionLabel.Bottom + 5,
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.Transparent
            };
            contentPanel.Controls.Add(gameSizeLabel);

            Button downloadButton = new Button
            {
                Text = "Download",
                Width = 150,
                Height = 35,
                Left = 179,
                Top = 107,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            downloadButton.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            downloadButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
            downloadButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 50);

            downloadButton.Click += (sender, eventArgs) =>
            {
                var key1 = new Key1
                {
                    GameName = game.Name,
                    GameToDownload = game
                };

                key1.Closed += (closedSender, closedEventArgs) => this.Close();

                key1.Show();

                this.Hide();
            };
            contentPanel.Controls.Add(downloadButton);

            return panel;
        }

        private async Task<Image> LoadImageFromUrlAsync(string url)
        {
            try
            {
                using (var response = await query.HttpClient.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();
                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    using (var ms = new MemoryStream(data))
                    {
                        return Image.FromStream(ms);
                    }
                }
            }
            catch
            {
                return SystemIcons.Warning.ToBitmap();
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

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SettingsForm settings = new SettingsForm();
            settings.Show();
            this.Hide();
        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}

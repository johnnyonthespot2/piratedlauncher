using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PiratedLauncher
{
    public partial class Key1 : Form
    {
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public Game GameToDownload { get; set; }
        public string GameName { get; set; }

        public Key1()
        {
            InitializeComponent();
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (Settings.whichKey == 1)
                Process.Start(GameToDownload.Step1Url);
            else if (Settings.whichKey == 2)
                Process.Start(GameToDownload.Step2Url);
        }

        private async void button4_Click(object sender, EventArgs e)
        {
            bool isValid = await Checker.CheckKey(textBox1.Text);

            if(!isValid)
            {
                MessageBox.Show("Invalid key.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                if(Settings.whichKey == 1)
                {
                    Settings.inputtedFirstKey = textBox1.Text;
                    textBox1.Text = "";
                    keyLabel.Text = "Key 2/2";
                    Settings.whichKey++;
                }
                else
                {
                    if(textBox1.Text == Settings.inputtedFirstKey)
                    {
                        MessageBox.Show("It's written 2/2 for some reason lol");
                        return;
                    }

                    Settings.inputtedSecondKey = textBox1.Text;
                    var downloader = new Download
                    {
                        GameTo_Download = GameToDownload
                    };

                    downloader.Closed += (closedSender, closedEventArgs) => this.Close();

                    downloader.Show();

                    this.Hide();
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
            "Do you want to proceed? You will have to re-do the key part.",
            "Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                Settings.inputtedFirstKey = string.Empty;
                Settings.inputtedSecondKey = string.Empty;
                Settings.whichKey = 1;

                var mainForm = new Updater();

                mainForm.Closed += (closedSender, closedEventArgs) => this.Close();

                mainForm.Show();

                this.Hide();
            }
        }

        private void Key1_Load(object sender, EventArgs e)
        {

        }
    }
}

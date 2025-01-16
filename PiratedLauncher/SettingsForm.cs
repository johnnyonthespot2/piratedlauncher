using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PiratedLauncher
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Updater updater = new Updater();    
            updater.Show();
            this.Hide();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            Updater updater = new Updater();
            updater.Show();
            this.Hide();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (double.TryParse(speedLimitTextBox.Text, out double speedLimitMB))
            {
                if (speedLimitMB > 0)
                {
                    if (speedLimitMB > 1000) // For example, limit to 1000 MB/s
                    {
                        MessageBox.Show("Speed limit cannot exceed 1000 MB/s.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Settings.downloadSpeedLimit = (int)(speedLimitMB * 1024 * 1024); // Convert MB/s to bytes/s
                    MessageBox.Show($"Speed limit set to {speedLimitMB:F2} MB/s.", "Speed Limiter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Please enter a positive value.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("Invalid number format. Please enter a valid number.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

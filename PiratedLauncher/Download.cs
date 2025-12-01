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
                // --- Step 1: Fetch the actual download URL ---
                string downloadUrl = $"{GameTo_Download.DownloadUrl}?key1={Settings.inputtedFirstKey}&key2={Settings.inputtedSecondKey}";
                
                if (!IsValidUrl(downloadUrl))
                {
                    ShowError("Invalid initial download URL.");
                    ResetSettingsAndShowMainForm(); // Go back if URL is bad
                    return;
                }

                string jsonResponse = await query.FetchDataAsync(downloadUrl);
                string actualUrl = JObject.Parse(jsonResponse)["result"]?.Value<string>();
               
                if (!IsValidUrl(actualUrl))
                {
                    ShowError("Invalid file URL received from server.");
                    ResetSettingsAndShowMainForm(); // Go back if URL is bad
                    return;
                }

                // --- Step 2: Download the main game file ---
                var gameDownloadProgress = new Progress<(float progress, string speed)>(report =>
                {
                    UpdateProgressUI(report.progress, $" {report.speed}");
                });

                currentProcess = ProcessState.Downloading;
                statusLabel.Text = "Downloading Game...";
                pauseButton.Enabled = true; // Enable pause during download
                pauseButton.Text = "Pause";

                var (isGameDownloadSuccess, gameOutputFileName) = await downloader.DownloadFileAsync(actualUrl, gameDownloadProgress, speedLimitBytesPerSecond: 1024 * 1024);

                if (!isGameDownloadSuccess)
                {
                    // Handle download failure (downloader should ideally throw exceptions or return specific error codes)
                    ShowError($"Failed to download the game file from {actualUrl}. Please check your connection or try again later.");
                    ResetSettingsAndShowMainForm(); // Go back on failure
                    return;
                }

                // --- Step 3: Ask user about extraction ---
                UpdateProgressUI(1.0f, "Game Download Complete."); // Show 100% for download
                pauseButton.Enabled = false; // Disable pause between steps
                currentProcess = ProcessState.None; // Temporarily no active process

                DialogResult extractionChoice = MessageBox.Show(
                    $"Game file '{Path.GetFileName(gameOutputFileName)}' downloaded successfully.\n\nDo you want the launcher to automatically extract it now?\n(If you choose No, you'll need to extract it manually later)",
                    "Extraction Confirmation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                // --- Step 4: Handle game extraction (if user chose Yes) ---
                if (extractionChoice == DialogResult.Yes)
                {
                    // Check if the downloaded file is an archive before attempting extraction
                    if (IsArchiveFile(gameOutputFileName))
                    {
                        await HandleExtraction(gameOutputFileName, Path.Combine(Directory.GetCurrentDirectory(), GameTo_Download.Name + GameTo_Download.GameVersion), "");
                    }
                    else
                    {
                        // If it's not an archive, just keep the file and inform the user
                        MessageBox.Show($"Downloaded file '{Path.GetFileName(gameOutputFileName)}' is not a recognized archive (.zip, .rar, .7z). Skipping extraction.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // Optionally delete if it's useless without extraction? Depends on requirements.
                        // For now, we keep it.
                    }
                }
                else
                {
                    statusLabel.Text = "Skipping game extraction.";
                    await Task.Delay(1500); // Brief pause to show status
                }


                // --- Step 5: Download and handle the crack file ---
                await HandleCrackDownload(); // This will handle download and its own extraction

                // --- Step 6: Finalize ---
                currentProcess = ProcessState.Completed;
                pauseButton.Enabled = false;
                statusLabel.Text = "Process Finished.";
                MessageBox.Show("Download process finished.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetSettingsAndShowMainForm();
            }
            catch (OperationCanceledException) // Catch cancellation if downloader/extractor supports it
            {
                ShowError("Operation was cancelled.");
                // Decide if you want to ResetSettingsAndShowMainForm() here too
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred: {ex.Message}\n\nDetails: {ex.StackTrace}");
                // Optionally try to clean up partial files here
                ResetSettingsAndShowMainForm(); // Go back on error
            }
            finally
            {
                // Ensure UI elements are in a consistent state if an error occurred mid-process
                if (currentProcess != ProcessState.Completed)
                {
                    pauseButton.Enabled = false;
                    pauseButton.Text = "Pause";
                    // Maybe reset progress bar?
                    // progressBar1.Value = 0;
                    // progressLabel.Text = "0%";
                }
            }
        }

        // Modified HandleExtraction to accept target directory and status message
        private async Task HandleExtraction(string filePath, string extractToPath, string statusMsg)
        {
            if (!File.Exists(filePath))
            {
                ShowError($"Archive file not found: {filePath}");
                return; // Exit if file doesn't exist
            }

            if (!IsArchiveFile(filePath)) // Double check it's an archive
            {
                MessageBox.Show($"File '{Path.GetFileName(filePath)}' is not a supported archive (.zip, .rar, .7z). Cannot extract.", "Extraction Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }


            currentProcess = ProcessState.Extracting;
            statusLabel.Text = statusMsg;
            pauseButton.Enabled = true; // Enable pause during extraction
            pauseButton.Text = "Pause";

            var progress = new Progress<(float progress, string status)>(report =>
            {
                UpdateProgressUI(report.progress, $"{statusMsg} {report.status}");
            });

            bool success = false;
            try
            {
                // Ensure the target directory exists
                Directory.CreateDirectory(extractToPath);

                success = await Task.Run(() => extractor.ExtractArchiveAsync(filePath, extractToPath, progress));

                if (success)
                {
                    // Safely delete the archive after successful extraction
                    try { File.Delete(filePath); } catch (IOException ex) { Console.WriteLine($"Warning: Could not delete archive {filePath}: {ex.Message}"); }
                    statusLabel.Text = "Extraction complete";
                    progressLabel.Text = "100% - Done";
                    UpdateProgressUI(1.0f, "Extraction Complete.");
                }
                else
                {
                    // Extractor should ideally provide more specific errors
                    MessageBox.Show($"There was an error extracting '{Path.GetFileName(filePath)}'.\nTry extracting manually with 7-Zip or WinRAR.\nThe file is located at: {filePath}", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Don't delete the file if extraction failed
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred during extraction of '{Path.GetFileName(filePath)}': {ex.Message}\nThe file is located at: {filePath}", "Extraction Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Don't delete the file if extraction failed
                success = false; // Ensure success is false
            }
            finally
            {
                currentProcess = ProcessState.None; // Reset state after extraction attempt
                pauseButton.Enabled = false;
            }
        }

        private async Task HandleCrackDownload()
        {
            if (string.IsNullOrEmpty(GameTo_Download.CrackUrl) || !IsValidUrl(GameTo_Download.CrackUrl))
            {
                // No crack URL provided or invalid, just skip this step
                Console.WriteLine("No valid crack URL provided. Skipping crack download.");
                return;
            }

            statusLabel.Text = "Downloading Crack...";
            currentProcess = ProcessState.Downloading; // Set state for potential pause/resume
            pauseButton.Enabled = true;
            pauseButton.Text = "Pause";

            var crackDownloadProgress = new Progress<(float progress, string speed)>(report =>
            {
                UpdateProgressUI(report.progress, $"Downloading Crack... {report.speed}");
            });

            // Note: Using a smaller speed limit for crack as an example
            var (isCrackDownloadSuccess, crackFileName) = await downloader.DownloadFileAsync(GameTo_Download.CrackUrl, crackDownloadProgress, speedLimitBytesPerSecond: 512 * 1024);

            pauseButton.Enabled = false; // Disable pause after download finishes
            currentProcess = ProcessState.None;

            if (isCrackDownloadSuccess)
            {
                statusLabel.Text = "Crack Download Complete.";
                UpdateProgressUI(1.0f, "Crack Download Complete.");
                await Task.Delay(500); // Brief pause

                // Check if the downloaded crack is an archive and extract it
                if (IsArchiveFile(crackFileName))
                {
                    // Extract crack to a specific folder (e.g., GameNameCrack)
                    string crackExtractPath = Path.Combine(Directory.GetCurrentDirectory(), GameTo_Download.Name + "Crack");
                    await HandleExtraction(crackFileName, crackExtractPath, "Extracting Crack...");
                }
                else
                {
                    // If the crack isn't an archive, maybe it's an .exe or .dll?
                    // Decide what to do - keep it, move it, inform user?
                    // For now, just inform and keep it in the download directory.
                    MessageBox.Show($"Downloaded crack file '{Path.GetFileName(crackFileName)}' is not an archive. It has been saved but not extracted.", "Crack Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Maybe move it to the crack folder anyway?
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), GameTo_Download.Name + "Crack"));
                        string destPath = Path.Combine(Directory.GetCurrentDirectory(), GameTo_Download.Name + "Crack", Path.GetFileName(crackFileName));
                        if (File.Exists(destPath)) File.Delete(destPath); // Overwrite if exists
                        File.Move(crackFileName, destPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not move non-archive crack file: {ex.Message}");
                    }
                }
            }
            else
            {
                ShowError($"Failed to download the crack file from {GameTo_Download.CrackUrl}.");
                // Continue without the crack? Or halt? For now, we continue.
                statusLabel.Text = "Crack download failed.";
                await Task.Delay(1500);
            }
        }

        // Helper to update progress UI safely from any thread
        private void UpdateProgressUI(float progress, string statusText)
        {
            if (progressBar1.InvokeRequired)
            {
                progressBar1.Invoke(new Action(() =>
                {
                    progressBar1.Value = Math.Min(100, Math.Max(0, (int)(progress * 100))); // Clamp value between 0 and 100
                    progressLabel.Text = $"{progressBar1.Value}% - {statusText}";
                }));
            }
            else
            {
                progressBar1.Value = Math.Min(100, Math.Max(0, (int)(progress * 100))); // Clamp value between 0 and 100
                progressLabel.Text = $"{progressBar1.Value}% - {statusText}";
            }
        }


        private void ResetSettingsAndShowMainForm()
        {
            // Ensure this runs on the UI thread if called from background tasks potentially
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ResetSettingsAndShowMainFormInternal()));
            }
            else
            {
                ResetSettingsAndShowMainFormInternal();
            }
        }

        private void ResetSettingsAndShowMainFormInternal()
        {
            currentProcess = ProcessState.Completed;

            Settings.inputtedFirstKey = string.Empty;
            Settings.inputtedSecondKey = string.Empty;
            Settings.whichKey = 1;

            // Check if Updater form already exists to avoid creating multiple instances
            Updater mainForm = Application.OpenForms["Updater"] as Updater;
            if (mainForm == null)
            {
                mainForm = new Updater();
                 // Ensure app exits if main form closes
                mainForm.Show();
                mainForm.FormClosed += (s, args) => this.Close();
            }
            else
            {
                mainForm.BringToFront();
                mainForm.WindowState = FormWindowState.Normal; // Restore if minimized
            }

            this.Hide(); // Hide the download form
            Environment.Exit(1);
        }


        private void ShowError(string message)
        {
            // Ensure MessageBox is shown on the UI thread
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
            else
            {
                MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static bool IsValidUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Basic check for common schemes and valid URI structure
            return Uri.TryCreate(input, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        // Helper to check file extension for common archive types
        private static bool IsArchiveFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".zip" || extension == ".rar" || extension == ".7z";
        }


        // --- UI Event Handlers ---

        private void button1_Click(object sender, EventArgs e) // Close Button
        {
            if (currentProcess == ProcessState.None || currentProcess == ProcessState.Completed)
            {
                // If nothing is running or it's finished, close immediately or go back
                ResetSettingsAndShowMainForm(); // Or just Environment.Exit(0); if preferred
                                                // Environment.Exit(0);
            }
            else // Ask for confirmation if downloading or extracting
            {
                DialogResult result = MessageBox.Show(
                    $"A process ({currentProcess}) is currently active. Are you sure you want to cancel and exit?",
                    "Confirmation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    // Attempt graceful cancellation/pause before exiting
                    downloader?.PauseDownload(); // Add CancelDownload method to FileDownloader if possible
                    extractor?.Cancel(); // Add Cancel method to FileExtractor if possible
                    // Give a moment for cancellation to process
                    Task.Delay(500).ContinueWith(_ => Environment.Exit(0), TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private void button2_Click(object sender, EventArgs e) // Minimize Button
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e) // Title Bar Drag
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
                    // Status label should be updated by the progress reporter
                }
                else
                {
                    downloader.PauseDownload();
                    pauseButton.Text = "Resume";
                    statusLabel.Text = "Paused (Download)"; // Explicitly show paused state
                }
            }
            else if (currentProcess == ProcessState.Extracting && extractor != null)
            {
                if (extractor._isPaused) // Assuming extractor exposes an _isPaused flag
                {
                    extractor.Resume(); // Assuming extractor has Resume()
                    pauseButton.Text = "Pause";
                    // Status label should be updated by the progress reporter
                }
                else
                {
                    extractor.Pause(); // Assuming extractor has Pause()
                    pauseButton.Text = "Resume";
                    statusLabel.Text = "Paused (Extraction)"; // Explicitly show paused state
                }
            }
        }

        // Keep this empty event handler if needed by the designer
        private void progressLabel_Click(object sender, EventArgs e) { }
    }
}
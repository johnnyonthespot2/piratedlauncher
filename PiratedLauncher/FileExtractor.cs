using SevenZipExtractor;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public class FileExtractor
{
    private CancellationTokenSource _cancellationTokenSource;
    private ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);
    public bool _isPaused = false;
    public bool _isExtracting = false;

    public async Task<bool> ExtractArchiveAsync(string archivePath, string destinationDirectory, IProgress<(float progress, string status)> progress)
    {
        try
        {
            Directory.CreateDirectory(destinationDirectory);
            _cancellationTokenSource = new CancellationTokenSource();
            _isExtracting = true;

            using (ArchiveFile archiveFile = new ArchiveFile(archivePath))
            {
                int totalFiles = archiveFile.Entries.Count;
                int extractedFiles = 0;

                // Process sequentially for reliability
                await Task.Run(() =>
                {
                    foreach (var entry in archiveFile.Entries)
                    {
                        _pauseEvent.Wait();
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        string filePath = Path.Combine(destinationDirectory, entry.FileName);
                        if (entry.IsFolder)
                        {
                            Directory.CreateDirectory(filePath);
                        }
                        else
                        {
                            string directoryName = Path.GetDirectoryName(filePath);
                            if (!string.IsNullOrEmpty(directoryName))
                                Directory.CreateDirectory(directoryName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                entry.Extract(fileStream);
                                fileStream.Flush(); // Ensure data is written to disk
                            }
                        }

                        Interlocked.Increment(ref extractedFiles);
                        float progressValue = (float)extractedFiles / totalFiles;
                        progress.Report((progressValue, $"Extracting {entry.FileName}"));
                    }
                });
            }

            _isExtracting = false;
            progress.Report((1.0f, "Extraction Complete"));
            return true;
        }
        catch (OperationCanceledException)
        {
            progress.Report((0.0f, "Extraction Canceled"));
            return false;
        }
        catch (Exception ex)
        {
            ShowError("Error extracting files. Check clipboard for details.", ex);
            progress.Report((0.0f, "Extraction Failed"));
            return false;
        }
    }

    public void Pause()
    {
        if (_isExtracting && !_isPaused)
        {
            _pauseEvent.Reset();
            _isPaused = true;
        }
    }

    public void Resume()
    {
        if (_isExtracting && _isPaused)
        {
            _pauseEvent.Set();
            _isPaused = false;
        }
    }

    public void Cancel()
    {
        if (_isExtracting)
        {
            _cancellationTokenSource.Cancel();
            _pauseEvent.Set();
        }
    }

    private void ShowError(string message, Exception ex)
    {
        MessageBox.Show(message, "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        if (Application.OpenForms[0].InvokeRequired)
        {
            Application.OpenForms[0].Invoke(new Action(() => Clipboard.SetText(ex.ToString())));
        }
        else
        {
            Clipboard.SetText(ex.ToString());
        }
    }
}

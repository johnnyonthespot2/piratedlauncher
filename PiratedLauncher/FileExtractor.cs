using SharpCompress.Archives;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public class FileExtractor
{
    private CancellationTokenSource _pauseTokenSource;
    private bool _isPaused;

    public bool IsPaused => _isPaused;

    public FileExtractor()
    {
        _pauseTokenSource = new CancellationTokenSource();
    }

    public void PauseExtraction()
    {
        if (_isPaused) return;
        _isPaused = true;
        _pauseTokenSource?.Cancel();
        _pauseTokenSource = new CancellationTokenSource();
    }

    public void ResumeExtraction()
    {
        if (!_isPaused) return;
        _isPaused = false;
    }

    public bool ExtractArchive(string archivePath, string destinationDirectory, IProgress<(float progress, string status)> progress = null)
    {
        try
        {
            Directory.CreateDirectory(destinationDirectory);

            using (var archive = ArchiveFactory.Open(archivePath))
            {
                var entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
                long totalSize = entries.Sum(entry => entry.Size);
                long totalBytesExtracted = 0;

                foreach (var entry in entries)
                {
                    string filePath = Path.Combine(destinationDirectory, entry.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    using (var entryStream = entry.OpenEntryStream())
                    using (var fileStream = File.Create(filePath))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            while (_isPaused)
                            {
                                Task.Delay(100, _pauseTokenSource.Token).Wait();
                            }

                            fileStream.Write(buffer, 0, bytesRead);
                            totalBytesExtracted += bytesRead;

                            if (progress != null && totalSize > 0)
                            {
                                float percentComplete = (float)totalBytesExtracted / totalSize;
                                progress.Report((percentComplete, $"Extracting {Path.GetFileName(entry.Key)}"));
                            }
                        }
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("There was an error extracting the files. Error was copied to your clipboard in case you want to send it on the discord server for help.", "PiratedLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Clipboard.SetText(ex.ToString());
            return false;
        }
    }
}
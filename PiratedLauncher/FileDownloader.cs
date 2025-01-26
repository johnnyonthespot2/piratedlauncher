using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PiratedLauncher;

public class FileDownloader
{
    private static readonly HttpClient httpClient = new HttpClient();
    private const int MaxRetries = 3;
    private const int DelayBetweenRetriesInSeconds = 2;
    private const int BufferSize = 8192;
    private const int SpeedUpdateIntervalMs = 50;

    private CancellationTokenSource _pauseTokenSource;
    private bool _isPaused;

    public bool IsPaused => _isPaused;

    public FileDownloader()
    {
        _pauseTokenSource = new CancellationTokenSource();
    }

    public void PauseDownload()
    {
        if (_isPaused) return;
        _isPaused = true;
        _pauseTokenSource?.Cancel();
        // Create new token source for when we resume
        _pauseTokenSource = new CancellationTokenSource();
    }

    public void ResumeDownload()
    {
        if (!_isPaused) return;
        _isPaused = false;
    }

    public async Task<(bool isSuccess, string outputFileName)> DownloadFileAsync(
        string fileUrl,
        IProgress<(float progress, string speed)> progress = null,
        int speedLimitBytesPerSecond = 0)
    {
        int attempt = 0;
        bool success = false;
        string outputFileName = null;
        long? totalSize = null;

        speedLimitBytesPerSecond = Settings.downloadSpeedLimit;

        // Get file info first
        using (var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            totalSize = response.Content.Headers.ContentLength;
            outputFileName = GetFileNameFromContentDisposition(response) ??
                           Path.GetFileName(new Uri(fileUrl).LocalPath);
            outputFileName = SanitizeFileName(outputFileName);
        }

        string tempFilePath = Path.Combine(Directory.GetCurrentDirectory(), outputFileName + ".partial");
        string finalFilePath = Path.Combine(Directory.GetCurrentDirectory(), outputFileName);

        while (attempt < MaxRetries && !success)
        {
            try
            {
                attempt++;
                long existingLength = 0;

                if (File.Exists(tempFilePath))
                {
                    existingLength = new FileInfo(tempFilePath).Length;
                }

                // If the file is already complete, just move it and return
                if (totalSize.HasValue && existingLength >= totalSize.Value)
                {
                    File.Move(tempFilePath, finalFilePath);
                    if (progress != null)
                    {
                        progress.Report((1.0f, "Complete"));
                    }
                    return (true, outputFileName);
                }

                var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
                if (existingLength > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
                }

                using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        File.Move(tempFilePath, finalFilePath);
                        if (progress != null)
                        {
                            progress.Report((1.0f, "Complete"));
                        }
                        return (true, outputFileName);
                    }

                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                           fileStream = new FileStream(tempFilePath,
                               existingLength > 0 ? FileMode.Append : FileMode.Create,
                               FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                    {
                        var buffer = new byte[BufferSize];
                        long totalBytesRead = existingLength; // Include existing bytes in total
                        int bytesRead;

                        var speedTimer = new Stopwatch();
                        speedTimer.Start();

                        long bytesReadInCurrentInterval = 0;
                        long lastUpdateTime = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            while (_isPaused)
                            {
                                await Task.Delay(100, _pauseTokenSource.Token);
                            }

                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            bytesReadInCurrentInterval += bytesRead;

                            long elapsedMs = speedTimer.ElapsedMilliseconds;

                            if (elapsedMs - lastUpdateTime >= SpeedUpdateIntervalMs)
                            {
                                if (speedLimitBytesPerSecond > 0)
                                {
                                    double currentSpeed = bytesReadInCurrentInterval * (1000.0 / (elapsedMs - lastUpdateTime));
                                    if (currentSpeed > speedLimitBytesPerSecond)
                                    {
                                        double desiredTime = bytesReadInCurrentInterval * 1000.0 / speedLimitBytesPerSecond;
                                        int delayMs = (int)(desiredTime - (elapsedMs - lastUpdateTime));
                                        if (delayMs > 0)
                                        {
                                            await Task.Delay(delayMs);
                                            elapsedMs = speedTimer.ElapsedMilliseconds;
                                        }
                                    }
                                }

                                if (progress != null && totalSize.HasValue)
                                {
                                    // Calculate progress based on total file size, capped at 100%
                                    float percentComplete = Math.Min(1.0f, (float)totalBytesRead / totalSize.Value);
                                    double speed = bytesReadInCurrentInterval * (1000.0 / (elapsedMs - lastUpdateTime));
                                    progress.Report((percentComplete, FormatSpeed(speed)));
                                }

                                bytesReadInCurrentInterval = 0;
                                lastUpdateTime = elapsedMs;
                            }
                        }
                    }
                }

                if (File.Exists(finalFilePath))
                {
                    File.Delete(finalFilePath);
                }
                File.Move(tempFilePath, finalFilePath);

                success = true;
            }
            catch (OperationCanceledException) when (_isPaused)
            {
                // This is expected during pause, continue the loop
                attempt--; // Don't count this as a failed attempt
                continue;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                await ShowRetryMessageAsync(attempt, ex);
                await HandleRetryDelayAsync();
            }
            catch (Exception ex)
            {
                await LogErrorAsync(attempt, ex);
                throw;
            }
        }

        return (success, outputFileName);
    }

    private async Task ShowRetryMessageAsync(int attempt, Exception ex)
    {
        await Task.Run(() =>
        {
            MessageBox.Show($"Attempt {attempt} failed: {ex.Message}. Retrying in {DelayBetweenRetriesInSeconds} seconds...");
        });
    }

    private async Task HandleRetryDelayAsync()
    {
        for (int i = 0; i < DelayBetweenRetriesInSeconds * 10; i++)
        {
            if (_isPaused)
            {
                while (_isPaused)
                {
                    await Task.Delay(100);
                }
            }
            await Task.Delay(100);
        }
    }

    private async Task LogErrorAsync(int attempt, Exception ex)
    {
        await Task.Run(() =>
        {
            Console.WriteLine($"Download failed after {attempt} attempts: {ex.Message}");
        });
    }

    private string GetFileNameFromContentDisposition(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentDisposition != null)
        {
            return response.Content.Headers.ContentDisposition.FileName?.Trim('"');
        }
        return null;
    }

    private string SanitizeFileName(string fileName)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }
        return fileName;
    }

    private string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond > 1024 * 1024)
            return $"{bytesPerSecond / (1024 * 1024):F2} MB/s";
        if (bytesPerSecond > 1024)
            return $"{bytesPerSecond / 1024:F2} KB/s";
        return $"{bytesPerSecond:F2} B/s";
    }
}
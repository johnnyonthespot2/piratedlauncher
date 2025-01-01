using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class FileDownloader
{
    private static readonly HttpClient httpClient = new HttpClient();
    private const int MaxRetries = 3;
    private const int DelayBetweenRetriesInSeconds = 2;

    public async Task<(bool isSuccess, string outputFileName)> DownloadFileAsync(string fileUrl, IProgress<float> progress = null)
    {
        int attempt = 0;
        bool success = false;
        string outputFileName = null;

        while (attempt < MaxRetries && !success)
        {
            try
            {
                attempt++;
                using (HttpResponseMessage response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    string fileName = GetFileNameFromContentDisposition(response) ?? Path.GetFileName(new Uri(fileUrl).LocalPath);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = "downloaded_file";
                    }

                    fileName = SanitizeFileName(fileName);

                    outputFileName = fileName;

                    string destinationFilePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                                  fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
                    {
                        var buffer = new byte[8192];
                        long totalBytesRead = 0;
                        int bytesRead;
                        long? contentLength = response.Content.Headers.ContentLength;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            if (contentLength.HasValue && progress != null)
                            {
                                float percentComplete = (float)totalBytesRead / contentLength.Value;
                                progress.Report(percentComplete);
                            }
                        }
                    }
                }
                success = true;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Console.WriteLine($"Attempt {attempt} failed: {ex.Message}. Retrying in {DelayBetweenRetriesInSeconds} seconds...");
                await Task.Delay(DelayBetweenRetriesInSeconds * 1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download failed after {attempt} attempts: {ex.Message}");
                throw;
            }
        }

        return (success, outputFileName);
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
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace PiratedLauncher
{
    internal class Query
    {
        private WebClient wc;
        private HttpClientHandler _baseHandler;
        private HttpClient _httpClient;
        private const string DEFAULT_USER_AGENT = "PiratedLauncher";
        private const int DEFAULT_TIMEOUT = 5000;

        private string _resolvedIp;
        private string _originalDomain;

        public HttpClient HttpClient => _httpClient;

        public class ResolvedIpHandler : DelegatingHandler
        {
            private readonly string _resolvedIp;
            private readonly string _originalDomain;

            public ResolvedIpHandler(string resolvedIp, string originalDomain, HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
                _resolvedIp = resolvedIp;
                _originalDomain = originalDomain;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var uri = request.RequestUri;
                if (uri.Host.Equals(_originalDomain, StringComparison.OrdinalIgnoreCase))
                {
                    var builder = new UriBuilder(uri)
                    {
                        Host = _resolvedIp
                    };
                    request.RequestUri = builder.Uri;
                    if (!request.Headers.Contains("Host"))
                    {
                        request.Headers.Host = _originalDomain;
                    }
                }
                return base.SendAsync(request, cancellationToken);
            }
        }

        public async Task<string> FetchDataAsync(string url)
        {
            var exceptions = new List<Exception>();
            string result = null;

            Uri uri = new Uri(url);
            string domain = uri.Host;

            // Method 1: WebClient with timeout
            try
            {
                var tcs = new TaskCompletionSource<string>();
                using (var cts = new CancellationTokenSource(DEFAULT_TIMEOUT))
                {
                    var downloadTask = Task.Run(() => wc.DownloadString(url));
                    var timeoutTask = Task.Delay(DEFAULT_TIMEOUT, cts.Token);

                    var completedTask = await Task.WhenAny(downloadTask, timeoutTask);
                    if (completedTask == downloadTask && !string.IsNullOrEmpty(downloadTask.Result))
                    {
                        return downloadTask.Result;
                    }
                }
            }
            catch (Exception ex)
            {
                //exceptions.Add(ex);
            }

            // Method 2: Try direct HTTPS
            try
            {
                using (var cts = new CancellationTokenSource(DEFAULT_TIMEOUT))
                {
                    using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        result = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(result)) return result;
                    }
                }
            }
            catch (Exception ex)
            {
                //exceptions.Add(ex);
            }

            // Method 3: Try HTTP
            try
            {
                string httpUrl = url.Replace("https://", "http://");
                using (var cts = new CancellationTokenSource(DEFAULT_TIMEOUT))
                {
                    using (var response = await _httpClient.GetAsync(httpUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        result = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(result)) return result;
                    }
                }
            }
            catch (Exception ex)
            {
                //exceptions.Add(ex);
            }

            // Method 4: Direct IP Connection (HTTPS)
            try
            {
                using (var cts = new CancellationTokenSource(DEFAULT_TIMEOUT))
                {
                    string ip = "172.233.121.188"; // Hardcoded IP address
                    _resolvedIp = ip;
                    _originalDomain = domain;

                    // Reconfigure HttpClient with the resolved IP handler
                    var resolvedIpHandler = new ResolvedIpHandler(_resolvedIp, _originalDomain, _baseHandler);
                    _httpClient = new HttpClient(resolvedIpHandler)
                    {
                        Timeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT)
                    };
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", DEFAULT_USER_AGENT);

                    // Try HTTPS (443) first
                    using (var tcpClient = new TcpClient())
                    {
                        var connectTask = tcpClient.ConnectAsync(ip, 443);
                        if (await Task.WhenAny(connectTask, Task.Delay(DEFAULT_TIMEOUT)) == connectTask)
                        {
                            using (var sslStream = new SslStream(tcpClient.GetStream(), false,
                                (sender, certificate, chain, errors) => true))
                            {
                                try
                                {
                                    await sslStream.AuthenticateAsClientAsync(domain);

                                    using (var writer = new StreamWriter(sslStream))
                                    using (var reader = new StreamReader(sslStream))
                                    {
                                        var timeoutTask = Task.Delay(DEFAULT_TIMEOUT);
                                        var writeTask = writer.WriteAsync(
                                            $"GET {uri.PathAndQuery} HTTP/1.1\r\n" +
                                            $"Host: {domain}\r\n" +
                                            $"User-Agent: {DEFAULT_USER_AGENT}\r\n" +
                                            "Connection: close\r\n\r\n");

                                        if (await Task.WhenAny(writeTask, timeoutTask) == writeTask)
                                        {
                                            await writer.FlushAsync();
                                            var readTask = reader.ReadToEndAsync();

                                            if (await Task.WhenAny(readTask, Task.Delay(DEFAULT_TIMEOUT)) == readTask)
                                            {
                                                string response2 = readTask.Result;
                                                int bodyIndex = response2.IndexOf("\r\n\r\n");
                                                if (bodyIndex != -1)
                                                {
                                                    result = response2.Substring(bodyIndex + 4);
                                                    if (!string.IsNullOrEmpty(result)) return result;
                                                }
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // If HTTPS fails, fall back to HTTP on port 80
                                    using (var httpTcpClient = new TcpClient())
                                    {
                                        var httpConnectTask = httpTcpClient.ConnectAsync(ip, 80);
                                        if (await Task.WhenAny(httpConnectTask, Task.Delay(DEFAULT_TIMEOUT)) == httpConnectTask)
                                        {
                                            using (var stream = httpTcpClient.GetStream())
                                            using (var writer = new StreamWriter(stream))
                                            using (var reader = new StreamReader(stream))
                                            {
                                                var timeoutTask = Task.Delay(DEFAULT_TIMEOUT);
                                                var writeTask = writer.WriteAsync(
                                                    $"GET {uri.PathAndQuery} HTTP/1.1\r\n" +
                                                    $"Host: {domain}\r\n" +
                                                    $"User-Agent: {DEFAULT_USER_AGENT}\r\n" +
                                                    "Connection: close\r\n\r\n");

                                                if (await Task.WhenAny(writeTask, timeoutTask) == writeTask)
                                                {
                                                    await writer.FlushAsync();
                                                    var readTask = reader.ReadToEndAsync();

                                                    if (await Task.WhenAny(readTask, Task.Delay(DEFAULT_TIMEOUT)) == readTask)
                                                    {
                                                        string response3 = readTask.Result;
                                                        int bodyIndex = response3.IndexOf("\r\n\r\n");
                                                        if (bodyIndex != -1)
                                                        {
                                                            result = response3.Substring(bodyIndex + 4);
                                                            if (!string.IsNullOrEmpty(result)) return result;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // exceptions.Add(ex);
            }

            throw new AggregateException($"All download methods failed for URL: {url}", exceptions);
        }

        public void Initialize()
        {
            wc = new WebClient
            {
                Encoding = Encoding.UTF8
            };
            wc.Headers.Add(HttpRequestHeader.UserAgent, DEFAULT_USER_AGENT);

            _baseHandler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(_baseHandler)
            {
                Timeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", DEFAULT_USER_AGENT);
        }
    }
}
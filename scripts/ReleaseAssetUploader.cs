using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace LocalFork;

static class ReleaseAssetUploader
{
    public static void Upload(HttpClient client, string uploadUrl, string path, string name, string token)
    {
        // Match gh's three retries for transport failures and server errors, reopening the file each time.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                UploadOnce(client, uploadUrl, path, name, token);
                return;
            }
            catch (HttpRequestException error) when (attempt < 3 && (error.StatusCode is null || (int)error.StatusCode >= 500))
            {
                Console.WriteLine($"Upload interrupted; retrying {name} ({attempt + 1}/3).");
                Thread.Sleep(200);
            }
        }
    }

    private static void UploadOnce(HttpClient client, string uploadUrl, string path, string name, string token)
    {
        var endpoint = new Uri(uploadUrl.Split('{')[0]);
        if (endpoint.Scheme != "https" || endpoint.Host != "uploads.github.com" || !endpoint.IsDefaultPort || endpoint.UserInfo.Length != 0)
            throw new InvalidOperationException($"Unexpected GitHub upload endpoint: {endpoint}");
        var uri = new UriBuilder(endpoint) { Query = $"name={Uri.EscapeDataString(name)}" }.Uri;
        using var file = File.OpenRead(path);
        using var content = new UploadContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("OpenChamber-Release-Publisher/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        Console.WriteLine($"Uploading: {name} ({file.Length} bytes)");
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            using var progress = new UploadProgress(file.Length);
            var upload = client.SendAsync(request, cancellation.Token);
            while (!upload.IsCompleted)
            {
                progress.Render(content.BytesSent);
                Thread.Sleep(100);
            }
            using var response = upload.GetAwaiter().GetResult();
            if (response.StatusCode != HttpStatusCode.Created)
                throw new HttpRequestException($"GitHub upload failed for {name}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n"
                    + response.Content.ReadAsStringAsync(cancellation.Token).GetAwaiter().GetResult(), null, response.StatusCode);
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    sealed class UploadContent(Stream source) : HttpContent
    {
        private long bytesSent;
        public long BytesSent => Interlocked.Read(ref bytesSent);

        protected override bool TryComputeLength(out long length)
        {
            length = source.Length;
            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024];
            int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken)) != 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                Interlocked.Add(ref bytesSent, count);
            }
        }

    }

    sealed class UploadProgress(long total) : IDisposable
    {
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private int previousWidth;

        public void Render(long sent)
        {
            if (Console.IsErrorRedirected) return;
            var fraction = total == 0 ? 1 : Math.Clamp((double)sent / total, 0, 1);
            var filled = (int)(fraction * 20);
            var speed = sent / Math.Max(elapsed.Elapsed.TotalSeconds, 0.001) / (1024 * 1024);
            var line = $"[{new string('=', filled)}{new string(' ', 20 - filled)}] {fraction,6:P1} {sent / 1048576d:F1}/{total / 1048576d:F1} MiB {speed:F2} MiB/s";
            if (sent == total) line += " (waiting for GitHub)";
            var width = Math.Max(0, Console.WindowWidth - 1);
            if (line.Length > width) line = line[..width];
            Console.Error.Write("\r" + line + new string(' ', Math.Max(0, Math.Min(previousWidth, width) - line.Length)) + "\r");
            previousWidth = line.Length;
        }

        public void Dispose()
        {
            if (previousWidth > 0) Console.Error.Write("\r" + new string(' ', previousWidth) + "\r");
        }
    }
}

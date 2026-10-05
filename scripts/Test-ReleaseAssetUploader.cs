#!/usr/bin/env dotnet
#:include ReleaseAssetUploader.cs

using System.Net;
using System.Security.Cryptography;
using LocalFork;

if (args.Length != 1) throw new ArgumentException("Pass an existing local file to exercise streaming uploads without network access.");
var path = Path.GetFullPath(args[0]);
using var source = File.OpenRead(path);
var expectedHash = SHA256.HashData(source);
var handler = new UploadHandler(source.Length, expectedHash);
using var client = new HttpClient(handler);
const string endpoint = "https://uploads.github.com/repos/test/repo/releases/1/assets{?name,label}";
ReleaseAssetUploader.Upload(client, endpoint, path, "test file+#.bin", "test-token");
if (handler.Calls != 1) throw new Exception("Expected exactly one upload.");
Console.WriteLine("PASS: streamed request bytes, content length, authentication, content type and escaped asset name.");

handler.Status = HttpStatusCode.BadGateway;
try
{
    ReleaseAssetUploader.Upload(client, endpoint, path, "test file+#.bin", "test-token");
    throw new Exception("Expected HTTP failure.");
}
catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.BadGateway && error.Message.Contains("test upload failure"))
{
    if (handler.Calls != 5) throw new Exception("Expected three retries for server errors.");
    Console.WriteLine("PASS: server errors retried with complete file bodies, then surfaced.");
}

handler.Status = HttpStatusCode.UnprocessableEntity;
try
{
    ReleaseAssetUploader.Upload(client, endpoint, path, "test file+#.bin", "test-token");
    throw new Exception("Expected HTTP failure.");
}
catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.UnprocessableEntity)
{
    if (handler.Calls != 6) throw new Exception("Client errors must not be retried.");
    Console.WriteLine("PASS: client errors surfaced without retry.");
}

handler.FailTransport = true;
try
{
    ReleaseAssetUploader.Upload(client, endpoint, path, "test file+#.bin", "test-token");
    throw new Exception("Expected transport failure.");
}
catch (HttpRequestException error) when (error.Message == "test transport failure")
{
    if (handler.Calls != 10) throw new Exception("Expected three retries for transport errors.");
    Console.WriteLine("PASS: transport failure retried, then surfaced.");
}

var calls = handler.Calls;
try
{
    ReleaseAssetUploader.Upload(client, "https://example.com/assets", path, "test.bin", "test-token");
    throw new Exception("Expected untrusted endpoint rejection.");
}
catch (InvalidOperationException error) when (error.Message.Contains("Unexpected GitHub upload endpoint"))
{
    if (handler.Calls != calls) throw new Exception("Credentials were sent to an unexpected host.");
    Console.WriteLine("PASS: untrusted endpoint rejected before sending credentials.");
}

sealed class UploadHandler(long size, byte[] expectedHash) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
    public bool FailTransport { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        if (request.Method != HttpMethod.Post || request.RequestUri!.AbsoluteUri != "https://uploads.github.com/repos/test/repo/releases/1/assets?name=test%20file%2B%23.bin")
            throw new Exception("Unexpected upload request.");
        if (request.Headers.Authorization?.ToString() != "Bearer test-token") throw new Exception("Missing authentication.");
        if (request.Content!.Headers.ContentLength != size) throw new Exception("Incorrect upload length.");
        if (request.Content.Headers.ContentType?.MediaType != "application/octet-stream") throw new Exception("Incorrect content type.");
        if (FailTransport) throw new HttpRequestException("test transport failure");
        using var sink = new HashSink();
        await request.Content.CopyToAsync(sink, cancellationToken);
        if (sink.Bytes != size || !sink.Hash.GetHashAndReset().SequenceEqual(expectedHash)) throw new Exception("Upload body changed.");
        // Keep the local response pending briefly to exercise temporary progress display and cleanup.
        await Task.Delay(300, cancellationToken);
        return new HttpResponseMessage(Status) { Content = new StringContent(Status == HttpStatusCode.Created ? "{}" : "test upload failure") };
    }
}

sealed class HashSink : Stream
{
    public IncrementalHash Hash { get; } = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    public long Bytes { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => Bytes;
    public override long Position { get => Bytes; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count > 64 * 1024) throw new Exception("Upload was buffered instead of streamed.");
        Hash.AppendData(buffer, offset, count);
        Bytes += count;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Hash.Dispose();
        base.Dispose(disposing);
    }
}

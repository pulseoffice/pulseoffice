using System.Net;
using System.Text;

namespace PulseOffice.Remote.Tests;

public sealed class WebDavTestServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();

    private string? _lockToken;

    public WebDavTestServer(int port)
    {
        Address = $"http://127.0.0.1:{port}/dav";

        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        _ = Task.Run(LoopAsync);
    }

    public string Address { get; }

    public string? Authorization { get; private set; }

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public void Put(string path, byte[] content) => _files[path] = content;

    private async Task LoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                Handle(context);
            }
            catch (Exception)
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }
    }

    private void Handle(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url!.AbsolutePath["/dav".Length..];

        Authorization = request.Headers["Authorization"];

        switch (request.HttpMethod)
        {
            case "PROPFIND":
                Write(response, 207, Listing(path), "application/xml");
                return;

            case "GET":
                if (!_files.TryGetValue(path, out var content))
                {
                    Write(response, 404, string.Empty, "text/plain");
                    return;
                }

                response.StatusCode = 200;
                response.OutputStream.Write(content);
                response.Close();
                return;

            case "PUT":
                if (_lockToken is not null && !(request.Headers["If"] ?? string.Empty).Contains(_lockToken, StringComparison.Ordinal))
                {
                    Write(response, 423, string.Empty, "text/plain");
                    return;
                }

                using (var buffer = new MemoryStream())
                {
                    request.InputStream.CopyTo(buffer);
                    _files[path] = buffer.ToArray();
                }

                Write(response, 201, string.Empty, "text/plain");
                return;

            case "LOCK":
                if (_lockToken is not null)
                {
                    Write(response, 423, string.Empty, "text/plain");
                    return;
                }

                _lockToken = "opaquelocktoken:" + Guid.NewGuid().ToString("N");

                Write(response, 200,
                    $"""
                     <?xml version="1.0"?>
                     <D:prop xmlns:D="DAV:"><D:lockdiscovery><D:activelock>
                       <D:locktoken><D:href>{_lockToken}</D:href></D:locktoken>
                     </D:activelock></D:lockdiscovery></D:prop>
                     """,
                    "application/xml");
                return;

            case "UNLOCK":
                _lockToken = null;
                Write(response, 204, string.Empty, "text/plain");
                return;

            default:
                Write(response, 405, string.Empty, "text/plain");
                return;
        }
    }

    private string Listing(string path)
    {
        var body = new StringBuilder();

        body.Append("<?xml version=\"1.0\"?><D:multistatus xmlns:D=\"DAV:\">");
        body.Append($"<D:response><D:href>/dav{path}</D:href><D:propstat><D:prop>")
            .Append("<D:resourcetype><D:collection/></D:resourcetype>")
            .Append("</D:prop></D:propstat></D:response>");

        foreach (var (file, content) in _files)
            body.Append($"<D:response><D:href>/dav{Uri.EscapeDataString(file.TrimStart('/')).Replace("%2F", "/", StringComparison.Ordinal)}</D:href>")
                .Append("<D:propstat><D:prop><D:resourcetype/>")
                .Append($"<D:getcontentlength>{content.Length}</D:getcontentlength>")
                .Append("</D:prop></D:propstat></D:response>");

        return body.Append("</D:multistatus>").ToString();
    }

    private static void Write(HttpListenerResponse response, int status, string body, string type)
    {
        var bytes = Encoding.UTF8.GetBytes(body);

        response.StatusCode = status;
        response.ContentType = type;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes);
        response.Close();
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Close();
        _stopping.Dispose();
    }
}

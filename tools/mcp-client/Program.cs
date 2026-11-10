using System.Text.Json.Nodes;
using PulseOffice.Tools.McpClient;

var options = new Options(args);
if (options.TokenFile is not null && options.Token is null)
{
    Console.Error.WriteLine($"pulse-mcp: the access code file {options.TokenFile} does not exist yet; start PulseOffice with the agent enabled first.");
    return 2;
}
if (options.Command is null || options.Url is null || options.Token is null)
{
    Console.Error.WriteLine("""
        pulse-mcp: a small MCP client for PulseOffice.

          pulse-mcp bridge --url http://127.0.0.1:47820/mcp --token-file <file>
              stdio <-> HTTP bridge, for clients that start MCP servers as commands (Claude Desktop).
          pulse-mcp tools  --url ... --token-file <file>
              lists the tools.
          pulse-mcp call   --url ... --token-file <file> <tool> ['{"json":"arguments"}'] [--image <file.png>]
              calls a tool and prints the result; an image is written to --image.
        """);
    return 2;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
var client = new McpHttpClient(http, new Uri(options.Url), options.Token);
var ct = CancellationToken.None;

switch (options.Command)
{
    case "bridge":
        await new StdioBridge(client, Console.In, Console.Out, Console.Error).RunAsync(ct);
        return 0;

    case "tools":
        await client.InitializeAsync(ct);
        foreach (var tool in (await client.RequestAsync("tools/list", new JsonObject(), ct))["tools"]!.AsArray())
            Console.WriteLine($"{tool!["name"]}\t{tool["description"]}");
        await client.EndAsync(ct);
        return 0;

    case "call" when options.Rest.Count >= 1:
        var initialized = await client.InitializeAsync(ct);
        Console.Error.WriteLine($"connected to {initialized["serverInfo"]?["name"]} {initialized["serverInfo"]?["version"]}, protocol {initialized["protocolVersion"]}");
        var arguments = options.Rest.Count > 1 ? JsonNode.Parse(options.Rest[1])!.AsObject() : new JsonObject();
        var result = await client.RequestAsync("tools/call", new JsonObject { ["name"] = options.Rest[0], ["arguments"] = arguments }, ct);
        foreach (var item in result["content"]!.AsArray())
        {
            if (item!["type"]!.GetValue<string>() == "image")
            {
                var bytes = Convert.FromBase64String(item["data"]!.GetValue<string>());
                var target = options.Image ?? Path.Combine(Path.GetTempPath(), "pulse-mcp.png");
                await File.WriteAllBytesAsync(target, bytes, ct);
                Console.WriteLine($"image: {target} ({bytes.Length} bytes)");
            }
            else
            {
                Console.WriteLine(item["text"]!.GetValue<string>());
            }
        }

        await client.EndAsync(ct);
        return result["isError"]?.GetValue<bool>() == true ? 1 : 0;

    default:
        Console.Error.WriteLine($"unknown command {options.Command}");
        return 2;
}

internal sealed class Options
{
    public Options(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    Url = args[++i];
                    break;
                case "--token-file" when i + 1 < args.Length:
                    TokenFile = args[++i];
                    Token = File.Exists(TokenFile) ? File.ReadAllText(TokenFile).Trim() : null;
                    break;
                case "--image" when i + 1 < args.Length:
                    Image = args[++i];
                    break;
                default:
                    if (Command is null) Command = args[i];
                    else Rest.Add(args[i]);
                    break;
            }
        }
    }

    public string? Command { get; }

    public string? Url { get; }

    public string? Token { get; }

    public string? TokenFile { get; }

    public string? Image { get; }

    public List<string> Rest { get; } = [];
}

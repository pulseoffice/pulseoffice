using System.Net;
using PulseOffice.Localization;

using PulseOffice.Presentations.Remote;

namespace PulseOffice.Presentations.UI.Remote;

public sealed class RemotePage(ITextCatalog texts) : IRemotePage
{
    private const string Style =
        "body{margin:0;font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:#0f172a;color:#e2e8f0;display:flex;flex-direction:column;min-height:100vh}" +
        "header{padding:16px 20px;font-weight:600;border-bottom:3px solid #dc2626}main{flex:1;padding:20px;display:flex;flex-direction:column;gap:16px}" +
        ".slide{font-size:15px;color:#94a3b8}.title{font-size:22px;font-weight:600}.notes{white-space:pre-wrap;background:#1e293b;border-radius:12px;padding:14px;min-height:80px;font-size:16px;line-height:1.45}" +
        ".row{display:flex;gap:12px}button{flex:1;border:0;border-radius:14px;padding:22px 0;font-size:20px;font-weight:600;background:#1e293b;color:#e2e8f0}" +
        "button.main{background:#dc2626;color:#fff;font-size:26px;padding:34px 0}input{font-size:28px;padding:12px;border-radius:12px;border:0;width:140px;text-align:center}.error{background:#7f1d1d;color:#fecaca;border-radius:12px;padding:12px 14px;margin:0}";

    public string Login(RemoteLoginNotice notice, TimeSpan retryAfter) =>
        Page($"<main>{Notice(notice, retryAfter)}<p>{Encode(texts["remote.page.pin"])}</p><form method=\"get\" action=\"/\"><input name=\"pin\" inputmode=\"numeric\" maxlength=\"4\" autofocus> " +
             $"<button type=\"submit\">{Encode(texts["remote.page.connect"])}</button></form></main>");

    public string Controller(string pin) =>
        Page("<main><div class=\"slide\" id=\"slide\"></div><div class=\"title\" id=\"title\"></div><div class=\"notes\" id=\"notes\"></div>" +
             $"<button class=\"main\" onclick=\"go('next')\">{Encode(texts["remote.page.next"])} ▶</button>" +
             $"<div class=\"row\"><button onclick=\"go('previous')\">◀ {Encode(texts["remote.page.previous"])}</button></div>" +
             $"<div class=\"row\"><button onclick=\"go('first')\">⏮ {Encode(texts["remote.page.first"])}</button><button onclick=\"go('last')\">{Encode(texts["remote.page.last"])} ⏭</button></div></main>" +
             "<script>" +
             $"const pin={Script(pin)};const idle={Script(texts["remote.page.idle"])};const slideWord={Script(texts["remote.page.slide"])};" +
             "function show(s){document.getElementById('slide').textContent=s.running?slideWord+' '+s.position+' / '+s.count:idle;" +
             "document.getElementById('title').textContent=s.title||'';document.getElementById('notes').textContent=s.notes||'';}" +
             "async function go(a){const r=await fetch('/go/'+a+'?pin='+pin,{method:'POST'});if(r.ok)show(await r.json());}" +
             "async function poll(){try{const r=await fetch('/state?pin='+pin);if(r.ok)show(await r.json());}catch(e){}}" +
             "poll();setInterval(poll,1500);</script>");

    private string Page(string body) =>
        $"<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>PulseOffice Deck</title><style>{Style}</style></head>" +
        $"<body><header>Deck · {Encode(texts["remote.page.header"])}</header>{body}</body></html>";

    private string Notice(RemoteLoginNotice notice, TimeSpan retryAfter) => notice switch
    {
        RemoteLoginNotice.WrongPin => $"<p class=\"error\">{Encode(texts["remote.page.wrongPin"])}</p>",
        RemoteLoginNotice.TooManyAttempts => $"<p class=\"error\">{Encode(texts.Format("remote.page.wait", Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))))}</p>",
        _ => string.Empty,
    };

    private string Script(string text) => System.Text.Json.JsonSerializer.Serialize(text);

    private string Encode(string text) => WebUtility.HtmlEncode(text).Replace("'", "&#39;", StringComparison.Ordinal);
}

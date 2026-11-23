using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AKCardScanner;

// Talks to Play!'s Arcade I/O HTTP Server, exactly as the original Windows app does.
public class PlayClient
{
    private readonly HttpClient _http = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(4) };
    public int Port { get; set; } = 9876;
    private string Url => $"http://127.0.0.1:{Port}/sys147/barcode";

    public async Task<(bool ok, string error)> Send(string data)
    {
        try
        {
            using var resp = await _http.PostAsync(Url, new StringContent(data, Encoding.UTF8, "application/json"));
            return resp.IsSuccessStatusCode ? (true, null) : (false, $"Play! answered {(int)resp.StatusCode}");
        }
        catch (Exception ex) { return (false, ex is TaskCanceledException ? "Timed out" : ex.Message); }
    }

    public Task<(bool ok, string error)> Ping(int port)
    {
        var old = Port; Port = port;
        var t = Send("");
        Port = old;
        return t;
    }
}

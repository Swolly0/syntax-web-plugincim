using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReportDcPlugin;

public class ReportDcPlugin : BasePlugin
{
    public override string ModuleName => "Discord Giris Cikis";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "www.plugincim.com";
    public override string ModuleDescription => "Sunucuya giren cikanlari köprü üzerinden Discord’a iletir.";

    private static readonly HttpClient _httpClient;
    private const string BridgeUrl = "https://syntax-web.com/cs2-dc.php"; // sabit köprü

    static ReportDcPlugin()
    {
        _httpClient = new HttpClient();
    }

    public class Config
    {
        public string ServerName { get; set; }
        public string Webhook { get; set; }
    }

    private static Config? _config;

    public override void Load(bool hotReload)
    {
        var configPath = Path.Join(ModuleDirectory, "Config.json");
        if (!File.Exists(configPath))
        {
            var data = new Config()
            {
                ServerName = "PRO",
                Webhook = "https://discord.com/api/webhooks/XXX/YYY"
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
            _config = data;
        }
        else
        {
            var text = File.ReadAllText(configPath);
            _config = JsonSerializer.Deserialize<Config>(text);
        }

        if (_config?.Webhook != null)
        {
            // Oyuncu bağlanma ve ayrılma olaylarını kaydet
            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        }

        base.Load(hotReload);
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || ValidateCallerPlayer(player) == false)
            return;

        var msg = $"( {player.PlayerName} | [{player.AuthorizedSteamID?.SteamId3} - {player.AuthorizedSteamID?.SteamId64}] sunucuya bağlandı.";
        if (!string.IsNullOrWhiteSpace(_config?.ServerName))
            msg = $"{_config.ServerName} | {msg}";

        Server.NextFrame(async () =>
        {
            await PostAsync(BridgeUrl, _config.Webhook, msg);
        });
    }

    private void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player == null || ValidateCallerPlayer(player) == false)
            return;

        var msg = $"( {player.PlayerName} | [{player.AuthorizedSteamID?.SteamId3} - {player.AuthorizedSteamID?.SteamId64}] sunucudan ayrıldı.";
        if (!string.IsNullOrWhiteSpace(_config?.ServerName))
            msg = $"{_config.ServerName} | {msg}";

        Server.NextFrame(async () =>
        {
            await PostAsync(BridgeUrl, _config.Webhook, msg);
        });
    }

















    private static bool ValidateCallerPlayer(CCSPlayerController? player)
    {
        if (player == null) return false;
        if (player.IsBot) return false;
        if (!player.IsValid
            || player.PlayerPawn == null
            || !player.PlayerPawn.IsValid
            || player.PlayerPawn.Value == null
            || !player.PlayerPawn.Value.IsValid)
            return false;
        return true;
    }

    private async Task PostAsync(string bridgeUri, string webhook, string message)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { webhook = webhook, content = message });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await _httpClient.PostAsync(bridgeUri, content);
        }
        catch
        {
            // hata sessizce geçilir
        }
    }
}

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReportDcPlugin;

public class ReportDcPlugin : BasePlugin
{
    public override string ModuleName => "Discord Rapor";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "www.plugincim.com";
    public override string ModuleDescription => "Oyuncuların raporlarını köprü üzerinden Discord’a iletir.";

    private static readonly HttpClient _httpClient;
    private const string BridgeUrl = "https://syntax-web.com/cs2-dc.php"; // sabit köprü

    static ReportDcPlugin()
    {
        _httpClient = new HttpClient();
    }

    public class Config
    {
        public string Prefix { get; set; }
        public string PlayerResponseNotEnoughInput { get; set; }
        public string PlayerResponseSuccessfull { get; set; }
        public string ServerName { get; set; }
        public Dictionary<string, string> Commands { get; set; } // komut=webhook
    }

    private static Config? _config;

    public override void Load(bool hotReload)
    {
        var configPath = Path.Join(ModuleDirectory, "Config.json");
        if (!File.Exists(configPath))
        {
            var data = new Config()
            {
                Prefix = "Kalendar",
                PlayerResponseNotEnoughInput = "❌ Rapor göndermek için daha fazla bilgi girmelisiniz.",
                PlayerResponseSuccessfull = "✅ Raporunuz başarıyla iletildi.",
                ServerName = "PRO",
                Commands = new Dictionary<string, string>()
                {
                    { "rapor", "https://discord.com/api/webhooks/XXX/YYY" }
                }
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

        if (_config?.Commands != null)
        {
            foreach (var command in _config.Commands)
            {
                AddCommand(command.Key, command.Key, (player, info) =>
                {
                    if (ValidateCallerPlayer(player) == false)
                        return;

                    if (info.ArgCount <= 1)
                    {
                        player.PrintToChat($" \x0b[{_config.Prefix}] \x01{_config.PlayerResponseNotEnoughInput}");
                        return;
                    }

                    var msg = $"( {player.PlayerName} | [{player.AuthorizedSteamID.SteamId3} - {player.AuthorizedSteamID.SteamId64}] = {info.ArgString}";
                    if (!string.IsNullOrWhiteSpace(_config.ServerName))
                        msg = $"{_config.ServerName} | {msg}";

                    Server.NextFrame(async () =>
                    {
                        await PostAsync(BridgeUrl, command.Value, msg);
                    });

                    player.PrintToChat($" \x0b[{_config.Prefix}] \x01{_config.PlayerResponseSuccessfull}");
                });
            }
        }

        base.Load(hotReload);
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

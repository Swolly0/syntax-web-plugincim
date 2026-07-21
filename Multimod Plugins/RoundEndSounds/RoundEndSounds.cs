using System;
using System.Linq;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace RoundEndSounds;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("ResetPeriod")]
    public int? play_type { get; set; } = 1; // 0 == team || 1 == random

    [JsonPropertyName("SoundPath")]
    public SoundPath[]? SoundPath { get; set; } = { new SoundPath { path = "filepath", team = 1 } };
}

public class SoundPath
{
    public string? path { get; set; } = ""; // sound file path
    public int? team { get; set; } = 1;  // 1 == all team || 2 == t || 3 == ct || Only for play_type = 1
}

public class RoundEndSounds : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Round End Sounds";
    public override string ModuleVersion => "1.0.5";
    public override string ModuleDescription => "Round sonlarinda muzik calmasini saglar.";
    public override string ModuleAuthor => "Plugincim.com";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

    private Config _config = new Config();
    public static List<SoundPath> ISoundPath = new List<SoundPath>();
    private readonly Dictionary<CCSPlayerController, bool> _inactive = new();

    private const int LicenseExpiryYear = 2024;
    private const int LicenseExpiryMonth = 12;
    private const int LicenseExpiryDay = 30;

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[RES] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(LicenseExpiryYear, LicenseExpiryMonth, LicenseExpiryDay, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            AddCommand("css_res", "Round End Sounds", (player, _) => Res(player));
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
        }
    }

    private void Res(CCSPlayerController? player)
    {
        if (player != null && _inactive[player])
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["res_active"]);
            _inactive[player] = false;
        }
        else
        {
            player?.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["res_inactive"]);
            if (player != null) _inactive[player] = true;
        }
    }


    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (Config.SoundPath != null && Config.SoundPath.Any())
        {
            if (Config.play_type == 1)
            {
                var random = new Random();
                var sound = Config.SoundPath.OrderBy(_ => random.NextDouble()).First();

                if (!string.IsNullOrEmpty(sound.path))
                {
                    foreach (var p in Utilities.GetPlayers())
                        if (p is { IsValid: true, IsBot: false, IsHLTV: false } && !_inactive[p])
                            p.ExecuteClientCommand($"play {sound.path}");
                }
            }
            else
            {

                foreach (var sound in Config.SoundPath.Where(c => c.team == @event.Winner).OrderBy(_ => Guid.NewGuid()).Take(1).ToList())
                {
                    if (!string.IsNullOrEmpty(sound.path))
                    {
                        foreach (var p in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false } && !_inactive[p]))
                            p.ExecuteClientCommand($"play {sound.path}");
                    }
                }
            }
        }

        return HookResult.Continue;
    }

    private void OnClientConnected(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        _inactive[player] = false;
    }

    private static string ReplaceTags(string message, bool remove = false)
    {
        string[] colorPatterns =
        {
            "{Default}", "{White}", "{Darkred}", "{Green}", "{LightYellow}", "{LightBlue}", "{Olive}", "{Lime}", "{Red}",
            "{LightPurple}", "{Purple}", "{Grey}", "{Yellow}", "{Gold}", "{Silver}", "{Blue}", "{DarkBlue}", "{BlueGrey}",
            "{Magenta}", "{LightRed}", "{Orange}"
        };
        string[] colorReplacements =
        {
            "\x01", "\x01", "\x02", "\x04", "\x09", "\x0B", "\x05", "\x06", "\x07", "\x03", "\x0E", "\x08", "\x09", "\x10",
            "\x0A", "\x0B", "\x0C", "\x0A", "\x0E", "\x0F", "\x10"
        };

        for (var i = 0; i < colorPatterns.Length; i++)
            if (!remove)
                message = "\u200e" + message.Replace(colorPatterns[i], colorReplacements[i]);
            else
                message = "\u200e" + message.Replace(colorPatterns[i], "");

        return message;

    }
}

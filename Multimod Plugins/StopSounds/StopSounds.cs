using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json.Serialization;
using MenuManager;
using CounterStrikeSharp.API.Core.Capabilities;

namespace StopSounds;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}

public class StopSounds : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Stop Sounds";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!ses ile oyun seslerini kontrol edebilirsin.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";


    public readonly Dictionary<CCSPlayerController, int?> AllSoundStatus = new();
    public readonly Dictionary<CCSPlayerController, int?> MuteKnifeStatus = new();
    public readonly Dictionary<CCSPlayerController, int?> MuteWalkStatus = new();
    public readonly Dictionary<CCSPlayerController, int?> MuteWeaponStatus = new();

    HashSet<uint> KnifeSounds = new()
    {
        3475734633, // Knife_Rightstab
        1769891506, // Knife_Leftstab
        3634660983, // Knife_SwingAir
        2486534908, // Knife_StabWall
        3124768561, // PlayerPOVScreen_Got_Damage_ClientSide
        524041390,  // Player_Got_Damage_ServerSide
        708038349,  // Player_Got_Damage_ClientSide
        427534867,  // Player_Got_Damage_FriendlyDamage_ServerSide
        2486534908,
        3767841471,
        2447320252,
        856190898
    };

    HashSet<uint> WalkSounds = new()
    {
        1194677450,
        1543118744,
        1016523349,
        4160462271,
        3666896632,
        3753692454,
        3688939408,
        961838155,
        1692050905
    };

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    private IMenuApi? _api;
    private readonly PluginCapability<IMenuApi?> _pluginCapability = new("menu:nfcore");
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        _api = _pluginCapability.Get();
        if (_api == null) Console.WriteLine("MenuManager Core not found...");
    }

    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            AddCommand("css_ses", "ses kontrol menusu", SesKomut);
            RegisterEventHandler<EventPlayerConnect>(OnPlayerConnect);

            HookUserMessage(452, um =>
            {
                var recipientfilter = new RecipientFilter();
                foreach (var p in Utilities.GetPlayers())
                    if (p.IsValid && !p.IsBot && (!MuteWeaponStatus.ContainsKey(p) || MuteWeaponStatus[p] == 0) && (!AllSoundStatus.ContainsKey(p) || AllSoundStatus[p] == 0))
                        recipientfilter.Add(p);

                um.Recipients = recipientfilter;
                um.Send();

                return HookResult.Continue;
            }, HookMode.Pre);

            HookUserMessage(208, um =>
            {
                var soundevent = um.ReadUInt("soundevent_hash");

                // Yeni alıcıları eklemek için mevcut Recipients listesine erişim sağlıyoruz
                var recipientfilter = new RecipientFilter();

                // Eğer ses KnifeSounds içerisinde ise işlem yap
                if (KnifeSounds.Contains(soundevent))
                {
                    foreach (var p in um.Recipients) // um.Recipients ile doğrudan alıcıları alıyoruz
                    {
                        if (p.IsValid && !p.IsBot && (!MuteKnifeStatus.ContainsKey(p) || MuteKnifeStatus[p] == 0) && (!AllSoundStatus.ContainsKey(p) || AllSoundStatus[p] == 0))
                            recipientfilter.Add(p);
                    }

                    um.Recipients = recipientfilter;
                    um.Send();
                }
                else
                if (WalkSounds.Contains(soundevent))
                {
                    foreach (var p in um.Recipients) // um.Recipients ile doğrudan alıcıları alıyoruz
                    {
                        if (p.IsValid && !p.IsBot && (!MuteWalkStatus.ContainsKey(p) || MuteWalkStatus[p] == 0) && (!AllSoundStatus.ContainsKey(p) || AllSoundStatus[p] == 0))
                            recipientfilter.Add(p);
                    }

                    um.Recipients = recipientfilter;
                    um.Send();
                }
                else
                {
                    foreach (var p in um.Recipients) // um.Recipients ile doğrudan alıcıları alıyoruz
                    {
                        if (p.IsValid && !p.IsBot && (!AllSoundStatus.ContainsKey(p) || AllSoundStatus[p] == 0))
                            recipientfilter.Add(p);
                    }

                    um.Recipients = recipientfilter;
                    um.Send();
                }

                return HookResult.Continue;
            }, HookMode.Pre);
        }
    }

    private void SesKomut(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;
        SesMenu(player);
    }

    public void SesMenu(CCSPlayerController player)
    {
        var menu = _api!.GetMenu(Localizer["menu_title"]);

        if (!AllSoundStatus.ContainsKey(player) || AllSoundStatus[player] == 0)
            menu.AddMenuOption(Localizer["menu_allsounds"] + Localizer["disable"], (player, option) => SesMenu_(player, "allsounds"));
        else
            menu.AddMenuOption(Localizer["menu_allsounds"] + Localizer["enable"], (player, option) => SesMenu_(player, "allsounds"));

        if (!MuteKnifeStatus.ContainsKey(player) || MuteKnifeStatus[player] == 0)
            menu.AddMenuOption(Localizer["menu_knife"] + Localizer["disable"], (player, option) => SesMenu_(player, "knife"));
        else
            menu.AddMenuOption(Localizer["menu_knife"] + Localizer["enable"], (player, option) => SesMenu_(player, "knife"));

        if (!MuteWalkStatus.ContainsKey(player) || MuteWalkStatus[player] == 0)
            menu.AddMenuOption(Localizer["menu_walk"] + Localizer["disable"], (player, option) => SesMenu_(player, "walk"));
        else
            menu.AddMenuOption(Localizer["menu_walk"] + Localizer["enable"], (player, option) => SesMenu_(player, "walk"));

        if (!MuteWeaponStatus.ContainsKey(player) || MuteWeaponStatus[player] == 0)
            menu.AddMenuOption(Localizer["menu_weapon"] + Localizer["disable"], (player, option) => SesMenu_(player, "weapon"));
        else
            menu.AddMenuOption(Localizer["menu_weapon"] + Localizer["enable"], (player, option) => SesMenu_(player, "weapon"));

        menu.Open(player);
    }

    public void SesMenu_(CCSPlayerController player, string option)
    {
        if (option == "allsounds")
        {
            if (!AllSoundStatus.ContainsKey(player) || AllSoundStatus[player] == 0)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["allsound_mute"]);
                AllSoundStatus[player] = 1;
            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["allsound_unmute"]);
                AllSoundStatus[player] = 0;
            }
        }
        else
        if (option == "knife")
        {
            if (!MuteKnifeStatus.ContainsKey(player) || MuteKnifeStatus[player] == 0)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["knife_mute"]);
                MuteKnifeStatus[player] = 1;
            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["knife_unmute"]);
                MuteKnifeStatus[player] = 0;
            }
        }
        else
        if (option == "walk")
        {
            if (!MuteWalkStatus.ContainsKey(player) || MuteWalkStatus[player] == 0)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["walk_mute"]);
                MuteWalkStatus[player] = 1;
            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["walk_unmute"]);
                MuteWalkStatus[player] = 0;
            }
        }
        else
        if (option == "weapon")
        {
            if (!MuteWeaponStatus.ContainsKey(player) || MuteWeaponStatus[player] == 0)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["weapon_mute"]);
                MuteWeaponStatus[player] = 1;
            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["weapon_unmute"]);
                MuteWeaponStatus[player] = 0;
            }
        }

        SesMenu(player);
    }

    private HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        if (@event.Userid == null)
            return HookResult.Continue;

        AllSoundStatus[@event.Userid] = 0;
        MuteKnifeStatus[@event.Userid] = 0;
        MuteWalkStatus[@event.Userid] = 0;
        MuteWeaponStatus[@event.Userid] = 0;
        return HookResult.Continue;
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

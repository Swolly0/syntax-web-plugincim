using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBKomutcuSure;

public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;

	[JsonPropertyName("command_time")]
    public int komut_suresi { get; set; } = 30;
	
	[JsonPropertyName("vote_time")]
    public int oylama_suresi { get; set; } = 30;
	
	[JsonPropertyName("vote_end_auto_t")]
    public bool oylama_sonu_oto_t { get; set; } = true;
}

public class JBKomutcuSure : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "JB Komutcu Sure";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "!komsure ile komutcunun suresi takip edilebilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;


    private static int iKalanSure = -1;
    private static int iVoteCountDown = -1;
    private static int Degis = 0;
    private static int Kal = 0;

    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex;
    private readonly Dictionary<CCSPlayerController, bool> Oy = new();
    CCSPlayerController? iWarden;

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 19; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Warden Time] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
        }

        Config = config;
        Stringlocalizer = Localizer;
    }
	
    public override void Load(bool hotReload)
    {
        var dateTime = new DateTime(lisans_bitis_yil, lisans_bitis_ay, lisans_bitis_gun, 0, 0, 0, DateTimeKind.Utc);
        var dateWithOffset = new DateTimeOffset(dateTime).ToUniversalTime();
        long timestamp = dateWithOffset.ToUnixTimeSeconds();

        if (timestamp > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
			// KOMUTÇU SİSTEMİ
			AddCommand("css_w", "", (player, command) => Warden(player, command));
			AddCommand("css_uw", "", (player, command) => UnWarden(player, command));

			RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
			RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

			AddTimer(1.0f, () =>
			{
				if(iWarden == null || !iWarden.is_valid())
				{
					iKalanSure = -1;
					iVoteCountDown = -1;
				}

				if (!iWarden.is_valid() || !iWarden.is_ct())
					Reset();
			}, TimerFlags.STOP_ON_MAPCHANGE);
			// KOMUTÇU SİSTEMİ

			AddCommand("css_komsure", "Komutcunun kalan komut suresini degistirir.", (player, command) => Kom_Sure(player, command));
			AddCommand("css_komkalan", "Komutcunun kalan komut suresi.", (player, command) => Kom_Kalan(player, command));
			AddCommand("css_komdk", "Komutcu degis kal oylamasi yapar.", (player, command) => Kom_DK(player, command));
			AddCommand("css_komdk0", "Komutcu degis kal oylamasini iptal eder.", (player, command) => Kom_DK0(player, command));

			RegisterListener<Listeners.OnTick>(() =>
			{
                if(iVoteCountDown < 0 && timer_ex != null && iWarden == null) { timer_ex!.Kill(); timer_ex = null; }

				if (iVoteCountDown >= 1 && iKalanSure == -1 && iWarden.is_valid())
					foreach (var p in Utilities.GetPlayers())
						if (p != null && p.IsValid && !p.IsBot && !p.IsHLTV)
							if (iVoteCountDown <= 10)
								p.PrintToCenterHtml($"<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png' width='256px' height='128px'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["vote_title1"] + $"<br/><img src='https://www.plugincim.com/assets/images/cs2/numbers/{Convert.ToInt32(iVoteCountDown)}.png' width='256px' height='128px'/><br><br><font color='#FF0000'>" + Localizer["vote_change"] + $"(!1):</font> {Degis}   <font color='#00FF00'>" + Localizer["vote_remain"] + $"(!2):</font> {Kal}</p>");
							else
								p.PrintToCenterHtml($"<img src='https://www.plugincim.com/forum/data/assets/logo/yataybeyaz.png' width='256px' height='128px'/><br/><br/><p style='font-weight: 700; color: red; font-size: 20px;'>" + Localizer["vote_title2", iVoteCountDown] + $"<br><br><font color='#FF0000'>" + Localizer["vote_change"] + $"(!1):</font> {Degis}   <font color='#00FF00'>" + Localizer["vote_remain"] + $"(!2):</font> {Kal}</p>");
			});
		}
    }
	
    public void Kom_Sure(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            var Sure = command.ArgByIndex(1);

            if (Sure != null && Sure != "" && IsInt(Sure))
            {
                if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
                {
                    iKalanSure = Convert.ToInt32(Sure) * 60;
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_change_time", player.PlayerName, Sure]);
                } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["command_usage"]);
        }
    }

    public void Kom_Kalan(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            int kalan = iKalanSure;

            if (iKalanSure == -1)
                kalan = 0;

            int dakika = 0;

            while (kalan - 60 >= 0)
            {
                kalan -= 60;
                dakika++;
            }

            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_vote_countdown", dakika, kalan]);
        }

        return;
    }

    public void Kom_DK(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if(iWarden == null || !iWarden.is_valid() || !iWarden.is_ct())
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["countdown_not_active"]);
                return;
            }

            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
                Start_Vote();
            else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
        }
    }

    public void Kom_DK0(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && NativeAPI.GetMapName().Contains("jb_"))
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") || iWarden == player)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_stop", player.PlayerName]);
                iVoteCountDown = -1;
            } else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);

        }
    }

    public void Start_Vote()
    {
        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_start"]);
        iVoteCountDown = Config.oylama_suresi;
        iKalanSure = -1;
        Degis = 0; Kal = 0;

        var voteMenu = new CenterHtmlMenu(Localizer["vote_title", Config.komut_suresi]);
        voteMenu.AddMenuOption(Localizer["vote_change"], (player, option) => HandleVote(player, 0));
        voteMenu.AddMenuOption(Localizer["vote_remain"], (player, option) => HandleVote(player, 1));

        foreach (var p in Utilities.GetPlayers())
            if (p.is_valid() && p.is_t())
            {
                Oy[p] = false;
                MenuManager.OpenCenterHtmlMenu(this, p, voteMenu);
            }
    }

    private void HandleVote(CCSPlayerController player, int option)
    {
        if (iVoteCountDown >= 1 && !Oy[player] && player.is_t())
        {
            if (option == 0)
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_vote_change"]);
                Degis++;
            }
            else
            {
                player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_vote_remain"]);
                Kal++;
            }

            Oy[player] = true;
        }
    }

    public void Reset()
    {
        iKalanSure = -1;
        iVoteCountDown = -1;
        iWarden = null;
    }

    public void StartTimer()
    {
        if (timer_ex != null) timer_ex!.Kill();

        timer_ex = AddTimer(1.0f, () =>
        {
            if (iWarden.is_valid())
            {
                if (iKalanSure >= 1)
                {
                    iKalanSure--;

                    if (iKalanSure >= 0 && iKalanSure <= 10)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_count_down1", iKalanSure]);
                    else
                    if (iKalanSure == 60 || iKalanSure == 300 || iKalanSure == 600 || iKalanSure == 900 || iKalanSure == 1200)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_count_down2", iKalanSure / 60]);
                }
                else
                if (iKalanSure == 0)
                {
                    iKalanSure = -1;
                    Start_Vote();
                }


                if (iVoteCountDown >= 1)
                    iVoteCountDown--;
                else
                if (iVoteCountDown == 0)
                {
                    iVoteCountDown = -1;
                    if (Kal > Degis)
                    {
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_end_win", Config.komut_suresi]);
                        iKalanSure = Config.komut_suresi * 60;
                    }
                    else
                    if (Degis > Kal)
                    {
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_end_lose"]);

                        if (Config.oylama_sonu_oto_t)
                        {
                            foreach (var p in Utilities.GetPlayers())
                                if (p.is_valid() && p.is_ct())
                                    p.SwitchTeam(CsTeam.Terrorist);

                            iWarden = null;
                        }
                    }
                    else
                    {
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_end_draw"]);
                        Start_Vote();
                    }
                }

            }
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }





    public void Warden(CCSPlayerController? player, CommandInfo command)
    {
        if ((iWarden == null || !iWarden.is_valid() || !iWarden.is_ct()) && player.is_valid() && player.is_ct())
        {
            iWarden = player;
            iKalanSure = Config.komut_suresi * 60;
            StartTimer();
        }

        return;
    }

    public void UnWarden(CCSPlayerController? player, CommandInfo command)
    {
        if (iWarden == player)
            Reset();

        return;
    }


    [GameEventHandler]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player.is_valid() && iWarden == player)
                Reset();

            return HookResult.Continue;
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
    {
        if (NativeAPI.GetMapName().Contains("jb_"))
        {
            CCSPlayerController player = @event.Userid;

            if (player.is_valid() && iWarden.is_valid() && iWarden == player)
            {
                if (@event.Team != 3)
                    Reset();

                return HookResult.Continue;
            }
        }

        return HookResult.Continue;
    }

    private bool IsInt(string sVal)
    {
        foreach (char c in sVal)
        {
            int iN = (int)c;
            if ((iN > 57) || (iN < 48))
                return false;
        }
        return true;
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
    private static bool IsValidConfigString(string value) => !string.IsNullOrEmpty(value) && value != "-"; // This is a "lambda expression body method"
}



public static class Lib
{
    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid && player.PlayerPawn.IsValid;
    }

    static public bool is_t(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 2;
    }
    static public bool is_ct(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 3;
    }
}
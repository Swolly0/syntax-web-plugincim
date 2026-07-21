using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using System.Text.Json.Serialization;

namespace JBKomutcuAl;
public class Config : IBasePluginConfig
{
    [JsonPropertyName("Prefix")]
    public string EklentiTagi { get; set; } = "{Blue}[www.plugincim.com]";

    [JsonPropertyName("ConfigVersion")]
    public int Version { get; set; } = 1;
}
public class JBKomutcuAl : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName { get; } = "Komutcu Al";
    public override string ModuleVersion { get; } = "1.0.0";
    public override string ModuleDescription { get; } = "JB | !komal komutu ile komutcu oylamasi baslatilir.";
    public override string ModuleAuthor { get; } = "https://www.plugincim.com/";
    private int ModuleConfigVersion => 1;
    internal static IStringLocalizer? Stringlocalizer;

	// LSANS BİTİŞ
    private static int Toplam_Aday_Limiti = 5;
    private static int Geri_Sayim_Suresi = 15;


    public int Toplam_Aday;
    public float Geri_Sayim;
    public bool Komutcu_Oylamasi;
    public bool Oylama_Basladi;
    public CounterStrikeSharp.API.Modules.Timers.Timer? timer_ex;
    private readonly Dictionary<CCSPlayerController, bool> Aday = new();
    private readonly Dictionary<CCSPlayerController, bool> Oy = new();
	private readonly Dictionary<CCSPlayerController, int> Toplam_Oy = new();
    private readonly Dictionary<CCSPlayerController, int> AdayOption = new();

    // LISANS
    public int lisans_bitis_yil = 2025; // Lisansin bitecegi yil
    public int lisans_bitis_ay = 12; // Lisansin bitecegi ay
    public int lisans_bitis_gun = 30; // Lisansin bitecegi gun
    // LISANS

    public required Config Config { get; set; }
    public void OnConfigParsed(Config config)
    {
        if (config.Version < ModuleConfigVersion)
        {
            Console.WriteLine($"[Komutcu Al] You are using an old configuration file. Version you are using:{config.Version} - New Version: {ModuleConfigVersion}");
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
			AddCommand("css_komal", "Komutcu oylamasi baslatma.", (player, command) => Komutcu_Al(player, command));
			AddCommand("css_komal0", "Komutcu oylamasi iptal etme.", (player, command) => Komutcu_Al0(player, command));

			AddCommand("css_komaday", "Komutcu oylamasina aday olma.", (player, command) => Komutcu_Aday(player, command));
			AddCommand("css_komaday0", "Komutcu oylamasi adayligini siler.", (player, command) => Komutcu_Aday0(player, command));
			AddCommand("css_komadaysil", "Komutcu oylamasi adayligini siler.", (player, command) => Komutcu_Aday0(player, command));

			RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
			RegisterListener<Listeners.OnTick>(() =>
			{
				if(Oylama_Basladi)
				{
					string printcenter = "<img src='https://www.plugincim.com/assets/images/plugin-banner.png' width='256px' height='128px'/><br/><br/>";
					foreach (var p in Utilities.GetPlayers())
						if(p != null && p.IsValid && !p.IsBot && Aday[p])
							printcenter = $"{printcenter} {p.PlayerName} <font color='#00FF00'>{Toplam_Oy[p]} " + Localizer["vote"] + ".</font><br>";

					printcenter = $"{printcenter}<br>";

					foreach (var p in Utilities.GetPlayers())
						if(p.is_valid()){
							p.PrintToCenterHtml($"{printcenter}");
						}	
				}
			});
		}
	}

    public void Komutcu_Al(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !NativeAPI.GetMapName().Contains("jb_")) return;
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);
            return;
        }
        if (Komutcu_Oylamasi)
        {
            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_is_active"]);
            return;
        }

        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["warden_voting_start", player.PlayerName]);

        Komutcu_Oylamasi = true;
        Oylama_Basladi = false;
        Geri_Sayim = Geri_Sayim_Suresi;
        Toplam_Aday = 0;

        foreach (var p in Utilities.GetPlayers().Where(p => p.is_valid()))
        {
            Aday[p] = false;
            Oy[p] = false;
            Toplam_Oy[p] = 0;
        }

        timer_ex?.Kill();
        timer_ex = AddTimer(1.0f, () =>
        {
            if (Geri_Sayim <= 0.0f)
            {
                timer_ex?.Kill();
                timer_ex = null;

                var adaylar = Utilities.GetPlayers().Where(p => p.is_valid() && Aday[p]).ToList();
                foreach (var p in Utilities.GetPlayers().Where(p => p.is_valid()))
                {
                    MenuManager.CloseActiveMenu(p);
                    p.PrintToCenterHtml("", 0);
                }

                if (adaylar.Count >= 2)
                {
                    KonusturAdaylariSirayla(adaylar, 0);
                }
                else if (adaylar.Count == 1)
                {
                    Komutcu_Oylamasi = false;
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_one_player", adaylar[0].PlayerName]);
                    adaylar[0].SwitchTeam(CsTeam.CounterTerrorist);
                }
                else
                {
                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_no_player"]);
                    Komutcu_Oylamasi = false;
                }
                return;
            }

            Geri_Sayim -= 1.0f;
            if (Geri_Sayim <= 10.0f)
            {
                Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_countdown", Geri_Sayim]);
            }
        }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void KonusturAdaylariSirayla(List<CCSPlayerController> adaylar, int index)
    {
        if (index >= adaylar.Count)
        {
            ShowVoteMenu();
            return;
        }

        var aday = adaylar[index];
        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["can_talk", aday.PlayerName]);

        aday.VoiceFlags = VoiceFlags.Normal; // Konuşmayı aç

        AddTimer(5.0f, () =>
        {
            aday.VoiceFlags = VoiceFlags.Muted; // 5 saniye sonra sustur
            KonusturAdaylariSirayla(adaylar, index + 1); // Sıradaki adaya geç
        });
    }

    public void Komutcu_Al0(CCSPlayerController? player, CommandInfo info)
    {	
		if(player != null && NativeAPI.GetMapName().Contains("jb_"))
			if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
			{
				Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_stop", player.PlayerName]);
				Komutcu_Oylamasi = false;
				Oylama_Basladi = false;

                foreach (var p in Utilities.GetPlayers())
                    if (p.is_valid())
                    {
                        MenuManager.CloseActiveMenu(p);
                        p.PrintToCenterHtml("", 0);
                    }

                if (timer_ex != null)
					timer_ex?.Kill();

				timer_ex = null;
			} 
			else 
				player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["permissions"]);							
    }	
	
	public void Komutcu_Aday(CCSPlayerController? player, CommandInfo info)
    {	
		if(player != null && NativeAPI.GetMapName().Contains("jb_")){
			if(Komutcu_Oylamasi && !Oylama_Basladi)
            {
				if(Toplam_Aday < Toplam_Aday_Limiti){
					if(!Aday[player]){

						Aday[player] = true;
						Toplam_Aday++;
                        player.VoiceFlags = VoiceFlags.Normal;

                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_join_vote", player.PlayerName, Toplam_Aday, Toplam_Aday_Limiti]);

					} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_join_already"]);		
				} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_join_limit", Toplam_Aday, Toplam_Aday_Limiti]);		
			} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_not_active"]);							
		}
    }	
	
	public void Komutcu_Aday0(CCSPlayerController? player, CommandInfo info)
    {	
		if(player != null && NativeAPI.GetMapName().Contains("jb_")){
			if(Komutcu_Oylamasi && !Oylama_Basladi){
				if(Aday[player]){

					Aday[player] = false;
					Toplam_Aday--;
                    player.VoiceFlags = VoiceFlags.Muted;

                    Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_leave_vote", player.PlayerName, Toplam_Aday, Toplam_Aday_Limiti]);
				
				} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["player_not_join"]);		
			} else player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_not_active"]);
        }
    }	
	
    private void OnClientDisconnect(int playerSlot)
    {	
	    CCSPlayerController player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player.is_valid()) return;
		
		if(Komutcu_Oylamasi && Aday[player] && Oylama_Basladi){
			Aday[player] = false;
			Toplam_Aday--;
		}
	}
	
	
	
	
	
	
	
	
	
	
	
	
	
	
    private void ShowVoteMenu()
    {
		if(Komutcu_Oylamasi){
			
			Oylama_Basladi = true;
			Geri_Sayim = 10;
			timer_ex = AddTimer(1.0f, () =>
			{
				if(Geri_Sayim == 0.0){
					Komutcu_Oylamasi = false;	
					Oylama_Basladi = false;
										
					int Oylar = -1;
					CCSPlayerController? Kazanan = null;
					
					foreach (var p in Utilities.GetPlayers())
						if(p != null && p.IsValid && !p.IsBot && Aday[p]){
							if(Toplam_Oy[p] > Oylar)
							{
								Oylar = Toplam_Oy[p];
								Kazanan = p;
							}			
						}							
											
					if(Kazanan.is_valid()){
						Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_win_player", Kazanan.PlayerName, Oylar]);		
						Kazanan.SwitchTeam(CsTeam.CounterTerrorist);
					}

                    timer_ex?.Kill();
                    timer_ex = null;

                    return;
				} else {
                    if (Geri_Sayim <= 10.0)
                        Server.PrintToChatAll(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_end_countdown", Geri_Sayim]);

                    Geri_Sayim -= 1.0f;
                }
            }, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);


			int i = 0;
			var voteMenu = new CenterHtmlMenu(Localizer["vote_title"]);
			foreach (var p in Utilities.GetPlayers())
				if(p != null && p.IsValid && !p.IsBot && Aday[p])
				{
					i++;
                    AdayOption[p] = i;

                    string playerName = p.PlayerName;
					voteMenu.AddMenuOption(playerName, (player, option) => HandleVote(player, p));
				}
				
			foreach (var p in Utilities.GetPlayers())
				if(p.is_valid())
					MenuManager.OpenCenterHtmlMenu(this, p, voteMenu);
		}
    }

    private void HandleVote(CCSPlayerController player, CCSPlayerController target)
    {
		if(Komutcu_Oylamasi && !Oy[player]){			
			Toplam_Oy[target]++;
			Oy[player] = true;

            MenuManager.CloseActiveMenu(player);
            player.PrintToCenterHtml("", 0);

            player.PrintToChat(ReplaceTags($"{Config.EklentiTagi} ") + Localizer["vote_given", target.PlayerName, Toplam_Oy[target]]);			
		}
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






public static class Lib{

    static public bool is_valid(this CCSPlayerController? player)
    {
        return player != null && player.IsValid &&  player.PlayerPawn.IsValid;
    }

    static public bool is_t(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 2;
    }

    static public bool is_ct(this CCSPlayerController? player)
    {
        return player != null && is_valid(player) && player.TeamNum == 3;
    }

    // yes i know the null check is redundant but C# is dumb
    static public bool is_valid_alive(this CCSPlayerController? player)
    {
        return player != null && player.is_valid() && player.PawnIsAlive && player.get_health() > 0;
    }

    static public CCSPlayerPawn? pawn(this CCSPlayerController? player)
    {
        if(player == null || !player.is_valid())
        {
            return null;
        }

        CCSPlayerPawn? pawn = player.PlayerPawn.Value;

        return pawn;
    }	
	
    static public int get_health(this CCSPlayerController? player)
    {
        CCSPlayerPawn? pawn = player.pawn();

        if(pawn == null)
        {
            return 100;
        }

        return pawn.Health;
    }	
}
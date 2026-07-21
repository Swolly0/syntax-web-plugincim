using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using StealthSpectator.Extensions;  // SetNoDraw uzantısını içerir.

namespace StealthSpectator
{
    // Bu eklenti, adminlerin "gizli" modda olmasını sağlar.
    // Oyuncu modeli NoDraw bayrağı ile gizlenir ve admin izleyici moduna geçer.
    // Diğer oyuncular, gizli admini oyunda göremez.
    public class StealthSpectator : BasePlugin
    {
        public override string ModuleName => "Gizli İzleyici";
        public override string ModuleAuthor => "MPYawn";
        public override string ModuleVersion => "1.2.2";  // Güncellenmiş sürüm

        // Gizli modda olan admin oyuncuları (SteamID ile) saklar.
        private readonly HashSet<ulong> _hiddenPlayers = new HashSet<ulong>();
        // Adminin gizli moddan çıkarken önceki takımını geri yüklemek için saklar.
        private readonly Dictionary<ulong, CsTeam> _previousTeams = new Dictionary<ulong, CsTeam>();

        // Gerekli admin yetki bayrağı
        private const string RequiredFlag = "@css/ban";

        public override void Load(bool hotReload)
        {
            // Opsiyonel: yükleme olaylarını loglayabilir veya ek başlatma işlemleri yapabilirsiniz.
        }

        // Komut: !css_hide (ve alias !css_stealth)
        // Admini gizli izleyici moduna alır.
        [ConsoleCommand("css_hide", "Gizli izleyici moduna geçiş yap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnCommandHide(CCSPlayerController? player, CommandInfo command)
        {
            if (!PermissionCheck(player))
                return;
            if (AlreadyHiddenCheck(player))
                return;
            if (AlreadySpectatorCheck(player))
                return;

            EnterStealthMode(player);
        }

        // !css_hide için alias
        [ConsoleCommand("css_stealth", "Alias: !css_hide")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnCommandStealth(CCSPlayerController? player, CommandInfo command)
            => OnCommandHide(player, command);

        private void EnterStealthMode(CCSPlayerController player)
        {
            ulong steamId = player.SteamID;
            _previousTeams[steamId] = player.Team;
            _hiddenPlayers.Add(steamId);

            // Admini izleyici moduna al
            player.ChangeTeam(CsTeam.Spectator);
            // Adminin modelini gizle
            player.Entity.SetNoDraw(true);

            SendMessage(player, "Gizli izleyici moduna girdiniz. Çıkmak için !css_unhide komutunu kullanın.");
        }

        // Komut: !css_unhide
        // Gizli moddan çıkar ve adminin önceki takımına döner.
        [ConsoleCommand("css_unhide", "Gizli izleyici modundan çık ve önceki takıma dön")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnCommandUnhide(CCSPlayerController? player, CommandInfo command)
        {
            if (!PermissionCheck(player))
                return;
            if (!IsHiddenCheck(player))
                return;

            ulong steamId = player.SteamID;
            _hiddenPlayers.Remove(steamId);

            // NoDraw bayrağını kaldır
            player.Entity.SetNoDraw(false);

            // Önceki takımı geri yükle
            if (_previousTeams.TryGetValue(steamId, out var previousTeam))
            {
                player.ChangeTeam(previousTeam);
                _previousTeams.Remove(steamId);
                SendMessage(player, "Gizli izleyici modundan çıktınız.");
            }
            else
            {
                player.ChangeTeam(CsTeam.Terrorist); // Varsayılan takım, ihtiyaca göre değiştirilebilir
                SendMessage(player, "Gizli izleyici modundan çıktınız (önceki takım bulunamadı, Terörist takımına alındınız).");
            }
        }

        // Yardımcı: Oyuncunun geçerli olup olmadığını, bot olup olmadığını ve gerekli yetkiye sahip olup olmadığını kontrol et.
        private bool PermissionCheck(CCSPlayerController? player)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return false;
            if (!AdminManager.PlayerHasPermissions(player, RequiredFlag))
            {
                SendMessage(player, "Bu komutu kullanmak için gerekli izinlere sahip değilsiniz.");
                return false;
            }
            return true;
        }

        private bool AlreadyHiddenCheck(CCSPlayerController? player)
        {
            if (player != null && _hiddenPlayers.Contains(player.SteamID))
            {
                SendMessage(player, "Zaten gizli izleyici modundasınız.");
                return true;
            }
            return false;
        }

        private bool AlreadySpectatorCheck(CCSPlayerController? player)
        {
            if (player != null && player.Team == CsTeam.Spectator && !_hiddenPlayers.Contains(player.SteamID))
            {
                SendMessage(player, "Zaten izleyicisiniz. Gizli moddan çıkmak için !css_unhide komutunu kullanın.");
                return true;
            }
            return false;
        }

        private bool IsHiddenCheck(CCSPlayerController? player)
        {
            if (player != null && !_hiddenPlayers.Contains(player.SteamID))
            {
                SendMessage(player, "Gizli izleyici modunda değilsiniz.");
                return false;
            }
            return true;
        }

        // Yardımcı: Oyuncuya sohbet mesajı gönder
        private void SendMessage(CCSPlayerController player, string message)
        {
            player.PrintToChat($" \x0b[GIZLEN] \x01{message}");
        }
    }
}

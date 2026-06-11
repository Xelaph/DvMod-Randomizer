using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;

namespace DvMod.Randomizer {
    [HarmonyPatch(typeof(PlayerManager))]
    public class DeathLinkPatch {
        private static void SendDeathLink(TrainCar _) =>
            Main.Player!.deathLinkService!.SendDeathLink(new DeathLink(Main.Player.Session.Players.ActivePlayer.Name, "derailed"));
        
        public static void Derail(DeathLink _) => PlayerManager.Car?.Derail();
        
        [HarmonyPrefix, HarmonyPatch(nameof(PlayerManager.SetCar))]
        public static void Prefix(TrainCar newCar) {
            if (Main.Player == null) return;
            if (newCar == PlayerManager.Car) return;
            if (!Main.Player.Config.DeathLink) return;
            if (newCar != null)
                newCar.OnDerailed += SendDeathLink;
            if (PlayerManager.Car != null)
                PlayerManager.Car.OnDerailed -= SendDeathLink;
        }
    }
}
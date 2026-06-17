using HarmonyLib;
using LocoSim.Implementations;


namespace DvMod.Randomizer {
    [HarmonyPatch(typeof(SimulationFlow), "Tick")]
    public static class UpdatePatch {
        public static void Postfix() {
            if (Main.IsConnected) Main.Player.CallUpdate();
        }
    }
}
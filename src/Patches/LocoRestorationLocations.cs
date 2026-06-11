using HarmonyLib;
using LocoSim.Implementations;


namespace DvMod.Randomizer {
    [HarmonyPatch(typeof(SimulationFlow))]
    public static class UpdatePatch {
        [HarmonyPostfix, HarmonyPatch("Tick")]
        public static void Postfix() {
            Main.Player?.CallUpdate();
        }
    }
}
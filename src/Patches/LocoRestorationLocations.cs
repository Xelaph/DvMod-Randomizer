using HarmonyLib;
using LocoSim.Implementations;


namespace DvMod.Randomizer {
    [RiderHarmonyPatch(typeof(SimulationFlow))]
    public static class UpdatePatch {
        [HarmonyPostfix, RiderHarmonyPatch("Tick")]
        public static void Postfix() {
            Main.Player?.CallUpdate();
        }
    }
}
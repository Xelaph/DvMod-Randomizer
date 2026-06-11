using HarmonyLib;
using DV;
using DV.ThingTypes;
using DV.Utils;
using System.Linq;

namespace DvMod.Randomizer
{
    [HarmonyPatch(typeof(CommsRadioCrewVehicle))]
    public static class CrewCommsPatch {
        [HarmonyPostfix, HarmonyPatch("UpdateAvailableVehicles")]
        public static void CustomVehicles(CommsRadioCrewVehicle __instance) {
            if (Main.Player == null) 
                return;
            __instance.availableVehiclesForSpawn.Clear();
            __instance.availableVehiclesForSpawn.AddRange(
                SingletonBehaviour<CarSpawner>.Instance.crewVehicleGarages
                .Where(Main.Player.HasUnlocked)
                .SelectMany(g => g.garageCarLiveries));
            __instance.availableVehiclesForSpawn.AddRange(SingletonBehaviour<CarSpawner>.Instance.vehiclesWithoutGarage);
        }
    }

    [HarmonyPatch(typeof(GaragePadlockUnlocker))]
    public static class GaragePatcher {
        [HarmonyPrefix, HarmonyPatch("OnGarageUnlocked")]
        public static void Prefix(GarageType_v2 unlockedGarageType) {
            if (Main.Player == null) return;
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (unlockedGarageType.v1) {
                case Garage.Caboose: Main.Player.UnlockCheck(0x691); break;
                case Garage.DM1U: Main.Player.UnlockCheck(0x693); break;
                case Garage.Bob: Main.Player.UnlockCheck(0x692); break;
                case Garage.DE6_Slug: Main.Player.UnlockCheck(0x690); break;
            }
        }
    }
}
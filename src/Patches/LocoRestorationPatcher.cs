using DV.LocoRestoration;
using DV.Utils;
using HarmonyLib;

namespace DvMod.Randomizer
{
    [HarmonyPatch(typeof(PaintStationItemInstantiator))]
    public class PainterItemInstantiatorPatch {
        [HarmonyPrefix, HarmonyPatch("Awake")]
        public static bool Prefix() => Main.Player == null;
    }
    [HarmonyPatch(typeof(LocoRestorationController))]
    public static class LocoRestorationPatcher {

        [HarmonyPrefix, HarmonyPatch("InitCarForRestoration")]
        public static bool Prefix(LocoRestorationController __instance, TrainCar car){
            if (!Main.PlayerExists) return true;
            if (__instance.State <= LocoRestorationController.RestorationState.S3_RerailedCars) {
                Main.Log("AP item not acquired: deleting car "+car.carType);
                SingletonBehaviour<CarSpawner>.Instance.DeleteCar(car);
                return false;
            }
            return true;
        }
        [HarmonyPostfix, HarmonyPatch("DeliverPartCoro")]
        public static void PartsPostfix(TrainCar ___loco, LocoRestorationController __instance) {
            if (!Main.PlayerExists) return;
            Main.Player.UnlockCheck(RandoCommonData.AP_ID.LOC_RELIC_PARTS+RandoCommonData.GetOrderFromLocoType(___loco.carType));
            if (!Main.Player.CanFinishRelic(___loco.carType)) {
                __instance.installPartsModule.ThingBought -= __instance.OnInstallPartsPaid;
                __instance.installPartsModule.SetUnitsToBuy(0f);
            }

        }

        [HarmonyPostfix, HarmonyPatch("SetupListenersForPaintJob")]
        public static void PaintPostfix(TrainCar ___loco, bool on) {
            if (Main.Player != null && !on) Main.Player.UnlockCheck(RandoCommonData.AP_ID.LOC_RELIC_PAINTED+RandoCommonData.GetOrderFromLocoType(___loco.carType));
        }
    }
}
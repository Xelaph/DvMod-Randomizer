using DV.UI;
using DV.Utils;
using HarmonyLib;
using JetBrains.Annotations;
using UnityModManagerNet;


namespace DvMod.Randomizer
{

    /*[HarmonyPatch(typeof(DevUtil), nameof(DevUtil.IsDevMachine))]
    public static class DevPatch {
        public static void Postfix(ref bool __result) => __result = true;
    }*/

    public class Settings : UnityModManager.ModSettings, IDrawable {
        [Draw("Server address")] public string serverName = "localhost";
        [Draw("Port")] public int Port = 38281;
        [Draw("Slot name (Must correspond to the name given to the Archipelago Server)")] public string User="";
        [Draw("Password (leave blank if no password)")] public string Password = "";
        [Draw("Create a new Archipelago save on new career save?")] public bool CreateAPSave = false;
        public override void Save(UnityModManager.ModEntry mod){
            Save(this, mod);
        }
        public void OnChange(){}
    }

    /*[HarmonyPatch(typeof(CarsSaveManager), nameof(CarsSaveManager.RestoreCarState))]
    public class Poutou {
        private static LocoRestorationController controller =>
                LocoRestorationController.allLocoRestorationControllers.Find(
                    c => c.locoLivery.v1 == TrainCarType.LocoDM3);

        private static string guid;
        public static void Postfix(TrainCar spawnedCar) {
            if (spawnedCar.PaintExterior.CurrentTheme == controller.abandonedTheme && 
                spawnedCar.carType == TrainCarType.LocoDM3) {
                guid = spawnedCar.CarGUID;
                Main.Player.UpdateEvent += RepairSavefile;
            }
        }

        private static void RepairSavefile() {
            if (controller == null || controller.saveData == null) return;
            controller.saveData.SetString("loco", guid);
            Main.Player.UpdateEvent -= RepairSavefile;
        }
    }*/

    public static class Main {
        public const int VERSION = 2;
        public static Settings Settings = null!;
        public static UnityModManager.ModEntry Mod = null!;
        // ReSharper disable once InconsistentNaming
        private static RandoPlayer? _player = null;
        public static RandoPlayer Player => _player ?? new RandoPlayer();
        
        [UsedImplicitly]
        public static void Load(UnityModManager.ModEntry modEntry)
        {
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Mod = modEntry;
            Mod.OnToggle = OnToggle;
            Mod.OnGUI += OnGUI;
            Mod.OnSaveGUI += OnSaveGUI;
        }
        public static void CreatePlayer(RandoSaveData? saveData) => _player = new RandoPlayer(saveData);
        public static void QuitGame() => _player = null;
        public static void OnGUI(UnityModManager.ModEntry modEntry) {
            Settings.Draw(modEntry);
        }
        public static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
            Settings.Save(modEntry);
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Harmony harmony = new(modEntry.Info.Id);

            if (value)
            {
                harmony.PatchAll();
            }
            else
            {
                harmony.UnpatchAll(modEntry.Info.Id);
            }
            return true;
        }

        public static void Log(string message) {
            Mod.Logger.Log(message);
        }
        public static void Error(string message) {
            Mod.Logger.Error(message);
        }
        public static void NotifyPlayer(string message) {
            SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ShowNotification(
                message,
                duration: 5f,
                localize: false
            );
        }
    }


}

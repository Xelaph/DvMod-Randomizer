using System;
using System.Collections;
using System.Linq;
using DV;
using DV.Common;
using DV.JObjectExtstensions;
using DV.Scenarios.Common;
using DV.TerrainSystem;
using DV.UI;
using DV.UserManagement;
using DV.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace DvMod.Randomizer
{

    [HarmonyPatch(typeof(StartGameData_FromSaveGame))]
    public class LoadingPatch {
        private static void ExitWithMessage(string message) {
            Main.Error(message);
            MainMenu.GoBackToMainMenu();
            SceneManager.UnloadSceneAsync((int)DVScenes.Game);
            SingletonBehaviour<CoroutineManager>.Instance.StopCoroutine("LoadingRoutine");
        }

        [HarmonyPostfix, HarmonyPatch("Initialize")]
        public static void SaveLoadingEndPatch(SaveGameData ___saveGameData) {
            RandoSaveData? data = ___saveGameData.GetObject<RandoSaveData>("RandoData");
            if (data == null) {
                Main.Log("Launching game in normal mode");
                return;
            }
            if (data.Version != Main.VERSION) {
                ExitWithMessage($"Randomizer detected but versions do not match: Mod version = {Main.VERSION}/Save version = {data.Version}. Returning to main menu...");
                return;
            }
            try {
                Main.player ??= new(data);
            } catch (TimeoutException) {
                ExitWithMessage($"Could not connect to server. Returning to main menu...");
                Main.player = null;
                return;
            }
            Main.player.InitGame();
        }

    }    
    [HarmonyPatch(typeof(SaveGameManager))]
    public class SavingPatch {
        [HarmonyPrefix, HarmonyPatch("UpdateInternalData")]
        public static void SavePrefix(SaveGameData ___data) {
            if (Main.player == null) return;
            ___data.SetObject("RandoData", Main.player.Data);
        }
    }

    [HarmonyPatch(typeof(StartGameData_NewCareer), nameof(StartGameData_NewCareer.PrepareNewSaveData))]
    public class NewSavePatch {
        private static IEnumerator TeleportPlayer() {
            while (StationController.allStations == null || StationController.allStations.Count == 0)
                yield return WaitFor.Seconds(0.5f);
            Transform teleportAnchor = 
                    StationController.allStations
                    .Find(sc => sc.stationInfo.YardID.Equals(Main.player!.SlotData.StartStation))
                    .stationRange
                    .stationCenterAnchor;
            PlayerManager.TeleportPlayer(teleportAnchor.position, teleportAnchor.rotation, null, useRotation: true);
            while (!SingletonBehaviour<WorldStreamingInit>.Instance.IsSceneAndTerrainRegionLoaded(teleportAnchor.position))
            {
                yield return WaitFor.Seconds(0.5f);
                Debug.LogWarning("Waiting terrains and streamers to finish loading");
            }
            PlayerManager.TeleportPlayer(teleportAnchor.position, teleportAnchor.rotation, null, useRotation: true);
            Main.settings!.CreateAPSave = false;
        }
        public static bool Prefix(StartGameData_NewCareer __instance, ref SaveGameData saveGameData, IGameSession session, IDifficulty difficultyParams) {
            if (!Main.settings!.CreateAPSave) return true;
            try {
                Main.player ??= new(null);
            } catch (TimeoutException) {
                Main.Log("Tried, but failed. Sorry");
                MainMenu.GoBackToMainMenu();
                return false;
            }
            saveGameData ??= SaveGameManager.MakeEmptySave();
            saveGameData.Clear();
            saveGameData.SetString("Game_mode", session.GameMode);
            saveGameData.SetString("World", session.World);
            saveGameData.SetDouble("Starting_time_and_date", AStartGameData.BaseTimeAndDate.ToOADate());
            IDifficulty DifficultyToUse = difficultyParams ?? DifficultyParamsSetter.Standard;
            DifficultyParamsSetter.SetDifficultyParams(DifficultyToUse);
            session.PerformGameplayEntryDifficultyCheck(DifficultyToUse);
            SingletonBehaviour<CoroutineManager>.Instance.Run(TeleportPlayer());
            //__instance.DifficultyToUse = DifficultyToUse;
            saveGameData.SetFloat("Player_money", Main.player.SlotData.Money);
            saveGameData.SetBool("Tutorial_01_completed", value: true);
            saveGameData.SetBool("Tutorial_02_completed", value: true);
            saveGameData.SetBool("Tutorial_03_completed", value: true);
            saveGameData.SetInt("Starting_items", 0);
            session.GameData.SetBool("Difficulty_picked", value: true);
            Main.player.InitGame();
            return false;
        }
    }
    
}
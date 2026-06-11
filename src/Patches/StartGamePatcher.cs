using System;
using System.Collections;
using DV.Common;
using DV.JObjectExtstensions;
using DV.Scenarios.Common;
using DV.UI;
using DV.UserManagement;
using DV.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace DvMod.Randomizer
{

    [RiderHarmonyPatch(typeof(StartGameData_FromSaveGame))]
    public class LoadingPatch {
        private static void ExitWithMessage(string message) {
            Main.Error(message);
            MainMenu.GoBackToMainMenu();
            SceneManager.UnloadSceneAsync((int)DVScenes.Game);
            // ReSharper disable once NotResolvedInText
            SingletonBehaviour<CoroutineManager>.Instance.StopCoroutine("LoadingRoutine");
        }

        [HarmonyPostfix, RiderHarmonyPatch("Initialize")]
        public static void SaveLoadingEndPatch(SaveGameData ___saveGameData) {
            RandoSaveData? data = ___saveGameData.GetObject<RandoSaveData>("RandoData");
            if (data == null) {
                Main.Log("Launching game in normal mode");
                return;
            }
            if (data.Version != Main.Version) {
                ExitWithMessage($"Randomizer detected but versions do not match: Mod version = {Main.Version}/Save version = {data.Version}. Returning to main menu...");
                return;
            }
            try {
                Main.Player ??= new(data);
            } catch (TimeoutException) {
                ExitWithMessage($"Could not connect to server. Returning to main menu...");
                Main.Player = null;
                return;
            }
            Main.Log("Player created and session connected");
            Main.Player.InitGame();
            Main.Log("Game initialized. Waiting for original game to finish...");
        }

    }    
    [RiderHarmonyPatch(typeof(SaveGameManager))]
    public class SavingPatch {
        [HarmonyPrefix, RiderHarmonyPatch("UpdateInternalData")]
        public static void SavePrefix(SaveGameData ___data) {
            if (Main.Player == null) return;
            ___data.SetObject("RandoData", Main.Player.Data);
        }
    }

    [RiderHarmonyPatch(typeof(StartGameData_NewCareer))]
    public class NewSavePatch {
        private static IEnumerator TeleportPlayer() {
            while (StationController.allStations == null || StationController.allStations.Count == 0)
                yield return WaitFor.Seconds(0.5f);
            Transform teleportAnchor = 
                    StationController.allStations
                    .Find(sc => sc.stationInfo.YardID.Equals(Main.Player!.SlotData.StartStation))
                    .stationRange
                    .stationCenterAnchor;
            PlayerManager.TeleportPlayer(teleportAnchor.position, teleportAnchor.rotation, null, useRotation: true);
            while (!SingletonBehaviour<WorldStreamingInit>.Instance.IsSceneAndTerrainRegionLoaded(teleportAnchor.position))
            {
                yield return WaitFor.Seconds(0.5f);
                Debug.LogWarning("Waiting terrains and streamers to finish loading");
            }
            PlayerManager.TeleportPlayer(teleportAnchor.position, teleportAnchor.rotation, null, useRotation: true);
            Main.Settings.CreateAPSave = false;
        }
        [HarmonyPrefix, RiderHarmonyPatch(nameof(StartGameData_NewCareer.PrepareNewSaveData))]
        public static bool Prefix(StartGameData_NewCareer __instance, IGameSession session, IDifficulty difficultyParams) {
            if (!Main.Settings.CreateAPSave) return true;
            try {
                Main.Player ??= new RandoPlayer(null);
            } catch (TimeoutException) {
                Main.Log("Tried, but failed. Sorry");
                MainMenu.GoBackToMainMenu();
                return false;
            }
            SaveGameData saveGameData = SaveGameManager.MakeEmptySave();
            saveGameData.Clear();
            saveGameData.SetString("Game_mode", session.GameMode);
            saveGameData.SetString("World", session.World);
            saveGameData.SetDouble("Starting_time_and_date", AStartGameData.BaseTimeAndDate.ToOADate());
            IDifficulty difficultyToUse = difficultyParams ?? DifficultyParamsSetter.Standard;
            DifficultyParamsSetter.SetDifficultyParams(difficultyToUse);
            session.PerformGameplayEntryDifficultyCheck(difficultyToUse);
            SingletonBehaviour<CoroutineManager>.Instance.Run(TeleportPlayer());
            //__instance.DifficultyToUse = DifficultyToUse;
            saveGameData.SetFloat("Player_money", Main.Player.SlotData.Money);
            saveGameData.SetBool("Tutorial_01_completed", value: true);
            saveGameData.SetBool("Tutorial_02_completed", value: true);
            saveGameData.SetBool("Tutorial_03_completed", value: true);
            saveGameData.SetInt("Starting_items", 0);
            session.GameData.SetBool("Difficulty_picked", value: true);
            __instance.saveGameData = saveGameData;
            Main.Player.InitGame();
            return false;
        }
    }
    
}
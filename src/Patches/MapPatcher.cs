using System.Collections;
using DV.Teleporters;
using DV.Utils;
using HarmonyLib;
using UnityEngine;

namespace DvMod.Randomizer {
    [HarmonyPatch(typeof(MapMarker), nameof(MapMarker.Init))]
    public static class MapMarkerPatcher {
        private static readonly Color GOT_LICENSE = new(0f,3f,0f);
        private static readonly Color NO_LICENSE = new(3f,0f,0f);
        private static readonly MapMarker[] allMarkers = new MapMarker[20];
        public static void Postfix(MapMarker __instance) {
            if (Main.player != null && __instance.fastTravelDestination.markerType == FastTravelDestination.MarkerType.Station) {
                string stationName = StationController.allStations.FindMin(
                        sc => Vector3.Distance(sc.stationRange.stationCenterAnchor.position, __instance.fastTravelDestination.playerTeleportAnchor.position)
                )!.stationInfo.YardID;
                int order = RandoCommonData.GetOrderFromStationName(stationName);
                allMarkers[order] = __instance;
                if (Main.player.GotStationLicense(stationName))
                    GotLicense(stationName);
                else
                    NoLicense(stationName);
                
            }
        }
        private static IEnumerator ChangeMarkerColor(int order, Color color) {
            while (allMarkers[order]==null) yield return null;
            MeshRenderer renderer = allMarkers[order].GetComponentInChildren<MeshRenderer>();
            renderer.material.SetColor("_Color", color);
            yield break;
        }
        public static void GotLicense(string stationName) => SingletonBehaviour<CoroutineManager>.Instance.Run(ChangeMarkerColor(RandoCommonData.GetOrderFromStationName(stationName), GOT_LICENSE));
        public static void NoLicense(string stationName) => SingletonBehaviour<CoroutineManager>.Instance.Run(ChangeMarkerColor(RandoCommonData.GetOrderFromStationName(stationName), NO_LICENSE));
    }
}
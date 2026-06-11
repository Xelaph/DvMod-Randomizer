using System.Collections;
using DV.Teleporters;
using DV.Utils;
using HarmonyLib;
using UnityEngine;

namespace DvMod.Randomizer {
    [HarmonyPatch(typeof(MapMarker))]
    public static class MapMarkerPatcher {
        private static readonly Color GotLicenseColor = new(0f,1f,0f);
        private static readonly Color NoLicenseColor = new(1f,0f,0f);
        private static readonly MapMarker[] AllMarkers = new MapMarker[20];
        private static readonly int ColorFieldName = Shader.PropertyToID("_Color");

        [HarmonyPostfix, HarmonyPatch(nameof(MapMarker.Init))]
        public static void Postfix(MapMarker __instance) {
            if (Main.Player == null ||
                __instance.fastTravelDestination.markerType != FastTravelDestination.MarkerType.Station) return;
            string stationName = StationController.allStations.FindMin(
                sc => Vector3.Distance(sc.stationRange.stationCenterAnchor.position, __instance.fastTravelDestination.playerTeleportAnchor.position)
            )!.stationInfo.YardID;
            long order = RandoCommonData.GetOrderFromStationName(stationName);
            AllMarkers[order] = __instance;
            if (Main.Player.GotStationLicense(stationName))
                GotLicense(stationName);
            else
                NoLicense(stationName);
        }
        private static IEnumerator ChangeMarkerColor(long order, Color color) {
            while (AllMarkers[order]==null) yield return null;
            MeshRenderer renderer = AllMarkers[order].GetComponentInChildren<MeshRenderer>();
            renderer.material.SetColor(ColorFieldName, color);
        }
        public static void GotLicense(string stationName) => SingletonBehaviour<CoroutineManager>.Instance.Run(ChangeMarkerColor(RandoCommonData.GetOrderFromStationName(stationName), GotLicenseColor));
        public static void NoLicense(string stationName) => SingletonBehaviour<CoroutineManager>.Instance.Run(ChangeMarkerColor(RandoCommonData.GetOrderFromStationName(stationName), NoLicenseColor));
    }
}
using DV.Teleporters;
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
        private static void ChangeMarkerColor(MapMarker marker, Color color) {
            MeshRenderer renderer = marker.GetComponentInChildren<MeshRenderer>();
            renderer.material.SetColor("_Color", color);
        }
        public static void GotLicense(string stationName) => ChangeMarkerColor(allMarkers[RandoCommonData.GetOrderFromStationName(stationName)], GOT_LICENSE);
        public static void NoLicense(string stationName) => ChangeMarkerColor(allMarkers[RandoCommonData.GetOrderFromStationName(stationName)], NO_LICENSE);
    }
}
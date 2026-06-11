
using System;
using System.Collections.Generic;
using DV;
using DV.RenderTextureSystem.BookletRender;
using DV.ThingTypes;
using HarmonyLib;

namespace DvMod.Randomizer;

[RiderHarmonyPatch(typeof(LicenseManager))]
public static class LicenseManagerPatch {
    public static List<T> ProcessListOfIDs<T>(string[] ids, List<T> refs) where T: Thing_v2 {
        List<T> ret = [];
        if (ids.Length == 0) return ret;
        Array.ForEach(ids, s => ret.Add(refs.Find(x => x.id == s)));
        
        return ret;
    }
    [HarmonyPrefix, RiderHarmonyPatch(nameof(LicenseManager.LoadData))]
    public static bool Prefix(SaveGameData data, LicenseManager __instance) {
        if (Main.Player == null) return true;
        ProcessListOfIDs(data.GetStringArray("Licenses_General"), Globals.G.Types.generalLicenses).ForEach(__instance.AcquireGeneralLicense);

        ProcessListOfIDs(data.GetStringArray("Licenses_Jobs"), Globals.G.Types.jobLicenses).ForEach(__instance.AcquireJobLicense);
		
        ProcessListOfIDs(data.GetStringArray("Garages"), Globals.G.Types.garages).ForEach(__instance.UnlockGarage);

        return false;
    }
}
[RiderHarmonyPatch(typeof(StaticLicenseBookletRender))]
public static class LocoHintPatcher {
    [HarmonyPostfix, RiderHarmonyPatch(nameof(StaticLicenseBookletRender.GetStaticTemplatePaperData))]
    public static void Postfix(GeneralLicenseType_v2 ___generalLicense, ref TemplatePaperData[] __result) {
        if (Main.Player == null) return;
        int order = RandoCommonData.GetOrderFromLocoLicense(___generalLicense);
        if (order < 0 || !Main.Player.Config.HintsOnLocoLicense) return;
        LicenseTemplatePaperData firstPage = (LicenseTemplatePaperData) __result[0];
        firstPage.licenseDescription += $"\nIn {Main.Player.Config.LocoJobsThreshold[order]} job with this loco, you will earn a {Main.Player.GetItemNameFromLocationId(RandoCommonData.AP_ID.LOC_LOCO_NB_JOBS+order, true)}";
    }
}
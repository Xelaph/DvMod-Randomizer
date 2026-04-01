using UnityEngine;
using CommandTerminal;
using HarmonyLib;

namespace DvMod.Randomizer
{
    [HarmonyPatch(typeof(Terminal))]
    public static class RandoConsole {
        [HarmonyPrefix, HarmonyPatch("EnterCommand")]
        public static bool TerminalPatch(ref string ___command_text) {
            if (Main.player == null) return true;
            if (___command_text.Equals("/slicenses")) {
                Terminal.Log("Unlocked station licenses");
                for (int stationOrder = 0; stationOrder < 20; stationOrder++) {
                    string currStation = RandoCommonData.GetStationNameFromOrder(stationOrder);
                    if (Main.player.GotStationLicense(currStation)) {
                        Terminal.Log("-"+currStation);
                        static void PrintLine((int , int) val, string message) {
                            TerminalLogType LineColor = val.Item1 >= val.Item2 ? TerminalLogType.Warning : TerminalLogType.Message;
                            Terminal.Log(LineColor, message+$":{val.Item1}/{val.Item2}");
                        }
                        PrintLine(Main.player.GetShuntingData(currStation), "Shunting");
                        PrintLine(Main.player.GetTransportData(currStation), "Transport");
                        PrintLine(Main.player.GetVictoryData(currStation), "Victory");
                    }
                }
            } else {
                Main.player.Session.Say(___command_text);
                Terminal.Log(TerminalLogType.Input, Main.player.Session.Players.ActivePlayer.Name+":"+___command_text);
            }
            ___command_text = "";
            return false;

        }
    }
}
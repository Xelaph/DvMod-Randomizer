using CommandTerminal;
using HarmonyLib;

namespace DvMod.Randomizer
{
    [HarmonyPatch(typeof(Terminal))]
    public static class RandoConsole {
        [HarmonyPrefix, HarmonyPatch("EnterCommand")]
        // ReSharper disable once InconsistentNaming
        public static bool TerminalPatch(ref string ___command_text) {
            if (Main.Player == null) return true;
            if (___command_text.Equals("/slicenses")) {
                Terminal.Log("Unlocked station licenses");
                for (int stationOrder = 0; stationOrder < 20; stationOrder++) {
                    string currStation = RandoCommonData.GetStationNameFromOrder(stationOrder);
                    if (!Main.Player.GotStationLicense(currStation)) continue;
                    Terminal.Log("-"+currStation);
                    PrintLine(Main.Player.GetShuntingData(currStation), "Shunting");
                    PrintLine(Main.Player.GetTransportData(currStation), "Transport");
                    PrintLine(Main.Player.GetVictoryData(currStation), "Victory");
                    continue;

                    static void PrintLine((int , int) val, string message) {
                        TerminalLogType lineColor = val.Item1 >= val.Item2 ? TerminalLogType.Warning : TerminalLogType.Message;
                        Terminal.Log(lineColor, message+$":{val.Item1}/{val.Item2}");
                    }
                }
            } else {
                Main.Player.Session.Say(___command_text);
                Terminal.Log(TerminalLogType.Input, Main.Player.Session.Players.ActivePlayer.Name+":"+___command_text);
            }
            ___command_text = "";
            return false;

        }
    }
}
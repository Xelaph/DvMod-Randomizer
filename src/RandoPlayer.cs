using DV.Utils;
using DV.ThingTypes;
using CommandTerminal;
using DV.LocoRestoration;
using UnityEngine;
using DV.ThingTypes.TransitionHelpers;
using System;
using DV.Booklets;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.Enums;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using WebSocketSharp;
using System.Linq;
using System.Runtime.InteropServices;
using Archipelago.MultiClient.Net.DataPackage;
using System.Deployment.Internal;
using DV.UI;
using DV.Teleporters;
using System.Collections;
using DV;
using DV.OriginShift;
using DV.Shops;
using Archipelago.MultiClient.Net.Packets;
using DV.Util.EventWrapper;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using DV.ServicePenalty.UI;
using System.Data.Common;

namespace DvMod.Randomizer
{

    public class JobFinishState {
        public bool HasWon;
        public ItemInfo? Item;
        public int RemainingForVictory;
        public int RemainingJobs;
        public bool IsShunting;
    }
    public class DVConfig(int[] ShuntThreshold, int[] FreightThreshold, int[] LocoJobsThreshold, int Victory, int VictoryThreshold, bool HintsOnLocoLicense, bool HintsOnStationLicense, bool DeathLink) {
                public int[] ShuntThreshold = ShuntThreshold;
                public int[] FreightThreshold = FreightThreshold;
                public int[] LocoJobsThreshold = LocoJobsThreshold;
                public int Victory=Victory;
                public int VictoryThreshold = VictoryThreshold;
                public bool HintsOnLocoLicense = HintsOnLocoLicense;
                public bool HintsOnStationLicense = HintsOnStationLicense;
                public bool DeathLink = DeathLink;
            }
    public class RandoSaveData(
        int Version,
        bool[] StationLicenses, 
        bool[] HiddenGarages, 
        bool[] JobLocations,
        bool[] GeneralLocations,
        bool[] LocoLocations,
        int[] ReceivedRelics, 
        int Index, 
        int[] Shunts, 
        int[] Freights, 
        int[] LocoJobs, 
        bool AlreadyWon,
        HashSet<long> LocationsChecked,
        DVConfig config
        ) {
            
        public bool[] StationLicenses = StationLicenses;
        public bool[] HiddenGarages = HiddenGarages;
        public bool[] JobLocations = JobLocations;
        public bool[] GeneralLocations = GeneralLocations;
        public bool[] LocoLocations = LocoLocations;
        public int[] ReceivedRelics = ReceivedRelics;
        public int[] Shunts = Shunts;
        public int Index = Index;
        public int[] Freights = Freights;
        public int[] LocoJobs = LocoJobs;
        public bool AlreadyWon = AlreadyWon;
        public int Version = Version;
        public HashSet<long> LocationsChecked = LocationsChecked;
        public DVConfig Config = config;
        public static RandoSaveData CreateSaveData(DVConfig config) => new(
            2,
            new bool[20],
            new bool[4],
            new bool[12],
            new bool[13],
            new bool[57],
            new int[6],
            0,
            new int[20],
            new int[20],
            new int[6],
            false,
            new(),
            config
        );
    }
    
    public class RandoPlayer
    {
        internal class DemoLocoListener(int idx, float spatialthreshold = 5f, float timeThreshold = 20f) {
            private readonly float SpatialThreshold = spatialthreshold;
            private readonly float TimeThreshold = timeThreshold;
            private Vector3 LocoPosition = RandoCommonData.GetInfoRestorationFromLocoLocationOrder(idx);
            private readonly long CheckId = RandoCommonData.AP_ID.LOC_LOCO_RESTORATION + idx;
            private float LastTime = 0f;
            public void CheckPosition() {
                if (PlayerManager.PlayerTransform == null) return;
                object x = 1;
                if (Time.time - LastTime > TimeThreshold && (PlayerManager.PlayerTransform.AbsolutePosition() - LocoPosition).magnitude < SpatialThreshold) {
                    string stationNeeded = RandoCommonData.GetStationFromLocoLocations(LocoPosition);
                    bool StationOk = Main.player!.GotStationLicense(stationNeeded);
                    bool MuseumOk = SingletonBehaviour<LicenseManager>.Instance.IsGeneralLicenseAcquired(GeneralLicenseType.MuseumCitySouth.ToV2());
                    if (StationOk && MuseumOk) {
                        ItemInfo item = Main.player.UnlockCheck(CheckId);
                        Main.player.NotifyPlayer($"You found a {item.ItemDisplayName} for {item.Player.Name} on the ground!");
                        Main.player.UpdateEvent -= CheckPosition;
                    } else{
                        LastTime = Time.time;
                        if (StationOk && !MuseumOk)
                            Main.player.NotifyPlayer("There is something here but you cannot take it... You need the museum license");
                        else if (!StationOk && MuseumOk)
                            Main.player.NotifyPlayer("There is something here but you cannot take it... You need the "+stationNeeded+" station license");
                        else
                            Main.player.NotifyPlayer("There is something here but you cannot take it... You need the museum license and the "+stationNeeded+" station license");
                    }
                }
            }
        }
    #region Player fields, properties and constructor/destructor

        public Vector3 Position => PlayerManager.ActiveCamera.transform.position + PlayerManager.ActiveCamera.transform.forward * 0.5f;
        public Quaternion Rotation => PlayerManager.ActiveCamera.transform.rotation;
        public RandoSaveData Data {get; private set;}
        public DVConfig Config {get => Data.Config;}
        private readonly ConcurrentQueue<DV_APItem> waitingQueue = new();
        private PauseMenu Menu {get => UnityEngine.Object.FindObjectOfType<PauseMenu>();}
        public ArchipelagoSession Session;
        public APSlotData SlotData {get; private set;}
        public event Action? UpdateEvent;
        public DeathLinkService? deathLinkService = null;
        public JobFinishState FinishJob(Job_data data) {
            string Station = data.type switch {
                JobType.ShuntingUnload => data.chainDestinationStationInfo.YardID,
                _ => data.chainOriginStationInfo.YardID
            };
            bool IsShunting = data.type == JobType.ShuntingLoad || data.type == JobType.ShuntingUnload;
            if (!GotStationLicense(Station)) {
                return new() {
                    HasWon = Data.AlreadyWon,
                    Item = null,
                    RemainingForVictory = -1,
                    IsShunting = IsShunting,
                    RemainingJobs = -1
                };
            }
            (int Remaining, ItemInfo? Item) = IsShunting ? FinishShunting(Station) : FinishTransport(Station);
            int RemainingForVictory = CheckVictory(Station);
            return new() {
                HasWon = Data.AlreadyWon,
                Item = Item,
                RemainingJobs = Remaining,
                RemainingForVictory = RemainingForVictory,
                IsShunting = IsShunting
            };

        }

        public bool AddLocation(long id) {
            return Data.LocationsChecked.Add(id);
        }
        public void InitGame() {
            //Check if the locations match (Can happen if the game crashes and progress is lost)
            foreach (long checkId in Session.Locations.AllLocationsChecked){
                if (!Data.LocationsChecked.Contains(checkId)) {
                    WorldStreamingInit.LoadingFinished += RandoCommonData.GetAPLocation(checkId).EmergencyCheck;
                }
            }
            //Check if we need to resync (items received while we were offline)
            int ItemNumberReceived = Session.Items.AllItemsReceived.Count;
            if (Data.Index < ItemNumberReceived) {
                Main.Log($"Re-syncing...");
                for (int id = Data.Index ; id < ItemNumberReceived; id++) {
                    DV_APItem item = RandoCommonData.GetAPItem(id, Session.Items.AllItemsReceived[id]);
                    waitingQueue.Enqueue(item);
                }
                Data.Index = ItemNumberReceived;
            }
            SetupListeners(true);
            UpdateEvent += ProcessItems;
            //Add prices for normally tutorial acquired licenses
            GeneralLicenseType.DE2.ToV2().price = 5000;
            GeneralLicenseType.TrainDriver.ToV2().price = 1000;
            JobLicenses.FreightHaul.ToV2().price = 10000;
            //Set up demo loco locations
            for (int i = 0; i < Data.LocoLocations.Count(); i++) {
                if (!Data.LocoLocations[i])
                    UpdateEvent += new DemoLocoListener(i).CheckPosition;
            }
        }
        private IEnumerator Subscribe() {
            while (Menu == null) yield return null;
            Menu.controller.ExitLevelRequested += Dispose;
            Menu.controller.QuitGameRequested += Dispose;
        }
        public RandoPlayer(RandoSaveData? saveData) {
            string Server = "ws://"+Main.settings!.serverName;
            Session = ArchipelagoSessionFactory.CreateSession(Server, Main.settings!.Port);
            LoginResult login = Session.TryConnectAndLogin("Derail Valley", Main.settings!.User, ItemsHandlingFlags.AllItems, password: Main.settings!.Password);
            if (login is LoginFailure failLogin) {
                Main.Log("Error! We got the following error while connecting: "+failLogin.Errors.Aggregate((acc, s) => acc+"/"+s));
                MainMenu.GoBackToMainMenu();
                throw new Exception();
            }
            SlotData = ((LoginSuccessful)login).SlotData;
            SingletonBehaviour<CoroutineManager>.Instance.Run(Subscribe());
            Data = saveData ?? RandoSaveData.CreateSaveData(SlotData.Config);
            if (Data.Config.DeathLink) {
                deathLinkService = Session.CreateDeathLinkService();
                deathLinkService.OnDeathLinkReceived += DeathLinkPatch.Derail;
                deathLinkService.EnableDeathLink();
            }

        }
        public void Dispose() {
            Main.player = null;
        }
        ~RandoPlayer() {
            Menu.controller.ExitLevelRequested -= Dispose;
            Menu.controller.QuitGameRequested -= Dispose;
            Data.Index -= waitingQueue.Count;
            SetupListeners(false);
            deathLinkService = null;
            Session.Socket.DisconnectAsync();
            UpdateEvent = null;
        }
        public void CallUpdate() {
            UpdateEvent?.Invoke();
        }
    #endregion
    #region Network methods helpers
        public ItemInfo UnlockCheck(long checkId) {
            RandoCommonData.GetAPLocation(checkId).FullCheck();
            var askTask = Session.Locations.ScoutLocationsAsync(checkId);
            askTask.Wait();
            return askTask.Result[checkId];
        }
        private void SetupListeners(bool on) {
            if (on) {
                Session.Items.ItemReceived += ReceivedItem;
                Session.MessageLog.OnMessageReceived += ReceivedMessage;
                Session.Socket.ErrorReceived += ReceivedError;
                
            } else {
                Session.Items.ItemReceived -= ReceivedItem;
                Session.MessageLog.OnMessageReceived -= ReceivedMessage;
                Session.Socket.ErrorReceived -= ReceivedError;
            }
        }
        private async void ProcessItems() {
            if (waitingQueue.TryDequeue(out var item)){
                await item.Acquire();
            }
        }
        private void ReceivedItem(ReceivedItemsHelper itemHelper) {
            Queue<ItemInfo> CurrQueue = new();
            while (itemHelper.Any()) {
                CurrQueue.Enqueue(itemHelper.DequeueItem());
            }
            if (itemHelper.Index == Data.Index + CurrQueue.Count) {
                while (CurrQueue.Any()) {
                    waitingQueue.Enqueue(RandoCommonData.GetAPItem(Data.Index++, CurrQueue.Dequeue()));
                }
            } else {
                while (Data.Index < itemHelper.Index)
                    waitingQueue.Enqueue(RandoCommonData.GetAPItem(Data.Index, itemHelper.AllItemsReceived[Data.Index++]));
            }
        }

        public void ReceivedError(Exception e, string message) {
            //Terminal.Log(TerminalLogType.Error, "[AP] Error "+e+":"+message);
            Main.Error("[AP] "+message);
        }
         public void ReceivedMessage(LogMessage message) {
            switch (message) {
                case AdminCommandResultLogMessage:
                Terminal.Log(TerminalLogType.Input, "[ADMIN] "+message.ToString());
                break;
                case ServerChatLogMessage:
                Terminal.Log(TerminalLogType.Message, message.ToString());
                break;
                case ItemSendLogMessage:
                Terminal.Log(TerminalLogType.Warning, message.ToString());
                break;
                case CommandResultLogMessage:
                Terminal.Log(TerminalLogType.Input, message.ToString());
                break;
                case TutorialLogMessage:
                Terminal.Log(TerminalLogType.Input, message.ToString());
                break;
                case CountdownLogMessage:
                Terminal.Log(TerminalLogType.Input, message.ToString());
                break;
                case ChatLogMessage chat:
                if (!chat.IsActivePlayer)
                    Terminal.Log(TerminalLogType.Message, chat.ToString());
                break;
            }
        }
        public string GetItemNameFromLocationId(long id, bool asHint=false) {
            Task<Dictionary<long, ScoutedItemInfo>> ask = Session.Locations.ScoutLocationsAsync(asHint?HintCreationPolicy.CreateAndAnnounceOnce:HintCreationPolicy.None, id);
            ask.Wait();
            ScoutedItemInfo info = ask.Result[id];
            return info.ItemDisplayName+" ("+info.Player.Name+")";
        }
        public void NotifyPlayer(string message) {
            SingletonBehaviour<ACanvasController<CanvasController.ElementType>>.Instance.NotificationManager.ShowNotification(
                message,
                duration: 5f,
                localize: false
            );
        }
        #endregion
        #region Acquiring items
        public void BypassItem(DV_APItem item) => waitingQueue.Enqueue(item);
        public int CheckVictory(string Station) {
            int toReturn = -1;
            int StOrder = RandoCommonData.GetOrderFromStationName(Station);
            if (!Data.AlreadyWon) {
                int StationFinished = 0;
                for (int i = 0; i < 20; i++) {
                    int currRem = Data.Config.VictoryThreshold - (Data.Shunts[i] + Data.Freights[i]);
                    if (currRem <= 0) StationFinished++;
                    if (i == StOrder) toReturn = Math.Max(0, currRem);
                }
                if (StationFinished >= Data.Config.Victory) {
                    Terminal.Log(TerminalLogType.Warning, "You won the game!");
                    Data.AlreadyWon = true;
                    Session.SetGoalAchieved();
                }
            }
            return toReturn;
        }
        public int AddRelic(long id) {
            return ++Data.ReceivedRelics[id-RandoCommonData.AP_ID.RELIC];
        }
        public void AcquireLicense(string Station) {
            Data.StationLicenses[RandoCommonData.GetOrderFromStationName(Station)] = true;
        }
        public (long, int) FinishLoco(TrainCarType carType) {
            int locoIdx = RandoCommonData.GetOrderFromLocoType(carType);
            if (Data.Config.LocoJobsThreshold[locoIdx] == Data.LocoJobs[locoIdx]) return (-1L, -1);
            Data.LocoJobs[locoIdx]++;
            return (0x600+locoIdx, Data.Config.LocoJobsThreshold[locoIdx] - Data.LocoJobs[locoIdx]);
        }
        public (int, int) GetShuntingData(string station) {
            int StIdx = RandoCommonData.GetOrderFromStationName(station);
            return (Data.Shunts[StIdx], Data.Config.ShuntThreshold[StIdx]);
        }
        public (int, int) GetTransportData(string station) {
            int StIdx = RandoCommonData.GetOrderFromStationName(station);
            return (Data.Freights[StIdx], Data.Config.FreightThreshold[StIdx]);
        }
        public (int, int) GetVictoryData(string station) {
            int StIdx = RandoCommonData.GetOrderFromStationName(station);
            return (Data.Freights[StIdx]+Data.Shunts[StIdx], Data.Config.VictoryThreshold);
        }
        public (int, ItemInfo?) FinishShunting(string station) {
            int StOrder = RandoCommonData.GetOrderFromStationName(station);
            Data.Shunts[StOrder] += 1;
            int Remaining = Data.Config.ShuntThreshold[StOrder] - Data.Shunts[StOrder];
            if (Remaining >= 0) {
                return (Remaining, UnlockCheck(0x2000 + StOrder * 0x100 + Data.Shunts[StOrder] - 1));
            }
            return (Remaining, null);
        }
        public (int, ItemInfo?) FinishTransport(string station) {
            int StOrder = RandoCommonData.GetOrderFromStationName(station);
            Data.Freights[StOrder] += 1;
            int Remaining = Data.Config.FreightThreshold[StOrder] - Data.Freights[StOrder];
            if (Remaining >= 0) {
                return (Remaining, UnlockCheck(0x4000 + StOrder * 0x100 + Data.Freights[StOrder] - 1));
            }
            return (Remaining, null);
        }

        #endregion
        #region Checking player possibilities
        private bool HasChecked<T>(Func<T, int> f, T value, bool[] map) {
            int id = f(value);
            if (id < 0) return true;
            return map[id];
        }
        public bool HasChecked(Vector3 position) {
            return HasChecked(RandoCommonData.GetIdFromLocoLocations, position, Data.LocoLocations);
        }
        public bool HasChecked(JobLicenseType_v2 jobLicense) {
            return HasChecked(x => RandoCommonData.GetIDFromJobLicense(x).Item2, jobLicense,  Data.JobLocations);
        }
        public bool HasChecked(GeneralLicenseType_v2 generalLicense) {
            return HasChecked(x => RandoCommonData.GetIDFromGeneralLicense(x).Item2, generalLicense, Data.GeneralLocations);
        }

        public bool GotStationLicense(string name) {
            return Data.StationLicenses[RandoCommonData.GetOrderFromStationName(name)];
        }

        
    

        public bool GotRestorationLoco(long id) {
            return Data.ReceivedRelics[id-RandoCommonData.AP_ID.RELIC] > 0;
        }
        public bool GotRestorationLoco(TrainCarType carType) {
            return Data.ReceivedRelics[RandoCommonData.GetOrderFromLocoType(carType)] > 0;
        }
        public bool CanFinishRelic(long id) {
            return Data.ReceivedRelics[id-RandoCommonData.AP_ID.RELIC] > 1;
        }
        public bool CanFinishRelic(TrainCarType carType) {
            return Data.ReceivedRelics[RandoCommonData.GetOrderFromLocoType(carType)] == 2;
        }
    
        public bool HasUnlocked(GarageType_v2 g) {
            return g.v1 switch
            {
                Garage.Bob => Data.HiddenGarages[0],
                Garage.Caboose => Data.HiddenGarages[1],
                Garage.DE6_Slug => Data.HiddenGarages[2],
                Garage.Museum_FlatbedShort => SingletonBehaviour<LicenseManager>.Instance.IsGeneralLicenseAcquired(GeneralLicenseType.MuseumCitySouth.ToV2()),
                Garage.DM1U => Data.HiddenGarages[3],
                Garage.DE2_Relic or Garage.DM3_Relic or Garage.DH4_Relic or Garage.DE6_Relic or Garage.S060_Relic or Garage.S282_Relic => 
                    (RandoCommonData.GetState(g.garageCarLiveries[0].v1) == LocoRestorationController.RestorationState.S9_LocoServiced) 
                 || (RandoCommonData.GetState(g.garageCarLiveries[0].v1) == LocoRestorationController.RestorationState.S10_PaintJobDone),
                _ => false
            };
        }
        
        public bool IsJobLicenseAcquired(JobLicenseType_v2 jobLicense) {
            return Data.JobLocations[RandoCommonData.GetIDFromJobLicense(jobLicense).Item2];
        }
        public bool IsGeneralLicenseAcquired(GeneralLicenseType_v2 jobLicense) {
            return Data.GeneralLocations[RandoCommonData.GetIDFromGeneralLicense(jobLicense).Item2];
        }
        public void UnlockGarage(long id) {
            Data.HiddenGarages[id-RandoCommonData.AP_ID.GARAGES] = true;
        }
        public void CheckRestoLoco(long id) {
            Data.LocoLocations[id - RandoCommonData.AP_ID.LOC_LOCO_RESTORATION] = true;
        }
        public void CheckGLicense(long Id) {
            Data.GeneralLocations[Id - RandoCommonData.AP_ID.LOC_GENERAL_LICENSES] = true;
        }
        public void CheckJLicense(long Id) {
            Data.JobLocations[Id - RandoCommonData.AP_ID.LOC_JOB_LICENSES] = true;
        }

    }
#endregion
}

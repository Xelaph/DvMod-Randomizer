using System;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.Models;
using DV;
using DV.Booklets;
using DV.CabControls;
using DV.Customization.Paint;
using DV.Damage;
using DV.InventorySystem;
using DV.JObjectExtstensions;
using DV.LocoRestoration;
using DV.Simulation.Cars;
using DV.ThingTypes;
using DV.Utils;
using UnityEngine;

namespace DvMod.Randomizer {
    public abstract class ArchipelagoItem(int idx, ItemInfo item) {
        public int Idx {get;} = idx;
        protected ItemInfo Item = item;
        private readonly bool _localItem = item.Player.Slot == Main.Player!.Session.Players.ActivePlayer.Slot;
        public long Id {get => Item.ItemId;}
        public string LocationDisplayName {
            get => Item.Player.Name + " ("+Item.LocationDisplayName+")";
        }
        protected abstract string Name {get;}
        public string DisplayName {
            get => Name+RandoCommonData.GetFromFlags(Item.Flags);
        }
        
        public async Task Acquire() {
            if (IsObtainable){
                bool gotItem;
                do {
                    while (!WorldStreamingInit.IsLoaded) await Task.Yield();
                    if (!_localItem)
                        Main.NotifyPlayer($"You got a {DisplayName} from {LocationDisplayName}");
                    gotItem = AcquireUnconditional();
                    await Task.Yield();
                } while (!gotItem);
            } else if (!_localItem)
                Main.NotifyPlayer($"You received a {DisplayName} from {LocationDisplayName}, but you cannot have anymore");
            
        }
        protected abstract bool AcquireUnconditional();
        public abstract bool IsObtainable {get;}
    }

    public class AP_StationLicense(int idx, ItemInfo item) : ArchipelagoItem(idx, item)
    {
        private string Station => RandoCommonData.GetStationNameFromId(Id);
        protected override string Name => Station+" station license";
        protected override bool AcquireUnconditional() {
            Main.Player!.AcquireLicense(Station);
            MapMarkerPatcher.GotLicense(Station);
            RandoCommonData.AcquireStationLicense(Station);
            return true;
        }
        
        
        public override bool IsObtainable
        {
            get => !Main.Player!.GotStationLicense(Station);
        }
    }
    public class AP_GeneralLicense : ArchipelagoItem
    {
        private readonly GeneralLicenseType_v2 _license;
        public AP_GeneralLicense(int idx, ItemInfo item) :
            base(idx, item) {
            GeneralLicenseType_v2[] gLicenseFamily = RandoCommonData.GetGeneralLicenseFromId(Id);
            int licenseIdx = 0;
            while (licenseIdx < gLicenseFamily.Count() && SingletonBehaviour<LicenseManager>.Instance.IsGeneralLicenseAcquired(gLicenseFamily[licenseIdx])) licenseIdx++;
            _license = gLicenseFamily[licenseIdx == gLicenseFamily.Count() ? licenseIdx - 1 : licenseIdx];
        }

        protected override bool AcquireUnconditional()
        {
            SingletonBehaviour<LicenseManager>.Instance.AcquireGeneralLicense(_license);
            BookletCreator.CreateLicense(_license, Main.Player!.Position, Main.Player.Rotation, WorldMover.OriginShiftParent);
            return true;
        }  

        public override bool IsObtainable
        {
            get => !SingletonBehaviour<LicenseManager>.Instance.IsGeneralLicenseAcquired(_license);
        }

        protected override string Name => _license.ToString();
    }
    public class AP_JobLicense : ArchipelagoItem
    {
        private readonly JobLicenseType_v2 _license;
        public AP_JobLicense(int idx, ItemInfo item) :
            base(idx, item) {
            JobLicenseType_v2[] jLicenseFamily = RandoCommonData.GetJobLicenseFromId(Id).CopyLast();
            int licenseIdx = 0;
            while (licenseIdx < jLicenseFamily.Count() && SingletonBehaviour<LicenseManager>.Instance.IsJobLicenseAcquired(jLicenseFamily[licenseIdx])) licenseIdx++;
            _license = jLicenseFamily[licenseIdx == jLicenseFamily.Count() ? licenseIdx - 1 : licenseIdx];
        }

        protected override bool AcquireUnconditional()
        {
            SingletonBehaviour<LicenseManager>.Instance.AcquireJobLicense(_license);
            BookletCreator.CreateLicense(_license, Main.Player!.Position, Main.Player.Rotation, WorldMover.OriginShiftParent);
            return true;
        }  

        public override bool IsObtainable
        {
            get => !SingletonBehaviour<LicenseManager>.Instance.IsJobLicenseAcquired(_license);
        }

        protected override string Name => _license.ToString();
    }

    public class AP_PhysicalItem(int idx, ItemInfo item) : ArchipelagoItem(idx, item)
    {
        protected override string Name => RandoCommonData.GetItemPrefabFromId(Id);
        protected override bool AcquireUnconditional()
        {
            InventoryItemSpec spec = Globals.G.Items.items.Find(sc => sc.itemPrefabName.Equals(DisplayName));
            InventoryItemSpec inventoryItemSpec = UnityEngine.Object.Instantiate(spec, Main.Player!.Position, Main.Player!.Rotation);
            inventoryItemSpec.BelongsToPlayer = true;
            ItemBase component = inventoryItemSpec.GetComponent<ItemBase>();
            SingletonBehaviour<StorageController>.Instance.AddItemToWorldStorage(component);
            return true;
        }

        public override bool IsObtainable
        {
            get => true;
        }
    }
    public class AP_DoubleToken(int idx, ItemInfo item) : ArchipelagoItem(idx, item)
    {
        public override bool IsObtainable => true;

        protected override string Name => "Double Job Token";

        protected override bool AcquireUnconditional() { 
            Main.Player!.AddToken();
            Main.NotifyPlayer("You got a double job token!");
            return true;
        }
        
    }
    public class AP_Money(int idx, ItemInfo item) : ArchipelagoItem(idx, item)
    {
        protected override string Name => "Money";
        protected override bool AcquireUnconditional()
        {
            SingletonBehaviour<Inventory>.Instance.AddMoney(5000);
            return true;
        }
        public override bool IsObtainable
        {
            get => true;
        }
    }

    public class AP_Nothing(int idx, ItemInfo item) : ArchipelagoItem(idx, item)
    {
        protected override string Name => "Nothing";
        protected override bool AcquireUnconditional()
        {
            throw new ArgumentException("Cannot acquire a nothing item!");
        }
        public override bool IsObtainable
        {
            get => false;
        }
    }
    public class AP_RelicLoco(int idx, ItemInfo item) : ArchipelagoItem(idx, item)
    {
        private PaintTheme? AbandonedTheme => LocoRestorationController.allLocoRestorationControllers?[0]?.abandonedTheme;
        protected override string Name => RandoCommonData.GetRelicNameFromId(Id)+" demo loco advancement";
        protected override bool AcquireUnconditional()
        {
            int relicLevel = Main.Player!.AddRelic(Id);
            LocoRestorationController controller = RandoCommonData.GetLocoControllerFromId(Id);
            switch (relicLevel) {
                case 1:
                //First level relic: Spawn relic in museum
                controller.loco = SpawnOneRelic(controller.garageSpawner.locoSpawnPoint.transform.position, controller.locoLivery, controller.garageSpawner.flipSpawnLoco);
                if (controller.loco == null) return false;
                if (controller.secondCarLivery != null) {
                    controller.secondCar = SpawnOneRelic(controller.garageSpawner.locoSpawnPoint.transform.position,
                        controller.secondCarLivery, controller.garageSpawner.flipSpawnLoco);
                    controller.saveData.SetString("secondCar", controller.secondCar.CarGUID);
                }
                controller.SetState(LocoRestorationController.RestorationState.S4_OnDestinationTrack);
                controller.saveData.SetString("loco", controller.loco.CarGUID);
                controller.orderPartsModule.AddThingToCart();
                controller.orderPartsModule.ThingBought += controller.OnPartsOrdered;
                break;
                case 2:
                //Second level relic: Can buy parts installation
                if (controller.State == LocoRestorationController.RestorationState.S7_PartDelivered) {
                    controller.installPartsModule.AddThingToCart();
                    controller.installPartsModule.ThingBought += controller.OnInstallPartsPaid;
                }
                break;
                default:
                throw new ArgumentException("Relic level not right: "+relicLevel);
            }
            return true;
        }
        private TrainCar? SpawnOneRelic(Vector3 position, TrainCarLivery carLivery, bool flipLoco) {
            TrainCar car = SingletonBehaviour<CarSpawner>.Instance.SpawnCarOnClosestTrack(position, carLivery, flipLoco, true, true);
            if (AbandonedTheme is null){
                return null;
            }
            if (car.PaintExterior != null)
            {
                car.PaintExterior.CurrentTheme = AbandonedTheme;
            }
            if (car.PaintInterior != null)
            {
                car.PaintInterior.CurrentTheme = AbandonedTheme;
            }
            if (car.TryGetComponent<DamageController>(out var component))
            {
                component.DamageFullyAll();
                if (component.windows != null)
                {
                    component.windows.windowsBroken = true;
                }
            }
            if (car.TryGetComponent<SimController>(out var component2) && component2.resourceContainerController != null)
            {
                component2.resourceContainerController.DepleteAllResourceContainers();
            }
            car.preventDelete = true;
            return car;
        }
        public override bool IsObtainable
        {
            get => !Main.Player!.CanFinishRelic(Id);
        }
    }

    public class AP_CrewVehicle(int idx, ItemInfo item) : ArchipelagoItem(idx, item)
    {
        protected override bool AcquireUnconditional(){
            Main.Player!.UnlockGarage(Id);
            return true;
        }
        public override bool IsObtainable
        {
            get => !Main.Player!.HasUnlocked(RandoCommonData.GetGarageFromId(Id));
        }
        protected override string Name => RandoCommonData.GetNameFromGarageID(Id)+" spawn rights";
    }
}
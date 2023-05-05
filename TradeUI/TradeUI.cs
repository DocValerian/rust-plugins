/*
 * https://github.com/DocValerian/rust-plugins
 * Copyright (C) 2022 DocValerian
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * Questions and Support can be found in our Discord: https://discord.gg/8tkBFRjg3W
 * 
 */

using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Network;
using ProtoBuf;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("TradeUI", "DocValerian", "1.7.2")]
    class TradeUI : RustPlugin
    {
        static TradeUI Plugin;

        [PluginReference]
        private Plugin ImageLibrary;

        const string permUse = "tradeui.use";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region ConfigDataLoad
        protected override void LoadDefaultConfig()
        {
            Cfg = new ConfigFile();
            Puts("Loaded default configuration file");
        }
        protected override void SaveConfig() => Config.WriteObject(Cfg);
        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                Cfg = Config.ReadObject<ConfigFile>();
            }
            catch (JsonException ex)
            {
                Puts(ex.Message);
                PrintError("Your config file contains errors!");
                return;
            }
            catch (Exception ex)
            {
                Puts(ex.Message);
                LoadDefaultConfig();
            }

            if (Cfg == null)
            {
                Puts("Config is null");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        static ConfigFile Cfg = new ConfigFile();

        class ConfigFile
        {
            [JsonProperty(PropertyName = "Prevent selling damaged items")]
            public bool preventDamaged = false;
            [JsonProperty(PropertyName = "Default home category")]
            public string defaultHome = "Resources";
            [JsonProperty(PropertyName = "Log vendor update info")]
            public bool showUpdates = true;
            [JsonProperty(PropertyName = "Allow owner to edit prices in ui?")]
            public bool allowEdits = true;
            [JsonProperty(PropertyName = "Allow access to NPC vendors?")]
            public bool allowNPCs = true;
            [JsonProperty(PropertyName = "Show stock amount in UI?")]
            public bool showStock = true;

        }
        class StoredData
        {
            public List<ulong> Players = new List<ulong>();
            public Dictionary<ulong, PlayerFavorite> fav = new Dictionary<ulong, PlayerFavorite>();
        }

        StoredData storedData;
        private NumberFormatInfo nfi = new CultureInfo("en-GB", false).NumberFormat;
        private Dictionary<ulong, VendingMachine> VendorList = new Dictionary<ulong, VendingMachine>();
        private Dictionary<int, List<ulong>> VendorItemList = new Dictionary<int, List<ulong>>();
        private List<KeyValuePair<int, List<ulong>>> SortedVendorList = new List<KeyValuePair<int, List<ulong>>>();
        private DateTime LastRefreshTime = DateTime.Now.AddHours(-2);
        private List<string> categoryList = new List<string>();
        private Dictionary<string, ProtoBuf.VendingMachine.SellOrder> CurrentEditOrders = new Dictionary<string, ProtoBuf.VendingMachine.SellOrder>();
        private Dictionary<ulong, FavoriteItem> currentPlayerFav = new Dictionary<ulong, FavoriteItem>();
        private Dictionary<ulong, string> currentPlayerSell = new Dictionary<ulong, string>();
        private HashSet<ulong> taintedVendorIds = new HashSet<ulong>();

        class PlayerFavorite
        {
            public FavoriteItem f1 { get; set; }
            public FavoriteItem f2 { get; set; }
            public FavoriteItem f3 { get; set; }
            public FavoriteItem f4 { get; set; }
            public FavoriteItem f5 { get; set; }
        }
        class FavoriteItem
        {
            public int itemSoldId { get; set; }
            public string itemCat { get; set; }
            public string vendorNetId { get; set; }
            public string consoleCommand { get; set; }

            public string toString()
            {
                switch (consoleCommand)
                {
                    case "tradeui.openhomecat":
                        return "Category \t'" + itemCat + "'";
                    case "tradeui.openvendor":
                        VendingMachine v = Plugin.VendorList[Convert.ToUInt64(vendorNetId)];
                        if (v == null) return "Vendor not existing";
                        return "Vendor \t\t'" + v.shopName + "'";
                    case "tradeui.openvendorlist":
                        return "Item \t\t'" + ItemManager.itemDictionary[itemSoldId].displayName.translated + "'";
                    default:
                        return "FalseCommandSaved";
                }
            }

            public void runCommand(BasePlayer player)
            {
                switch (consoleCommand)
                {
                    case "tradeui.openhomecat":
                        player.SendConsoleCommand("tradeui.openhomecat", (object)itemCat);
                        break;
                    case "tradeui.openvendor":
                        Plugin.timer.Once(0.5f, () => {
                            player.SendConsoleCommand("tradeui.openvendor", (object)vendorNetId);
                        });
                        break;
                    case "tradeui.openvendorlist":
                        player.SendConsoleCommand("tradeui.openvendorlist", (object)itemSoldId, (object)itemCat);
                        break;
                }
            }
        }
        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            permission.RegisterPermission(permUse, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("TradeUI");
            SaveData();

            nfi.NumberDecimalSeparator = ",";
            nfi.NumberGroupSeparator = ".";
            nfi.NumberDecimalDigits = 0;

            updateVendorList();
        }
        private void OnServerInitialized()
        {
            // preload item icons - this might be excessive, but the list is very dynamic otherwise
            // this way the load should only happen once and have all icons prewarmed
            List<KeyValuePair<string, ulong>> itemIconList = ItemManager.GetItemDefinitions().Select(itemDef => new KeyValuePair<string, ulong>(itemDef.shortname, 0)).ToList();
            ImageLibrary.Call("LoadImageList", Title, itemIconList, null);
        }
        void Unload()
        {
            if (UiPlayers.Count <= 0) return;
            foreach (var player in UiPlayers.ToList())
            {
                killUI(player);
            }
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("TradeUI", storedData);
        }

        #endregion

        #region Hooks

        object CanVendingAcceptItem(VendingMachine vending, Item item, int targetPos)
        {
            //This prevents vendors globally from accepting damaged goods, if configured;
            if (!Cfg.preventDamaged) return null;
            if (item.info.condition.enabled && item.condition < item.info.condition.max - 5f)
            {
                SendReply(item.parent.playerOwner, "You can't sell damaged goods! Use /fix to repair.");
                return false;
            }
            return null;
        }
        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || !(container is NPCVendingMachine)) return null;
            if (!(container is InvisibleVendingMachine)) return null;
            InvisibleVendingMachine v = container as InvisibleVendingMachine;
            if (!v.CanOpenLootPanel(player, v.customerPanel) || !player.inventory.loot.StartLootingEntity((BaseEntity)v, false))
                return false;
            v.SetFlag(BaseEntity.Flags.Open, true, false, true);
            v.AddContainers(player.inventory.loot);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer<string>((Connection)null, player, "RPC_OpenLootPanel", v.customerPanel);
            v.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            return true;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            killUI(player);
            clearPlayerSell(player);
        }
        private object OnEntityVisibilityCheck(BaseEntity entity, BasePlayer player, uint rpcId, string debugName, float maximumDistance)
        {
            if (entity is VendingMachine)
            {
                return true;
            }
            return null;
        }
        object OnBuyVendingItem(VendingMachine machine, BasePlayer player, int sellOrderId, int numberOfTransactions)
        {
            if (machine is InvisibleVendingMachine)
            {
                InvisibleVendingMachine im = machine as InvisibleVendingMachine;
            }
            machine.SetPendingOrder(player, sellOrderId, numberOfTransactions);
            DoTransaction(machine, player, sellOrderId, numberOfTransactions);
            machine.ClearPendingOrder();
            var finaleffect = new Effect("assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab", player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(finaleffect, player.net.connection);

            Decay.RadialDecayTouch(machine.transform.position, 40f, 2097408);
            return true;
        }
        #endregion

        #region Commands

        [ConsoleCommand("tradeui.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            killUI(player);
            player.ClientRPCPlayer(null, player, "OnRespawnInformation", new RespawnInformation { spawnOptions = new List<RespawnInformation.SpawnOptions>() }.ToProtoBytes());
        }
        [ConsoleCommand("tradeui.closeedit")]
        private void CmdCloseEdit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            CuiHelper.DestroyUi(player, mainName + "_offer");
        }
        [ConsoleCommand("tradeui.closesell")]
        private void CmdCloseSell(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            CuiHelper.DestroyUi(player, mainName + "_sell");
        }
        [ConsoleCommand("tradeui.opensell")]
        private void CmdOpenSell(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string itemShortname = arg.GetString(0);
            CuiHelper.DestroyUi(player, mainName + "_sell");
            if (itemShortname == null) return;

            currentPlayerSell[player.userID] = itemShortname;
            reloadUI(player);
        }
        [ConsoleCommand("tradeui.opensellfilter")]
        private void CmdOpenSellFilter(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            CuiHelper.DestroyUi(player, mainName + "_sell");
            displaySellUI(player);
        }
        [ConsoleCommand("tradeui.clearsellfilter")]
        private void CmdClearSellFilter(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            clearPlayerSell(player);
            reloadUI(player);
        }
        [ConsoleCommand("tradeui.back")]
        private void CmdBack(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string itemCat = arg.GetString(0);

            CuiHelper.DestroyUi(player, mainName + "_shop");
            GUIHomeElement(player, mainName + "_home", itemCat);
            player.ClientRPCPlayer(null, player, "OnRespawnInformation", new RespawnInformation { spawnOptions = new List<RespawnInformation.SpawnOptions>() }.ToProtoBytes());

            currentPlayerFav[player.userID] = new FavoriteItem { itemCat = itemCat, consoleCommand = "tradeui.openhomecat" };
        }

        [ConsoleCommand("tradeui.openhomecat")]
        private void CmdOpenHomeCat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string itemCat = arg.GetString(0);

            CuiHelper.DestroyUi(player, mainName + "_home");
            GUIHomeElement(player, mainName + "_home", itemCat);
            currentPlayerFav[player.userID] = new FavoriteItem { itemCat = itemCat, consoleCommand = "tradeui.openhomecat" };
        }

        [ConsoleCommand("tradeui.openvendorlist")]
        private void CmdOpenVendorList(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            int itemSoldId = arg.GetInt(0);
            string itemCat = arg.GetString(1);

            CuiHelper.DestroyUi(player, mainName + "_home");
            GUIShopElement(player, mainName + "_shop", itemSoldId, itemCat);
            currentPlayerFav[player.userID] = new FavoriteItem { itemSoldId = itemSoldId, itemCat = itemCat, consoleCommand = "tradeui.openvendorlist" };

        }

        [ConsoleCommand("tradeui.openvendor")]
        private void CmdOpenVendor(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string vendorNetId = arg.GetString(0);
            VendingMachine v = VendorList[Convert.ToUInt64(vendorNetId)];

            /*  Net manipulation -> no longer required? 
            if (!v.net.group.subscribers.Contains(player.net.connection))
            {
                v.net.group.AddSubscriber(player.net.connection);
                v.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
            */
            player.ClientRPCPlayer(null, player, "RPC_OpenShop", Convert.ToUInt64(vendorNetId));
            OpenVendorUI(player, v);
            currentPlayerFav[player.userID] = new FavoriteItem { vendorNetId = vendorNetId, consoleCommand = "tradeui.openvendor" };

        }
        [ConsoleCommand("tradeui.editoffer")]
        private void CmdEditOffer(ConsoleSystem.Arg arg)
        {
            if (!Cfg.allowEdits) return;
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string vendorNetId = arg.GetString(0);
            string sellOrderId = arg.GetString(1);
            VendingMachine v = VendorList[Convert.ToUInt64(vendorNetId)];
            if (v == null) return;
            if (v.OwnerID == 0 && !player.IsAdmin || v.OwnerID != 0 && v.OwnerID != player.userID) return;

            GUIOfferElement(player, mainName + "_offer", v, sellOrderId);

        }


        [ConsoleCommand("tradeui.editofferinput")]
        private void CmdEditOfferInput(ConsoleSystem.Arg arg)
        {
            if (!Cfg.allowEdits) return;
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string vendorNetId = arg.GetString(0);
            string itemOrPrice = arg.GetString(1);
            string sellOrderId = arg.GetString(2);
            string amount = arg.GetString(3);

            VendingMachine v = VendorList[Convert.ToUInt64(vendorNetId)];
            if (v == null) return;
            if (v.OwnerID == 0 && !player.IsAdmin || v.OwnerID != 0 && v.OwnerID != player.userID) return;

            int amountNum = 0;

            if (!int.TryParse(amount, out amountNum))
            {
                CuiHelper.DestroyUi(player, mainName + "_offer");
                GUIOfferElement(player, mainName + "_offer", v, sellOrderId, "Entry must be a number!");
                return;
            }
            if (amountNum < 1 || amountNum > 99999)
            {
                CuiHelper.DestroyUi(player, mainName + "_offer");
                GUIOfferElement(player, mainName + "_offer", v, sellOrderId, "Entry must be between 1 and 99999!");
                return;
            }

            ProtoBuf.VendingMachine.SellOrder thisOrder = CurrentEditOrders[sellOrderId];
            if (thisOrder == null)
            {
                CuiHelper.DestroyUi(player, mainName + "_offer");
                GUIOfferElement(player, mainName + "_offer", v, sellOrderId, "Invalid sell order!");
                return;
            }
            if (itemOrPrice == "item")
            {
                thisOrder.itemToSellAmount = amountNum;
            }
            else
            {
                thisOrder.currencyAmountPerItem = amountNum;
            }
            v.SendNetworkUpdateImmediate();
            CuiHelper.DestroyUi(player, mainName + "_offer");
            string itemCat = ItemManager.itemDictionary[thisOrder.itemToSellID].category.ToString("G");

            //Puts("DEBUG: - " + itemCat + " itemAmnt " + thisOrder.itemToSellAmount + " for " + thisOrder.currencyAmountPerItem);

            CuiHelper.DestroyUi(player, mainName + "_shop");
            GUIShopElement(player, mainName + "_shop", thisOrder.itemToSellID, itemCat);
        }


        [ConsoleCommand("tradeui.savefav")]
        private void CmdSaveFavorite(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string favstring = arg.GetString(0);
            //initial save
            if (!storedData.fav.ContainsKey(player.userID))
            {
                storedData.fav[player.userID] = new PlayerFavorite();
            }
            // no current?
            if (!currentPlayerFav.ContainsKey(player.userID))
            {
                currentPlayerFav[player.userID] = new FavoriteItem();
            }

            switch (favstring)
            {
                case "f1":
                    storedData.fav[player.userID].f1 = currentPlayerFav[player.userID];
                    break;
                case "f2":
                    storedData.fav[player.userID].f2 = currentPlayerFav[player.userID];
                    break;
                case "f3":
                    storedData.fav[player.userID].f3 = currentPlayerFav[player.userID];
                    break;
                case "f4":
                    storedData.fav[player.userID].f4 = currentPlayerFav[player.userID];
                    break;
                case "f5":
                    storedData.fav[player.userID].f5 = currentPlayerFav[player.userID];
                    break;
                default:
                    ShowInfoPopup(player, "Error, wrong favorite", true);
                    return;
            }

            string msg = "Saving " + favstring;
            msg += "\n<color=white>(check </color><color=orange>/trade info</color><color=white> for details)</color>";
            ShowInfoPopup(player, msg);
            SaveData();
        }
        [ChatCommand("sell")]
        void CmdTradeSell(BasePlayer player, string command, string[] args)
        {
            if (!permCheckVerbose(player, permUse)) return;
            displaySellUI(player);
        }

        [ChatCommand("trade")]
        void CmdTradeUI(BasePlayer player, string command, string[] args)
        {
            if (!permCheckVerbose(player, permUse)) return;
            updateVendorList();
            if(args.Length == 0)
            {
                currentPlayerFav[player.userID] = new FavoriteItem { itemCat = Cfg.defaultHome, consoleCommand = "tradeui.openhomecat" };
                reloadUI(player, "test");
                return;
            }
            //open fav
            if(args.Length == 1)
            {
                switch (args[0])
                {
                    case "f1":
                        currentPlayerFav[player.userID] = storedData.fav[player.userID].f1;
                        break;
                    case "f2":
                        currentPlayerFav[player.userID] = storedData.fav[player.userID].f2;
                        break;
                    case "f3":
                        currentPlayerFav[player.userID] = storedData.fav[player.userID].f3;
                        break;
                    case "f4":
                        currentPlayerFav[player.userID] = storedData.fav[player.userID].f4;
                        break;
                    case "f5":
                        currentPlayerFav[player.userID] = storedData.fav[player.userID].f5;
                        break;
                    case "info":
                        getFavInfo(player);
                        return;
                    default:
                        SendReply(player, " <color=red>ERROR:</color> usage example <color=green>/trade f1</color> (f1 to f5)");
                        return;
                }
            }
            if(currentPlayerFav[player.userID] == null)
            {
                currentPlayerFav[player.userID] = new FavoriteItem { itemCat = Cfg.defaultHome, consoleCommand = "tradeui.openhomecat" };
            }
            reloadUI(player, "test");
            currentPlayerFav[player.userID].runCommand(player);


        }

        #endregion

        #region Functions
        private void ShowInfoPopup(BasePlayer player, string msg, bool isError = false)
        {

            timer.Once(1f, () => {
                CuiElementContainer GUITEXT = new CuiElementContainer();
                string color = (isError) ? "1 0 0 1" : "0 9 0 1";
                //GUITEXT = new CuiElementContainer();

                GUITEXT.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        FadeIn = 0.5f
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.3 0.7",
                        AnchorMax = "0.7 0.8"
                    },
                    CursorEnabled = false
                }, "Overlay", "AnnouncementText");
                GUITEXT.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = msg,
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = color,
                        FadeIn = 0.9f
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }, "AnnouncementText");

                CuiHelper.DestroyUi(player, "AnnouncementText");
                CuiHelper.AddUi(player, GUITEXT);

                timer.Once(5f, () => {
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                });

            });
        }

        public bool DoTransaction(VendingMachine vm, BasePlayer buyer, int sellOrderId, int numberOfTransactions = 1)
        {
            if (sellOrderId < 0 || sellOrderId > vm.sellOrders.sellOrders.Count)
                return false;
            object obj1 = Interface.CallHook("OnVendingTransaction", (object)vm, (object)buyer, (object)sellOrderId, (object)numberOfTransactions);
            if (obj1 is bool)
                return (bool)obj1;
            ProtoBuf.VendingMachine.SellOrder sellOrder = vm.sellOrders.sellOrders[sellOrderId];
            List<Item> source1 = vm.inventory.FindItemsByItemID(sellOrder.itemToSellID);
            if (sellOrder.itemToSellIsBP)
                source1 = vm.inventory.FindItemsByItemID(vm.blueprintBaseDef.itemid).Where<Item>((Func<Item, bool>)(x => x.blueprintTarget == sellOrder.itemToSellID)).ToList<Item>();
            if (source1 == null || source1.Count == 0)
                return false;
            numberOfTransactions = Mathf.Clamp(numberOfTransactions, 1, source1[0].hasCondition ? 1 : 1000000);
            int num1 = sellOrder.itemToSellAmount * numberOfTransactions;
            int num2 = source1.Sum<Item>((Func<Item, int>)(x => x.amount));
            if (num1 > num2)
                return false;
            List<Item> source2 = buyer.inventory.FindItemIDs(sellOrder.currencyID);
            if (sellOrder.currencyIsBP)
                source2 = buyer.inventory.FindItemIDs(vm.blueprintBaseDef.itemid).Where<Item>((Func<Item, bool>)(x => x.blueprintTarget == sellOrder.currencyID)).ToList<Item>();
            List<Item> list = source2.Where<Item>((Func<Item, bool>)(x =>
            {
                if (!x.hasCondition)
                    return true;
                if ((double)x.conditionNormalized >= 0.5)
                    return (double)x.maxConditionNormalized > 0.5;
                return false;
            })).ToList<Item>();
            if (list.Count == 0)
                return false;
            int num3 = list.Sum<Item>((Func<Item, int>)(x => x.amount));
            int num4 = sellOrder.currencyAmountPerItem * numberOfTransactions;
            int num5 = num4;
            if (num3 < num5)
                return false;
            vm.transactionActive = true;
            int num6 = 0;
            foreach (Item obj2 in list)
            {
                int split_Amount = Mathf.Min(num4 - num6, obj2.amount);
                vm.TakeCurrencyItem(obj2.amount > split_Amount ? obj2.SplitItem(split_Amount) : obj2);
                num6 += split_Amount;
                if (num6 >= num4)
                    break;
            }
            int num7 = 0;
            foreach (Item obj2 in source1)
            {
                int split_Amount = num1 - num7;
                Item soldItem = obj2.amount > split_Amount ? obj2.SplitItem(split_Amount) : obj2;
                if (soldItem == null)
                {
                    Debug.LogError((object)"Vending machine error, contact developers!");
                }
                else
                {
                    num7 += soldItem.amount;
                    vm.GiveSoldItem(soldItem, buyer);
                }
                if (num7 >= num1)
                    break;
            }
            vm.UpdateEmptyFlag();
            vm.transactionActive = false;
            return true;
        }

        private void updateVendorList()
        {
            if ((DateTime.Now - LastRefreshTime).TotalSeconds < 60) return;

            var Vendors = Resources.FindObjectsOfTypeAll<VendingMachine>();
            VendorItemList?.Clear();
            categoryList?.Clear();

            foreach (VendingMachine v in Vendors)
            {
                if (v.net == null) continue;
                if (!v.IsBroadcasting()) continue;
                if(!Cfg.allowNPCs && v.OwnerID == 0) continue;
                v.globalBroadcast = true;
                v.UpdateNetworkGroup();

                if (!VendorList.ContainsKey(v.net.ID.Value))
                {
                    VendorList.Add(v.net.ID.Value, v);
                }
                foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in v.sellOrders.sellOrders)
                {
                    if (sellOrder.inStock < 1) continue;
                    if (VendorItemList.ContainsKey(sellOrder.itemToSellID))
                    {
                        if (VendorItemList[sellOrder.itemToSellID].Contains(v.net.ID.Value)) continue;
                        VendorItemList[sellOrder.itemToSellID].Add(v.net.ID.Value);
                    }
                    else
                    {
                        VendorItemList.Add(sellOrder.itemToSellID, new List<ulong>() { v.net.ID.Value });
                    }
                    if (!categoryList.Contains(ItemManager.itemDictionary[sellOrder.itemToSellID].category.ToString("G")))
                    {
                        categoryList.Add(ItemManager.itemDictionary[sellOrder.itemToSellID].category.ToString("G"));
                    }
                }
            }
            SortedVendorList = VendorItemList.OrderBy(o => ItemManager.itemDictionary[o.Key].displayName.translated).ToList();
            categoryList.Sort();

            if(Cfg.showUpdates) Puts("INFO: Updated vendors after " + (DateTime.Now - LastRefreshTime).TotalSeconds + "s of inactivity - found: " +VendorList.Count());
            LastRefreshTime = DateTime.Now;
        }


        private void OpenVendorUI(BasePlayer player, VendingMachine v)
        {
            v.globalBroadcast = true;
            v.UpdateNetworkGroup();
            v.SendSellOrders(player);
            v.PlayerOpenLoot(player, v.customerPanel, false);
            Interface.CallHook("OnOpenVendingShop", (object)v, (object)player);
        }

        private bool permCheckVerbose(BasePlayer player, string perm)
        {
            if (HasPermission(player.UserIDString, perm)) return true;
            SendReply(player, "No permission to use this command!");
            return false;
        }

        private string getSellOrderId(ulong vid, ProtoBuf.VendingMachine.SellOrder s)
        {
            string sid = vid + "s" + s.itemToSellID + "a" + s.itemToSellAmount + "c" + s.currencyID + "p" + s.currencyAmountPerItem + "b" + s.currencyIsBP;
            if (!CurrentEditOrders.ContainsKey(sid))
            {
                CurrentEditOrders.Add(sid, s);
            }
            return sid;
        }

        private void getFavInfo(BasePlayer player)
        {

            string msg = "<color=orange>============ TradeUI (v" + Plugin.Version + ") ============</color>";
            if (!(storedData.fav.ContainsKey(player.userID)))
            {
                msg += "\nYou don't have any favorites yet.\nSet them via <color=green>/trade</color> GUI";
            }
            else
            {
                PlayerFavorite pf = storedData.fav[player.userID];
                msg += "\nF1: " + ((pf.f1 != null) ? pf.f1.toString() : "not set");
                msg += "\nF2: " + ((pf.f2 != null) ? pf.f2.toString() : "not set");
                msg += "\nF3: " + ((pf.f3 != null) ? pf.f3.toString() : "not set");
                msg += "\nF4: " + ((pf.f4 != null) ? pf.f4.toString() : "not set");
                msg += "\nF5: " + ((pf.f5 != null) ? pf.f5.toString() : "not set");
            }
            SendReply(player, msg);
        }

        private void clearPlayerSell(BasePlayer player)
        {
            if (currentPlayerSell.ContainsKey(player.userID)) currentPlayerSell.Remove(player.userID);
        }
        #endregion

        #region GUI

        private const string globalNoErrorString = "none";
        private const string mainName = "TradeUI";


        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements =
        {
            mainName+"_bg",
            mainName+"_head",
            mainName+"_home",
            mainName+"_shop",
            mainName+"_offer",
            mainName+"_foot",
            mainName+"_sell"
        };
        private float globalLeftBoundary = 0.1f;
        private float globalRighttBoundary = 0.9f;
        private float globalTopBoundary = 0.95f;
        private float globalBottomBoundary = 0.1f;
        private float globalSpace = 0.01f;
        private float eContentWidth = 0.395f;
        private float eControlWidth = 0.15f;
        private float eHeadHeight = 0.05f;
        private float eFootHeight = 0.05f;
        private float eSlotHeight = 0.123f;


        private void reloadUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (!UiPlayers.Contains(player))
            {
                UiPlayers.Add(player);
            }
            foreach (string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }
            displayUI(player, errorMsg);

        }
        private void killUI(BasePlayer player)
        {
            foreach (string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }
            if (UiPlayers.Contains(player))
            {
                UiPlayers.Remove(player);
            }
        }

        private string numberCandy(int number)
        {
            return Convert.ToDecimal(number).ToString("N", nfi);
        }
        private void displayUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            //GUIOverlayElement(player, mainName+"_bg");
            GUIHeaderElement(player, mainName + "_head");
            GUIHomeElement(player, mainName + "_home", Cfg.defaultHome);
            GUIFooterElement(player, mainName + "_foot");
        }
        private void displaySellUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (!UiPlayers.Contains(player))
            {
                UiPlayers.Add(player);
            }
            CuiHelper.DestroyUi(player, mainName + "_sell");
            GUISellElement(player, mainName + "_sell");
        }

        private void GUIHeaderElement(BasePlayer player, string elUiId)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.12 0.56 0.12 1",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = "0 " + (1f-eHeadHeight),
                    AnchorMax = "1 " + (1f)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Global /trade System",
                    FontSize = 18,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0",
                    AnchorMax = "1 1"
                }
            }, elUiId);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = Plugin.Name +" v" + Plugin.Version + " by " + Plugin.Author,
                    FontSize = 10,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0.99 1"
                }
            }, elUiId);


            //Favorites items
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Set current page as favorite: (open with - /trade f1...f5)",
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.35 0",
                    AnchorMax = "0.65 1"
                }
            }, elUiId);
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0",
                },
                RectTransform =
                {
                    AnchorMin = "0.35 0",
                    AnchorMax = "0.65 0.3"
                },
                CursorEnabled = true
            }, elUiId, elUiId + "_scaler");

            float localLeft = 0.0f;
            for (int i = 1; i < 6; i++)
            {
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "tradeui.savefav f" + i,
                        Color = "0.8 0.8 0.8 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = localLeft + " 0",
                        AnchorMax = (localLeft+0.17f) + " 1"
                    },
                    Text =
                    {
                        Text = "F" + i ,
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId + "_scaler");
                localLeft += 0.18f;
            }

            CuiHelper.AddUi(player, elements);
        }
        private void GUIOverlayElement(BasePlayer player, string elUiId)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 1",
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            CuiHelper.AddUi(player, elements);
        }
        private void GUIFooterElement(BasePlayer player, string elUiId)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 1",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 " + (eFootHeight)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "tradeui.close",
                    Color = "0.56 0.12 0.12 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Text = "CLOSE",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(closeButton, elUiId);

            CuiHelper.AddUi(player, elements);
        }

        private void GUIHomeElement(BasePlayer player, string elUiId, string currentCategory)
        {
            var elements = new CuiElementContainer();

            float topBoundary = globalTopBoundary - eHeadHeight - globalSpace;
            float botBoundary = globalBottomBoundary + eFootHeight + globalSpace;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 " + (1f-eHeadHeight)
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            float contentStart = 0.97f;
            float itemsPerRow = 15f;
            float tabWidth = 1f / 16f;
            float tabHeight = 0.03f;
            float cellWidth = 0.9f / (itemsPerRow + 1f);
            float buttonHeight = 1.5f * cellWidth;
            float leftBoundary = 0.02f;
            float leftWidth = 0.95f;
            float space = 0.005f;

            int numItems = 0;
            List<KeyValuePair<int, List<ulong>>> sortedItems = new List<KeyValuePair<int, List<ulong>>>();
            int totalItems = 0;
            Dictionary<string, int> catItems = new Dictionary<string, int>();
            // switch between filter view
            foreach (KeyValuePair<int, List<ulong>> kv in SortedVendorList)
            {
                if (!catItems.ContainsKey(ItemManager.itemDictionary[kv.Key].category.ToString("G")))
                {
                    catItems.Add(ItemManager.itemDictionary[kv.Key].category.ToString("G"), 0);
                }

                if (currentPlayerSell.ContainsKey(player.userID))
                {
                    HashSet<int> tempItemsAdded = new HashSet<int>();
                    foreach (ulong vID in kv.Value)
                    {
                        foreach (ProtoBuf.VendingMachine.SellOrder s in VendorList[vID].sellOrders.sellOrders)
                        {
                            if (s.itemToSellID != kv.Key) continue;
                            if (ItemManager.itemDictionary[s.currencyID].shortname == currentPlayerSell[player.userID])
                            {
                                totalItems++;
                                if (!tempItemsAdded.Contains(kv.Key)) {
                                    sortedItems.Add(kv);
                                    tempItemsAdded.Add(kv.Key);
                                    catItems[ItemManager.itemDictionary[kv.Key].category.ToString("G")] += 1;
                                 }
                            }
                        }
                    }
                }
                else
                {
                    catItems[ItemManager.itemDictionary[kv.Key].category.ToString("G")] += 1;
                }
            }
            if (!currentPlayerSell.ContainsKey(player.userID))
            {
                sortedItems = SortedVendorList;
                totalItems = sortedItems.Count();
            }

            if(totalItems > 50)
            {
                foreach (string catName in categoryList)
                {
                    if (!catItems.ContainsKey(catName) || catItems[catName] <= 0) continue;

                    float localLeftBoundary = (float)(leftBoundary + (numItems % itemsPerRow * (tabWidth + space)));
                    string buttonColor = (catName == currentCategory) ? "0.7 0.38 0 1" : "0.3 0.3 0.3 1";

                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "tradeui.openhomecat " + catName,
                            Color = buttonColor
                        },
                        RectTransform =
                        {
                            AnchorMin = localLeftBoundary +" "+ (contentStart-tabHeight),
                            AnchorMax = (localLeftBoundary + tabWidth) + " "+ contentStart
                        },
                        Text =
                        {
                            Text = catName + " ("+catItems[catName]+")",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, elUiId);

                    numItems++;
                }
                contentStart = contentStart - buttonHeight;
            }
            else if (totalItems <= 0 && currentPlayerSell.ContainsKey(player.userID))
            {
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "No items for sale for " + ItemManager.itemDictionaryByName[currentPlayerSell[player.userID]].displayName.translated,
                    FontSize = 25,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.005 0.70",
                    AnchorMax = "0.995 0.995"
                }
                }, elUiId);
            }
            
            numItems = 0;
            foreach (KeyValuePair<int, List<ulong>> kv in sortedItems)
            {
                if (totalItems > 50)
                {
                    if (ItemManager.itemDictionary[kv.Key].category.ToString("G") != currentCategory) continue;
                }
                string sellItemName = ItemManager.itemDictionary[kv.Key].displayName.translated;

                int rowNum = numItems % (int)itemsPerRow;
                int lastRowNum = rowNum;
                int colNum = (int)Math.Floor(numItems / itemsPerRow);

                float localContentStart = (float)(contentStart - (Math.Floor(numItems / itemsPerRow) * (buttonHeight + 3 * space)));
                float localLeftBoundary = (float)(leftBoundary + (numItems % itemsPerRow * (cellWidth + space)));

                // Images
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = elUiId,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage(ItemManager.itemDictionary[kv.Key].shortname) },
                        new CuiRectTransformComponent {
                            AnchorMin = (localLeftBoundary+0.005f) +" "+ (localContentStart-buttonHeight+0.01f),
                            AnchorMax = (localLeftBoundary + cellWidth-0.005f) + " "+ localContentStart
                        }
                    }
                });

                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "tradeui.openvendorlist " + kv.Key + " " + currentCategory,
                        Color = "0.7 0.38 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = localLeftBoundary +" "+ (localContentStart-buttonHeight),
                        AnchorMax = (localLeftBoundary + cellWidth) + " "+ localContentStart
                    },
                    Text =
                    {
                        Text = sellItemName,
                        FontSize = 6,
                        Align = TextAnchor.LowerCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);



                numItems++;
            }
            if (currentPlayerSell.ContainsKey(player.userID)) {
                elements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = "tradeui.opensellfilter",
                                Color = "0.7 0.38 0 1"
                            },
                    RectTransform =
                            {
                                AnchorMin = "0.25 0.2",
                                AnchorMax = "0.49 0.3"
                            },
                    Text =
                            {
                                Text = "Change /sell filter",
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                }, elUiId); 
                elements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = "tradeui.clearsellfilter",
                                Color = "0.56 0.12 0.12 1"
                            },
                    RectTransform =
                            {
                                AnchorMin = "0.501 0.2",
                                AnchorMax = "0.75 0.3"
                            },
                    Text =
                            {
                                Text = "Clear /sell filter\n("+ItemManager.itemDictionaryByName[currentPlayerSell[player.userID]].displayName.translated+")",
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                }, elUiId);
            }
            else
            {
                elements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = "tradeui.opensellfilter",
                                Color = "0.7 0.38 0 1"
                            },
                    RectTransform =
                            {
                                AnchorMin = "0.4 0.2",
                                AnchorMax = "0.6 0.3"
                            },
                    Text =
                            {
                                Text = "Add /sell for item filter",
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                }, elUiId);
            }
            CuiHelper.AddUi(player, elements);

        }

        private void GUIShopElement(BasePlayer player, string elUiId, int itemSoldId, string currentCategory)
        {
            var elements = new CuiElementContainer();

            float topBoundary = globalTopBoundary - eHeadHeight - globalSpace;
            float botBoundary = globalBottomBoundary + eFootHeight + globalSpace;

            string sellItemName = ItemManager.itemDictionary[itemSoldId].displayName.translated;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 " + (1f-eHeadHeight)
                },
                CursorEnabled = true
            }, "Hud", elUiId);


            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Vendors selling "+ sellItemName,
                    FontSize = 18,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.90",
                    AnchorMax = "0.46 0.995"
                }
            }, elUiId);
            List<Item> playerInventory = player.inventory.containerMain.itemList;
            // Images
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage(ItemManager.itemDictionary[itemSoldId].shortname) },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.47 0.9",
                        AnchorMax = "0.53 0.995"
                    }
                }
            });
            float buttonHeight = 0.18f;
            float contentStart = 0.895f;
            float itemsPerRow = 8f;
            float cellWidth = 0.94f / (itemsPerRow);
            float leftBoundary = 0.005f;
            float leftWidth = 1f;
            float space = 0.005f;

            int numItems = 0;

            foreach (ulong vendorId in VendorItemList[itemSoldId])
            {


                string vendorUID = elUiId + "_v" + vendorId;
                VendingMachine v = VendorList[vendorId];
                /* Net manipulation -> no longer required? 
                if (!v.net.group.subscribers.Contains(player.net.connection))
                {
                    v.net.group.AddSubscriber(player.net.connection);
                    v.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                }
                */

                var inventory = v.sellOrders.sellOrders;
                string cleanName = Regex.Replace(v.shopName, @"[^a-zA-Z0-9.\-+ <>=/]+", ""); ; 
                string itemStr = "SHOP: " + cleanName + "\nPrice:";

                bool isMyShop = (v.OwnerID == player.userID);

                if (currentPlayerSell.ContainsKey(player.userID))
                {
                    bool foundItem = false;
                    foreach (ProtoBuf.VendingMachine.SellOrder s in inventory)
                    {
                        if (s.itemToSellID != itemSoldId) continue;
                        if (ItemManager.itemDictionary[s.currencyID].shortname == currentPlayerSell[player.userID])
                        {
                            foundItem = true;
                            break;
                        }
                    }
                    if (!foundItem) continue;
                }

                int rowNum = numItems % (int)itemsPerRow;
                int lastRowNum = rowNum;
                int colNum = (int)Math.Floor(numItems / itemsPerRow);

                float localContentStart = (float)(contentStart - (Math.Floor(numItems / itemsPerRow) * (buttonHeight + 3 * space)));
                float localLeftBoundary = (float)(leftBoundary + (numItems % itemsPerRow * (cellWidth + space)));
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0.0",
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary +" "+ (localContentStart-buttonHeight),
                    AnchorMax = (localLeftBoundary + cellWidth) + " "+ localContentStart
                },
                    CursorEnabled = true
                }, elUiId, vendorUID);
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = cleanName,
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.01 0.82",
                    AnchorMax = "0.99 0.96"
                }
                }, vendorUID);



                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "tradeui.openvendor " + v.net.ID.Value,
                        Color = "0.7 0.38 0 0.0"
                    },
                    RectTransform =
                    {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                    },
                    Text =
                    {
                        Text = "",
                        FontSize = 10,
                        Align = TextAnchor.UpperRight,
                        Color = "1 1 1 1"
                    }
                }, vendorUID);

                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0.7",
                },
                    RectTransform =
                {
                    AnchorMin = "0.0 0.0",
                    AnchorMax = "1.0 0.81"
                },
                    CursorEnabled = true
                }, vendorUID, vendorUID + "_l1");

                if(Cfg.showStock) elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "(in stock: "+v.inventory.GetAmount(itemSoldId,true)+")",
                    FontSize = 8,
                    Align = TextAnchor.UpperCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.01 0.70",
                    AnchorMax = "0.99 0.80"
                }
                }, vendorUID);

                int actualItems = 0;
                foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in inventory)
                {
                    if (sellOrder.itemToSellID != itemSoldId) continue;
                    actualItems++;
                }


                if (actualItems <= 2)
                {
                    _GUISmallShopList(inventory, itemSoldId, v, elements, vendorUID + "_l1", isMyShop, player);
                }
                else
                {
                    _GUIBigShopList(inventory, itemSoldId, v, elements, vendorUID + "_l1", isMyShop, player);
                }



                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "tradeui.openvendor " + v.net.ID.Value,
                        Color = "0.7 0.38 0 0.9"
                    },
                    RectTransform =
                    {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.15"
                    },
                    Text =
                    {
                        Text = "Open Shop",
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, vendorUID);

                numItems++;
            }


            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "tradeui.back " + currentCategory,
                    Color = "0.3 0.3 0.3 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.92",
                    AnchorMax = "0.18 0.97"
                },
                Text =
                {
                    Text = "<<< Back",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(closeButton, elUiId);

            if (currentPlayerSell.ContainsKey(player.userID))
            {
                elements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = "tradeui.opensellfilter",
                                Color = "0.7 0.38 0 1"
                            },
                    RectTransform =
                            {
                                AnchorMin = "0.8 0.92",
                                AnchorMax = "0.89 0.97"
                            },
                    Text =
                            {
                                Text = "Change /sell filter",
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                }, elUiId);
                elements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = "tradeui.clearsellfilter",
                                Color = "0.56 0.12 0.12 1"
                            },
                    RectTransform =
                            {
                                AnchorMin = "0.9 0.92",
                                AnchorMax = "1 0.97"
                            },
                    Text =
                            {
                                Text = "Clear /sell filter\n("+ItemManager.itemDictionaryByName[currentPlayerSell[player.userID]].displayName.translated+")",
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                }, elUiId);
            }
            else
            {
                elements.Add(new CuiButton
                {
                    Button =
                            {
                                Command = "tradeui.opensellfilter",
                                Color = "0.7 0.38 0 1"
                            },
                    RectTransform =
                            {
                                AnchorMin = "0.82 0.92",
                                AnchorMax = "1 0.97"
                            },
                    Text =
                            {
                                Text = "Add /sell for item filter",
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                }, elUiId);
            }

            CuiHelper.AddUi(player, elements);

        }

        private void _GUISmallShopList(List<ProtoBuf.VendingMachine.SellOrder> inventory, int itemSoldId, VendingMachine v, CuiElementContainer elements, string vendorUID, bool isMyShop, BasePlayer player)
        {
            float localShopRowTop = 0.9f;
            foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in inventory)
            {
                if (sellOrder.itemToSellID != itemSoldId) continue;
                string sellItemName = ItemManager.itemDictionary[sellOrder.itemToSellID].displayName.translated;
                string buyItemName = ItemManager.itemDictionary[sellOrder.currencyID].displayName.translated;
                //string itemStr += "\n- " + sellOrder.itemToSellAmount + "x for " + sellOrder.currencyAmountPerItem + "x " + buyItemName + " (stock: " + sellOrder.inStock + ")";


                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = vendorUID,
                    Components =
                        {
                            new CuiRawImageComponent {Png = GetImage(ItemManager.itemDictionary[sellOrder.itemToSellID].shortname) },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.01 "+ (localShopRowTop-0.3f),
                                AnchorMax = "0.23 " + localShopRowTop
                            }
                        }
                });
                elements.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = "x" + sellOrder.itemToSellAmount,
                            FontSize = 10,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                    RectTransform =
                        {
                            AnchorMin = "0.01 "+ (localShopRowTop-0.3f),
                            AnchorMax = "0.23 " + localShopRowTop
                        }
                }, vendorUID);

                elements.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = "for",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                    RectTransform =
                        {
                            AnchorMin = "0.24 "+ (localShopRowTop-0.3f),
                            AnchorMax = "0.76 " + localShopRowTop
                        }
                }, vendorUID);

                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = vendorUID,
                    Components =
                        {
                            new CuiRawImageComponent {Png = GetImage(ItemManager.itemDictionary[sellOrder.currencyID].shortname) },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.77 "+ (localShopRowTop-0.3f),
                                AnchorMax = "0.99 " + localShopRowTop
                            }
                        }
                });
                elements.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = "x"+ sellOrder.currencyAmountPerItem,
                            FontSize = 10,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                    RectTransform =
                        {
                                AnchorMin = "0.77 "+ (localShopRowTop-0.3f),
                                AnchorMax = "0.99 " + localShopRowTop
                        }
                }, vendorUID);

                if (Cfg.allowEdits && isMyShop)
                {
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "tradeui.editoffer " + v.net.ID.Value + " " + getSellOrderId(v.net.ID.Value, sellOrder),
                            Color = "0.7 0.38 0 1.0"
                        },
                        RectTransform =
                        {
                        AnchorMin = "0.36 "+ (localShopRowTop-0.3f),
                        AnchorMax = "0.64 " + (localShopRowTop-0.1f)
                        },
                        Text =
                        {
                            Text = "edit offer",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, vendorUID);

                }
                // next row
                localShopRowTop -= 0.35f;

            }
        }


        private void _GUIBigShopList(List<ProtoBuf.VendingMachine.SellOrder> inventory, int itemSoldId, VendingMachine v, CuiElementContainer elements, string vendorUID, bool isMyShop, BasePlayer player)
        {
            float localShopRowTop = 0.9f;
            float localLeft = 0.01f;
            float localIconSize = 0.22f;
            float itemNum = 1;
            foreach (ProtoBuf.VendingMachine.SellOrder sellOrder in inventory)
            {
                if (sellOrder.itemToSellID != itemSoldId) continue;
                string sellItemName = ItemManager.itemDictionary[sellOrder.itemToSellID].displayName.translated;
                string buyItemName = ItemManager.itemDictionary[sellOrder.currencyID].displayName.translated;

                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = vendorUID,
                    Components =
                        {
                            new CuiRawImageComponent {Png = GetImage(ItemManager.itemDictionary[sellOrder.currencyID].shortname) },
                            new CuiRectTransformComponent {
                                AnchorMin = localLeft +" "+ (localShopRowTop-0.3f),
                                AnchorMax = (localLeft+localIconSize)+" " + localShopRowTop
                            }
                        }
                });
                elements.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = "x"+ sellOrder.itemToSellAmount,
                            FontSize = 10,
                            Align = TextAnchor.UpperLeft,
                            Color = "1 1 1 1"
                        },
                    RectTransform =
                        {
                                AnchorMin = localLeft +" "+ (localShopRowTop-0.3f),
                                AnchorMax = (localLeft+localIconSize)+" " + localShopRowTop
                        }
                }, vendorUID);
                elements.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = "x"+ sellOrder.currencyAmountPerItem,
                            FontSize = 10,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                    RectTransform =
                        {
                                AnchorMin = localLeft +" "+ (localShopRowTop-0.3f),
                                AnchorMax = (localLeft+localIconSize)+" " + localShopRowTop
                        }
                }, vendorUID);

                if (Cfg.allowEdits && isMyShop)
                {
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "tradeui.editoffer " + v.net.ID.Value + " " + getSellOrderId(v.net.ID.Value, sellOrder),
                            Color = "0.7 0.38 0 1.0"
                        },
                        RectTransform =
                        {
                            AnchorMin = (localLeft+localIconSize-0.1) +" "+ (localShopRowTop-0.15f),
                            AnchorMax = (localLeft+localIconSize)+" " + localShopRowTop
                        },
                        Text =
                        {
                            Text = "E",
                            FontSize = 9,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, vendorUID);

                }
                // next row
                if(itemNum == 4)
                {
                    localShopRowTop -= 0.35f;
                    localLeft = 0.01f;
                }
                else
                {
                    localLeft = localLeft + localIconSize + 0.03f;
                }
                itemNum++;

            }
        }

        private void GUIOfferElement(BasePlayer player, string elUiId, VendingMachine v, string SellOrderId, string error = "")
        {
            var elements = new CuiElementContainer();

            float topBoundary = globalTopBoundary - eHeadHeight - globalSpace;
            float botBoundary = globalBottomBoundary + eFootHeight + globalSpace;

            ProtoBuf.VendingMachine.SellOrder sellOrder = CurrentEditOrders[SellOrderId];


            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = "0.2 0.4",
                    AnchorMax = "0.8 0.7"
                },
                CursorEnabled = true
            }, "Hud", elUiId);


            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Modify Sell Order",
                    FontSize = 14,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0.80",
                    AnchorMax = "0.95 0.95"
                }
            }, elUiId);
            string infoText = "Modify the amount you want to add and hit [enter] to change the offer! (values between 1 - 99.999)";
            string infoColor = "1 1 1 1";
            if (error != "")
            {
                infoText = "Error: " + error;
                infoColor = "1 0 0 1";
            }
            elements.Add(new CuiLabel
            {
                Text =
            {
                Text = infoText,
                FontSize = 12,
                Align = TextAnchor.MiddleLeft,
                Color = infoColor
            },
                RectTransform =
            {
                AnchorMin = "0.05 0.70",
                AnchorMax = "0.95 0.80"
            }
            }, elUiId);




            // icons
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                        {
                            new CuiRawImageComponent {Png = GetImage(ItemManager.itemDictionary[sellOrder.itemToSellID].shortname) },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.25 0.4",
                                AnchorMax = "0.35 0.7"
                            }
                        }
            });
            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "x"+ sellOrder.itemToSellAmount,
                            FontSize = 10,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                                AnchorMin = "0.25 0.4",
                                AnchorMax = "0.35 0.7"
                        }
            }, elUiId);

            //edit field
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "1 1 1 0.5",
                },
                RectTransform =
                {
                            AnchorMin = "0.20 0.25",
                            AnchorMax = "0.40 0.35"
                },
                CursorEnabled = true
            }, elUiId, elUiId + "i1");
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 50,
                            Color = "1 1 1 1",
                            Command = "tradeui.editofferinput "+ v.net.ID.Value + " item "+ SellOrderId,
                            FontSize = 10,
                            IsPassword = false,
                            Text = "test"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.20 0.25",
                            AnchorMax = "0.40 0.35"
                        }
                    }
            });


            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                        {
                            new CuiRawImageComponent {Png = GetImage(ItemManager.itemDictionary[sellOrder.currencyID].shortname) },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.65 0.4",
                                AnchorMax = "0.75 0.7"
                            }
                        }
            });
            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "x"+ sellOrder.currencyAmountPerItem,
                            FontSize = 10,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                                AnchorMin = "0.65 0.4",
                                AnchorMax = "0.75 0.7"
                        }
            }, elUiId);
            //edit field
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "1 1 1 0.5",
                },
                RectTransform =
                {
                            AnchorMin = "0.60 0.25",
                            AnchorMax = "0.80 0.35"
                },
                CursorEnabled = true
            }, elUiId, elUiId + "i1");
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 50,
                            Color = "1 1 1 1",
                            Command = "tradeui.editofferinput "+ v.net.ID.Value + " price "+ SellOrderId,
                            FontSize = 10,
                            IsPassword = false,
                            Text = "test123"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = "0.60 0.25",
                            AnchorMax = "0.80 0.35"
                        }
                    }
            });

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "tradeui.closeedit",
                    Color = "0.12 0.56 0.12 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.2 0.05",
                    AnchorMax = "0.8 0.2"
                },
                Text =
                {
                    Text = "Cancel (only if you didn't enter a value)",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(closeButton, elUiId);


            CuiHelper.AddUi(player, elements);

        }



        private void GUISellElement(BasePlayer player, string elUiId, string error = "")
        {
            var elements = new CuiElementContainer();

            float topBoundary = globalTopBoundary - eHeadHeight - globalSpace;
            float botBoundary = globalBottomBoundary + eFootHeight + globalSpace;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = "0.3 0.3",
                    AnchorMax = "0.7 0.8"
                },
                CursorEnabled = true
            }, "Hud", elUiId);


            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "What do you want to /Sell?",
                    FontSize = 14,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.93",
                    AnchorMax = "0.99 0.995"
                }
            }, elUiId);
            string infoText = "Click the Item (in you Inventory) you want to sell to open the filtered /trade UI";
            string infoColor = "1 1 1 1";
            if (error != "")
            {
                infoText = "Error: " + error;
                infoColor = "1 0 0 1";
            }
            elements.Add(new CuiLabel
            {
                Text =
            {
                Text = infoText,
                FontSize = 12,
                Align = TextAnchor.MiddleLeft,
                Color = infoColor
            },
                RectTransform =
            {
                AnchorMin = "0.01 0.85",
                AnchorMax = "0.99 0.95"
            }
            }, elUiId);

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0",
                    //Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.1",
                    AnchorMax = "0.99 0.85"
                },
                CursorEnabled = true
            }, elUiId, elUiId+"_items");

            _GUISellItem(elements, elUiId + "_items", player);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "tradeui.closesell",
                    Color = "0.56 0.12 0.12 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0.0",
                    AnchorMax = "0.999 0.1"
                },
                Text =
                {
                    Text = "Close",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(closeButton, elUiId);


            CuiHelper.AddUi(player, elements);

        }

        private void _GUISellItem(CuiElementContainer elements, string parentID, BasePlayer player)
        {
            float localLeft = 0f;
            float localTop = 1f;
            float itemheight = (1f / 4f) - 0.02f;
            float itemwidth = (1f / 6f) - 0.02f;
            int rownum = 0;
            int itemnum = 0;
           
            while(itemnum < 24)
            {
                Item i = player.inventory.containerMain.GetSlot(itemnum);
                
                if (itemnum > 0 && itemnum % 6 == 0)
                {
                    localLeft = 0f;
                    localTop = localTop - itemheight - 0.02f;
                    rownum++;
                }

                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "1 1 1 0.05",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                    RectTransform =

                {
                        AnchorMin = localLeft + " " + (localTop - itemheight),
                        AnchorMax = (localLeft + itemwidth) + " " + localTop
                },
                    CursorEnabled = true
                }, parentID, parentID + "_i"+itemnum);

                if(i != null)
                {
                    // icons
                    elements.Add(new CuiElement
                    {
                        Name = CuiHelper.GetGuid(),
                        Parent = parentID + "_i" + itemnum,
                        Components =
                            {
                                new CuiRawImageComponent {Png = GetImage(i.info.shortname) },
                                new CuiRectTransformComponent {
                                    AnchorMin = "0.2 0.15",
                                    AnchorMax = "0.8 0.85"
                                }
                            }
                    });
                    elements.Add(new CuiLabel
                    {
                        Text =
                                {
                                    Text = ItemManager.itemDictionary[i.info.itemid].displayName.translated,
                                    FontSize = 10,
                                    Align = TextAnchor.LowerCenter,
                                    Color = "1 1 1 1"
                                },
                        RectTransform =
                                {
                                    AnchorMin = "0.1 0.1",
                                    AnchorMax = "0.9 0.9"
                                }
                    }, parentID + "_i" + itemnum);
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "tradeui.opensell " + i.info.shortname,
                            Color = "0.56 0.12 0.12 0"
                        },
                                RectTransform =
                        {
                            AnchorMin = localLeft + " " + (localTop - itemheight),
                            AnchorMax = (localLeft + itemwidth) + " " + localTop
                        },
                                Text =
                        {
                            Text = "",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, parentID);
                }
                localLeft += itemwidth + 0.02f;
                itemnum++;
            }
        }

        #endregion

        private Dictionary<string, string> ImageCache = new Dictionary<string, string>();
        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }

    }
}

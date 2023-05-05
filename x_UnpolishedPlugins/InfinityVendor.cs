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
 * Questions and (limited) Support can be found in our Discord: https://discord.gg/8tkBFRjg3W
 * 
 */
using Facepunch;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("InfinityVendor", "DocValerian", "1.2.1")]
    class InfinityVendor : RustPlugin
    {
        static InfinityVendor Plugin;

        #region Lang&Config&Data

        private const string permInfinityVendor = "infinityvendor.use";
        private StoredData _storedData;
        private class StoredData {
            public List<ulong> InfinityMachines = new List<ulong>();
        }
        private void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("InfinityVendorMachines", _storedData);
        private void LoadData() {
            try {
                _storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("InfinityVendorMachines");
            } catch {
                _storedData = new StoredData();
            }
        }
        private ProtoBuf.VendingMachine.SellOrderContainer currentOrder = new ProtoBuf.VendingMachine.SellOrderContainer();
        private ItemContainer currentItems = new ItemContainer();
        ulong currentSkinId = 0;
        #endregion

        void Loaded()
        {
            Plugin = this;
            LoadData();
            permission.RegisterPermission(permInfinityVendor, this);

        }
        private void Unload() {
            SaveData();
        }
        object OnBuyVendingItem(VendingMachine machine, BasePlayer player, int sellOrderId, int numTransactions)
        {
            if (!_storedData.InfinityMachines.Contains(machine.net.ID.Value) 
                && !(machine is InvisibleVendingMachine)
                && !(machine.OwnerID == 0)) 
                return null;

            ProtoBuf.VendingMachine.SellOrder sellOrder = machine.sellOrders.sellOrders[sellOrderId];
            List<Item> items = machine.inventory.FindItemsByItemID(sellOrder.itemToSellID);
            List<Item> payment = machine.inventory.FindItemsByItemID(sellOrder.currencyID);
            int numberOfTransactions;
            int sellCount;
            if (items.Count == 0 || machine is InvisibleVendingMachine || machine.OwnerID == 0)
            {
                sellCount = ItemManager.itemDictionary[sellOrder.itemToSellID].stackable;
            }
            else
            {
                numberOfTransactions = Mathf.Clamp(numTransactions, 1, (!items[0].hasCondition ? 1000000 : 1));
                sellCount = sellOrder.itemToSellAmount * numberOfTransactions;
            }

            if(sellOrder.itemToSellIsBP)
            {
                //Puts("selling BP");
                Item i2 = ItemManager.CreateByItemID(-996920608, sellCount);
                i2.blueprintTarget = sellOrder.itemToSellID;
                machine.inventory.AddItem(i2.info, sellCount);

            }
            else
            {
                machine.inventory.AddItem(ItemManager.itemDictionary[sellOrder.itemToSellID], sellCount);
            }
            if (payment.Count != 0)
            {
                foreach (Item itm in payment){
                    // protect the first entry (may be an item for sale)
                    if(itm != payment[0]){
                        // remove the rest
                        machine.inventory.Remove(itm);
                    }
                }
            }
            return null;
        }

        #region ChatCommand
        [ChatCommand("ivcopy")]
        private void CopyInfinityVending(BasePlayer player)
        {
            if (!HasPermission(player))
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            object entity;
            if (GetRaycastTarget(player, out entity))
            {
                if (entity != null && entity is VendingMachine)
                {
                    currentItems = (entity as VendingMachine).inventory;
                    currentOrder = (entity as VendingMachine).sellOrders;
                    currentSkinId = (entity as BaseEntity).skinID;
                    SendReply(player, "Copied Inventory, Skin and SellOrders of this machine!\nUse /ivpaste to apply to another machine.");
                }
                else
                {
                    SendReply(player, "You're not looking at a vending machine!");
                }
            }
        }
        [ChatCommand("ivpaste")]
        private void PasteInfinityVending(BasePlayer player)
        {
            if (!HasPermission(player))
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            object entity;
            if (GetRaycastTarget(player, out entity))
            {
                if (entity != null && entity is VendingMachine)
                {
                    (entity as VendingMachine).inventory = currentItems;
                    (entity as VendingMachine).sellOrders = currentOrder;
                    (entity as BaseEntity).skinID = currentSkinId;
                    (entity as VendingMachine).SendNetworkUpdateImmediate();
                    SendReply(player, "Inventory, Skin and SellOrders applied from copy.");
                }
            }
        }
        [ChatCommand("ivall")]
        private void ToggleAllInfinityVending(BasePlayer player)
        {
            if (!HasPermission(player))
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            ulong vendingMachineId;
            var Vendors = UnityEngine.Object.FindObjectsOfType(typeof(VendingMachine));

            foreach (VendingMachine v in Vendors)
            {
                vendingMachineId = v.net.ID.Value;
                var priv = v.GetBuildingPrivilege();
                if (priv == null || !priv.IsAuthed(player)) continue;
                if (!_storedData.InfinityMachines.Contains(vendingMachineId))
                {
                    _storedData.InfinityMachines.Add(vendingMachineId);
                    SendReply(player, "Vending Machine ("+ vendingMachineId + ") is set to <color=green>infinity</color>");
                }
                else
                {
                    _storedData.InfinityMachines.Remove(vendingMachineId);
                    SendReply(player, "Vending Machine (" + vendingMachineId + ") is set to <color=red>normal vending</color>");
                }
                SaveData();
                
            }
            
        }
        [ChatCommand("ivend")]
        private void ToggleInfinityVending(BasePlayer player) {
            if(!HasPermission(player))
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            object entity;
            ulong vendingMachineId;
            if (GetRaycastTarget(player, out entity))
            {
                if (entity != null && entity is VendingMachine)
                {
                    vendingMachineId = (entity as VendingMachine).net.ID.Value;
                    if (!_storedData.InfinityMachines.Contains(vendingMachineId)) {
                        _storedData.InfinityMachines.Add(vendingMachineId);
                        SendReply(player, "Vending Machine is set to <color=green>infinity</color>");
                    } else {
                        _storedData.InfinityMachines.Remove(vendingMachineId);
                        SendReply(player, "Vending Machine is set to <color=red>normal vending</color>");
                    }
                    SaveData();
                }
                else
                {
                    SendReply(player, "You are not looking at a vending machine!");
                }
            }
            else
            {
                SendReply(player, "You are not looking at a vending machine!");
            }
        }
        #endregion

        // raycast to find entity being looked at
        bool GetRaycastTarget(BasePlayer player, out object closestEntity)
        {
            closestEntity = null;
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
                return false;
            closestEntity = hit.GetEntity();
            return true;
        }
        private bool HasPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, permInfinityVendor);
        }
    }
}
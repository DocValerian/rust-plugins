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
using Network;
using Oxide.Core.Plugins;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("UsrTools", "DocValerian", "1.3.1")]
    internal class UsrTools : RustPlugin
    {
        private static UsrTools Plugin;

        [PluginReference]
        private Plugin TPapi, ServerRewards, LootBoxSpawner;
        
        #region data

        private const string permUse = "usrtools.use";
        private const string permGetStuffBase = "usrtools.getmystuff.base";
        private const string permGetStuff = "usrtools.getmystuff";
        private const string permGetStuffFree = "usrtools.getmystuff.free";

        private bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        private void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permGetStuff, this);
            permission.RegisterPermission(permGetStuffBase, this);
            permission.RegisterPermission(permGetStuffFree, this);
        }
        private int cacheMinutes = 2;
        private Dictionary<ulong, PlayerStuff> stuffCache = new Dictionary<ulong, PlayerStuff>();

        class PlayerStuff
        {
            public PlayerCorpse[] clist { get; set; }
            public DroppedItemContainer[] blist { get; set; }

            public DateTime lastChecked { get; set; }

            public PlayerStuff(BasePlayer p) : base() {
                clist = UnityEngine.Object.FindObjectsOfType<PlayerCorpse>();
                blist = UnityEngine.Object.FindObjectsOfType<DroppedItemContainer>();
                lastChecked = DateTime.Now;
            }

        }
        #endregion

        #region hooks

        private void OnItemResearch(ResearchTable table, Item targetItem, BasePlayer player)
        {
            table.researchDuration = 0f;
            table.researchStartEffect = new GameObjectRef();
        }

        private object OnExperimentStart(Workbench workbench, BasePlayer player)
        {
            workbench.experimentStartEffect = new GameObjectRef();
            Item slot = workbench.inventory.GetSlot(0);
            if (slot != null)
            {
                if (!slot.MoveToContainer(player.inventory.containerMain, -1, true))
                    slot.Drop(workbench.GetDropPosition(), workbench.GetDropVelocity(), new Quaternion());
                player.inventory.loot.SendImmediate();
            }
            if (workbench.experimentStartEffect.isValid)
                Effect.server.Run(workbench.experimentStartEffect.resourcePath, (BaseEntity)workbench, 0U, Vector3.zero, Vector3.zero, (Connection)null, false);
            workbench.SetFlag(BaseEntity.Flags.On, true, false, true);
            workbench.inventory.SetLocked(true);
            workbench.CancelInvoke(new Action(workbench.ExperimentComplete));
            workbench.Invoke(new Action(workbench.ExperimentComplete), 0.5f);
            workbench.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            return true;
        }

        //instant compost
        /*
        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            if (item == null || inventory == null)
                return null;
            BasePlayer player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            ItemContainer container = inventory.FindContainer(targetContainer);
            ItemContainer originalContainer = item.GetRootContainer();
            if (container == null || originalContainer == null)
                return null;
            Composter comp = container.entityOwner as Composter;
            if (comp == null) return null;
            comp.CompostEntireStack = true;
            comp.UpdateComposting();
            return null;
        }
        */
        #endregion

        #region commands
        [ChatCommand("getmystuff")]
        private void CommandGetMyStuff(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permGetStuffBase))
            {
                SendReply(player, "Please unlock the new '/getmystuff Base' Skill in /p (for 0 SP!)");
                return;
            }

            PlayerStuff ps = getStuffList(player);
            if (ps == null) return;
            int currDistance = 0;
            int currFound = 0;
            int price = (HasPermission(player.UserIDString, permGetStuffFree)) ? 0 : 666;
            string msg = "<color=orange>========= Get My Stuff <size=10>v."+Plugin.Version+" by "+Plugin.Author+"</size> =========</color>";

            if (args.Length == 0)
            {
                msg += "\n<color=#aaaaaa>Results are cached! ( " + Math.Ceiling((DateTime.Now - ps.lastChecked).TotalSeconds)+" of "+ (cacheMinutes*60) + "s )</color>";
                if (ps.clist.Length > 0)
                {
                    foreach (PlayerCorpse itm in ps.clist)
                    {
                        if (itm.playerSteamID != player.userID || !itm.IsValid()) continue;
                        float q = Quaternion.LookRotation((itm.transform.position - player.eyes.position).normalized).eulerAngles.y;
                        currDistance = (int)Math.Ceiling((itm.transform.position - player.transform.position).magnitude);
                        msg += "\n<color=orange>Corpse</color>\tfound in <color=green>" + currDistance + "m</color>\t to the <color=green>" + GetDirectionAngle(q)+"</color>";
                        currFound++;
                    }
                }
                if(ps.blist.Length > 0)
                {
                    foreach (DroppedItemContainer itm in ps.blist)
                    {
                        if (itm.playerSteamID != player.userID || !itm.IsValid()) continue;
                        float q = Quaternion.LookRotation((itm.transform.position - player.eyes.position).normalized).eulerAngles.y;
                        currDistance = (int)Math.Ceiling((itm.transform.position - player.transform.position).magnitude);
                        msg += "\n<color=orange>Lootbag</color>\tfound in <color=green>" + currDistance + "m</color>\t to the <color=green>" + GetDirectionAngle(q) + "</color>";
                        currFound++;
                    }
                }
                if(currFound <= 0)
                {
                    msg += "\n\nNo leftover loot found! (Could have decayed)";
                }

                if (HasPermission(player.UserIDString, permGetStuff))
                {
                    msg += "\n\n- <color=green>/getmystuff buy</color> - to teleport all your corpse loot to you.";
                    msg += "\n\nThis will cost you <color=orange>" + price + " RP</color>\n(no matter IF and how many bags are found!)";
                }
                else
                {
                    msg += "\n\n- You can unlock a Skill in <color=green>/p</color> to teleport all your loot to you.";
                }
                SendReply(player, msg);
                return;
            }

            // buy command
            if (!HasPermission(player.UserIDString, permGetStuff))
            {
                SendReply(player, "Please unlock the '/getmystuff Buy' Skill in /p");
                return;
            }

            object bal = ServerRewards?.Call("CheckPoints", player.userID);
            if (bal == null) return;
            int playerRP = (int)bal;
            if (playerRP < price)
            {
                SendReply(player, "It costs " + price + " RP to use this command, you only have " + playerRP);
                return;
            }
            else
            {
                ServerRewards?.Call("TakePoints", player.userID, price);
            }

            SendReply(player, "You paid " + price + " RP ... searching for your stuff");
            Puts("DEBUG: " + player + " called /getmystuff buy from " + player.transform.position);
            int found = 0;
            int itemCnt = 0;
            int itemTotalCnt = 0;
            bool belowTerrain = false;
            DroppedItemContainer box = null;

            if (ps.clist.Length > 0)
            {
                foreach (PlayerCorpse c in ps.clist)
                {
                    if (c.playerSteamID != player.userID || !c.IsValid()) continue;

                    Puts("DEBUG: " + player + " found a corpse at " + c.transform.position);
                  
                    if (c.containers == null) continue;
                   
                    found++;
                    foreach (ItemContainer itemContainer in c.containers)
                    {
                        itemCnt += itemContainer.itemList.Count;
                    }
                    if (itemCnt <= 0)
                    {
                        Puts("DEBUG: " + player + " corpse has no containers! ");
                        continue;
                    }

                    box = LootBoxSpawner.Call("createStuffContainer", player, c.containers) as DroppedItemContainer;
                    // reset counters for next crate
                    Puts("DEBUG: " + player + " corpse added, itemCnt: " + itemCnt);
                    itemTotalCnt += itemCnt;
                    itemCnt = 0;
                    c.Kill();
                }
            }
            if (ps.blist.Length > 0)
            {
                foreach (DroppedItemContainer b in ps.blist)
                {
                    if (b.playerSteamID != player.userID || !b.IsValid()) continue;
                    Puts("DEBUG: " + player + " found a lootbag at " + b.transform.position);

                    if (b.inventory == null) continue;
                    found++;
                    itemCnt += b.inventory.itemList.Count;
                    if (itemCnt <= 0)
                    {
                        Puts("DEBUG: " + player + " lootbag has no containers! ");
                        continue;
                    }

                    box = LootBoxSpawner.Call("createStuffContainer", player, new ItemContainer[] { b.inventory }) as DroppedItemContainer;
                    // reset counters for next crate
                    Puts("DEBUG: " + player + " bag added, itemCnt: " + itemCnt);
                    itemTotalCnt += itemCnt;
                    itemCnt = 0;
                    b.Kill();
                }
            }
            if (found > 0)
            {
                Puts("DEBUG: " + player + " corpses/bags " + found + " with " + itemTotalCnt +" itemCnt total.");
                SendReply(player, found + " corpses/bags with " + itemTotalCnt + " items found & teleported to you!");
            }
            else
            {
                Puts("DEBUG: " + player + " had no itemCnt to be found and ported");
                SendReply(player, "No bag(s) found!");
            }
        }
        [ChatCommand("getout")]
        private void CommandGetOut(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            BuildingPrivlidge privilege = player.GetBuildingPrivilege();
            if (!privilege || !privilege.IsAuthed(player))
            {
                SendReply(player, "You must have TC auth.");
                return;
            }
            object entity;
            if (GetRaycastTarget(player, out entity))
            {
                if (entity != null && entity is BasePlayer && !(((BasePlayer)entity).IsNpc))
                {
                    BasePlayer target = (BasePlayer)entity;
                    
                    BuildingManager.Building building = privilege.GetBuilding();
                    if (!target.IsSleeping() && building.buildingBlocks.Count < 30)
                    {
                        SendReply(player, "This command can only be used on sleepers or in bigger buildings (>30 blocks)");
                        return;
                    }
                    if (!privilege.IsAuthed(target) && !target.IsAdmin)
                    {
                        Vector3 originalPosition = target.transform.position;
                        Vector3 where = TPapi.Call<Vector3>("GetPositionAround", originalPosition, 500f, 100f);
                        if (player.isMounted)
                        {
                            BaseMountable mt = player.GetMounted();
                            mt.DismountPlayer(player, true);
                            if (mt is RidableHorse) mt.Kill();
                        }
                            

                        TPapi?.Call("TeleportPlayerTo", target, where);
                        Puts("[GETOUT] " + player + " at " + originalPosition + " threw out " + target);
                    }
                    else
                    {
                        SendReply(player, "This player has a right to be here! (TC auth)");
                    }
                }
                else if(entity != null && entity is RidableHorse)
                {
                    RidableHorse target = (RidableHorse)entity;

                    BuildingManager.Building building = privilege.GetBuilding();
                    if (building.buildingBlocks.Count < 30)
                    {
                        SendReply(player, "This command can only be used on sleepers or in bigger buildings (>30 blocks)");
                        return;
                    }
                    if (!privilege.IsAuthed(target.OwnerID))
                    {
                        target.Kill(); 
                        Vector3 originalPosition = target.transform.position;
                        Puts("[GETOUT] " + player + " at " + originalPosition + " killed a horse " + target);
                    }
                    else
                    {
                        SendReply(player, "This horse has a right to be here! (Owner TC auth)");
                    }
                }

                else if (entity != null && entity is ScrapTransportHelicopter)
                {
                    ScrapTransportHelicopter target = (ScrapTransportHelicopter)entity;

                    BuildingManager.Building building = privilege.GetBuilding();
                    if (building.buildingBlocks.Count < 30)
                    {
                        SendReply(player, "This command can only be used on sleepers or in bigger buildings (>30 blocks)");
                        return;
                    }
                    if (!privilege.IsAuthed(target.OwnerID))
                    {
                        target.Kill(BaseNetworkable.DestroyMode.Gib);
                        Vector3 originalPosition = target.transform.position;
                        Puts("[GETOUT] " + player + " at " + originalPosition + " killed a ScrapTransportHelicopter " + target);
                    }
                    else
                    {
                        SendReply(player, "This ScrapTransportHelicopter has a right to be here! (Owner TC auth)");
                    }
                }

                else if (entity != null && entity is MiniCopter)
                {
                    MiniCopter target = (MiniCopter)entity;

                    BuildingManager.Building building = privilege.GetBuilding();
                    if (building.buildingBlocks.Count < 30)
                    {
                        SendReply(player, "This command can only be used on sleepers or in bigger buildings (>30 blocks)");
                        return;
                    }
                    if (!privilege.IsAuthed(target.OwnerID))
                    {
                        target.Kill(BaseNetworkable.DestroyMode.Gib);
                        Vector3 originalPosition = target.transform.position;
                        Puts("[GETOUT] " + player + " at " + originalPosition + " killed a MiniCopter " + target);
                    }
                    else
                    {
                        SendReply(player, "This MiniCopter has a right to be here! (Owner TC auth)");
                    }
                }
                else
                {
                    SendReply(player, "You are not looking at a player or horse!");
                }
            }
            else
            {
                SendReply(player, "You are not looking at a player!");
            }
        }
        #endregion

        #region functions
        string GetDirectionAngle(float angle)
        {
            if (angle > 337.5 || angle < 22.5)
                return "North";
            else if (angle > 22.5 && angle < 67.5)
                return "North-East";
            else if (angle > 67.5 && angle < 112.5)
                return "East";
            else if (angle > 112.5 && angle < 157.5)
                return "South-East";
            else if (angle > 157.5 && angle < 202.5)
                return "South";
            else if (angle > 202.5 && angle < 247.5)
                return "South-West";
            else if (angle > 247.5 && angle < 292.5)
                return "West";
            else if (angle > 292.5 && angle < 337.5)
                return "North-West";
            return "";
        }

        // raycast to find entity being looked at
        private bool GetRaycastTarget(BasePlayer player, out object closestEntity)
        {
            closestEntity = null;
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
                return false;
            closestEntity = hit.GetEntity();
            return true;
        }

        private PlayerStuff getStuffList(BasePlayer player)
        {
            if (!stuffCache.ContainsKey(player.userID) || (DateTime.Now - stuffCache[player.userID].lastChecked).TotalSeconds > cacheMinutes*60)
            {
                Puts("DEBUG: new stuff list");
                stuffCache[player.userID] = new PlayerStuff(player);
            }
            return stuffCache[player.userID];
        }

        public Vector3 getVector3(string rString)
        {
            string[] temp = rString.Substring(1, rString.Length - 2).Split(',');
            float x = float.Parse(temp[0]);
            float y = float.Parse(temp[1]);
            float z = float.Parse(temp[2]);
            Vector3 rValue = new Vector3(x, y, z);
            return rValue;
        }

        private BasePlayer FindPlayer(ulong userID)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            if (player == null)
            {
                player = BasePlayer.FindSleeping(userID);
            }
            return player;
        }
        #endregion
    }
}
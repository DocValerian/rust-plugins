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

using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System;
using Network;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;
using Mono.Unix.Native;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("AdmTools", "DocValerian", "1.0.35")]
    class AdmTools : RustPlugin
    {
        static AdmTools Plugin;

        [PluginReference]
        private Plugin TPapi, PlaytimeTracker, Kits, ZNui, ImageLibrary;

        private const string permUse = "admtools.use";
        private const string permChaos = "admtools.chaosmode";
        private BaseProjectile opGun;

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        private StoredData storedData;
        private class StoredData
        {
            public List<ulong> IndestructibleTCs = new List<ulong>();
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("AdmTools", storedData);
        }
        void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("AdmTools");
            SaveData();
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permChaos, this);
        }

        void Unload()
        {
        }
        #region hooks
        private void OnMapMarkerAdded(BasePlayer player, ProtoBuf.MapNote note)
        {
            if (player.IsAdmin)
            {
                float y = 45f;
                if (player.IsFlying) y = Mathf.Max(y, player.transform.position.y);
                player.Teleport(new Vector3(note.worldPosition.x, y, note.worldPosition.z));
            }
        }
       
        //Vending machine fix - make sure to remove after Rustside fix
        void OnEntityKill(BaseNetworkable entity)
        {

            if (entity is VendingMachine)
            {
                StorageContainer loot = entity as StorageContainer;
                if (loot != null)
                {
                    if (loot?.inventory == null) return;
                    DropUtil.DropItems(loot?.inventory, loot.transform.position);
                }
            }
        }
        void OnPlayerRespawned(BasePlayer player)
        {

            if (!HasPermission(player.UserIDString, permChaos)) return;
            player.inventory.containerBelt.Clear();
            Kits?.Call("TryGiveKit", player, "pvp_defender");
            player.health = player.MaxHealth();
        }
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!HasPermission(player.UserIDString, permChaos)) return;
            ResetSpawnTargets(player);
        }

        private void ResetSpawnTargets(BasePlayer player)
        {
            SleepingBag[] bags = SleepingBag.FindForPlayer(player.userID, true);
            foreach (SleepingBag bag in bags)
                bag.unlockTime = 0f;
        }
        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {

            if (!HasPermission(player.UserIDString, permChaos)) return;
            var weapon = player.GetActiveItem().GetHeldEntity() as BaseProjectile;
            if (weapon == null) return;
            player.GetActiveItem().condition = player.GetActiveItem().info.condition.max;
            if (weapon.primaryMagazine.contents > 0) return;
            if (weapon.ShortPrefabName == "mgl.entity" && !player.IsAdmin) return;
            weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
            weapon.ScaleRepeatDelay(0f);
            player.weaponDrawnDuration = 0f;


            weapon.SendNetworkUpdateImmediate();
        }
    
        object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (!player.IsAdmin) return null;
            return true;
        }
        object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.FlyHack)
            {
                return false;
            }
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return null;
            var priv = entity.GetBuildingPrivilege();
            if (priv == null || !(priv is BuildingPrivlidge) || entity is BasePlayer || entity.IsNpc) return null;

            if (storedData.IndestructibleTCs.Contains(priv.net.ID))
            {
                string m = ZNui?.Call<string>("CheckServerMode");
                if (m == "PVE") return null;
                BasePlayer player = info?.Initiator as BasePlayer;
                if (player != null)
                {
                    SendReply(player, "<color=green>TOWN cannot be damaged!</color>");
                }
                info.damageTypes.ScaleAll(0.0f);
                return true;
            }

            return null;
        }
        #endregion
        #region commands

       [ChatCommand("atc")]
        void CommandAtC(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            int i = 0;
            foreach (var ply in BasePlayer.sleepingPlayerList.ToList())
            {
                if (BasePlayer.activePlayerList.Contains(ply)) continue;
                if (!ply.IsDestroyed)
                {
                    Item paper = ItemManager.CreateByName("paper", 666);
                    paper.name = "Trade in Town for XP";
                    paper.MoveToContainer(ply.inventory.containerMain);
                    i++;
                }
            }

            SendReply(player, "You added 666 papers to " + i + " sleepers!");


        }

        [ChatCommand("atcu")]
        void CommandAtCundo(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }

            int i = 0;
            foreach (var ply in BasePlayer.sleepingPlayerList.ToList())
            {
                if (BasePlayer.activePlayerList.Contains(ply)) continue;
                if (!ply.IsDestroyed)
                {
                    ply.inventory.containerMain.Take(null, -1779183908, 666);

                    i++;
                }
            }

            SendReply(player, "You took 666 papers to " + i + " sleepers!");


        }

        [ChatCommand("attown")]
        private void CommandAdminTown(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            BuildingPrivlidge privilege = player.GetBuildingPrivilege();
            if (!privilege) return;

            if (!storedData.IndestructibleTCs.Contains(privilege.net.ID))
            {
                storedData.IndestructibleTCs.Add(privilege.net.ID);
                SendReply(player, "Building made Indestructible.");
            }
            SaveData();
        }
        [ChatCommand("botsay")]
        void BotSay(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            string msg = "<color=green>ZN-Bot</color> " + string.Join(" ", args);
            Server.Broadcast(msg);
        }
        [ChatCommand("atx")]
        void AdminX(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            object entity;
            if (GetRaycastTarget(player, out entity))
            {
                Puts("DEBUG looking at " + entity);
                if (entity != null && entity is VendingMachine)
                {
                    ((VendingMachine)entity).OwnerID = 0;
                }

                if (entity != null && entity is DecayEntity)
                {
                    ((DecayEntity)entity).OwnerID = 0;
                    SendReply(player, "Set Owner to 0");
                }

            }
        }

        [ChatCommand("athell")]
        void AdminHell(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            Vector3 spawnPoint = player.transform.position;

            timer.Repeat(1f, 20, () =>
            {
                for (int i = 0; i < 30; i++)
                {
                    float minDistance = 10f;
                    float randX = Random.Range(-minDistance, minDistance);
                    float randZ = Random.Range(-minDistance, minDistance);

                    float randY = Random.Range(1f, 20f);
                    Vector3 spawnPoint2 = spawnPoint + new Vector3(randX, randY, randZ);

                    string cratePrefab = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
                    var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint2);
                    if (entity == null) { return; }

                    entity.Spawn();

                }
            });

        }


        [ChatCommand("atgrpwipe")]
        void AdminGroups(BasePlayer player, string command, string[] args)
        {
            if (player.userID != 76561197998677819 && player.userID != 76561198079798636) return;

            List<string> GetGroups = permission.GetGroups().ToList();
            string[] Users;
            string[] validGroups = { "serversage", "servervet" };
            foreach (string group in GetGroups)
            {
                if (!validGroups.Contains(group)) continue;
                Users = permission.GetUsersInGroup(group);
                Puts("Group " + group + " Members: " + Users.Count());

                string name = string.Join(", ", Users);
                string[] uids = Regex.Split(name, @"\D+");
                name = string.Join(", ", uids);
                foreach (string uid in uids)
                {
                    permission.RemoveUserGroup(uid, group);
                    Puts("REMOVED P: " + uid + " from " + group);
                }
            }
        }

        [ChatCommand("atdebug")]
        void AdminDebug(BasePlayer player, string command, string[] args)
        {
            Vector3 playerPos = player.transform.position;
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(playerPos);
            SendReply(player, "DEBUG: your y: " + playerPos.y + " terrain: " + terrainHeight);
        }

        [ChatCommand("attime")]
        void AdminPlayerTime(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            object entity;
            if (GetRaycastTarget(player, out entity))
            {
                if (entity != null && entity is BasePlayer)
                {
                    BasePlayer target = (BasePlayer)entity;

                    object time = PlaytimeTracker?.Call("GetPlayTime", target.userID.ToString());
                    Puts("DEBUG: Playtime of " + target + " is " + time);
                }
                else
                {
                    SendReply(player, "You are not looking at a player!");
                }

            }
            else
            {
                SendReply(player, "You are not looking at a player!");
            }
        }


        [ChatCommand("attp")]
        void AdminPlayerTP(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            object entity;
            if (GetRaycastTarget(player, out entity))
            {
                if (entity != null && entity is BasePlayer)
                {
                    BasePlayer target = (BasePlayer)entity;
                    Vector3 originalPosition = target.transform.position;
                    Vector3 where = originalPosition + new Vector3(0, 350, 0);

                    if (args.Length == 1)
                    {
                        where = TPapi.Call<Vector3>("GetPositionAround", originalPosition, 500f, 100f);
                    }

                    TPapi?.Call("TeleportPlayerTo", target, where);
                    Puts("Player/Sleeper " + target + " moved to: " + target.transform.position + " by " + player);
                }
                else
                {
                    SendReply(player, "You are not looking at a player!");
                }

            }
            else
            {
                SendReply(player, "You are not looking at a player!");
            }
        }

        [ChatCommand("attc")]
        void TCAuthCheck(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            BuildingPrivlidge privilege = player.GetBuildingPrivilege();
            if (!privilege)
            {
                SendReply(player, "ERROR: You must be within a TC range!");
                return;
            };
            string msg = "<color=orange>=============== TC authed Players ================</color>";

            foreach (var entry in privilege.authorizedPlayers)
            {
                msg += "\n" + entry.userid + " - " + entry.username;
            }
            SendReply(player, msg);
        }
        int tcMasks = LayerMask.GetMask("Deployed");
        [ChatCommand("atpriv")]
        void TCListCheck(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            string msg = "<color=orange>========= Building Privs within 500m =========</color>";
            Vector3 position = player.transform.position;
            float range = 500f;
            if (args[0] != null)
            {
                double num;
                Double.TryParse(args[0], out num);
                range = (float)num;
            }
            float distance;
            List<BuildingPrivlidge> list = new List<BuildingPrivlidge>();
            Vis.Entities<BuildingPrivlidge>(position, range, list, tcMasks);
            foreach (BuildingPrivlidge entity in list)
            {
                distance = Vector3Ex.Distance2D(entity.transform.position, position);
                msg += "\nPos: " + entity.transform.position + "\nOwner: " + FindPlayer(entity.OwnerID) + "\ndistance " + Math.Ceiling(distance);
                msg += "\n----------";
            }
            SendReply(player, msg);
        }


        [ChatCommand("atop")]
        void AdminOPmod(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse)) return;
            var weapon = player.GetActiveItem().GetHeldEntity() as BaseProjectile;
            if (weapon == null)
            {
                if (opGun == null) return;
                opGun.primaryMagazine.contents = 0;
                opGun.Kill();
                opGun.SendNetworkUpdateImmediate(true);
            }
            else
            {
                Puts("atop run on " + weapon);
                weapon.recoil.shotsUntilMax = 2000;
                weapon.attackFX = new GameObjectRef();
                weapon.primaryMagazine.capacity = 1000;
                weapon.primaryMagazine.contents = 1000;

                weapon.SendNetworkUpdateImmediate(true);
                opGun = weapon;
                Puts("detais " + weapon.GetRecoil());
                return;
            }
        }
        #endregion
        #region functions
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


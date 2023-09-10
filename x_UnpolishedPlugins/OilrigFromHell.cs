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
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System;
using Network;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using UnityEngine.SceneManagement;

namespace Oxide.Plugins
{
    [Info("OilrigFromHell", "DocValerian", "1.4.3")]
    class OilrigFromHell : RustPlugin
    {
        static OilrigFromHell Plugin;

        [PluginReference]
        Plugin ServerRewards, TPapi;
        private const string permUse = "oilrigfromhell.use";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        private DateTime lastOilrigSpawnTime = DateTime.Now.AddMinutes(-90);
        private int spawnDelaySeconds = 120;
        private int spawnDelaySecondsPlayer = 1800;
        private int spawnPrice = 500;
        private int scientistHealth = 350;
        private Dictionary<ulong, DateTime> playerSpawns = new Dictionary<ulong, DateTime>();
        private Dictionary<ulong, ScientistNPC> currentZombieList = new Dictionary<ulong, ScientistNPC>();
        private int med_item_ID;

        private Vector3 rigPos;

        void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);
        }

        void OnServerInitialized()
        {
            FindLargeRig();
            DoorRemover();
            med_item_ID = ItemManager.itemDictionaryByName["syringe.medical"].itemid;
        }
        void Unload()
        {
            ClearAllOilScientists();
        }

        private void DoorRemover()
        {
            foreach (var GateToRemove in GameObject.FindObjectsOfType<Door>().Where(x => x.name.Contains("door.hinged.industrial")))
            {
                if (Vector3Ex.Distance2D(rigPos, GateToRemove.transform.position) > 50f) continue;
                Puts("DEBUG: Remove Oilrig door " + GateToRemove + " at " + GateToRemove.transform.position + " distance " + Vector3Ex.Distance2D(rigPos, GateToRemove.transform.position));
                GateToRemove.AdminKill();
            }
        }
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            // only apply to looting under the earth
            if (entity.OwnerID == player.userID || Vector3Ex.Distance2D(rigPos, entity.transform.position) > 50) return;

            // Under terrain NPC Loot -> 50% chance to drop items
            if (entity is DroppedItemContainer)
            {
                // don't work on player containers (crude hack)
                if (((DroppedItemContainer)entity).playerSteamID > 100000000000) return;

                ItemContainer inv = ((DroppedItemContainer)entity).inventory;
                if (inv.GetAmount(med_item_ID, false) == 0) return;
                var itm = inv.FindItemByItemName("syringe.medical");
                if (itm == null) return;
                inv.Remove(itm);

            }

           
        }
        private object OnTrapTrigger(BaseTrap trap, GameObject obj)
        {
            if (!(trap is BearTrap) && !(trap is Landmine))
                return null;
            var target = obj.GetComponent<BasePlayer>();
            if (target != null && target.IsNpc)
            {
                    return false;
            }
            else if(target != null && !target.IsNpc && trap.OwnerID == 0)
            {
                BasePlayer.FindByID(target.userID)?.metabolism.radiation_poison.Add(50f);
                BasePlayer.FindByID(target.userID)?.metabolism.bleeding.Add(25f);
            }
            return null;
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity is ScientistNPC && currentZombieList.ContainsKey(((BasePlayer)entity).userID))
            {
                ulong uid = ((BasePlayer)entity).userID;
                currentZombieList.Remove(uid);
                BaseEntity mine = GameManager.server.CreateEntity("assets/prefabs/deployable/landmine/landmine.prefab", entity.transform.position, new Quaternion(), true);
                mine.Spawn();
            } else if(entity is ScientistNPC && Vector3Ex.Distance2D(entity.transform.position, rigPos) < 50)
            {
                BaseEntity mine = GameManager.server.CreateEntity("assets/prefabs/deployable/landmine/landmine.prefab", entity.transform.position, new Quaternion(), true);
                mine.Spawn();
            }
        }

        void FindLargeRig()
        {
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if(monument.name == "OilrigAI2")
                {
                    rigPos = monument.transform.position;
                }
            }
        }
        private void ClearAllOilScientists()
        {
            foreach (KeyValuePair<ulong, ScientistNPC> npc in currentZombieList)
            {
                if (npc.Value != null && !npc.Value.IsDestroyed)
                {
                    npc.Value.Kill();
                }
            }
            currentZombieList.Clear();
        }
        private static ScientistNPC InstantiateEntity(Vector3 position)
        {
            var prefabName = "assets/rust.ai/agents/npcplayer/ScientistNPC/scientist/scientistnpc_oilrig.prefab";
            var prefab = GameManager.server.FindPrefab(prefabName);
            var go = Facepunch.Instantiate.GameObject(prefab, position, default(Quaternion));

            go.name = prefabName;
            SceneManager.MoveGameObjectToScene(go, Rust.Server.EntityScene);

            if (go.GetComponent<Spawnable>())
            {
                UnityEngine.Object.Destroy(go.GetComponent<Spawnable>());
            }

            if (!go.activeSelf)
            {
                go.SetActive(true);
            }
            return go.GetComponent<ScientistNPC>();
        }
        private void spawnRandoms(Vector3 basePos){
            
            //ScientistNPC entity = InstantiateEntity(GetNewPosition(basePos));

            var prefabName = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab";
            var prefab = GameManager.server.FindPrefab(prefabName);
            Puts("DEBUG: Prefab: " + prefab);
            Vector3 pos = GetNewPosition(basePos);
            if (pos.y < 25f) return;
            ScientistNPC entity = (ScientistNPC)GameManager.server.CreateEntity(prefabName, pos, default(Quaternion));
            if (entity == null) {return;}
            entity.enableSaving = false;
            entity.displayName = "Oilrig Guard Zombie";
            entity.startHealth = scientistHealth;
            entity.InitializeHealth(scientistHealth, scientistHealth);
            entity.SetMaxHealth(scientistHealth);
            entity.SetHealth(scientistHealth);
            entity.Spawn();

            currentZombieList.Add(entity.userID, entity);
            /*
            Rigidbody rigidbody = entity.gameObject.GetComponent<Rigidbody>();
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.mass = 2f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            entity.SendNetworkUpdateImmediate();
            */
        }

        private Vector3 GetNewPosition(Vector3 basePos, float range = 5f){
            Vector3 pos = TPapi.Call<Vector3>("GetPositionAround", basePos, range, 1f);
            if (pos.y > 25f) return pos;

            for (int i = 0; i < 3; i++)
            {
                if(pos.y < 25f)
                {
                    pos = TPapi.Call<Vector3>("GetPositionAround", basePos, range, 1f);
                }
                else
                {
                    break;
                }
            }
            return pos;

        }
        private void showUsageMessage(BasePlayer player)
        {
            string msg = "<color=orange>===BETA=== Oil Rig From Hell (v"+Plugin.Version+") ===BETA===</color>";
            msg += "\nSpawn a hackable crate on top of lage oil rig and start the event";
            msg += "\n\n<color=green>RULES:</color>";
            msg += "\n- Crate spawns in front of you. You must be in the right spot!";
            msg += "\n--- Close to the central tower/helipad, top level.";
            msg += "\n- Start the hack to summon a group of heavy scientists";
            msg += "\n- Multiple active spawns possible";
            msg += "\n- Only one spawn every " + spawnDelaySeconds + "s";
            msg += "\n- <color=red>Price: "+spawnPrice+" RP</color>";
            msg += "\n- <color=red>!!! Event & Loot is FREE FOR ALL !!!</color>";
            msg += "\n\n<color=green>Usage:</color>";
            msg += "\n/oilrig \t\t\tShow this info";
            msg += "\n/oilrig spawn \t\tSpawn the crate";

            SendReply(player, msg);
        }

        private void spawnRigCrate(BasePlayer player)
        {
            Vector3 spawnPoint = player.transform.position;
            if (Vector3Ex.Distance2D(spawnPoint, rigPos) < 20 && spawnPoint.y > 35)
            {
                // timer
                int secondsLeft = 0;
                if (playerSpawns.ContainsKey(player.userID) && (DateTime.Now - playerSpawns[player.userID]).TotalSeconds < spawnDelaySecondsPlayer)
                {
                    secondsLeft = (int)(spawnDelaySecondsPlayer - (DateTime.Now - playerSpawns[player.userID]).TotalSeconds);
                    SendReply(player, "You must wait another " + secondsLeft + "s to spawn another crate - individual spawn cooldown");
                    return;
                }
                if ((DateTime.Now - lastOilrigSpawnTime).TotalSeconds < spawnDelaySeconds)
                {
                    secondsLeft = (int)(spawnDelaySeconds - (DateTime.Now - lastOilrigSpawnTime).TotalSeconds);
                    SendReply(player, "You must wait another " + secondsLeft + "s to spawn another crate - global spawn cooldown");
                    return;
                }

                // money
                object bal = ServerRewards?.Call("CheckPoints", player.userID);
                if(bal == null) return;
                int playerRP = (int)bal;
                if(playerRP < spawnPrice)
                {
                    SendReply(player, "It costs " + spawnPrice + " RP to buy a locked crate, you only have " + playerRP);
                    return;
                }
                else
                {
                    ServerRewards?.Call("TakePoints", player.userID, spawnPrice);
                    SendReply(player, "You paid " + spawnPrice + " RP a locked crate.\n<color=red>NO REFUND</color> if it is not in the right place for the event!");
                }

                // spawn action
                string cratePrefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
                Vector3 playerDirection = player.eyes.HeadForward().normalized;
                float spawnDistance = 6f;
                spawnPoint = player.transform.position + playerDirection*spawnDistance;
                spawnPoint.y += 20;
                var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint);
                    if (entity == null) {return;}
                entity.Spawn();
                
                Puts(player + " spawned an oilrig crate at " + spawnPoint);
                lastOilrigSpawnTime = DateTime.Now;
                if (playerSpawns.ContainsKey(player.userID))
                {
                    playerSpawns[player.userID] = DateTime.Now;
                }
                else
                {
                    playerSpawns.Add(player.userID, DateTime.Now);
                }
                timer.Repeat(3f, 12, () =>
                {
                    spawnRandoms(spawnPoint);
                    spawnRandoms(spawnPoint);
                    spawnRandoms(spawnPoint);
                });
                
                foreach (var p in BasePlayer.activePlayerList) {
                    SendReply(p, "<color=#63ff64>" + player.displayName +" </color> used <color=orange>/oilrig</color> and spawned a crate at large oil rig. There will be Scientists!");
                }
            }
            else
            {
                SendReply(player, "You are too far from the crate area at top of Large Oil Rig!\nGet closer to the central tower.");
            }
        }
        
        [ChatCommand("oilrig")]
		void CmdOilrig(BasePlayer player, string command, string[] args)
		{
            if(!HasPermission(player.UserIDString, permUse)) return;

            if (args.Length == 0)
            {
                showUsageMessage(player);
                return;
            }
            if (args.Length == 1)
            {
                switch(args[0])
                {
                    case "info":
                        showUsageMessage(player);
                    break;
                    case "spawn":
                        FindLargeRig();
                        spawnRigCrate(player);
                    break;
                    default:
                        showUsageMessage(player);
                    break;
                }
            }



        }

    }
}   
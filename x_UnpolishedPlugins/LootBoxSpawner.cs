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
using UnityEngine;
using System.Linq;
using System;
using Network;
using Random = System.Random;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("LootBoxSpawner", "DocValerian", "1.3.26")]
    class LootBoxSpawner : RustPlugin
    {
        static LootBoxSpawner Plugin;
        [PluginReference]
        private Plugin ZNui;
        private const string ResizableLootPanelName = "generic_resizable";
        #region Config
        static ConfigFile Cfg = new ConfigFile();
        class ConfigFile
        {
            public string[] lootTypes = { "c4", "ressources", "ammo", "weapons", "special" };
            public Dictionary<string, Dictionary<string, int>> LootAmounts = new Dictionary<string, Dictionary<string, int>>
            {
                ["c4"] = new Dictionary<string, int>() {
                    ["normal"] = 1,
                    ["hardcore"] = 1,
                    ["ultra"] = 1,
                    ["hell"] = 1
                },
                ["ressources"] = new Dictionary<string, int>()
                {
                    ["normal"] = 2,
                    ["hardcore"] = 3,
                    ["ultra"] = 3,
                    ["hell"] = 4
                },
                ["ammo"] = new Dictionary<string, int>()
                {
                    ["normal"] = 2,
                    ["hardcore"] = 2,
                    ["ultra"] = 3,
                    ["hell"] = 3
                },
                ["weapons"] = new Dictionary<string, int>()
                {
                    ["normal"] = 1,
                    ["hardcore"] = 2,
                    ["ultra"] = 3,
                    ["hell"] = 4
                },
                ["special"] = new Dictionary<string, int>()
                {
                    ["normal"] = 1,
                    ["hardcore"] = 1,
                    ["ultra"] = 1,
                    ["hell"] = 1
                }
            };
            public Dictionary<string, Dictionary<string, float>> LootGroups = new Dictionary<string, Dictionary<string, float>>
            {
                ["c4"] = new Dictionary<string, float>
                {
                    ["explosive.timed"] = 0.45f
                },
                ["ressources"] = new Dictionary<string, float>
                {
                    ["techparts"] = 4f,
                    ["gunpowder"] = 300f,
                    ["scrap"] = 100f,
                    ["metal.refined"] = 150f,
                    ["explosives"] = 7f,
                    ["charcoal"] = 500f,
                    ["metal.fragments"] = 600f
                },
                ["ammo"] = new Dictionary<string, float>
                {
                    ["ammo.rifle.explosive"] = 50f,
                    ["ammo.rifle"] = 200f,
                    ["ammo.rocket.basic"] = 1.5f,
                    ["ammo.rocket.fire"] = 3f,
                    ["ammo.rocket.hv"] = 4f,
                    ["ammo.grenadelauncher.he"] = 1f
                },
                ["weapons"] = new Dictionary<string, float>
                {
                    ["lmg.m249"] = 0.05f,
                    ["multiplegrenadelauncher"] = 0.03f,
                    ["rifle.ak"] = 0.08f,
                    ["rifle.lr300"] = 0.05f,
                    ["rifle.l96"] = 0.05f,
                    ["rocket.launcher"] = 0.05f
                },
                ["special"] = new Dictionary<string, float>
                {
                    ["autoturret"] = 0.1f,
                    ["supply.signal"] = 0.1f,
                    ["keycard"] = 0.05f
                }
            };
        }
        string[] crateNames = {
            "crate_basic",
            "crate_mine",
            "crate_normal",
            "crate_tools",
            "foodbox",
            "loot_barrel_1",
            "loot_barrel_2",
            "minecart",
            "crate_elite",
            "crate_underwater_basic",
            "crate_underwater_advanced",
            "crate_normal_2",
            "crate_normal_2_food",
            "crate_normal_2_medical",
            "bradley_crate",
            "heli_crate",
            "supply_drop",
            "trash-pile-1",
            "codelockedhackablecrate",
            "codelockedhackablecrate_oilrig",
            "vehicle_parts",
        };

        Dictionary<string, int> SupplyLoot = new Dictionary<string, int>()
        {
            ["ammo.rifle.explosive"] = 300,
            ["ammo.rifle"] = 300,
            ["metal.refined"] = 200,
            ["sulfur"] = 5000,
            ["lowgradefuel"] = 350,
            ["crude.oil"] = 350,
            ["largemedkit"] = 6,
            ["pookie.bear"] = 2,
        };
        Dictionary<string, int> SupplyComps = new Dictionary<string, int>()
        {
            ["gears"] = 6,
            ["roadsigns"] = 6,
            ["metalpipe"] = 6,
            ["rope"] = 6,
            ["sheetmetal"] = 10,
            ["sewingkit"] = 6,
            ["tarp"] = 6,
            ["techparts"] = 10,
            ["riflebody"] = 4,
            ["metalspring"] = 6
        };

        // Zhunt loot boxes
        Dictionary<string, int> ZhuntLootAmounts = new Dictionary<string, int>()
        {
            ["l1"] = 1,
            ["l2"] = 50,
            ["l3"] = 100,
            ["l4"] = 200,
        };
        Dictionary<string, int> ZhuntLoot = new Dictionary<string, int>()
        {
            ["metalpipe"] = 25,
            ["rope"] = 25,
            ["sheetmetal"] = 25,
            ["sewingkit"] = 25,
            ["tarp"] = 25,
            ["gears"] = 25,
            ["smgbody"] = 15,
            ["metalspring"] = 15,
            ["crude.oil"] = 3000,
            ["scrap"] = 1500,
            ["ammo.pistol"] = 300,
            ["riflebody"] = 5,
            ["largemedkit"] = 15,
            ["roadsigns"] = 25,
            ["metalblade"] = 25,
            ["semibody"] = 10,
            ["ammo.rifle.explosive"] = 200,
            ["techparts"] = 25,
            ["explosives"] = 10,
            ["targeting.computer"] = 7,
            ["cctv.camera"] = 7,
            ["electric.generator.small"] = 1,
            ["researchpaper"] = 1500,
            ["metal.refined"] = 400,
        };
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file...");
            Config.WriteObject(Cfg, true);
        }


        #endregion

        private Dictionary<ulong, ulong> globalLootAssignments = new Dictionary<ulong, ulong>();

        class StoredData
        {
            public Dictionary<ulong, List<Dictionary<string, int>>> LootClaims = new Dictionary<ulong, List<Dictionary<string, int>>>();
        }
        StoredData storedData;

        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("LootBoxSpawner");
        }

        void Unload()
        {
            SaveData();
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("LootBoxSpawner", storedData);
        }
        private void OnNewSave()
        {
            storedData = new StoredData();
            SaveData();
        }

        #region UmodHooks
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || !(entity is CargoPlane)) return;
            entity.Kill();
        }
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal)) return;
            createSupplyDrop(player);
            entity.Kill();
        }
        void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal)) return;
            createSupplyDrop(player);
            entity.Kill();
        }

        // loot protection
        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            //deactivate at pvp day
            
            string prefabname = container.ShortPrefabName ?? string.Empty;
            if (prefabname.Contains("bradley_crate") || prefabname.Contains("supply_drop"))
            {
                if (globalLootAssignments.ContainsKey(container.net.ID))
                {
                    string m = ZNui?.Call<string>("CheckServerMode");
                    if (m == "PVP" || m == "CHAOS") return null;

                    if (globalLootAssignments[container.net.ID] == player.userID || player.IsAdmin)
                    {
                        return null;
                    }
                    else
                    {
                        SendReply(player, "Only the owner can loot this crate!");
                        return false;
                    }
                }
            }
            return null;
        }
        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (globalLootAssignments.ContainsKey(entity.net.ID))
            {
                // clear protection if owner has looted the entity
                globalLootAssignments.Remove(entity.net.ID);
                //SendReply(player, "You stopped looting, crate is no longer protected!");
                StorageContainer loot = entity as StorageContainer;
                if (loot != null)
                {
                    if (loot?.inventory == null) return;
                    DropUtil.DropItems(loot?.inventory, loot.transform.position);
                }
            }
            else if (crateNames.Contains(entity.ShortPrefabName))
            {
                StorageContainer loot = entity as StorageContainer;
                if (loot != null)
                {
                    if (loot?.inventory == null) return;
                    DropUtil.DropItems(loot?.inventory, loot.transform.position);
                }
            }
        }

        #endregion

        #region Commands
        [ChatCommand("loot")]
        void CommandLoot(BasePlayer player, string command, string[] args)
        {
            claimAllLoot(player);
        }

        [ChatCommand("atloot")]
        void AdminDebug(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            storeZhuntClaim(player.userID, 0, 201, 5600);
        }
        [ChatCommand("atinvo")]
        void AdminInvo(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            string msg = "ITEMS";
            foreach (Item i in player.inventory.containerMain.itemList)
            {
                msg += " - " + i.info.shortname;
            }
            Puts(msg);
        }




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

        #endregion

        #region HelperFunctions
        public IEnumerable<TKey> UniqueRandomKeys<TKey, TValue>(IDictionary<TKey, TValue> dict)
        {
            Random rand = new Random((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
            Dictionary<TKey, TValue> values = new Dictionary<TKey, TValue>(dict);
            while (values.Count > 0)
            {
                TKey randomKey = values.Keys.ElementAt(rand.Next(0, values.Count));  // hat tip @yshuditelu 
                values.Remove(randomKey);
                yield return randomKey;
            }
        }
        private List<KeyValuePair<string,int>> generateLoot(string level, int minValue, int maxValue, int boxId)
        {
            List<KeyValuePair<string, int>> itmList = new List<KeyValuePair<string, int>>();
            Random rnd = new Random((int)Math.Floor((float)DateTimeOffset.Now.ToUnixTimeMilliseconds() / (float)boxId));
            int currentAmountMultiplyer;
            int LootAmount;
            int totalLoot;

            foreach (string type in Cfg.lootTypes)
            {
                LootAmount = Cfg.LootAmounts[type][level];
                Dictionary<string, float> LootTable = Cfg.LootGroups[type];

                foreach (string prefab in UniqueRandomKeys(LootTable).Take(LootAmount))
                {
                    currentAmountMultiplyer = rnd.Next(minValue, maxValue + 1);
                    totalLoot = (int)Math.Ceiling(LootTable[prefab] * currentAmountMultiplyer);
                    if (totalLoot < 1) continue;

                    if (prefab == "keycard")
                    {
                        itmList.Add(new KeyValuePair<string, int>(prefab + "_green", totalLoot));
                        //itmList.Add(new KeyValuePair<string, int>(prefab + "_blue", totalLoot));
                        //itmList.Add(new KeyValuePair<string, int>(prefab + "_red", totalLoot));
                    }
                    else
                    {
                        if (type == "weapons")
                        {
                            for (int i = 0; i < totalLoot; i++)
                            {
                                itmList.Add(new KeyValuePair<string, int>(prefab, 1));
                            }
                        }
                        else
                        {
                            itmList.Add(new KeyValuePair<string, int>(prefab, totalLoot));
                        }
                    }
                }
            }
            return itmList;
        }

        private List<Item> generateSupplyLoot(int compsAmount, int lootAmount)
        {
            List<Item> itmList = new List<Item>();
            Item item;

            foreach (string prefab in UniqueRandomKeys(SupplyComps).Take(compsAmount))
            {
                item = ItemManager.CreateByName(prefab, SupplyComps[prefab]);
                itmList.Add(item);
            }

            foreach (string prefab in UniqueRandomKeys(SupplyLoot).Take(lootAmount))
            {
                item = ItemManager.CreateByName(prefab, SupplyLoot[prefab]);
                itmList.Add(item);
            }

            return itmList;
        }

        private List<Item> generateZhuntLoot(int kills, int blood)
        {
            List<Item> itmList = new List<Item>();
            Item item;
            if (blood > 0)
            {
                item = ItemManager.CreateByName("blood", blood);
                itmList.Add(item);
            }
            if (kills <= 0) return itmList;

            int itemAmount = 0;
            int minKills = 50;
            if (kills >= ZhuntLootAmounts["l1"]) itemAmount++;
            if (kills >= ZhuntLootAmounts["l2"]) itemAmount++;
            if (kills >= ZhuntLootAmounts["l3"]) itemAmount++;
            if (kills >= ZhuntLootAmounts["l4"]) itemAmount++;
            float multi = 1.0f;
            if (kills < minKills) multi = kills / minKills;

            foreach (string prefab in UniqueRandomKeys(ZhuntLoot).Take(itemAmount))
            {
                int amt = (int) Math.Floor(multi * ZhuntLoot[prefab]);
                if(amt >= 1)
                {
                    item = ItemManager.CreateByName(prefab, ZhuntLoot[prefab]);
                    itmList.Add(item);
                }
            }

            return itmList;
        }

        private int levelToInt(string level)
        {
            switch (level)
            {
                case "normal":
                    return 1;
                case "hardcore":
                    return 2;
                case "ultra":
                    return 3;
                case "helifromhell":
                    return 4;
                case "bradleyfromhell":
                    return 4;
                default:
                    return 1;
            }
        }
        private string levelToString(int level)
        {
            switch (level)
            {
                case 1:
                    return "normal";
                case 2:
                    return "hardcore";
                case 3:
                    return "ultra";
                case 4:
                    return "hell";
                default:
                    return "normal";
            }
        }
        #endregion

        #region  API

        private DroppedItemContainer createStuffContainer(BasePlayer player, ItemContainer[] containers)
        {
            Vector3 spawnPoint = player.transform.position;
            spawnPoint.y += 1;
            BaseEntity baseEntity = GameManager.server.CreateEntity(StringPool.Get(1519640547), spawnPoint, Quaternion.identity);
            if (baseEntity == null)
            {
                Puts("ERROR: couldn't create a bag!");
                return null;
            }
            DroppedItemContainer droppedItemContainer = baseEntity as DroppedItemContainer;
            droppedItemContainer.maxItemCount = 42;
            droppedItemContainer.lootPanelName = "generic_resizable";

            droppedItemContainer.playerName = player.displayName;
            droppedItemContainer.playerSteamID = player.userID;
            if (droppedItemContainer != null)
            {
                droppedItemContainer.TakeFrom(containers);
            }
            droppedItemContainer.Spawn();
            Rigidbody rigidbody = baseEntity.gameObject.GetComponent<Rigidbody>();
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.mass = 2f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            return droppedItemContainer;
        }
        //deprecated
        private StorageContainer createStuffCrate(BasePlayer player)
        {
            string cratePrefab = "assets/prefabs/npc/m2bradley/bradley_crate.prefab";
            Vector3 spawnPoint = player.transform.position;
            spawnPoint.y += 1;

            var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint);
            if (entity == null) { return null; }

            entity.Spawn();
            StorageContainer loot = entity.GetComponentInParent<StorageContainer>();
            if (loot != null)
            {
                loot.inventorySlots = 42;
                loot.inventory.capacity = 42;
                loot.panelName = ResizableLootPanelName;
                loot.inventory.Clear();

            }

            Rigidbody rigidbody = entity.gameObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.mass = 2f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            globalLootAssignments.Add(entity.net.ID, player.userID);
            return loot;
        }

        private void createRewardCrate(BasePlayer player, string level = "normal", int minValue = 1, int maxValue = 10, int boxId = 0)
        {
            string cratePrefab = "assets/prefabs/npc/m2bradley/bradley_crate.prefab";
            Vector3 spawnPoint = player.transform.position;
            spawnPoint.y += 1;
            var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint);
            if (entity == null) { return; }

            entity.Spawn();
            StorageContainer loot = entity.GetComponentInParent<StorageContainer>();
            if (loot != null)
            {
                loot.inventorySlots = 30;
                loot.inventory.capacity = 30;
                loot.panelName = ResizableLootPanelName;
                loot.inventory.Clear();

                List<KeyValuePair<string, int>> ilist = generateLoot(level, minValue, maxValue, boxId);
                foreach (KeyValuePair<string,int> itm in ilist)
                {
                    Item item = ItemManager.CreateByName(itm.Key, itm.Value);
                    loot.inventory.Insert(item);
                }
            }
            Rigidbody rigidbody = entity.gameObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.mass = 2f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            globalLootAssignments.Add(entity.net.ID, player.userID);

        }
        private void createSupplyDrop(BasePlayer player)
        {
            string cratePrefab = "assets/prefabs/misc/supply drop/supply_drop.prefab";
            Vector3 spawnPoint = player.transform.position;

            spawnPoint.x += UnityEngine.Random.Range(-3, 3);
            spawnPoint.z += UnityEngine.Random.Range(-3, 3);
            spawnPoint.y += 20;

            var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint);
            if (entity == null) { return; }

            entity.Spawn();
            StorageContainer loot = entity.GetComponentInParent<StorageContainer>();
            if (loot != null)
            {
                loot.inventorySlots = 30;
                loot.inventory.capacity = 30;
                loot.panelName = ResizableLootPanelName;
                List<Item> ilist = generateSupplyLoot(2, 3);
                foreach (Item itm in ilist)
                {
                    loot.inventory.Insert(itm);
                }
            }

            globalLootAssignments.Add(entity.net.ID, player.userID);


        }

        private void createZhuntReward(BasePlayer player, int kills, int blood)
        {
            timer.Once(0.5f, () =>
            {
                string cratePrefab = "assets/prefabs/npc/m2bradley/bradley_crate.prefab";
                Vector3 spawnPoint = player.transform.position;
                spawnPoint.y += 1;

                var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint);
                if (entity == null) { return; }

                entity.Spawn();
                StorageContainer loot = entity.GetComponentInParent<StorageContainer>();
                if (loot != null)
                {
                    loot.inventorySlots = 30;
                    loot.inventory.capacity = 30;
                    loot.panelName = ResizableLootPanelName;
                    loot.inventory.Clear();
                    List<Item> ilist = generateZhuntLoot(kills, blood);
                    foreach (Item itm in ilist)
                    {
                        loot.inventory.Insert(itm);
                    }
                }
                Rigidbody rigidbody = entity.gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.mass = 2f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                globalLootAssignments.Add(entity.net.ID, player.userID);

            });

        }
        private void createChallengeReward(BasePlayer player, int blood)
        {
            timer.Once(0.5f, () =>
            {
                string cratePrefab = "assets/prefabs/npc/m2bradley/bradley_crate.prefab";
                Vector3 spawnPoint = player.transform.position;
                spawnPoint.y += 1;

                var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint);
                if (entity == null) { return; }

                entity.Spawn();
                StorageContainer loot = entity.GetComponentInParent<StorageContainer>();
                if (loot != null)
                {
                    loot.inventorySlots = 30;
                    loot.inventory.capacity = 30;
                    loot.panelName = ResizableLootPanelName;
                    loot.inventory.Clear();
                    if (blood > 0)
                    {
                        Item item = ItemManager.CreateByName("blood", blood);
                        loot.inventory.Insert(item);
                    }
                    
                }
                Rigidbody rigidbody = entity.gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rigidbody.mass = 2f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                globalLootAssignments.Add(entity.net.ID, player.userID);

            });

        }

        private void storeZhuntClaim(ulong playerID, int tier, int multiplier, int blood)
        {
//Puts("DEBUG: " + playerID + " ... " + multiplier + " ... " + blood);
            Dictionary<string, int> claim = new Dictionary<string, int>()
            {
                ["zhunt"] = 1,
                ["tier"] = tier,
                ["multiplier"] = multiplier,
                ["blood"] = blood,
                ["added"] = (int)DateTimeOffset.Now.ToUnixTimeSeconds()
            };
            if (!storedData.LootClaims.ContainsKey(playerID))
            {
                storedData.LootClaims.Add(playerID, new List<Dictionary<string, int>>());
            }
            storedData.LootClaims[playerID].Add(claim);
            SaveData();
        }
        private void storeRewardClaim(ulong playerID, string level, int minValue, int maxValue, string type)
        {
            Dictionary<string, int> claim = new Dictionary<string, int>()
            {
                [type] = 1,
                ["level"] = levelToInt(level),
                ["minValue"] = minValue,
                ["maxValue"] = maxValue,
                ["added"] = (int)DateTimeOffset.Now.ToUnixTimeSeconds()
            };
            if (!storedData.LootClaims.ContainsKey(playerID))
            {
                storedData.LootClaims.Add(playerID, new List<Dictionary<string, int>>());
            }
            storedData.LootClaims[playerID].Add(claim);
            SaveData();
        }
        private void storeChallengeClaim(ulong playerID, int blood)
        {
            Dictionary<string, int> claim = new Dictionary<string, int>()
            {
                ["znquest"] = 1,
                ["blood"] = blood,
                ["added"] = (int)DateTimeOffset.Now.ToUnixTimeSeconds()
            };
            if (!storedData.LootClaims.ContainsKey(playerID))
            {
                storedData.LootClaims.Add(playerID, new List<Dictionary<string, int>>());
            }
            storedData.LootClaims[playerID].Add(claim);
            SaveData();
        }

        private void claimAllLoot(BasePlayer player)
        {
            if (!storedData.LootClaims.ContainsKey(player.userID))
            {
                SendReply(player, "You have nothing left to claim!");
                return;
            }
            string msg = "";
            int i = 0;
            foreach (Dictionary<string, int> claim in storedData.LootClaims[player.userID])
            {
                i += 1;
                msg += $"[{DateTime.Now.ToString("hh:mm:ss")}] " + player;
                if (claim.ContainsKey("zhunt"))
                {
                    msg += " claiming /zhunt loot t_" + claim["tier"] + " m_" + claim["multiplier"] + " b_" + claim["blood"];
                    createZhuntReward(player, claim["multiplier"], claim["blood"]);
                }
                if (claim.ContainsKey("zombie"))
                {
                    msg += " claiming /zombie loot lvl_" + claim["level"] + " min_" + claim["minValue"] + " max_" + claim["maxValue"];
                    createRewardCrate(player, levelToString(claim["level"]), claim["minValue"], claim["maxValue"], i);
                }
                if (claim.ContainsKey("heli"))
                {
                    msg += " claiming /heli loot lvl_" + claim["level"] + " min_" + claim["minValue"] + " max_" + claim["maxValue"];
                    createRewardCrate(player, levelToString(claim["level"]), claim["minValue"], claim["maxValue"], i);
                }
                if (claim.ContainsKey("brad"))
                {
                    msg +=" claiming /brad loot lvl_" + claim["level"] + " min_" + claim["minValue"] + " max_" + claim["maxValue"];
                    createRewardCrate(player, levelToString(claim["level"]), claim["minValue"], claim["maxValue"], i);
                }
                if (claim.ContainsKey("znquest"))
                {
                    msg += " claiming /cq loot b_" + claim["blood"];
                    createChallengeReward(player, claim["blood"]);
                }
                msg += " earned at: " + (claim.ContainsKey("added") ? ""+claim["added"] : "n/a");
                msg += " location: " + player.transform.position + "("+i+"/"+storedData.LootClaims[player.userID].Count()+")\n";
                
            }
            LogToFile($"LootClaims", msg, this);

            storedData.LootClaims.Remove(player.userID);
            SaveData();

        }
        #endregion

    }
}
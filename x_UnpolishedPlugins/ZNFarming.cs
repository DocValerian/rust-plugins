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
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = Oxide.Core.Random;

namespace Oxide.Plugins
{
    [Info("ZNFarming", "DocValerian", "1.2.9")]
    internal class ZNFarming : RustPlugin
    {
        private static ZNFarming Plugin;

        [PluginReference]
        private Plugin ZNExperience;

        private const string permUse = "znfarming.use";
        private const string permPickup = "znfarming.blood.pickup";

        private bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region Config

        private static Configuration Cfg = new Configuration();

        public class Configuration
        {
            [JsonProperty(PropertyName = "Mining Boosts per permission x OfNormal")]
            public Dictionary<string, FarmingBoost> MiningBoosts = new Dictionary<string, FarmingBoost>()
            {
                ["znfarming.mining.lv0"] =  new FarmingBoost { multi = 1.0f,  lvl = 0,  spCost = 0, prestige = false },
                ["znfarming.mining.lv1"] =  new FarmingBoost { multi = 1.5f,  lvl = 1,  spCost = 2, prestige = false },
                ["znfarming.mining.lv2"] =  new FarmingBoost { multi = 2.0f,  lvl = 2,  spCost = 3, prestige = false },
                ["znfarming.mining.lv3"] =  new FarmingBoost { multi = 2.5f,  lvl = 3,  spCost = 4, prestige = false },
                ["znfarming.mining.lv4"] =  new FarmingBoost { multi = 3.0f,  lvl = 4,  spCost = 5, prestige = false },
                ["znfarming.mining.lv5"] =  new FarmingBoost { multi = 4.0f,  lvl = 5,  spCost = 3, prestige = true  },
                ["znfarming.mining.lv6"] =  new FarmingBoost { multi = 5.0f,  lvl = 6,  spCost = 3, prestige = true  },
                ["znfarming.mining.lv7"] =  new FarmingBoost { multi = 6.0f, lvl = 7,  spCost = 3, prestige = true  },
                ["znfarming.mining.lv8"] =  new FarmingBoost { multi = 7.0f, lvl = 8,  spCost = 3, prestige = true  },
                ["znfarming.mining.lv9"] =  new FarmingBoost { multi = 8.0f, lvl = 9,  spCost = 3, prestige = true  },
                ["znfarming.mining.lv10"] = new FarmingBoost { multi = 9.0f, lvl = 10, spCost = 3, prestige = true  },
            };

            [JsonProperty(PropertyName = "Gathering Boosts per permission x OfNormal")]
            public Dictionary<string, FarmingBoost> gatherBoosts = new Dictionary<string, FarmingBoost>()
            {
                ["znfarming.gather.lv0"] =  new FarmingBoost { multi = 1.0f, lvl = 0,  spCost = 0, prestige = false },
                ["znfarming.gather.lv1"] =  new FarmingBoost { multi = 1.5f, lvl = 1,  spCost = 2, prestige = false },
                ["znfarming.gather.lv2"] =  new FarmingBoost { multi = 2.0f, lvl = 2,  spCost = 3, prestige = false },
                ["znfarming.gather.lv3"] =  new FarmingBoost { multi = 2.5f, lvl = 3,  spCost = 4, prestige = false },
                ["znfarming.gather.lv4"] =  new FarmingBoost { multi = 3.0f, lvl = 4,  spCost = 5, prestige = false },
                ["znfarming.gather.lv5"] =  new FarmingBoost { multi = 4.0f, lvl = 5,  spCost = 3, prestige = true  },
                ["znfarming.gather.lv6"] =  new FarmingBoost { multi = 5.0f, lvl = 6,  spCost = 3, prestige = true  },
                ["znfarming.gather.lv7"] =  new FarmingBoost { multi = 6.0f, lvl = 7,  spCost = 3, prestige = true  },
                ["znfarming.gather.lv8"] =  new FarmingBoost { multi = 7.0f, lvl = 8,  spCost = 3, prestige = true  },
                ["znfarming.gather.lv9"] =  new FarmingBoost { multi = 8.0f, lvl = 9,  spCost = 3, prestige = true  },
                ["znfarming.gather.lv10"] = new FarmingBoost { multi = 9.0f, lvl = 10, spCost = 3, prestige = true  },
            };

            [JsonProperty(PropertyName = "Woodcutting Boosts per permission x OfNormal")]
            public Dictionary<string, FarmingBoost> woodBoosts = new Dictionary<string, FarmingBoost>()
            {
                ["znfarming.wood.lv0"] =  new FarmingBoost { multi = 1.0f, lvl = 0,  spCost = 0, prestige = false },
                ["znfarming.wood.lv1"] =  new FarmingBoost { multi = 1.5f, lvl = 1,  spCost = 2, prestige = false },
                ["znfarming.wood.lv2"] =  new FarmingBoost { multi = 2.0f, lvl = 2,  spCost = 3, prestige = false },
                ["znfarming.wood.lv3"] =  new FarmingBoost { multi = 2.5f, lvl = 3,  spCost = 4, prestige = false },
                ["znfarming.wood.lv4"] =  new FarmingBoost { multi = 3.0f, lvl = 4,  spCost = 5, prestige = false },
                ["znfarming.wood.lv5"] =  new FarmingBoost { multi = 4.0f, lvl = 5,  spCost = 3, prestige = true  },
                ["znfarming.wood.lv6"] =  new FarmingBoost { multi = 5.0f, lvl = 6,  spCost = 3, prestige = true  },
                ["znfarming.wood.lv7"] =  new FarmingBoost { multi = 6.0f, lvl = 7,  spCost = 3, prestige = true  },
                ["znfarming.wood.lv8"] =  new FarmingBoost { multi = 7.0f, lvl = 8,  spCost = 3, prestige = true  },
                ["znfarming.wood.lv9"] =  new FarmingBoost { multi = 8.0f, lvl = 9,  spCost = 3, prestige = true  },
                ["znfarming.wood.lv10"] = new FarmingBoost { multi = 9.0f, lvl = 10, spCost = 3, prestige = true },
            };

            [JsonProperty(PropertyName = "Skinning Boosts per permission x OfNormal")]
            public Dictionary<string, FarmingBoost> skinBoosts = new Dictionary<string, FarmingBoost>()
            {
                ["znfarming.skin.lv0"] = new FarmingBoost { multi = 3.0f,  lvl = 0,  spCost = 0, prestige = false },
                ["znfarming.skin.lv1"] = new FarmingBoost { multi = 4.0f,  lvl = 1,  spCost = 2, prestige = false },
                ["znfarming.skin.lv2"] = new FarmingBoost { multi = 5.0f,  lvl = 2,  spCost = 3, prestige = false },
                ["znfarming.skin.lv3"] = new FarmingBoost { multi = 6.0f,  lvl = 3,  spCost = 4, prestige = false },
                ["znfarming.skin.lv4"] = new FarmingBoost { multi = 7.0f,  lvl = 4,  spCost = 5, prestige = false },
                ["znfarming.skin.lv5"] = new FarmingBoost { multi = 8.0f,  lvl = 5,  spCost = 3, prestige = true  },
                ["znfarming.skin.lv6"] = new FarmingBoost { multi = 9.0f,  lvl = 6,  spCost = 3, prestige = true  },
                ["znfarming.skin.lv7"] = new FarmingBoost { multi = 10.0f, lvl = 7,  spCost = 3, prestige = true  },
                ["znfarming.skin.lv8"] = new FarmingBoost { multi = 11.0f, lvl = 8,  spCost = 3, prestige = true  },
                ["znfarming.skin.lv9"] = new FarmingBoost { multi = 12.0f, lvl = 9,  spCost = 3, prestige = true  },
                ["znfarming.skin.lv10"] =new FarmingBoost { multi = 13.0f, lvl = 10, spCost = 3, prestige = true  },
            };
            [JsonProperty(PropertyName = "Gatherables to be considered")]
            public HashSet<string> plantList = new HashSet<string>() { "potato", "cloth", "red.berry", "blue.berry", "green.berry", "yellow.berry", "white.berry", "black.berry", "mushroom" };

            [JsonProperty(PropertyName = "Lower XP for rapid growing river plant types")]
            public HashSet<string> riverPlantList = new HashSet<string>() { "pumpkin", "corn" };
            public int NodeXP = 90;
            public int TreeXP = 60;
            public int ResPileXP = 75;
            public int OpenPlantXP = 45;
            public int RiverPlantXP = 22;
            public int FarmPlantXP = 2;
            public int FishXP = 80;

            public int NodeRP = 60;
            public int TreeRP = 20;
            public int ResPileRP = 30;
            public int OpenPlantRP = 20;
            public int RiverPlantRP = 3;
            public int FarmPlantRP = 1;
            public int FarmPlantExtraMult = 1;
            public int FishRP = 30;
        }

        protected override void LoadDefaultConfig()
        {
            Cfg = new Configuration();
            Puts("Loaded default configuration file");
        }

        public class FarmingBoost
        {
            [JsonProperty(PropertyName = "Boosts x times Normal")]
            public float multi { get; set; }
            [JsonProperty(PropertyName = "Level Number")]
            public int lvl { get; set; }

            [JsonProperty(PropertyName = "Price in SP")]
            public int spCost { get; set; }
            [JsonProperty(PropertyName = "Needs Prestige")]
            public bool prestige { get; set; }
        }
        public class PlayerBoost
        {
            public float mining { get; set; }
            public float wood { get; set; }
            public float skin { get; set; }
            public float gather { get; set; }
            public bool autoPickup { get; set; }

            public PlayerBoost(BasePlayer player) : base()
            {
                gather = 1.0f;
                skin = 1.0f;
                wood = 1.0f;
                mining = 1.0f;
                autoPickup = false;
                if (player == null || player.UserIDString == null) return;
                if (Plugin.HasPermission(player.UserIDString, permPickup))
                {
                    autoPickup = true;
                }

                foreach (KeyValuePair<string, FarmingBoost> b in Cfg.gatherBoosts)
                {
                    if (Plugin.HasPermission(player.UserIDString, b.Key) && b.Value.multi > gather) gather = b.Value.multi;
                }

                foreach (KeyValuePair<string, FarmingBoost> b in Cfg.skinBoosts)
                {
                    if (Plugin.HasPermission(player.UserIDString, b.Key) && b.Value.multi > skin) skin = b.Value.multi;
                }

                foreach (KeyValuePair<string, FarmingBoost> b in Cfg.woodBoosts)
                {
                    if (Plugin.HasPermission(player.UserIDString, b.Key) && b.Value.multi > wood) wood = b.Value.multi;
                }

                foreach (KeyValuePair<string, FarmingBoost> b in Cfg.MiningBoosts)
                {
                    if (Plugin.HasPermission(player.UserIDString, b.Key) && b.Value.multi > mining) mining = b.Value.multi;
                }

                //Plugin.Puts("DEBUG: set up boosts for " + player + " m: " + mining + " w: " + wood + " s: " + skin + " g: " + gather);
            }
        }

        protected override void SaveConfig() => Config.WriteObject(Cfg);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                Cfg = Config.ReadObject<Configuration>();
            }
            catch (JsonException ex)
            {
                Puts(ex.Message);
                PrintError("Your configuration file contains a json error, shown above. Please fix this.");
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
        #endregion Config
        #region GlobalVars
        private Dictionary<ulong, PlayerBoost> playerCache = new Dictionary<ulong, PlayerBoost>();
        #endregion
        #region hooks

        private void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permPickup, this);
            foreach (string p in Cfg.MiningBoosts.Keys)
            {
                permission.RegisterPermission(p, this);
            }
            foreach (string p in Cfg.gatherBoosts.Keys)
            {
                permission.RegisterPermission(p, this);
            }
            foreach (string p in Cfg.woodBoosts.Keys)
            {
                permission.RegisterPermission(p, this);
            }
            foreach (string p in Cfg.skinBoosts.Keys)
            {
                permission.RegisterPermission(p, this);
            }
        }

        private void OnServerInitialized()
        {
            initSkills();
            initPlayerCache();
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity as BasePlayer;
            if (player == null) return;

            if (dispenser.name.Contains("driftwood") || dispenser.name.Contains("dead_log"))
            { 
                bool autoPickup = canAutoPickup(player);
                int bloodAmt = (item.amount >= 20) ? (int)Math.Ceiling(item.amount / 20f) : 1;
                int xpAmt = (item.amount >= 10) ? (int)Math.Ceiling(item.amount / 10f) : 1;
                Item blood = ItemManager.CreateByName("blood", bloodAmt);
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((dispenser.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
                ZNExperience.Call("AddXp", player.userID, xpAmt);
                dispenser.fractionRemaining = 0f;

                //Puts("DEBUG: remaining " + dispenser.fractionRemaining + " blood: "+ bloodAmt + " XP: " + xpAmt);
            }

            float multiplier = getPermMulti(player, dispenser.gatherType);
            item.amount = Mathf.CeilToInt((float)(item.amount * multiplier));
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {

            OnDispenserGather(dispenser, entity, item);

            var player = entity as BasePlayer;
            if (player == null) return;
            bool autoPickup = canAutoPickup(player);
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                ZNExperience.Call("AddXp", player.userID, Cfg.TreeXP);
                Item blood = ItemManager.CreateByName("blood", Cfg.TreeRP);
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((dispenser.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
            }
            else
            {
                if (item.info.shortname == "hq.metal.ore") return;
                ZNExperience.Call("AddXp", player.userID, Cfg.NodeXP);
                Item blood = ItemManager.CreateByName("blood", Cfg.NodeRP);
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((dispenser.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
            }
        }

        private void CollectionFn(BasePlayer player, CollectibleEntity collectible, ItemAmount itemAmount)
        {

            string itemName = itemAmount.itemDef.shortname;
            if (itemName.Contains("seed")) return;
            //Puts("DEBUG: Picking up " + item);
            float multiplier = getGatherMulti(player);
            bool autoPickup = canAutoPickup(player);
            itemAmount.amount = Mathf.CeilToInt((float)(itemAmount.amount * multiplier));
            //special diesel boost
            if (itemName == "diesel_barrel")
            {
                itemAmount.amount *= 5;
            }
            if (Cfg.plantList.Contains(itemName))
            {
                ZNExperience.Call("AddXp", player.userID, Cfg.OpenPlantXP);
                Item blood = ItemManager.CreateByName("blood", Cfg.OpenPlantRP);
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((collectible.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
            }
            else if (Cfg.riverPlantList.Contains(itemName))
            {
                ZNExperience.Call("AddXp", player.userID, Cfg.RiverPlantXP);
                Item blood = ItemManager.CreateByName("blood", Cfg.RiverPlantRP);
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((collectible.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
            }
            else
            {
                ZNExperience.Call("AddXp", player.userID, Cfg.ResPileXP);
                Item blood = ItemManager.CreateByName("blood", Cfg.ResPileRP);
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((collectible.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
            }
        }

        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null) return null;
            Item item = collectible.GetItem();
            for (int i = 0; i < collectible.itemList.Length; i++)
            {
                ItemAmount itemAmount = collectible.itemList[i];
                CollectionFn(player, collectible, itemAmount);
            }
           
            return null;
        }
        void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            float multiplier = getGatherMulti(player);
            bool autoPickup = canAutoPickup(player);
            item.amount = Mathf.CeilToInt((float)(item.amount * multiplier * Cfg.FarmPlantExtraMult));
            if (Cfg.plantList.Contains(item.info.shortname) || Cfg.riverPlantList.Contains(item.info.shortname))
            {
                ZNExperience.Call("AddXp", player.userID, (Cfg.FarmPlantXP * Cfg.FarmPlantExtraMult));
                Item blood = ItemManager.CreateByName("blood", (Cfg.FarmPlantRP * Cfg.FarmPlantExtraMult));
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((plant.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
            }
        }
        object CanTakeCutting(BasePlayer player, GrowableEntity entity)
        {
            bool autoPickup = canAutoPickup(player);
            if (player.CanBuild())
            {
                ZNExperience.Call("AddXp", player.userID, Cfg.FarmPlantXP);
                Item blood = ItemManager.CreateByName("blood", Cfg.FarmPlantRP);
                if (autoPickup)
                {
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else
                {
                    blood.Drop((entity.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                }
            }
            return null;
        }
        // Special fish gutting stuff
        string[] fishAnimalParts = 
            {
            "bone.fragments",
            "fish.raw",
            "fat.animal"
            };
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!item.info.shortname.Contains("fish")) return null;
            //Puts("OnItemAction works!" + item.info + " .. " + item.parent + " action " + action);
            if(action == "Gut")
            {
                //Puts("DEBUG: Gutting "+item.amount+" fish " + item.info.itemMods.Length);
                if (!(playerCache.ContainsKey(player.userID)))
                {
                    updatePlayerCache(player);
                }
                float multiplier = playerCache[player.userID].skin / 2;

                foreach (ItemMod m in item.info.itemMods)
                {
                    if (m.GetType() != typeof(ItemModSwap)) continue;
                    ItemModSwap s = m as ItemModSwap;
                    if (item.amount < 1)
                    {
                        break;
                    }
                    ItemAmount[] array = s.becomeItem;
                    foreach (ItemAmount itemAmount in array)
                    {
                        //Puts("DEBUG: give " + itemAmount.itemDef.shortname);
                        int thisAmount = (fishAnimalParts.Contains(itemAmount.itemDef.shortname)) ? (int)Math.Ceiling(itemAmount.amount * multiplier) : (int)itemAmount.amount;
                        Item item2 = ItemManager.Create(itemAmount.itemDef, thisAmount, 0uL);
                        if (item2 != null)
                        {

                            if (!item2.MoveToContainer(item.parent))
                            {
                                player.GiveItem(item2);
                            }
                        }
                    }
                    if (s.RandomOptions.Length != 0)
                    {
                        int num = Random.Range(0, s.RandomOptions.Length);
                        //Puts("DEBUG: give Random " + s.RandomOptions[num].itemDef.shortname);
                        int thisAmount = (fishAnimalParts.Contains(s.RandomOptions[num].itemDef.shortname)) ? (int)Math.Ceiling(s.RandomOptions[num].amount * multiplier) : (int)s.RandomOptions[num].amount;

                        Item item3 = ItemManager.Create(s.RandomOptions[num].itemDef, thisAmount, 0uL);
                        if (item3 != null)
                        {
                            if (!item3.MoveToContainer(item.parent))
                            {
                                player.GiveItem(item3);
                            }
                        }
                    }
                    if (s.sendPlayerDropNotification)
                    {
                        player.Command("note.inv", item.info.itemid, -1);
                    }
                    if (s.actionEffect.isValid)
                    {
                        Effect.server.Run(s.actionEffect.resourcePath, player.transform.position, Vector3.up);
                    }
                    item.UseItem();



                }
                //item.Drop(Vector3.zero, Vector3.zero);
                return false;
            }
            return null;
        }

        Item OnFishCatch(Item fish, BaseFishingRod fishingRod, BasePlayer player)
        {
            if (!(playerCache.ContainsKey(player.userID)))
            {
                updatePlayerCache(player);
            }
            //float multiplier = playerCache[player.userID].skin;
            //fish.amount = (int)Math.Ceiling(fish.amount * multiplier);
            //Puts("DEBUG: " + player + " got a " + fish + " value " + fish.info.shortname);
            bool autoPickup = canAutoPickup(player);
            ZNExperience.Call("AddXp", player.userID, Cfg.FishXP);
            Item blood = ItemManager.CreateByName("blood", Cfg.FishRP);
            if (autoPickup)
            {
                player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
            }
            else
            {
                blood.Drop((player.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
            }
            return fish;
        }
        private void OnEntityDeath(BaseCombatEntity victimEntity, HitInfo hitInfo)
        {
            // Ignore - there is no victim for some reason
            if (victimEntity == null)
                return;

            // Try to avoid error when entity was destroyed
            if (victimEntity.gameObject == null)
                return;

            // barrel zombie
            var name = victimEntity.ShortPrefabName;
            if (name.Contains("barrel") && !name.Contains("hobo"))
            {
                var KillerEntity = victimEntity.lastAttacker ?? hitInfo?.Initiator;
                if (!(KillerEntity is BasePlayer))
                    return;
                BasePlayer player = KillerEntity as BasePlayer;
                float randEval = Random.Range(1, 20);
                float randAmt = Random.Range(2, 9);
                if (randEval < (getGatherMulti(player)*2))
                {
                    Item scrap = ItemManager.CreateByName("scrap", (int)Math.Ceiling(randAmt));
                    scrap.Drop((victimEntity.transform.position + new Vector3(0f, 1f, 0f)), Vector3.down);
                    //Puts("DEBUG: Dropping extra Scrap for " + randEval + " amt " + randAmt);
                }

            }
        }
        #endregion hooks

        #region Functions
        private void initPlayerCache()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                playerCache.Add(p.userID, new PlayerBoost(p));
            }
        }

        private void updatePlayerCache(BasePlayer p)
        {
            playerCache[p.userID] = new PlayerBoost(p);
        }

        private bool canAutoPickup(BasePlayer player)
        {
            if (!(playerCache.ContainsKey(player.userID)))
            {
                updatePlayerCache(player);
            }
            return playerCache[player.userID].autoPickup;
        }
        private float getGatherMulti(BasePlayer player)
        {
            if (!(playerCache.ContainsKey(player.userID)))
            {
                updatePlayerCache(player);
            }
            return playerCache[player.userID].gather;
        }
        private float getPermMulti(BasePlayer player, ResourceDispenser.GatherType type)
        {
            float multiplier = 1.0f;
            if (!(playerCache.ContainsKey(player.userID)))
            {
                updatePlayerCache(player);
            }
            switch (type)
            {
                case ResourceDispenser.GatherType.Tree:
                    multiplier = playerCache[player.userID].wood;
                    break;

                case ResourceDispenser.GatherType.Ore:
                    multiplier = playerCache[player.userID].mining;
                    break;

                case ResourceDispenser.GatherType.Flesh:
                    multiplier = playerCache[player.userID].skin;
                    break;

                default:
                    return multiplier;
            }
            return multiplier;
        }

        private void initSkills()
        {
            initBoostList("znfarming.mining.lv", "Mining", Cfg.MiningBoosts);
            initBoostList("znfarming.gather.lv", "Gather", Cfg.gatherBoosts);
            initBoostList("znfarming.wood.lv", "Wood", Cfg.woodBoosts);
            initBoostList("znfarming.skin.lv", "Animal", Cfg.skinBoosts);

            ZNExperience.Call("SaveSkillSetup");
        }

        private string getDesc(string hName, float multi)
        {
            switch (hName)
            {
                case "Wood":
                    return "Increases your Woodcutting rate to: <color=green> " + multi + "x </color> vanilla";
                case "Mining":
                    return "Increases your Node Mining rate to: <color=green> " + multi + "x </color> vanilla";
                case "Animal":
                    return "Increases your Animal skinning rate to: <color=green>" + multi + "x </color> \nAND fish-parts from gutting by <color=green>" + (multi/2) + "x</color> of vanilla <i>(does NOT affect random fish items)</i>";
                case "Gather":
                    return "Increases your resource and plant gathering rate to: <color=green> " + multi + "x </color> vanilla AND increases the chance to get extra (3-9) scrap from barrels to <color=green>" + Math.Floor(100*(2*multi / 20)) + "%</color>.";
                default:
                    return "";
            }
        }
        private void initBoostList(string lvl, string hName, Dictionary<string, FarmingBoost> fb)
        {
            string id = "";
            string name = "";
            string groupId = "";
            int spCost = 0;
            List<string> permissionEffect = new List<string>();
            string description = "";
            string prerequisiteSkillId = "";
            string followUpSkillId = "";
            bool isDefault = false;
            string prestigeUnlockId = "";
            string iconURL = "";

            foreach (KeyValuePair<string, FarmingBoost> b in fb)
            {
                id = b.Key;
                name = hName + ((b.Value.lvl == 0) ? " Base" : (" Lv. " + b.Value.lvl));
                groupId = "farming";
                spCost = b.Value.spCost;
                permissionEffect = new List<string> { b.Key };
                description = getDesc(hName, b.Value.multi);
                followUpSkillId = lvl + (b.Value.lvl + 1);
                if (b.Value.lvl == 0)
                {
                    isDefault = true;
                }
                else
                {
                    isDefault = false;
                    prerequisiteSkillId = lvl + (b.Value.lvl - 1);
                    if (b.Value.lvl == 10)
                    {
                        followUpSkillId = "";
                    }
                }
                if (b.Value.prestige)
                {
                    prestigeUnlockId = "p" + ((hName == "Animal") ? "Skinning" : hName) ;
                }
                ZNExperience.Call("RegisterSkill", id, name, groupId, spCost, permissionEffect, description, prerequisiteSkillId, followUpSkillId, isDefault, prestigeUnlockId, iconURL);
            }
        }

        #endregion Functions

        #region commands

        /*
        [ChatCommand("ll")]
        void TESTcommand(BasePlayer player, string command, string[] args)
        {
            updatePlayerCache(player);
            return;
        }
        */
        

        #endregion commands
    }
}
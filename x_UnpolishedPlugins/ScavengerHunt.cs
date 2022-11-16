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
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Random = System.Random;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("ScavengerHunt", "DocValerian", "1.4.7")]
    class ScavengerHunt : RustPlugin
    {
        static ScavengerHunt Plugin;

        [PluginReference]
        private Plugin ImageLibrary, ServerRewards, LootBoxSpawner, ZNTitleManager;

        const string permUse = "scavengerhunt.use";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region ConfigDataLoad
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
        protected override void LoadDefaultConfig()
        {
            Cfg = new Configuration();
        }
        static Configuration Cfg = new Configuration();
        class Configuration
        {
            public int maxActiveHunts = 1;
            public float rerollBasePrice = 100f;
            public float rerollMultiplyer = 0.9f;

            [JsonProperty(PropertyName = "Scanvenger Quest Levels")]
            public Dictionary<string, ZNScavengerLevel> huntLevels = new Dictionary<string, ZNScavengerLevel>()
            {
                ["scavenge.easy"] = new ZNScavengerLevel { id = "scavenge.easy", numItems = 3, rewardPerCompletion = 1200, stacks = 3,
                    possibleItemNames = collectorItems,
                    questPoints = 4
                },
                ["scavenge.normal"] = new ZNScavengerLevel { id = "scavenge.normal", numItems = 4, rewardPerCompletion = 3000, stacks = 4,
                    possibleItemNames = collectorItems,
                    questPoints = 8
                },
                ["scavenge.hard"] = new ZNScavengerLevel { id = "scavenge.hard", numItems = 5, rewardPerCompletion = 3500, stacks = 6,
                    possibleItemNames = collectorItems,
                    questPoints = 16
                },
                ["scavenge.grind"] = new ZNScavengerLevel { id = "scavenge.grind", numItems = 7, rewardPerCompletion = 6000, stacks = 8,
                    possibleItemNames = collectorItems,
                    questPoints = 32
                },
                ["scavenge.insane"] = new ZNScavengerLevel { id = "scavenge.insane", numItems = 9, rewardPerCompletion = 10000, stacks = 9,
                    possibleItemNames = collectorItems,
                    questPoints = 64
                },
                ["hunting.normal"] = new ZNScavengerLevel
                {
                    id = "hunting.normal",
                    numItems = 3,
                    rewardPerCompletion = 4000,
                    stacks = 5,
                    possibleItemNames = hunterItems,
                    questPoints = 10
                },
                ["hunting.hard"] = new ZNScavengerLevel
                {
                    id = "hunting.hard",
                    numItems = 4,
                    rewardPerCompletion = 6500,
                    stacks = 7,
                    possibleItemNames = hunterItems,
                    questPoints = 20
                },
                ["hunting.grind"] = new ZNScavengerLevel
                {
                    id = "hunting.grind",
                    numItems = 6,
                    rewardPerCompletion = 10000,
                    stacks = 11,
                    possibleItemNames = hunterItems,
                    questPoints = 40
                },
                ["farming.normal"] = new ZNScavengerLevel
                {
                    id = "farming.normal",
                    numItems = 3,
                    rewardPerCompletion = 4000,
                    stacks = 2,
                    possibleItemNames = farmerItems,
                    questPoints = 10
                },
                ["farming.hard"] = new ZNScavengerLevel
                {
                    id = "farming.hard",
                    numItems = 4,
                    rewardPerCompletion = 6500,
                    stacks = 4,
                    possibleItemNames = farmerItems,
                    questPoints = 20
                },
                ["farming.grind"] = new ZNScavengerLevel
                {
                    id = "farming.grind",
                    numItems = 6,
                    rewardPerCompletion = 10000,
                    stacks = 7,
                    possibleItemNames = farmerItems,
                    questPoints = 40
                },
            };

            [JsonProperty(PropertyName = "Item Stacksize/Rarity Config")]
            public Dictionary<string, int> itemAmountPerStack = new Dictionary<string, int>()
            {
                ["bleach"] = 8,
                ["knife.butcher"] = 5,
                ["bucket.water"] = 3,
                ["pitchfork"] = 5,
                ["sickle"] = 5,
                ["woodcross"] = 3,
                ["smallwaterbottle"] = 3,
                ["antiradpills"] = 3,
                ["metalspring"] = 6,
                ["metalblade"] = 10,
                ["tarp"] = 3,
                ["roadsigns"] = 10,
                ["semibody"] = 2,
                ["can.beans"] = 5,
                ["gravestone"] = 3,
                ["coffin.storage"] = 3,
                ["chocholate"] = 10,
                ["smgbody"] = 1,
                ["sewingkit"] = 10,
                ["ammo.pistol.hv"] = 18,
                ["ammo.pistol.fire"] = 32,
                ["ammo.pistol"] = 37,
                ["hat.boonie"] = 3,
                ["mask.balaclava"] = 3,
                ["hat.beenie"] = 3,
                ["riot.helmet"] = 2,
                ["hat.wolf"] = 2,
                ["wall.graveyard.fence"] = 3,
                ["vehicle.1mod.cockpit.armored"] = 2,
                ["vehicle.1mod.passengers.armored"] = 2,
                ["vehicle.1mod.cockpit"] = 2,
                ["vehicle.1mod.cockpit.with.engine"] = 2,
                ["vehicle.2mod.passengers"] = 2,
                ["vehicle.1mod.rear.seats"] = 2,
                ["vehicle.1mod.engine"] = 2,
                ["vehicle.1mod.flatbed"] = 2,
                ["vehicle.2mod.fuel.tank"] = 2,
                ["vehicle.1mod.storage"] = 2,
                ["vehicle.2mod.flatbed"] = 2,
                ["stones"] = 350000,
                ["metal.ore"] = 100000,
                ["hq.metal.ore"] = 800,
                ["sulfur.ore"] = 70000,
                ["wood"] = 500000,
                ["scrap"] = 9000,
                ["fat.animal"] = 1000,
                ["bone.fragments"] = 2500,
                ["leather"] = 1000,
                ["bearmeat"] = 500,
                ["deermeat.raw"] = 50,
                ["meat.boar"] = 250,
                ["wolfmeat.raw"] = 200,
                ["chicken.raw"] = 50,
            };

            public int ChallengeEveryMinutes = 60;
            public int ChallengeCooldownMinutes = 20;
            public int WinnerReward = 5000;
            public int SecondReward = 2500;
            public int ThirdReward = 1200;
            public int ParticipantReward = 500;
            public int MinRewardScore = 30;
            public ulong infoPlayerID = 1212;
        }

        class StoredData
        {
            public DateTime lastChallengeTime = DateTime.Now.AddMinutes(-99);
            public string lastChallengeId = "";
            public CurrentChallenge currentChallenge;
            public HashSet<ulong> noUIPlayers = new HashSet<ulong>();
        }
        StoredData storedData;

        private List<TimedChallenge> Challenges = new List<TimedChallenge> {
            new TimedChallenge{ 
                id = "farmStone", 
                challengeScoreName = "Stone Nodes/Piles", 
                itemToFind = "stones", 
                name = "Stone Challenge", 
                duration = 30,
                title_f = "Stonemason",
                title_m = "Stonemason",
                title_e_f = "Queen of Rock",
                title_e_m = "King of Rock"
            },
            new TimedChallenge{ 
                id = "farmWood",
                challengeScoreName = "Trees/Stumps", 
                itemToFind = "wood", 
                name = "Lumber Challenge", 
                duration = 15,
                title_f = "Lumberjane",
                title_m = "Lumberjack",
                title_e_f = "Forest Queen",
                title_e_m = "Forest King"
            },
            new TimedChallenge{ 
                id = "farmMetal",   
                challengeScoreName = "Metal Nodes/Piles", 
                itemToFind = "metal.ore", 
                name = "Metal Challenge", 
                duration = 30,
                title_f = "Iron Lady",
                title_m = "Iron Man",
                title_e_f = "Queen of Metal",
                title_e_m = "King of Metal"
            },
            new TimedChallenge{ 
                id = "farmSulfur", 
                challengeScoreName = "Sulfur Nodes/Piles", 
                itemToFind = "sulfur.ore", 
                name = "Sulfur Challenge", 
                duration = 30,
                title_f = "Chemist",
                title_m = "Chemist",
                title_e_f = "Master Alchemist",
                title_e_m = "Master Alchemist"
            },
            new TimedChallenge{
                id = "farmBarrel",
                challengeScoreName = "Barrels",
                itemToFind = "barrel",
                name = "Barrel Challenge",
                duration = 15,
                title_f = "Road Warrior",
                title_m = "Road Warrior",
                title_e_f = "Queen of the Road",
                title_e_m = "King of the Road"
            },
            new TimedChallenge{
                id = "farmFish",
                challengeScoreName = "Fish",
                itemToFind = "fish",
                name = "Fishing Challenge",
                duration = 20,
                title_f = "Mermaid",
                title_m = "Master Fisher",
                title_e_f = "Queen of the Ocean",
                title_e_m = "King of the Ocean"
            },

            new TimedChallenge{
                id = "farmUnderworld",
                challengeScoreName = "Underground/-water NPCs",
                itemToFind = "dweller",
                name = "Underworld Challenge",
                duration = 20,
                title_f = "Dark Mistress",
                title_m = "Master of Darkness",
                title_e_f = "Goddess of the Underworld",
                title_e_m = "God of the Underworld"
            },
            new TimedChallenge{
                id = "farmPlants",
                challengeScoreName = "Plants",
                itemToFind = "plants",
                name = "Plant Challenge",
                duration = 20,
                title_f = "Herbalist",
                title_m = "Herbalist",
                title_e_f = "Goddess of Harvest",
                title_e_m = "God of Harvest"
            },
        };

        void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            //storedData = new StoredData();
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ScavengerHunt");
            SaveData(storedData);

        }

        private void OnServerInitialized()
        {
            StartChallengeCoroutine();
            loadLiveUI();
            initTitles();
        }

        void Unload()
        {
            StopChallengeCoroutine();
            foreach (var player in UiPlayers.ToList())
            {
                killUI(player);
            }

            foreach (var player in LiveUiPlayers.ToList())
            {
                killLiveUI(player);
            }
        }


        private static void LoadData<T>(out T data, string filename = null)
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? "ScavengerHunt");
            }
            catch (JsonException ex)
            {
                Plugin.Puts("E1:" + ex.Message);
                data = default(T);
            }
            catch (Exception ex)
            {
                Plugin.Puts("E2: " + ex.Message + "\n"+ex.InnerException);
                data = default(T);
            }
        }

        private static void SaveData<T>(T data, string filename = null) =>
        Interface.Oxide.DataFileSystem.WriteObject(filename ?? "ScavengerHunt", data);
        #endregion

        #region global params
        private static List<string> collectorItems = new List<string>() { 
            "knife.butcher", 
            "bucket.water", 
            "pitchfork", 
            "sickle", 
            "woodcross", 
            "smallwaterbottle", 
            "antiradpills", 
            "metalspring", 
            "metalblade",
            "tarp", 
            "roadsigns",
            "semibody", 
            "can.beans", 
            "gravestone", 
            "coffin.storage", 
            "chocholate", 
            "smgbody", 
            "sewingkit", 
            "ammo.pistol.hv", 
            "ammo.pistol.fire", 
            "ammo.pistol", 
            "hat.boonie", 
            "mask.balaclava", 
            "hat.beenie", 
            "riot.helmet", 
            "hat.wolf",
            "vehicle.1mod.cockpit.armored",
            "vehicle.1mod.passengers.armored", 
            "vehicle.1mod.cockpit",
            "vehicle.1mod.cockpit.with.engine",
            "vehicle.2mod.passengers", 
            "vehicle.1mod.rear.seats", 
            "vehicle.1mod.engine", 
            "vehicle.1mod.flatbed", 
            "vehicle.2mod.fuel.tank", 
            "vehicle.1mod.storage", 
            "vehicle.2mod.flatbed" 
        };
        private static List<string> farmerItems = new List<string>() {
            "stones",
            "metal.ore",
            "hq.metal.ore",
            "sulfur.ore",
            "wood",
            "scrap",
        };
        private static List<string> hunterItems = new List<string>() {
            "fat.animal",
            "bone.fragments",
            "leather",
            "bearmeat",
            "deermeat.raw",
            "meat.boar",
            "wolfmeat.raw",
            "chicken.raw",
        };
        private Dictionary<ulong, ScavengerManager> _sm = new Dictionary<ulong, ScavengerManager>();
        private Coroutine challengeCoroutine { get; set; }
        private static List<string> plantList = new List<string>() { "potato", "cloth", "red.berry", "blue.berry", "green.berry", "yellow.berry", "white.berry", "black.berry", "mushroom", "pumpkin", "corn"};
        private Dictionary<string, int> fishValueList = new Dictionary<string, int>()
        {
            ["fish.anchovy"] = 2,
            ["fish.catfish"] = 9,
            ["fish.herring"] = 2,
            ["fish.minnows"] = 1,
            ["fish.orangeroughy"] = 7,
            ["fish.salmon"] = 5,
            ["fish.sardine"] = 2,
            ["fish.smallshark"] = 8,
            ["fish.troutsmall"] = 3,
            ["fish.yellowperch"] = 5
        };
        #endregion

        #region Classes

        class TimedChallenge
        {
            public string id { get; set; }
            public string name { get; set; }
            public string itemToFind { get; set; }
            public string challengeScoreName { get; set; }
            public int duration { get; set; }

            public string type = "farming";
            public string title_m { get; set; }
            public string title_f { get; set; }
            public string title_e_m { get; set; }
            public string title_e_f { get; set; }
        }

        class CurrentChallenge
        {
            public TimedChallenge info { get; set; }
            public DateTime startTime { get; set; }
            public Dictionary<ulong, int> highscores { get; set; }
            public Dictionary<ulong, string> playerNames { get; set; }

            public CurrentChallenge(TimedChallenge tc) : base()
            {
                info = tc;
                startTime = DateTime.Now;
                highscores = new Dictionary<ulong, int>();
                playerNames = new Dictionary<ulong, string>();
            }
        }
      

        public class ZNScavengerLevel
        {
            public string id { get; set; }
            public int numItems { get; set; }
            public List<string> possibleItemNames { get; set; }
            public int rewardPerCompletion { get; set; }
            public int stacks { get; set; }
            public int questPoints { get; set; }

        } 

        public class ZNScavengingQuest
        {
            public ulong id { get; set; }
            public Dictionary<string, int> itemsToFind { get; set; }
            public int promisedReward { get; set; }
            public bool isComplete { get; set; }
            public int promisedQuestPoints { get; set; }
            public string level { get; set; }

            public ZNScavengingQuest() : base()
            {
            }
            public ZNScavengingQuest(string qLevel) : base()
            {
                id = (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
                level = qLevel;
                initQuest();
            }

            private void initQuest()
            {
                if (!Cfg.huntLevels.ContainsKey(level)) return;
                ZNScavengerLevel l = Cfg.huntLevels[level];

                itemsToFind = new Dictionary<string, int>();
                itemsToFind.Add("bleach", Cfg.itemAmountPerStack["bleach"] * l.stacks);
                int amountPerStack = 1;

                foreach (string prefab in Plugin.UniqueRandomKeys(l.possibleItemNames).Take(l.numItems))
                {
                    if (Cfg.itemAmountPerStack.ContainsKey(prefab)) amountPerStack = Cfg.itemAmountPerStack[prefab];
                    itemsToFind.Add(prefab, (amountPerStack * l.stacks));
                }
                promisedReward = l.rewardPerCompletion;
                promisedQuestPoints = l.questPoints;
            }

            public void Pay(BasePlayer player)
            {
                if (player.inventory.containerMain.IsEmpty()) return;

                Dictionary<string, int> playerItems = Plugin.GetPlayerItems(player);
                int taken = 0;
                int totalRemain = 0;
                int itmID;

                foreach (KeyValuePair<string, int> d in itemsToFind.ToList())
                {

                    totalRemain += itemsToFind[d.Key];
                    if (!(playerItems.ContainsKey(d.Key)) || d.Value == 0) continue;
                    totalRemain -= itemsToFind[d.Key];

                    itmID = ItemManager.itemDictionaryByName[d.Key].itemid;
                    taken = player.inventory.containerMain.Take(null, itmID, d.Value);

                    itemsToFind[d.Key] -= taken;
                    totalRemain += itemsToFind[d.Key];
                }
                if (totalRemain == 0) isComplete = true;

            }
        }
        private class ScavengerManager
        {
            public ulong playerID;
            private BasePlayer bPlayer;
            public Dictionary<ulong, ZNScavengingQuest> quests = new Dictionary<ulong, ZNScavengingQuest>();
            public ZNScavengingQuest lastTempQuest;
            public int questGenerationCounter = 0;
            public int questsCompleted = 0;
            public int questsAbandoned = 0;
            public int questsPoints = 0;

            public ScavengerManager(ulong playerId) : base()
            {
                playerID = playerId;
            }
            public static ScavengerManager Get(ulong playerId, bool wipe = false)
            {
                if (Plugin._sm.ContainsKey(playerId) && !wipe)
                    return Plugin._sm[playerId];

                var fileName = $"{Plugin.Name}/{playerId}";

                ScavengerManager manager;
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(fileName) && !wipe)
                {
                    // Load existing Data
                    ScavengerHunt.LoadData(out manager, fileName);
                    if (!(manager is ScavengerManager)) return null;
                }
                else
                {
                    // Create a completely new Playerdataset
                    manager = new ScavengerManager(playerId);
                    ScavengerHunt.SaveData(manager, fileName);
                }

                Interface.Oxide.DataFileSystem.GetDatafile(fileName).Settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Populate
                };
                manager.bPlayer = BasePlayer.FindAwakeOrSleeping(playerId.ToString());
                Plugin._sm[playerId] = manager;

                return manager;
            }

            public void SaveData()
            {
                ScavengerHunt.SaveData(this, $"{Plugin.Name}/{playerID}");
            }
            public void SetTempQuest(ZNScavengingQuest q)
            {
                lastTempQuest = q;
                questGenerationCounter++;
            }

            public void AcceptQuest()
            {
                if (lastTempQuest == null) return;
                quests[lastTempQuest.id] = lastTempQuest;
                lastTempQuest = null;
            }

            public void AbandonQuest()
            {
                if (CurrentQuest().isComplete)
                {
                    FinishQuest();
                    return;
                }
                quests.Clear();
                questsAbandoned++;
                SaveData();
            }
            public void Pay()
            {
                ZNScavengingQuest q = CurrentQuest();
                if (q.isComplete)
                {
                    FinishQuest();
                    return;
                }
                q.Pay(bPlayer);
                SaveData();
            }

            public void FinishQuest()
            {
                ZNScavengingQuest q = CurrentQuest();
                if (q.isComplete)
                {
                    lastTempQuest = null;
                    questGenerationCounter = 0;
                    quests.Clear();
                    questsCompleted++;
                    questsPoints += q.promisedQuestPoints;
                    Plugin.ZNTitleManager?.Call("CheckScore", Plugin.Name + "_" + "quests", (float)questsPoints, playerID);
                    SaveData(); 
                    Item blood = ItemManager.CreateByName("blood", q.promisedReward);
                    bPlayer.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
            }

            public ZNScavengingQuest CurrentQuest()
            {
                if (quests.Count == 0) return null;

                foreach(ZNScavengingQuest q in quests.Values)
                {
                    //for now we only need one
                    return q;
                }

                return null;
            }


        }

        public static class Coroutines
        {
            private static Dictionary<float, YieldInstruction> mem;
            public static void Clear()
            {
                if (mem != null)
                {
                    mem.Clear();
                    mem = null;
                }
            }
            public static YieldInstruction WaitForSeconds(float delay)
            {
                if (mem == null)
                {
                    mem = new Dictionary<float, YieldInstruction>();
                }

                YieldInstruction yield;
                if (!mem.TryGetValue(delay, out yield))
                {
                    yield = new WaitForSeconds(delay);
                    mem.Add(delay, yield);
                }

                return yield;
            }

            
        }
        #endregion

        #region Hooks
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!(storedData.noUIPlayers.Contains(player.userID)) && !LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Add(player);
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            killUI(player);
            killLiveUI(player);
            if (_sm.ContainsKey(player.userID))
            {
                _sm[player.userID].SaveData();
                _sm.Remove(player.userID);
            }
        }

        private void OnEntityDeath(BaseCombatEntity victimEntity, HitInfo hitInfo)
        {
            // Ignore - there is no victim for some reason
            if (victimEntity == null)
                return;

            // Try to avoid error when entity was destroyed
            if (victimEntity.gameObject == null)
                return;

            // barrel bleach
            var name = victimEntity.ShortPrefabName;
            if (name.Contains("barrel") && !name.Contains("hobo"))
            {
                var KillerEntity = victimEntity.lastAttacker ?? hitInfo?.Initiator;
                if (!(KillerEntity is BasePlayer))
                    return;

                checkChallengeProgress((BasePlayer)KillerEntity, "barrel");
                float rand = UnityEngine.Random.Range(1, 101);
                if (rand > 50) return;
                Item item = ItemManager.CreateByName("bleach", 1);
                item.name = "Quest Item /q";
                item.Drop((victimEntity.transform.position+new Vector3(0f,1f,0f)), Vector3.down);
            }
            // dweller kill challenge
            if (name.Contains("dweller"))
            {
                var KillerEntity = victimEntity.lastAttacker ?? hitInfo?.Initiator;
                if (!(KillerEntity is BasePlayer))
                    return;

                checkChallengeProgress((BasePlayer)KillerEntity, "dweller");
            }
        }

        Item OnFishCatch(Item fish, BaseFishingRod fishingRod, BasePlayer player)
        {
            if (fish.info == null || fish.info.shortname == null) return fish;

            if (fishValueList.ContainsKey(fish.info.shortname))
            {
                checkChallengeProgress(player, "fish", fishValueList[fish.info.shortname]);
            }
            return fish;
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity as BasePlayer;
            if (player == null) return;
            checkChallengeProgress(player, item.info.shortname);
        }
        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null) return null;
            Item item = collectible.GetItem();
            for (int i = 0; i < collectible.itemList.Length; i++)
            {
                ItemAmount itemAmount = collectible.itemList[i];
                string itemName = itemAmount.itemDef.shortname;
                checkChallengeProgress(player, itemName);
                if (plantList.Contains(itemName)) checkChallengeProgress(player, "plants");  
            }
            return null;
        }

        #endregion

        #region Commands

        [ConsoleCommand("shunt.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            killUI(player);

            if (_sm.ContainsKey(player.userID))
            {
                _sm[player.userID].SaveData();
            }
        }

        [ConsoleCommand("shunt.generate")]
        private void CmdGenerate(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string levelId = arg.GetString(0);

            if (_sm.ContainsKey(player.userID))
            {
                ZNScavengingQuest q = new ZNScavengingQuest(levelId);
                _sm[player.userID].SetTempQuest(q);
                CuiHelper.DestroyUi(player, mainName + "_quest");
                GUIQuestElement(player, mainName + "_quest", mainName + "_main", q, eSelectQTop);

            }
        }
        [ConsoleCommand("shunt.accept")]
        private void CmdAccept(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string levelId = arg.GetString(0);

            if (_sm.ContainsKey(player.userID))
            {
                _sm[player.userID].AcceptQuest();
                CuiHelper.DestroyUi(player, mainName + "_quest");
                CuiHelper.DestroyUi(player, mainName + "_main");
                GUIMainElement(player, mainName + "_main", _sm[player.userID]);

            }
        }
        [ConsoleCommand("shunt.abandon")]
        private void CmdAbandon(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string levelId = arg.GetString(0);

            if (_sm.ContainsKey(player.userID))
            {
                _sm[player.userID].AbandonQuest();
                CuiHelper.DestroyUi(player, mainName + "_quest");
                CuiHelper.DestroyUi(player, mainName + "_main");
                GUIMainElement(player, mainName + "_main", _sm[player.userID]);

            }
        }

        [ConsoleCommand("shunt.pay")]
        private void CmdPay(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string levelId = arg.GetString(0);

            if (_sm.ContainsKey(player.userID))
            {
                _sm[player.userID].Pay();
                CuiHelper.DestroyUi(player, mainName + "_quest");
                CuiHelper.DestroyUi(player, mainName + "_main");
                GUIMainElement(player, mainName + "_main", _sm[player.userID]);

            }
        }

        [ConsoleCommand("shunt.finish")]
        private void CmdFinish(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string levelId = arg.GetString(0);

            if (_sm.ContainsKey(player.userID))
            {
                _sm[player.userID].FinishQuest();
                CuiHelper.DestroyUi(player, mainName + "_quest");
                CuiHelper.DestroyUi(player, mainName + "_main");
                GUIMainElement(player, mainName + "_main", _sm[player.userID]);

            }
        }


        [ConsoleCommand("shunt.admset")]
        private void CmdAdmSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !player.IsAdmin)
                return;
            ulong pId = ulong.Parse(arg.GetString(0));
            int score = arg.GetInt(1);

           if(storedData.currentChallenge.highscores.ContainsKey(pId))
            {
                storedData.currentChallenge.highscores[pId] = score;
            }
        }

        [ChatCommand("qold")]
        void MainCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse))
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            if (!_sm.ContainsKey(player.userID))
            {
                _sm[player.userID] = ScavengerManager.Get(player.userID);
            }
            reloadUI(player);
        }

        [ChatCommand("qc")]
        void ChallengeUICommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse))
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }

            if(args.Length == 1)
            {
                switch (args[0])
                {
                    case "ui":
                        toggleUI(player);
                        return;
                    default:
                        break;
                }
            }
                
            showInfoMessage(player);
        }

        [ChatCommand("qcs")]
        void ChallengeADMCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            if(storedData.currentChallenge != null) finishChallenge();
            storedData.currentChallenge = null;
            storedData.lastChallengeTime = DateTime.Now.AddMinutes(-99);
            SaveData(storedData);
        }

        #endregion

        #region Functions
        private void initTitles() 
        {
            // Challenge Titles
            foreach(TimedChallenge c in Challenges)
            {
                ZNTitleManager?.Call("UpsertTitle", getTitleID(c), c.name, c.challengeScoreName, c.title_e_m, c.title_e_f, c.title_m, c.title_f);
            }
            // Quest Title
            ZNTitleManager?.Call("UpsertTitle", Plugin.Name + "_" +"quests", "Quests", "/Q Score", "Quest Master", "Quest Queen", "Adventurer", "Scavenger");

        }

        private string getTitleID(TimedChallenge t)
        {
            return Plugin.Name + "_" + t.id;
            
        }
        private void loadLiveUI()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (!(storedData.noUIPlayers.Contains(p.userID)) && !LiveUiPlayers.Contains(p))
                {
                    LiveUiPlayers.Add(p);
                }
            }
        }
        private void toggleUI(BasePlayer player)
        {
            string msg = "<color=green>ZN-Quests:</color> Challenge Quest UI ";
            if (storedData.noUIPlayers.Contains(player.userID))
            {
                storedData.noUIPlayers.Remove(player.userID);
                msg += "<color=green>Activated!</color>";
                reloadLiveUI(player);
            }
            else
            {
                storedData.noUIPlayers.Add(player.userID);
                msg += "<color=red>Disabled!</color>";
                killLiveUI(player);
            }
            SendReply(player, msg);
        }
        private void showInfoMessage(BasePlayer player)
        {
            string msg = "<color=orange>===== ZN Quests - Challenges ======</color>";
            msg += "\nQuest Challenges are timed server wide challenges. ";
            msg += "\nGoal is to collect the current resource (farm&gather).";
            msg += "\nIt counts only number of nodes finished or piles gathered, to";
            msg += "\nkeep it fair for everyone, no matter their levels";
            if (storedData.currentChallenge != null)
            {
                CurrentChallenge c = storedData.currentChallenge;
                msg += "\n\n<color=green>Current Challenge:</color>";
                msg += "\nFind: \t\t\t\t" + c.info.challengeScoreName;
                msg += "\nTime Left: \t\t\t"+ (c.info.duration - (DateTime.Now- c.startTime).Minutes) + " minutes";
            }
            else
            {
                msg += "\n\n<color=green>Next Challenge:</color>";
                msg += "\nStarts in: \t\t\t\t" + (Cfg.ChallengeCooldownMinutes - (DateTime.Now - storedData.lastChallengeTime).Minutes) + " minutes";
                msg += "\nCan't be: \t\t\t\t" + storedData.lastChallengeId;

            }
            msg += "\n\n<color=green>Commands:</color>";
            msg += "\n/qc \t\t\t\tshow this info";
            msg += "\n/qc ui \t\t\ttoggle info UI";
            msg += "\n/loot \t\t\t\tGet your reward if you won.";

            msg += "\n\n<color=green>Rules:</color>";
            msg += "\nA Challenge lasts: \t<color=orange>10-30 minutes</color>";
            msg += "\nChallenge cooldown: \t<color=orange>" + Cfg.ChallengeCooldownMinutes+ " minutes</color> (til next)";
            msg += "\nYou need to reach \t<color=red>Min Reward Score " + (Cfg.MinRewardScore) +"</color>";

            msg += "\nWinner gets: \t\t<color=orange>" + (Cfg.WinnerReward) + " Blood</color>";
            msg += "\n2nd gets: \t\t\t<color=orange>" + (Cfg.SecondReward) + " Blood</color>";
            msg += "\n3rd gets: \t\t\t<color=orange>" + (Cfg.ThirdReward) + " Blood</color>";
            msg += "\nParticipant: \t\t<color=orange>" + (Cfg.ParticipantReward) + " Blood</color>";

            SendReply(player, msg);
        }
        private void checkChallengeProgress(BasePlayer player, string item, int value = 1)
        {
            if (storedData.currentChallenge == null) return;
            CurrentChallenge c = storedData.currentChallenge;
            if (c.info.itemToFind != item)
            {
                return;
            }
            addPlayerScore(player, value);
        }

        private void addPlayerScore(BasePlayer player, int value = 1)
        {
            CurrentChallenge c = storedData.currentChallenge;
            if (c.highscores.ContainsKey(player.userID))
            {
                c.highscores[player.userID] += value;
            }
            else
            {
                c.highscores.Add(player.userID, value);
            }
            if (!(c.playerNames.ContainsKey(player.userID))) c.playerNames.Add(player.userID, player.displayName);
        }
        private void StopChallengeCoroutine()
        {
            if (challengeCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(challengeCoroutine);
                challengeCoroutine = null;
            }
        }

        private void StartChallengeCoroutine()
        {
            StopChallengeCoroutine();
            timer.Once(0.2f, () =>
            {
                challengeCoroutine = ServerMgr.Instance.StartCoroutine(ChallengeCoroutine());
            });
        }
        private IEnumerator ChallengeCoroutine()
        {
            float secondsToNextAction = 2f;
            while (true)
            {
                // start a new challenge
                if(storedData.currentChallenge == null)
                {
                    double minutesSinceLast = (DateTime.Now - storedData.lastChallengeTime).TotalMinutes;
                    if (minutesSinceLast <= Cfg.ChallengeCooldownMinutes)
                    {
                        secondsToNextAction = 60f;                       
                    }
                    else
                    {
                        secondsToNextAction = 2f;
                        TimedChallenge c = Challenges.GetRandom();
                        if (c.id == storedData.lastChallengeId) continue;
                        Puts("DEBUG: START new Challenge "  + c.name);
                        storedData.lastChallengeId = c.id;
                        storedData.currentChallenge = new CurrentChallenge(c);
                        Server.Broadcast("<color=green>ZN-Quests:</color> A new challenge quest for <color=orange>" + c.challengeScoreName + "</color> has started!");

                        storedData.currentChallenge.highscores.Add(Cfg.infoPlayerID, Cfg.MinRewardScore);
                        storedData.currentChallenge.playerNames.Add(Cfg.infoPlayerID, "--- MIN REWARD SCORE ---");

                        loadLiveUI();

                        /* DEBUG CODE
                        storedData.currentChallenge.highscores.Add(2325235235, 12);
                        storedData.currentChallenge.playerNames.Add(2325235235, "FooBar");
                        storedData.currentChallenge.highscores.Add(43534, 15);
                        storedData.currentChallenge.playerNames.Add(43534, "test er ment");

                        storedData.currentChallenge.highscores.Add(232355235235, 5);
                        storedData.currentChallenge.playerNames.Add(232355235235, "Foorethr Bar");
                        storedData.currentChallenge.highscores.Add(434534, 3);
                        storedData.currentChallenge.playerNames.Add(434534, "test 44ent");

                        storedData.currentChallenge.highscores.Add(23252352354, 2);
                        storedData.currentChallenge.playerNames.Add(23252352354, "F4f ar");
                        storedData.currentChallenge.highscores.Add(435344, 1);
                        storedData.currentChallenge.playerNames.Add(435344, "erhret ment");

                        storedData.currentChallenge.highscores.Add(232523523544, 9);
                        storedData.currentChallenge.playerNames.Add(232523523544, "trr rth");
                        storedData.currentChallenge.highscores.Add(4353444, 52);
                        storedData.currentChallenge.playerNames.Add(4353444, "bnm tzj tr");

                        storedData.currentChallenge.highscores.Add(2325235, 11);
                        storedData.currentChallenge.playerNames.Add(2325235, "ttz");
                        storedData.currentChallenge.highscores.Add(4353, 16);
                        storedData.currentChallenge.playerNames.Add(4353, "tzjukiuk");

                        storedData.currentChallenge.highscores.Add(232523525, 14);
                        storedData.currentChallenge.playerNames.Add(232523525, "vsddf");
                        storedData.currentChallenge.highscores.Add(4334, 42);
                        storedData.currentChallenge.playerNames.Add(4334, "bts");

                        storedData.currentChallenge.highscores.Add(76561197998677819, 44);
                        storedData.currentChallenge.playerNames.Add(76561197998677819, "DocValerian");
                        */
                    }

                }
                // finish challenge
                else if((DateTime.Now - storedData.currentChallenge.startTime).TotalMinutes >= storedData.currentChallenge.info.duration)
                {

                    finishChallenge();
                    storedData.lastChallengeTime = DateTime.Now;
                    storedData.currentChallenge = null;
                    secondsToNextAction = 15f;
                }
                // run challenge progression
                else
                {
                    if (LiveUiPlayers.Count > 0)
                    {
                        foreach (BasePlayer p in LiveUiPlayers)
                        {
                            reloadLiveUI(p);
                        }
                    }

                }

                SaveData(storedData);

                //yield return Coroutines.WaitForSeconds(2f);
                //continue;
                yield return Coroutines.WaitForSeconds(secondsToNextAction);
            }
        }

        private void finishChallenge()
        {
            CurrentChallenge c = storedData.currentChallenge;
            Puts("DEBUG: FINISH Challenge " + c.info.name);
            foreach (var player in LiveUiPlayers.ToList())
            {
                killLiveUI(player);
            }
            List<ulong> sortedChallengers = sortChallengers(c.highscores);
            int place = 1;
            string msg = "<color=green>ZN-Quests:</color> The " + c.info.name + " has ended.";
            foreach (ulong cId in sortedChallengers)
            {
                if (cId == Cfg.infoPlayerID) continue;
                if(c.highscores[cId] < Cfg.MinRewardScore)
                {
                    if(place == 1)
                    {
                        Server.Broadcast(msg + "\nNobody reached the minimum reward limit :(");
                    }
                    Puts("Place: " + place+ " player: " + cId + " did not reach min score of " + Cfg.MinRewardScore);
                    break;
                }
                else
                { 
                    switch (place)
                    {
                        case 1:
                            msg += "\nScores:";
                            setRewardClaim(cId, Cfg.WinnerReward, "WINNER");
                            ZNTitleManager?.Call("CheckScore", getTitleID(c.info), (float)c.highscores[cId], cId);
                            break;
                        case 2:
                            setRewardClaim(cId, Cfg.SecondReward, "SECOND");
                            break;
                        case 3:
                            setRewardClaim(cId, Cfg.ThirdReward, "THIRD");
                            break;
                        default:
                            setRewardClaim(cId, Cfg.ParticipantReward, "participant");
                            break;
                    }

                    if(place <= 10)
                    {
                        msg += "\n - " + place + " - <color=orange>" + c.playerNames[cId] + "</color> (score: " + c.highscores[cId] + ")";
                    }
                    place++;
                }
            }
            Server.Broadcast(msg);

        }

        private void setRewardClaim(ulong pId, int reward, string place)
        {
            Puts(place+": " + pId + " with " + storedData.currentChallenge.highscores[pId]);
            LootBoxSpawner.Call("storeChallengeClaim", pId, reward);
            var p = BasePlayer.FindByID(pId);
            if (p != null)
            {
                SendReply(p, "<color=green>ZN-Quest:</color> You are "+place+" in the last Challenge Quest.\nUse <color=orange>/loot</color> to claim your reward.");
            }
        }

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
        public IEnumerable<TKey> UniqueRandomKeys<TKey>(IEnumerable<TKey> list)
        {
            Random rand = new Random((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
            List<TKey> values = new List<TKey>(list);
            while (values.Count > 0)
            {
                TKey randomKey = values.ElementAt(rand.Next(0, values.Count));  // hat tip @yshuditelu 
                values.Remove(randomKey);
                yield return randomKey;
            }
        }

        
        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }

        private Dictionary<string, int> GetPlayerItems(BasePlayer player)
        {
            Dictionary<string, int> playerItems = new Dictionary<string, int>();
            foreach (Item i in player.inventory.containerMain.itemList)
            {
                if (playerItems.ContainsKey(i.info.shortname))
                {
                    playerItems[i.info.shortname] += i.amount;
                }
                else
                {
                    playerItems[i.info.shortname] = i.amount;
                }
            }
            return playerItems;
        }
        private List<ulong> sortChallengers(Dictionary<ulong, int> playerSet)
        {
            List<ulong> retVal = new List<ulong>();
            if (playerSet.Count == 0) return retVal;

            foreach (KeyValuePair<ulong, int> d in playerSet.OrderByDescending(x => x.Value))
            {
                retVal.Add(d.Key);
            }
            return retVal;
        }
        #endregion

        #region GUI


        private HashSet<BasePlayer> LiveUiPlayers = new HashSet<BasePlayer>();
        private void reloadLiveUI(BasePlayer player)
        {
            if (storedData.noUIPlayers.Contains(player.userID))
            {
                return;
            }

            if (!LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Add(player);
            }
            CuiHelper.DestroyUi(player, mainName + "_live");
            displayLiveUI(player);
            
        }
        private void killLiveUI(BasePlayer player)
        {
            if (LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Remove(player);
                CuiHelper.DestroyUi(player, mainName + "_live");
            }
        }

        private void displayLiveUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            GUILiveElement(player, mainName + "_live");
        }

        private void GUILiveElement(BasePlayer player, string elUiId)
        {

            if (storedData.currentChallenge == null) return;
            CurrentChallenge c = storedData.currentChallenge;

            List<ulong> sortedChallengers = sortChallengers(c.highscores);
            ulong pId = 0;
            int score = 0;
            string username = "";
            float localTop = 1f;
            int max = (sortedChallengers.Count() < 10) ? sortedChallengers.Count() : 10;

            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                    //Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin =  "0.9 0.6" ,
                    AnchorMax = "0.995 0.90"
                },
                CursorEnabled = false
            }, "Hud", elUiId);


            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text =  c.info.name+ " - /qc",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0 0.8 0 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 "+ (localTop-0.1f),
                    AnchorMax = "0.99 " + localTop
                }
            }, elUiId);
            localTop -= 0.1f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "find: " + c.info.challengeScoreName,
                    FontSize = 10,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 "+ (localTop-0.07f),
                    AnchorMax = "0.95 " + localTop
                }
            }, elUiId);
            localTop -= 0.07f;
            int timeLeft = (int)Math.Ceiling(c.info.duration - (DateTime.Now - storedData.currentChallenge.startTime).TotalMinutes);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = timeLeft+ "min left",
                    FontSize = 10,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 "+ (localTop-0.07f),
                    AnchorMax = "0.95 " + localTop
                }
            }, elUiId);
            localTop -= 0.02f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Highscores:",
                    FontSize = 11,
                    Align = TextAnchor.LowerLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 "+ (localTop-0.09f),
                    AnchorMax = "0.95 " + localTop
                }
            }, elUiId);

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.0",

                },
                RectTransform =
                {
                    AnchorMin =  "0.05 0" ,
                    AnchorMax = "0.95 0.70"
                },
                CursorEnabled = false
            }, elUiId, elUiId + "_data");

            localTop = 1f;
            string playerColor = "1 1 1 1";
            for (int i = 0; i < max; i++)
            {

                pId = sortedChallengers[i];
                score = c.highscores[pId];
                username = c.playerNames[pId];
                playerColor = "1 1 1 1";
                if (pId == player.userID) playerColor = "0 0.8 0 1";
                if (pId == Cfg.infoPlayerID) playerColor = "0.8 0 0 1";

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = username,
                    FontSize = 9,
                    Align = TextAnchor.MiddleLeft,
                    Color = playerColor
                },
                    RectTransform =
                {
                    AnchorMin = "0.005 "+ (localTop-0.1f),
                    AnchorMax = "0.995 " + localTop
                }
                }, elUiId + "_data");


                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = ""+score,
                    FontSize = 9,
                    Align = TextAnchor.MiddleRight,
                    Color = playerColor
                },
                    RectTransform =
                {
                    AnchorMin = "0.005 "+ (localTop-0.1f),
                    AnchorMax = "0.995 " + localTop
                }
                }, elUiId + "_data");

                localTop -= 0.1f;
            }

            CuiHelper.AddUi(player, elements);

        }




        private const string globalNoErrorString = "none";
        private const string mainName = "ScavengerHunt";
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements = 
        {
            mainName+"_head", 
            mainName+"_foot",
            mainName+"_quest",
            mainName+"_main"
        };
        private float globalLeftBoundary = 0.1f;
        private float globalRighttBoundary = 0.9f;
        private float globalTopBoundary = 0.90f;
        private float globalBottomBoundary = 0.15f;
        private float globalSpace = 0.01f;
        private float eContentWidth = 0.395f;
        private float eHeadHeight = 0.05f;
        private float eFootHeight = 0.05f;
        private float eSlotHeight = 0.123f;
        private float eSelectQTop = 0.79f;
        private float eActiveQTop = 0.895f;


        private void reloadUI(BasePlayer player, string errorMsg = globalNoErrorString){
            if(!UiPlayers.Contains(player)){
                UiPlayers.Add(player);
            }
            foreach(string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }
            
            displayUI(player, errorMsg);

        }
        private void killUI(BasePlayer player){
            if(UiPlayers.Contains(player)){
                UiPlayers.Remove(player);
                foreach(string ui in uiElements)
                {
                    CuiHelper.DestroyUi(player, ui);
                }
            }
        }

        private void displayUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (!_sm.ContainsKey(player.userID)) return;

            ScavengerManager sm = _sm[player.userID];
            GUIHeaderElement(player, mainName+"_head", errorMsg);
            GUIMainElement(player, mainName+ "_main", sm);
            GUIFooterElement(player, mainName+"_foot");
        }
        private void GUIHeaderElement(BasePlayer player, string elUiId, string errorMsg  = globalNoErrorString)
        {
            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5", 
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalLeftBoundary  +" " + (globalTopBoundary-eHeadHeight),
                    AnchorMax = globalRighttBoundary + " " + globalTopBoundary
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ZN Quests",
                    FontSize = 18,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.005",
                    AnchorMax = "0.995 0.995"
                }
            }, elUiId);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "v" + Plugin.Version + " by " + Plugin.Author,
                    FontSize = 10,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.005",
                    AnchorMax = "0.995 0.995"
                }
            }, elUiId);

            if(errorMsg != "none")
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Error: "+ errorMsg,
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 0 0 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.005 0.005",
                        AnchorMax = "0.995 0.995"
                    }
                }, elUiId);
            }

            CuiHelper.AddUi(player, elements);
        }

        private void GUIFooterElement(BasePlayer player, string elUiId)
        {
            
            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5", 
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalLeftBoundary + " " + globalBottomBoundary,
                    AnchorMax = globalRighttBoundary + " " + (globalBottomBoundary+eFootHeight)
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "shunt.close",
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

        private void GUIMainElement(BasePlayer player, string elUiId, ScavengerManager sm)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalLeftBoundary + " " + (globalBottomBoundary+eFootHeight+globalSpace),
                    AnchorMax = globalRighttBoundary + " " + (globalTopBoundary-eHeadHeight-globalSpace)
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Your Quest Score: " + sm.questsPoints,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = "0.005 0.9",
                        AnchorMax = "0.995 0.995"
                    }
            }, elUiId);

            if (sm.quests.Count == 0)
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Create a new Scavenger Quest!",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.005 0.9",
                        AnchorMax = "0.995 0.995"
                    }
                }, elUiId);

                int buttonNum = Cfg.huntLevels.Count;
                float localButtonWidth = (0.90f / buttonNum);
                float localLeft = 0.005f;

                foreach (ZNScavengerLevel sl in Cfg.huntLevels.Values)
                {
                    elements.Add(new CuiButton
                    {
                        Button =
                    {
                        Command = "shunt.generate " + sl.id,
                        Color = "0.3 0.3 0.3 1"
                    },
                        RectTransform =
                        {
                            AnchorMin = (localLeft) +" 0.8",
                            AnchorMax = (localLeft + localButtonWidth) +" 0.895"
                        },
                        Text =
                    {
                        Text = "Type: \n"+sl.id,
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                    }, elUiId);
                    localLeft = localLeft + localButtonWidth + 0.005f;
                }

                CuiHelper.AddUi(player, elements);

                if (sm.lastTempQuest != null)
                { 
                    CuiHelper.DestroyUi(player, mainName + "_quest");
                    GUIQuestElement(player, mainName + "_quest", elUiId, sm.lastTempQuest, eSelectQTop);
                }
                
            }
            // current Quest UI
            else
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Your current Quest!",
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.005 0.9",
                        AnchorMax = "0.995 0.995"
                    }
                }, elUiId);

                CuiHelper.AddUi(player, elements);
                if (sm.CurrentQuest() != null)
                {
                    CuiHelper.DestroyUi(player, mainName + "_quest");
                    GUIQuestElement(player, mainName + "_quest", elUiId, sm.CurrentQuest(), eActiveQTop);
                }
            }

        }

        private void GUIQuestElement(BasePlayer player, string elUiId, string parentUiId, ZNScavengingQuest q, float top)
        {

            var elements = new CuiElementContainer();
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
                    AnchorMax = "1 "+ top
                },
                CursorEnabled = true
            }, parentUiId, elUiId);

            string questTitle = "Quest: ID" + q.id + " Level: " + q.level;
            if (q.isComplete) questTitle += " <color=lime>COMPLETED!</color>";
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = questTitle,
                    FontSize = 16,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.9",
                    AnchorMax = "0.995 0.995"
                }
            }, elUiId);


            Dictionary<string, int> playerItems = GetPlayerItems(player);

            int buttonNum = 10;
            float localButtonWidth = (0.9f / buttonNum);
            float localLeft = 0.01f;
            float localButtonHeight = 0.16f;
            float localtop = 0.85f;

            foreach (KeyValuePair<string, int> itm in q.itemsToFind)
            {
                localtop = 0.85f;
                string itemName = ItemManager.itemDictionaryByName[itm.Key].displayName.translated;
                // Images
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = elUiId,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage(itm.Key) },
                        new CuiRectTransformComponent {
                            AnchorMin = (localLeft) +" " + (localtop-localButtonHeight),
                            AnchorMax = (localLeft + localButtonWidth) +" "+localtop
                        }
                    }
                });


                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "x"+itm.Value,
                    FontSize = 18,
                    Align = TextAnchor.LowerRight,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = (localLeft) +" " + (localtop-localButtonHeight),
                    AnchorMax = (localLeft + localButtonWidth) +" "+localtop
                }
                }, elUiId);

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = itemName,
                    FontSize = 9,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = (localLeft) +" " + (localtop-localButtonHeight-0.05f),
                    AnchorMax = (localLeft + localButtonWidth) +" "+ (localtop-localButtonHeight)
                }
                }, elUiId);

                if (playerItems.ContainsKey(itm.Key))
                {
                    localtop -= localButtonHeight;
                    elements.Add(new CuiLabel
                    {
                        Text =
                    {
                        Text = "You Have: " + playerItems[itm.Key],
                        FontSize = 12,
                        Align = TextAnchor.LowerCenter,
                        Color = "0 1 0 1"
                    },
                        RectTransform =
                    {
                        AnchorMin = (localLeft) +" " + (localtop-0.08f),
                        AnchorMax = (localLeft + localButtonWidth) +" "+localtop
                    }
                    }, elUiId);
                }
                

                localLeft = localLeft + localButtonWidth + 0.005f;
            }

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Reward:\n - <color=orange>" + q.promisedReward + "</color> Blood \n - <color=orange>" + q.promisedQuestPoints + "</color> Quest Score (for /hs)",
                    FontSize = 16,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.3",
                    AnchorMax = "0.995 0.6"
                }
            }, elUiId);

            if(top == eSelectQTop)
            {
                elements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = "shunt.accept",
                            Color = "0.7 0.38 0 1"
                        },
                    RectTransform =
                            {
                                AnchorMin = "0.4 0.2",
                                AnchorMax = "0.6 0.3"
                            },
                    Text =
                        {
                            Text = "Accept Quest!",
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                }, elUiId);
            }
            else
            {
                if (q.isComplete)
                {
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "shunt.finish",
                            Color = "0.7 0.38 0 1"
                        },
                        RectTransform =
                            {
                                AnchorMin = "0.4 0.2",
                                AnchorMax = "0.6 0.3"
                            },
                        Text =
                        {
                            Text = "Get Reward!",
                            FontSize = 14,
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
                            Command = "shunt.abandon",
                            Color =  "0.3 0.3 0.3 1"
                        },
                        RectTransform =
                            {
                                AnchorMin = "0.2 0.2",
                                AnchorMax = "0.45 0.3"
                            },
                        Text =
                        {
                            Text = "Abandon Quest\n(No Refunds!)",
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, elUiId);

                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "shunt.pay",
                            Color = "0.7 0.38 0 1"
                        },
                        RectTransform =
                            {
                                AnchorMin = "0.55 0.2",
                                AnchorMax = "0.8 0.3"
                            },
                        Text =
                        {
                            Text = "Pay Items",
                            FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, elUiId);

                }
            }
            
            CuiHelper.AddUi(player, elements);
        }
        #endregion

    }
}   
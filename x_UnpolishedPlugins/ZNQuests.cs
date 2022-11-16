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
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("ZNQuests", "DocValerian", "1.2.4")]
    class ZNQuests : RustPlugin
    {
        static ZNQuests Plugin;

        [PluginReference]
        private Plugin ImageLibrary, ServerRewards, InfoAPI, ZNExperience, Clans;

        const string permUse = "znquests.use";
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
            Puts("Loaded default configuration file");
        }
        static Configuration Cfg = new Configuration();
        class Configuration
        {
            public int questAmount = 10;
            public int questAmountEasy = 3;
            public int questAmountNormal = 4;
            public int questAmountHard = 3;
            public string costItem = "bleach";
            public int rerollRPCost = 20000;
            public bool useClans = true;
            public int clanMinMembers = 3;
            public float clanQuestMemberFactor = 0.5f;
            public int globalQuestClaimThreshold = 4;
            public int globalRewardCap = 40000;
            public int globalRewardPerContributor = 750;

        }
        class StoredData
        {
            public List<ulong> PvpAdmins = new List<ulong>();
        }

        StoredData storedData;
        void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ZNQuests");
            SaveData(storedData);

            nfi.NumberDecimalSeparator = ",";
            nfi.NumberGroupSeparator = ".";
            nfi.NumberDecimalDigits = 0;
        }
        private void OnServerInitialized()
        {
            costItemId = ItemManager.itemDictionaryByName[Cfg.costItem].itemid;
            foreach (var p in BasePlayer.activePlayerList)
            {
                QuestManager.Get(p.userID);
            }
            PopulateRewardList();
            ImageLibrary?.Call("ImportImageList", "xpimg", new Dictionary<string, string>() { ["rp_img"] = "http://m2v.eu/i/zn/rp1.png", ["xp_img"] = "http://m2v.eu/i/zn/xp.png" });
            _gm = GlobalQuestManager.Get();
        }
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(player);
                killUI(player);

            }
            _gm.SaveData();
        }

        private static void LoadData<T>(out T data, string filename = null)
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? "ZNQuests");
            }
            catch (JsonException ex)
            {
                Plugin.Puts("E1:" + ex.Message);
                data = default(T);
            }
            catch (Exception ex)
            {
                Plugin.Puts("E2: " + ex.Message + "\n" + ex.InnerException);
                data = default(T);
            }
        }

        private static void SaveData<T>(T data, string filename = null) =>
            Interface.Oxide.DataFileSystem.WriteObject(filename ?? "ZNQuests", data);

        #endregion

        #region Lists and Clases
        private int costItemId;
        private Dictionary<ulong, QuestManager> _qm = new Dictionary<ulong, QuestManager>();
        private GlobalQuestManager _gm = null;
        private static List<ZQuest> AvailableEasyQuests = new List<ZQuest>
        {
            // kill quests
            new ZQuest{ type = "kill", item = "chicken",                difficulty = "easy", amountGoal = 3, desc = "Hunt and kill a number of Chicken"  },
            new ZQuest{ type = "kill", item = "wolf",                   difficulty = "easy", amountGoal = 5, desc = "Hunt and kill a number of Wolfs"  },
            //item list kills                                           difficulty = "easy", 
            new ZQuest{ type = "kill", item = "itemRoadsigns",          difficulty = "easy", amountGoal = 10, desc = "Destroy a number of Roadsigns" },
            new ZQuest{ type = "kill", item = "itemBarrels",            difficulty = "easy", amountGoal = 20, desc = "Destroy a number of Barrels" },
            new ZQuest{ type = "kill", item = "itemNPC",                difficulty = "easy", amountGoal = 20,desc = "Kill a number of Scientists or Zombies (no Dwellers!)" },
            new ZQuest{ type = "kill", item = "itemBear",               difficulty = "easy", amountGoal = 2, desc = "Hunt and kill a number of Bears or Polarbears"  },
            // farm quests                                              difficulty = "easy", 
            new ZQuest{ type = "farm", item = "stones",                 difficulty = "easy", amountGoal = 10,desc = "Farm a number of Stone nodes" },
            new ZQuest{ type = "farm", item = "wood",                   difficulty = "easy", amountGoal = 20, desc = "Farm a number of Trees" },
            new ZQuest{ type = "farm", item = "metal.ore",              difficulty = "easy", amountGoal = 10, desc = "Farm a number of Metal nodes" },
            new ZQuest{ type = "farm", item = "sulfur.ore",             difficulty = "easy", amountGoal = 10, desc = "Farm a number of Sulfur nodes" },
            // collect quests                                           difficulty = "easy", 
            new ZQuest{ type = "collect", item = "stones",              difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Stone piles" },
            new ZQuest{ type = "collect", item = "wood",                difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Treestumps" },
            new ZQuest{ type = "collect", item = "metal.ore",           difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Metal piles" },
            new ZQuest{ type = "collect", item = "sulfur.ore",          difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Sulfur piles" },
            new ZQuest{ type = "collect", item = "pumpkin",             difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Pumpkins" },
            new ZQuest{ type = "collect", item = "corn",                difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Corn" },
            new ZQuest{ type = "collect", item = "mushroom",            difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Mushrooms" },
            new ZQuest{ type = "collect", item = "itemBerry",           difficulty = "easy", amountGoal = 10, desc = "Pick up a number of Berries" },
            // catch quests                                             difficulty = "easy", 
            new ZQuest{ type = "catch", item = "fish",                  difficulty = "easy", amountGoal = 5, desc = "Catch a number of (any) Fish" },
            // travel quests                                            difficulty = "easy", 
            new ZQuest{ type = "travel", item = "saddletest",           difficulty = "easy", amountGoal = 500, desc = "Ride a Horse for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "miniheliseat",         difficulty = "easy", amountGoal = 1000, desc = "Pilot a Minicopter for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "transporthelicopilot", difficulty = "easy", amountGoal = 1000, desc = "Co-Pilot a Scrapheli for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "smallboatdriver",      difficulty = "easy", amountGoal = 1000, desc = "Captain a Boat for total distance (mount to unmount point)" },

            new ZQuest{ type = "travel", item = "snowmobiledriverseat",             difficulty = "easy", amountGoal = 1000, desc = "Drive a SnowMobile for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarinesolodriverstanding",      difficulty = "easy", amountGoal = 1000, desc = "Captain a Solo Submarine for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarineduodriverseat",           difficulty = "easy", amountGoal = 1000, desc = "Captain a Duo Submarine for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "modularcardriverseat",             difficulty = "easy", amountGoal = 1000, desc = "Drive a Modular Car for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "itemCarPassenger",                 difficulty = "easy", amountGoal = 1000, desc = "Be a Modular Car passenger for total distance (mount to unmount point)" },

            //modularcardriverseat submarineduodriverseat ...(15:55:02) | [ZNQuests] DEBUG: dismount submarineduopassengerseat ...
        };
        private static List<ZQuest> AvailableQuests = new List<ZQuest>
        {
            // kill quests
            new ZQuest{ type = "kill", item = "boar",                   difficulty = "normal", amountGoal = 10,  desc = "Hunt and kill a number of Boars"  },
            new ZQuest{ type = "kill", item = "chicken",                difficulty = "normal", amountGoal = 6, desc = "Hunt and kill a number of Chicken"  },
            new ZQuest{ type = "kill", item = "wolf",                   difficulty = "normal", amountGoal = 10, desc = "Hunt and kill a number of Wolfs"  },
            new ZQuest{ type = "kill", item = "stag",                   difficulty = "normal", amountGoal = 10, desc = "Hunt and kill a number of Stags" },
            //item list kills                                           difficulty = "normal", 
            new ZQuest{ type = "kill", item = "itemRoadsigns",          difficulty = "normal", amountGoal = 25, desc = "Destroy a number of Roadsigns" },
            new ZQuest{ type = "kill", item = "itemBarrels",            difficulty = "normal", amountGoal = 50, desc = "Destroy a number of Barrels" },
            new ZQuest{ type = "kill", item = "itemNPC",                difficulty = "normal", amountGoal = 100,desc = "Kill a number of Scientists or Zombies (no Dwellers!)" },
            new ZQuest{ type = "kill", item = "itemBear",               difficulty = "normal", amountGoal = 6, desc = "Hunt and kill a number of Bears or Polarbears"  },
            // farm quests                                              difficulty = "normal", 
            new ZQuest{ type = "farm", item = "stones",                 difficulty = "normal", amountGoal = 40,desc = "Farm a number of Stone nodes" },
            new ZQuest{ type = "farm", item = "wood",                   difficulty = "normal", amountGoal = 60, desc = "Farm a number of Trees" },
            new ZQuest{ type = "farm", item = "metal.ore",              difficulty = "normal", amountGoal = 40, desc = "Farm a number of Metal nodes" },
            new ZQuest{ type = "farm", item = "sulfur.ore",             difficulty = "normal", amountGoal = 40, desc = "Farm a number of Sulfur nodes" },
            // collect quests                                           difficulty = "normal", 
            new ZQuest{ type = "collect", item = "stones",              difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Stone piles" },
            new ZQuest{ type = "collect", item = "wood",                difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Treestumps" },
            new ZQuest{ type = "collect", item = "metal.ore",           difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Metal piles" },
            new ZQuest{ type = "collect", item = "sulfur.ore",          difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Sulfur piles" },
            new ZQuest{ type = "collect", item = "pumpkin",             difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Pumpkins" },
            new ZQuest{ type = "collect", item = "corn",                difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Corn" },
            new ZQuest{ type = "collect", item = "mushroom",            difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Mushrooms" },
            new ZQuest{ type = "collect", item = "seed.hemp",           difficulty = "normal", amountGoal = 30, desc = "Pick up a number of Hemp Seeds" },
            new ZQuest{ type = "collect", item = "itemBerry",           difficulty = "normal", amountGoal = 40, desc = "Pick up a number of Berries" },
            // catch quests                                             difficulty = "normal", 
            new ZQuest{ type = "catch", item = "fish",                  difficulty = "normal", amountGoal = 15, desc = "Catch a number of (any) Fish" },
            // travel quests                                            difficulty = "normal", 
            new ZQuest{ type = "travel", item = "saddletest",           difficulty = "normal", amountGoal = 1500, desc = "Ride a Horse for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "miniheliseat",         difficulty = "normal", amountGoal = 5000, desc = "Pilot a Minicopter for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "minihelipassenger",    difficulty = "normal", amountGoal = 4000, desc = "Be passenger in a Minicopter for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "transporthelipilot",   difficulty = "normal", amountGoal = 5000, desc = "Pilot a Scrapheli for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "transporthelicopilot", difficulty = "normal", amountGoal = 4000, desc = "Co-Pilot a Scrapheli for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "smallboatdriver",      difficulty = "normal", amountGoal = 2000, desc = "Captain a Boat for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "smallboatpassenger",   difficulty = "normal", amountGoal = 2000, desc = "Be a Boat/RHIB passenger for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "standingdriver",       difficulty = "normal", amountGoal = 2000, desc = "Captain a RHIB for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "snowmobiledriverseat",             difficulty = "normal", amountGoal = 3500, desc = "Drive a SnowMobile for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "snowmobilepassengerseat tomaha",   difficulty = "normal", amountGoal = 3500, desc = "Be a SnowMobile passenger for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarinesolodriverstanding",      difficulty = "normal", amountGoal = 2500, desc = "Captain a Solo Submarine for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarineduodriverseat",           difficulty = "normal", amountGoal = 2500, desc = "Captain a Duo Submarine for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarineduopassengerseat",        difficulty = "normal", amountGoal = 2500, desc = "Be a Duo Submarine passenger for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "modularcardriverseat",             difficulty = "normal", amountGoal = 3500, desc = "Drive a Modular Car for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "itemCarPassenger",                 difficulty = "normal", amountGoal = 3500, desc = "Be a Modular Car passenger for total distance (mount to unmount point)" },

        };
        private static List<ZQuest> AvailableHardQuests = new List<ZQuest>
        {
            // kill quests
            new ZQuest{ type = "kill", item = "boar",                    difficulty = "hard", amountGoal = 20,  desc = "Hunt and kill a number of Boars"  },
            new ZQuest{ type = "kill", item = "npc_tunneldweller",       difficulty = "hard", amountGoal = 30, desc = "Hunt and kill a number of Tunnel Dwellers"  },
            new ZQuest{ type = "kill", item = "npc_underwaterdweller",   difficulty = "hard", amountGoal = 20, desc = "Hunt and kill a number of Underwater Dwellers"  },
            new ZQuest{ type = "kill", item = "chicken",                 difficulty = "hard", amountGoal = 10, desc = "Hunt and kill a number of Chicken"  },
            new ZQuest{ type = "kill", item = "wolf",                    difficulty = "hard", amountGoal = 20, desc = "Hunt and kill a number of Wolfs"  },
            new ZQuest{ type = "kill", item = "stag",                    difficulty = "hard", amountGoal = 15, desc = "Hunt and kill a number of Stags" },
            //item list kills                                            
            new ZQuest{ type = "kill", item = "itemRoadsigns",           difficulty = "hard", amountGoal = 50, desc = "Destroy a number of Roadsigns" },
            new ZQuest{ type = "kill", item = "itemBarrels",             difficulty = "hard", amountGoal = 100, desc = "Destroy a number of Barrels" },
            new ZQuest{ type = "kill", item = "itemNPC",                 difficulty = "hard", amountGoal = 250,desc = "Kill a number of Scientists or Zombies (no Dwellers!)" },
            new ZQuest{ type = "kill", item = "itemBear",                difficulty = "hard", amountGoal = 15, desc = "Hunt and kill a number of Bears or Polarbears"  },
            // farm quests                                              
            new ZQuest{ type = "farm", item = "stones",                  difficulty = "hard", amountGoal = 80,desc = "Farm a number of Stone nodes" },
            new ZQuest{ type = "farm", item = "wood",                    difficulty = "hard", amountGoal = 100, desc = "Farm a number of Trees" },
            new ZQuest{ type = "farm", item = "metal.ore",               difficulty = "hard", amountGoal = 80, desc = "Farm a number of Metal nodes" },
            new ZQuest{ type = "farm", item = "sulfur.ore",              difficulty = "hard", amountGoal = 80, desc = "Farm a number of Sulfur nodes" },
            // collect quests                                           
            new ZQuest{ type = "collect", item = "stones",               difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Stone piles" },
            new ZQuest{ type = "collect", item = "wood",                 difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Treestumps" },
            new ZQuest{ type = "collect", item = "metal.ore",            difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Metal piles" },
            new ZQuest{ type = "collect", item = "sulfur.ore",           difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Sulfur piles" },
            new ZQuest{ type = "collect", item = "pumpkin",              difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Pumpkins" },
            new ZQuest{ type = "collect", item = "corn",                 difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Corn" },
            new ZQuest{ type = "collect", item = "mushroom",             difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Mushrooms" },
            new ZQuest{ type = "collect", item = "seed.hemp",            difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Hemp Seeds" },
            new ZQuest{ type = "collect", item = "itemBerry",            difficulty = "hard", amountGoal = 80, desc = "Pick up a number of Berries" },
            // catch quests                                             
            new ZQuest{ type = "catch", item = "fish",                   difficulty = "hard", amountGoal = 30, desc = "Catch a number of (any) Fish" },
            // travel quests                                            
            new ZQuest{ type = "travel", item = "saddletest",            difficulty = "hard", amountGoal = 5000, desc = "Ride a Horse for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "miniheliseat",          difficulty = "hard", amountGoal = 10000, desc = "Pilot a Minicopter for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "minihelipassenger",     difficulty = "hard", amountGoal = 10000, desc = "Be passenger in a Minicopter for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "transporthelipilot",    difficulty = "hard", amountGoal = 10000, desc = "Pilot a Scrapheli for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "transporthelicopilot",  difficulty = "hard", amountGoal = 10000, desc = "Co-Pilot a Scrapheli for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "smallboatdriver",       difficulty = "hard", amountGoal = 10000, desc = "Captain a Boat for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "smallboatpassenger",    difficulty = "hard", amountGoal = 10000, desc = "Be a Boat/RHIB passenger for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "standingdriver",        difficulty = "hard", amountGoal = 10000, desc = "Captain a RHIB for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "snowmobiledriverseat",             difficulty = "hard", amountGoal = 10000, desc = "Drive a SnowMobile for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "snowmobilepassengerseat tomaha",   difficulty = "hard", amountGoal = 10000, desc = "Be a SnowMobile passenger for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarinesolodriverstanding",      difficulty = "hard", amountGoal = 10000, desc = "Captain a Solo Submarine for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarineduodriverseat",           difficulty = "hard", amountGoal = 10000, desc = "Captain a Duo Submarine for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "submarineduopassengerseat",        difficulty = "hard", amountGoal = 10000, desc = "Be a Duo Submarine passenger for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "modularcardriverseat",             difficulty = "hard", amountGoal = 10000, desc = "Drive a Modular Car for total distance (mount to unmount point)" },
            new ZQuest{ type = "travel", item = "itemCarPassenger",                 difficulty = "hard", amountGoal = 10000, desc = "Be a Modular Car passenger for total distance (mount to unmount point)" },

        };
        private List<Dictionary<string, int>> rewardList = new List<Dictionary<string, int>>();

        public Dictionary<ulong, Vector3> playerMountPoints = new Dictionary<ulong, Vector3>();
        // multi type items to kill
        public Dictionary<string, string> itemList = new Dictionary<string, string>()
        {

            ["bear"] = "itemBear",
            ["polarbear"] = "itemBear",
            ["roadsign1"] = "itemRoadsigns",
            ["roadsign2"] = "itemRoadsigns",
            ["roadsign3"] = "itemRoadsigns",
            ["roadsign4"] = "itemRoadsigns",
            ["roadsign5"] = "itemRoadsigns",
            ["roadsign6"] = "itemRoadsigns",
            ["roadsign7"] = "itemRoadsigns",
            ["roadsign8"] = "itemRoadsigns",
            ["roadsign9"] = "itemRoadsigns",
            ["loot_barrel_1"] = "itemBarrels",
            ["loot_barrel_2"] = "itemBarrels",
            ["loot-barrel-1"] = "itemBarrels",
            ["loot-barrel-2"] = "itemBarrels",
            ["oil_barrel"] = "itemBarrels",
            ["scientistnpc_heavy"] = "itemNPC",
            ["scientist"] = "itemNPC",
            ["scientistnpc_junkpile_pistol"] = "itemNPC",
            ["scientistnpc_full_lr300"] = "itemNPC",
            ["scientistnpc_full_mp5"] = "itemNPC",
            ["scientistnpc_full_pistol"] = "itemNPC",
            ["scientistnpc_full_any"] = "itemNPC",
            ["scientistnpc_full_shotgun"] = "itemNPC",
            ["scientistnpc"] = "itemNPC",
            ["scientistnpc_oilrig"] = "itemNPC",
            ["scarecrow"] = "itemNPC",
            ["heavyscientist"] = "itemNPC",
            ["heavyscientistad"] = "itemNPC",
            ["scientistnpc_roamtethered"] = "itemNPC",
            ["scientistnpc_roam"] = "itemNPC",
            ["scientistnpc_patrol"] = "itemNPC",
            ["red.berry"] = "itemBerry",
            ["blue.berry"] = "itemBerry",
            ["green.berry"] = "itemBerry",
            ["yellow.berry"] = "itemBerry",
            ["white.berry"] = "itemBerry",
            ["black.berry"] = "itemBerry",
            ["modularcarpassengerseatlesslegroomleft"] = "itemCarPassenger",
            ["modularcarpassengerseatlesslegroomright"] = "itemCarPassenger",
            ["modularcarpassengerseatleft"] = "itemCarPassenger",
            ["modularcarpassengerseatright"] = "itemCarPassenger",
            ["modularcarpassengerseatsidewayleft"] = "itemCarPassenger"

        };

        public class ZQuest
        {
            public string id() { return this.type + "_" + this.item; }
            public string type { get; set; }
            public string item { get; set; }
            public int amountGoal { get; set; }
            public int amountDone { get; set; } = 0;
            public string desc { get; set; }

            public bool completed { get; set; } = false;
            public bool claimed { get; set; } = false;
            public string difficulty { get; set; } = "normal";

            public ZQuest()
            {
            }
            public ZQuest(ZQuest other)
            {
                type = other.type;
                item = other.item;
                amountGoal = other.amountGoal;
                amountDone = 0;
                desc = other.desc;
                completed = false;
                claimed = false;
                difficulty = other.difficulty;
            }

        }
        private class QuestManager
        {
            public ulong playerID;
            private BasePlayer bPlayer;
            public Dictionary<string, ZQuest> questMap = new Dictionary<string, ZQuest>();
            public DateTime questDay = DateTime.Now;
            public int questGenerationCounter = 0;
            public int questsCompleted = 0;
            public int questsClaimed = 0;
            public int lifetimeCompleted = 0;
            public int lifetimeClaimed = 0;
            public int lifetimeClanContributed = 0;
            public int lifetimeClanClaimed = 0;
            public int lifetimeGlobalContributed = 0;
            public int lifetimeGlobalClaimed = 0;
            public bool hasRerolled = false;
            public bool hasClanContributed = false;
            public bool hasClanClaimed = false;
            public bool hasClanClaimedYesterday = false;
            public bool hasGlobalClaimed = false;
            public bool hasGlobalClaimedYesterday = false;
            public string PlayerClan = "";

            public QuestManager(ulong playerId) : base()
            {
                playerID = playerId;
            }
            public static QuestManager Get(ulong playerId, bool wipe = false)
            {
                if (Plugin._qm.ContainsKey(playerId) && !wipe)
                    return Plugin._qm[playerId];

                var fileName = $"{Plugin.Name}/{playerId}";

                QuestManager manager;
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(fileName) && !wipe)
                {
                    // Load existing Data
                    ZNQuests.LoadData(out manager, fileName);
                    if (!(manager is QuestManager)) return null;
                }
                else
                {
                    // Create a completely new Playerdataset
                    manager = new QuestManager(playerId);
                    ZNQuests.SaveData(manager, fileName);
                }

                Interface.Oxide.DataFileSystem.GetDatafile(fileName).Settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Populate
                };
                manager.bPlayer = BasePlayer.FindAwakeOrSleeping(playerId.ToString());
                Plugin._qm[playerId] = manager;
                manager.checkQuestsToday();

                return manager;
            }

            public void SaveData()
            {
                ZNQuests.SaveData(this, $"{Plugin.Name}/{playerID}");
            }

            public void checkQuestsToday()
            {
                // check if list is empty (first launch)
                if (questMap == null || questMap.Count == 0)
                {
                    createNewQuestMap();
                }
                // create new quest list
                if (questDay.Day != DateTime.Now.Day)
                {
                    createNewQuestMap();
                    hasRerolled = false;
                    hasClanContributed = false;
                    hasClanClaimed = false;
                    hasClanClaimedYesterday = false;
                    hasGlobalClaimed = false;
                    hasGlobalClaimedYesterday = false;
                }
            }
            private void createNewQuestMap()
            {
                // reset all the stats
                if (questMap == null) questMap = new Dictionary<string, ZQuest>();
                questGenerationCounter = 0;
                questMap.Clear();
                questsClaimed = 0;
                questsCompleted = 0;
                questDay = DateTime.Now;
                int travelCount = 0;
                int i = 0;
                //reroll quests
                while (i < Cfg.questAmountEasy)
                {
                    Random rand = new Random((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
                    ZQuest randomQuest = new ZQuest(ZNQuests.AvailableEasyQuests.ElementAt(rand.Next(0, ZNQuests.AvailableEasyQuests.Count)));
                    // don't add the same quest twice
                    if (questMap.ContainsKey(randomQuest.id())) continue;
                    if(randomQuest.type == "travel")
                    {
                        if (travelCount >= 2) continue;
                        travelCount++;
                    }

                    questMap.Add(randomQuest.id(), randomQuest);
                    questGenerationCounter++;
                    i++;
                }
                i = 0;
                while (i < Cfg.questAmountNormal)
                {
                    Random rand = new Random((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
                    ZQuest randomQuest = new ZQuest(ZNQuests.AvailableQuests.ElementAt(rand.Next(0, ZNQuests.AvailableQuests.Count)));

                    // don't add the same quest twice
                    if (questMap.ContainsKey(randomQuest.id())) continue;
                    if (randomQuest.type == "travel")
                    {
                        if (travelCount >= 2) continue;
                        travelCount++;
                    }

                    questMap.Add(randomQuest.id(), randomQuest);
                    questGenerationCounter++;
                    i++;
                }
                i = 0;
                while (i < Cfg.questAmountHard)
                {
                    Random rand = new Random((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
                    ZQuest randomQuest = new ZQuest(ZNQuests.AvailableHardQuests.ElementAt(rand.Next(0, ZNQuests.AvailableHardQuests.Count)));

                    // don't add the same quest twice
                    if (questMap.ContainsKey(randomQuest.id())) continue;
                    if (randomQuest.type == "travel")
                    {
                        if (travelCount >= 2) continue;
                        travelCount++;
                    }

                    questMap.Add(randomQuest.id(), randomQuest);
                    questGenerationCounter++;
                    i++;
                }
                SaveData();
                Plugin.SendReply(bPlayer, "<color=green>[ZN-Quests]</color> New Daily Quests are available! Check /q");

            }

            public bool HasActiveQuest(string type, string item)
            {
                if (questMap.Count == 0)
                {
                    createNewQuestMap();
                }
                return questMap.ContainsKey(type + "_" + item) && !questMap[type + "_" + item].completed;
            }
            public bool HasQuest(string id)
            {
                if (questMap.Count == 0)
                {
                    createNewQuestMap();
                }
                return questMap.ContainsKey(id) && !questMap[id].claimed;
            }

            public bool CanReroll()
            {
                return questsClaimed == 0;
            }

            public void RerollQuests()
            {
                createNewQuestMap();
                hasRerolled = true;
            }

            public void AddQuestProgress(string type, string item, int amount)
            {
                string qid = type + "_" + item;
                if (!questMap.ContainsKey(qid) || questMap[qid].completed) return;
                questMap[qid].amountDone += amount;
                // completed quest logic
                if (questMap[qid].amountDone >= questMap[qid].amountGoal)
                {
                    questMap[qid].amountDone = questMap[qid].amountGoal;
                    questMap[qid].completed = true;
                    questsCompleted++;
                    lifetimeCompleted++;
                    Plugin.InfoAPI.Call("ShowInfoPopup", bPlayer, "You completed a Daily Quest!\nCheck /q!");
                }

            }
            public void Claim(string id)
            {
                if (questMap.ContainsKey(id))
                {
                    questMap[id].claimed = true;
                    questsClaimed++;
                    lifetimeClaimed++;
                    if(!hasGlobalClaimed) Plugin._gm.AddContribution(bPlayer, this);
                }
            }



        }
        private class GlobalQuestManager
        {
            public Dictionary<ulong,int> todayUnclaimed = new Dictionary<ulong, int>();
            public Dictionary<ulong, int> yesterdayUnclaimed = new Dictionary<ulong, int>();
            public DateTime questDay = DateTime.Now;

            public int todayContributors = 0;
            public int yesterdayContributors = 0;


            public GlobalQuestManager() : base()
            {
            }
            public static GlobalQuestManager Get(bool wipe = false)
            {
                if (Plugin._gm != null && !wipe)

                    return Plugin._gm;

                var fileName = $"{Plugin.Name}/global/globalData";

                GlobalQuestManager manager;
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(fileName) && !wipe)
                {
                    // Load existing Data
                    ZNQuests.LoadData(out manager, fileName);
                    if (!(manager is GlobalQuestManager)) return null;
                }
                else
                {
                    // Create a completely new Playerdataset
                    manager = new GlobalQuestManager();
                    ZNQuests.SaveData(manager, fileName);
                }

                Interface.Oxide.DataFileSystem.GetDatafile(fileName).Settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Populate
                };
                Plugin._gm = manager;
                manager.checkQuestsToday();

                return manager;
            }

            public void SaveData()
            {
                ZNQuests.SaveData(this, $"{Plugin.Name}/global/globalData");
            }


            public void checkQuestsToday()
            {
                if (todayUnclaimed == null) todayUnclaimed = new Dictionary<ulong, int>();
                if (yesterdayUnclaimed == null) yesterdayUnclaimed = new Dictionary<ulong, int>();
                // create new quest list
                if (questDay.Day != DateTime.Now.Day)
                {
                    RefreshDay();
                }
            }

            private void RefreshDay()
            {
                yesterdayContributors = todayContributors;
                yesterdayUnclaimed.Clear();
                foreach (KeyValuePair<ulong, int> cont in todayUnclaimed)
                {
                    yesterdayUnclaimed.Add(cont.Key, cont.Value);
                }
                todayContributors = 0;
                todayUnclaimed.Clear();
                questDay = DateTime.Now;
                SaveData();
            }

            public int GetCurrentBonus(BasePlayer player, bool yesterday = false)
            {
                int bonus = 0;
                int claims;
                int contribCnt;

                if (yesterday)
                {
                    if (!CanClaimYesterday(player)) return bonus;
                    claims = yesterdayUnclaimed[player.userID];
                    contribCnt = yesterdayContributors;
                }
                else
                {
                    if (!CanClaim(player)) return bonus;
                    claims = todayUnclaimed[player.userID];
                    contribCnt = todayContributors;
                }

                bonus = (int) Math.Ceiling(contribCnt * Cfg.globalRewardPerContributor * (claims / 10f));

                if (bonus > Cfg.globalRewardCap) bonus = Cfg.globalRewardCap;

                return bonus;
            }

            public int GetCurrentSharePercent(BasePlayer player)
            {
                if (todayUnclaimed.ContainsKey(player.userID))
                {
                    return ((todayUnclaimed[player.userID] >= Cfg.globalQuestClaimThreshold) ? todayUnclaimed[player.userID] * 10 : 0);
                }
                else
                {
                    return 0;
                }
            }


            public bool CanClaim(BasePlayer player)
            {
                return (todayUnclaimed.ContainsKey(player.userID) && todayUnclaimed[player.userID] >= Cfg.globalQuestClaimThreshold);
            }
            public bool CanClaimYesterday(BasePlayer player)
            {
                return (yesterdayUnclaimed.ContainsKey(player.userID) && yesterdayUnclaimed[player.userID] >= Cfg.globalQuestClaimThreshold);
            }



            public void Claim(BasePlayer player, QuestManager qm)
            {
                if (!CanClaim(player)) return;

                qm.lifetimeGlobalClaimed++;
                qm.hasGlobalClaimed = true;
                qm.SaveData();
                int currentBonus = GetCurrentBonus(player);
                todayUnclaimed.Remove(player.userID);
                SaveData();

                Plugin.ZNExperience?.Call("AddXp", player.userID, currentBonus);
                Plugin.Puts("INFO: " + player + " claimed today's Global Bonus of " + currentBonus);
            }

            public void ClaimYesterday(BasePlayer player, QuestManager qm)
            {
                if (!CanClaimYesterday(player)) return;

                qm.lifetimeGlobalClaimed++;
                qm.hasGlobalClaimedYesterday = true;
                qm.SaveData();
                int currentBonus = GetCurrentBonus(player, true);
                yesterdayUnclaimed.Remove(player.userID);
                SaveData();

                Plugin.ZNExperience?.Call("AddXp", player.userID, currentBonus);
                Plugin.Puts("INFO: " + player + " claimed yesterday's Global Bonus of " + currentBonus);
            }

            public void AddContribution(BasePlayer player, QuestManager qm)
            {
                if (todayUnclaimed.ContainsKey(player.userID))
                {
                    todayUnclaimed[player.userID]++;
                    if (todayUnclaimed[player.userID] == Cfg.globalQuestClaimThreshold)
                    {
                        todayContributors++;
                        qm.lifetimeGlobalContributed++;
                        qm.SaveData();
                        Plugin.Puts("INFO: " + player + " contributed " + Cfg.globalQuestClaimThreshold + " quest claims to Global");
                    }
                }
                else
                {
                    todayUnclaimed.Add(player.userID, 1);
                }
                SaveData();
            }
        }

        #endregion

        #region Hooks

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player is NPCPlayer || player is HumanNPC || player.IsNpc) return;
            if (!_qm.ContainsKey(player.userID))
            {
                _qm[player.userID] = QuestManager.Get(player.userID);
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            killUI(player);

            if (_qm.ContainsKey(player.userID))
            {
                _qm[player.userID].SaveData();
                _qm.Remove(player.userID);
            }
        }



        private void OnEntityDeath(BaseCombatEntity victimEntity, HitInfo info)
        {
            // Ignore - there is no victim for some reason
            if (victimEntity == null)
                return;

            // Try to avoid error when entity was destroyed
            if (victimEntity.gameObject == null)
                return;

            // barrel quest
            var name = victimEntity.ShortPrefabName;
            if (name != null)
            {
                if (info != null && info.Initiator != null)
                {
                    if (!(info.Initiator is BasePlayer) || (info.Initiator as BasePlayer).IsNpc) return;

                    if (info.Initiator is BasePlayer)
                    {
                        try
                        {
                            BasePlayer bplayer = info.Initiator.ToPlayer();
                            if (bplayer != null)
                                questProgress(bplayer, "kill", name);

                        }
                        catch { }
                    }
                }

            }
        }

        Item OnFishCatch(Item fish, BaseFishingRod fishingRod, BasePlayer player)
        {
            if (fish.info == null || fish.info.shortname == null) return fish;
            questProgress(player, "catch", "fish");
            return fish;
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity as BasePlayer;
            if (player == null) return;
            questProgress(player, "farm", item.info?.shortname);


        }

        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (player == null) return null;
            Item item = collectible.GetItem();
            for (int i = 0; i < collectible.itemList.Length; i++)
            {
                ItemAmount itemAmount = collectible.itemList[i];
                string itemName = itemAmount.itemDef.shortname; 
                questProgress(player, "collect", itemName);

            }
            return null;
        }


        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (playerMountPoints.ContainsKey(player.userID))
            {
                playerMountPoints[player.userID] = entity.transform.position;
            }
            else
            {
                playerMountPoints.Add(player.userID, entity.transform.position);
            }
        }
        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (playerMountPoints.ContainsKey(player.userID))
            {
                int distance = (int)Math.Floor(Vector3Ex.Distance2D(entity.transform.position, playerMountPoints[player.userID]));
                playerMountPoints.Remove(player.userID);
                questProgress(player, "travel", entity.ShortPrefabName, distance);
                //Puts("DEBUG: dismount " + entity.ShortPrefabName + " ...");
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("znquests.admreset")]
        private void CmdAdmReset(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (!player.IsAdmin) return;
            killUI(player);

            if (_qm.ContainsKey(player.userID))
            {
                _qm[player.userID] = QuestManager.Get(player.userID, true);
            }
        }

        [ConsoleCommand("znquests.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;

            killUI(player);

            if (_qm.ContainsKey(player.userID))
            {
                _qm[player.userID].SaveData();
            }
        }

        [ConsoleCommand("znquests.claim")]
        private void CmdClaim(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)// || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string qid = arg.GetString(0);
            string reward = arg.GetString(1);
            if (arg.Args.Length == 3)
            {
                qid = arg.GetString(0) + " " + arg.GetString(1);
                reward = arg.GetString(2);
            }
            //check & Update ProfileManager
            if (_qm.ContainsKey(player.userID))
            {
                Item costItem = player.inventory.FindItemID(costItemId);
                QuestManager qm = _qm[player.userID];
                if (qm.questsCompleted == 0 || qm.questsCompleted == qm.questsClaimed) return;
                if (!qm.HasQuest(qid)) return;

                Dictionary<string, int> rl = rewardList.ElementAt(qm.questsClaimed);
                // safety from overbyuing
                if (costItem == null || costItem.amount < 1)
                {
                    InfoAPI.Call("ShowInfoPopup", player, "You don't have any " + Cfg.costItem + "!", true);
                    return;
                }
                if (reward == "rp")
                {
                    if (costItem.amount < rl["rp_reward_item"])
                    {
                        InfoAPI.Call("ShowInfoPopup", player, "It costs " + rl["rp_reward_item"] + " " + Cfg.costItem + " to use this command,\nyou only have " + costItem.amount, true);
                        return;
                    }
                    player.inventory.Take(null, costItemId, rl["rp_reward_item"]);
                    Item blood = ItemManager.CreateByName("blood", rl["rp_reward"]);
                    player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                }
                else if (reward == "xp")
                {
                    if (costItem.amount < rl["xp_reward_item"])
                    {
                        InfoAPI.Call("ShowInfoPopup", player, "It costs " + rl["xp_reward_item"] + " " + Cfg.costItem + " to use this command,\nyou only have " + costItem.amount, true);
                        return;
                    }
                    object bal = ServerRewards?.Call("CheckPoints", player.userID);
                    if (bal == null) return;
                    int playerRP = (int)bal;
                    if (playerRP < rl["xp_reward_rp"])
                    {
                        InfoAPI.Call("ShowInfoPopup", player, "It costs " + rl["xp_reward_rp"] + " RP to use this command, you only have " + playerRP, true);
                        return;
                    }
                    player.inventory.Take(null, costItemId, rl["xp_reward_item"]);
                    ServerRewards?.Call("TakePoints", player.userID, rl["xp_reward_rp"]);
                    ZNExperience?.Call("AddXp", player.userID, rl["xp_reward"]);

                }

                qm.Claim(qid);
                reloadUI(player);
                InfoAPI.Call("ShowInfoPopup", player, "You successfully claimed a reward!");

                //_qm[player.userID].Claim(qid);
            }

            reloadUI(player);
        }

        [ConsoleCommand("znquests.reroll")]
        private void CmdReroll(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)// || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            //check & Update ProfileManager
            if (_qm.ContainsKey(player.userID))
            {
                QuestManager qm = _qm[player.userID];
                
                if (qm.questsClaimed != 0)
                {
                    InfoAPI.Call("ShowInfoPopup", player, "You can only reroll when you haven't claimed rewards yet!", true);
                    return;
                }
                // cost after reroll once
                if (qm.hasRerolled)
                {
                    object bal = ServerRewards?.Call("CheckPoints", player.userID);
                    if (bal == null) return;
                    int playerRP = (int)bal;
                    if (playerRP < Cfg.rerollRPCost)
                    {
                        InfoAPI.Call("ShowInfoPopup", player, "It costs " + Cfg.rerollRPCost + " RP to use this command again, you only have " + playerRP, true);
                        return;
                    }
                    ServerRewards?.Call("TakePoints", player.userID, Cfg.rerollRPCost);
                }
                qm.RerollQuests();
                reloadUI(player);
                InfoAPI.Call("ShowInfoPopup", player, "You successfully rerolled your Quests!");

            }

            reloadUI(player);
        }

        
        [ConsoleCommand("znquests.globalclaim")]
        private void CmdGlobalClaim(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)// || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            //check & Update ProfileManager
            if (_qm.ContainsKey(player.userID))
            {
                QuestManager qm = _qm[player.userID];
                
                GlobalQuestManager gm = GlobalQuestManager.Get();

                if (!gm.CanClaim(player))
                {
                    InfoAPI.Call("ShowInfoPopup", player, "You can't run this command yet or again today!", true);
                    return;
                }

                gm.Claim(player, qm);
                reloadUI(player);
                InfoAPI.Call("ShowInfoPopup", player, "You successfully claimed to your Global Bonus!");

            }
        }
        [ConsoleCommand("znquests.globalclaimyesterday")]
        private void CmdGlobalClaimYesterday(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)// || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            //check & Update
            if (_qm.ContainsKey(player.userID))
            {
                QuestManager qm = _qm[player.userID];

                GlobalQuestManager gm = GlobalQuestManager.Get();

                if (!gm.CanClaimYesterday(player))
                {
                    InfoAPI.Call("ShowInfoPopup", player, "You can't run this command yet or again today!", true);
                    return;
                }

                gm.ClaimYesterday(player, qm);
                reloadUI(player);
                InfoAPI.Call("ShowInfoPopup", player, "You successfully claimed to your Clan Bonus!");

            }
        }


        [ChatCommand("q")]
        void MainCommand(BasePlayer player, string command, string[] args)
        {
            
            if (!_qm.ContainsKey(player.userID))
            {
                _qm[player.userID] = QuestManager.Get(player.userID);
            }
            reloadUI(player);
        }

        #endregion

        #region Functions
        private void PopulateRewardList()
        {
            rewardList.Clear();
            for (int i = 1; i <= Cfg.questAmount; i++)
            {
                rewardList.Add(new Dictionary<string, int>()
                {
                    ["rp_reward"] = i * 1500,
                    ["xp_reward"] = i * 1500,
                    ["rp_reward_item"] = i * 3,
                    ["xp_reward_item"] = i * 5,
                    ["xp_reward_rp"] = i * 3000
                });
            }
            rewardList.Add(new Dictionary<string, int>()
            {
                ["rp_reward"] = 0,
                ["xp_reward"] = 0,
                ["rp_reward_item"] = 0,
                ["xp_reward_item"] = 0,
                ["xp_reward_rp"] = 0
            });
        }

        private void questProgress(BasePlayer player, string type, string itemName, int amount = 1)
        {
            if (!_qm.ContainsKey(player.userID)) return;

            //Puts("DEBUG: Player " + player + " did: " + type + " with: " + itemName);
            if (itemList.ContainsKey(itemName)) { itemName = itemList[itemName]; }

            if (_qm[player.userID].HasActiveQuest(type, itemName))
            {
                _qm[player.userID].AddQuestProgress(type, itemName, amount);
            }

        }

        private void showInfoText(BasePlayer player)
        {
            string infoText = "<color=orange>=== ZN Trade (v" + Plugin.Version + " ALPHA) ===</color>";
            infoText += "\n Commands to create and manage your own PVP challenge";
            infoText += "\n A team of attackers and defenders fight against each other";
            infoText += "\n THIS IS AN ALPHA Plugin!";

            infoText += "\n\n<color=green>Usage:</color>";
            infoText += "\n/pvpevent \t\t\t\tDisplays the command info";
            infoText += "\n/pvpevent score \t\t\tShow score screen";
            infoText += "\n/pvpevent join a\t\t\tJoin the attackers";
            //infoText += "\n/pvpevent join d\t\t\tJoin the defenders (currently not allowed)";
            infoText += "\n/pvpevent leave\t\t\tLeave the Event";
            infoText += "\n/pvpevent tcmd auth\t\tAdd your group (a/d) to /tcmd";
            infoText += "\n/pvpevent hcmd auth\t\tAdd your group (a/d) to /hcmd";
            infoText += "\n\n<color=green>GM commands:</color>";
            infoText += "\n/pvpevent start \t\t\tStart the event";
            infoText += "\n/pvpevent stop \t\t\tEnd the event";
            infoText += "\n/pvpevent a add <player> \tAdd <player> to attackers";
            infoText += "\n/pvpevent d add <player> \tAdd <player> to defenders";
            SendReply(player, infoText);

        }
        #endregion

        #region GUI

        private const string globalNoErrorString = "none";
        private const string mainName = "ZNQuests";
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements =
        {
            mainName+"_head",
            mainName+"_foot",
            mainName+"_main",
            mainName+"_menu",
            mainName+"_clan",
            mainName+"_global",
            mainName+"_info"
        };
        private static float globalSpace = 0.01f;
        private static float globalLeftBoundary = 0.1f;
        private static float globalRighttBoundary = 0.9f;
        private static float globalMenuEnd = globalLeftBoundary + 0.1f;
        private static float globalMainStart = globalMenuEnd + globalSpace;
        private static float globalMainEnd = globalMainStart + 0.4f;
        private static float globalClanStart = globalMainEnd + globalSpace;
        private static float globalTopBoundary = 0.99f;
        private static float globalBottomBoundary = 0.05f;
        private static float eContentWidth = 0.395f;
        private static float eHeadHeight = 0.05f;
        private static float eFootHeight = 0.05f;
        private static float eSlotHeight = 0.08f;
        private int baseFontSize = 12;


        private void reloadUI(BasePlayer player, string errorMsg = globalNoErrorString) {
            if (!UiPlayers.Contains(player)) {
                UiPlayers.Add(player);
            }
            foreach (string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }
            displayUI(player, errorMsg);

        }
        private void killUI(BasePlayer player) {
            if (UiPlayers.Contains(player)) {
                UiPlayers.Remove(player);
            }
            foreach (string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }
        }

        private void RunUIPreloadUpdates(BasePlayer player, QuestManager qm)
        {
            // check Day reset
            qm.checkQuestsToday();
            _gm.checkQuestsToday();
        }

        private void displayUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (!_qm.ContainsKey(player.userID)) return;
            QuestManager qm = _qm[player.userID];
            RunUIPreloadUpdates(player, qm);

            GUIHeaderElement(player, mainName + "_head", errorMsg);
            GUIMenuElement(player, mainName + "_menu", qm);
            GUIMainElement(player, mainName + "_main", qm);
            GUIGlobalElement(player, mainName + "_global", qm);
            GUIInfoElement(player, mainName + "_info");

            GUIFooterElement(player, mainName + "_foot");
        }
        private void GUIHeaderElement(BasePlayer player, string elUiId, string errorMsg = globalNoErrorString)
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
                    AnchorMin = globalLeftBoundary  +" " + (globalTopBoundary-eHeadHeight),
                    AnchorMax = globalRighttBoundary + " " + globalTopBoundary
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            TimeSpan untilMidnight = DateTime.Today.AddDays(1.0) - DateTime.Now;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ZN Daily Quests -- BETA -- (Day reset in: " + untilMidnight.Hours + "h " + untilMidnight.Minutes + "min)",
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

            if (errorMsg != "none")
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
            }, "Overlay", elUiId);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "znquests.close",
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
        private void GUIMenuElement(BasePlayer player, string elUiId, QuestManager qm)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.14 0.13 0.11 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalLeftBoundary + " " + (globalBottomBoundary+eFootHeight+globalSpace),
                    AnchorMax = globalMenuEnd + " " + (globalTopBoundary-eHeadHeight-globalSpace)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);


            float localLeftBoundary = 0.05f;
            float localRightBoundary = 0.95f;
            float localContentStart = 0.98f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Your Stats",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.5 0.8 0.0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.04f;


            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Completed:",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = qm.questsCompleted +"/"+qm.questGenerationCounter,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);

            localContentStart -= 0.04f;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Claimed:",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = qm.questsClaimed +"/"+ qm.questsCompleted,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            localContentStart -= 0.04f;

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Lifetime Completed:",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = ""+qm.lifetimeCompleted,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);

            localContentStart -= 0.04f;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Lifetime Claimed:",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = ""+qm.lifetimeClaimed,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            localContentStart -= 0.04f;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Clan Contributions:",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = ""+qm.lifetimeClanContributed,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);

            localContentStart -= 0.04f;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Clan Bonus Claims:",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = ""+qm.lifetimeClanClaimed,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);

            localContentStart -= 0.1f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Options",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.5 0.8 0.0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);

            localContentStart -= 0.04f;

            OptionsButton(elements, elUiId, "znquests.reroll", qm.CanReroll(), "    Reroll daily quests", "    - Only free once.\n    - All progress is lost!", "rp_img", ((qm.hasRerolled) ? Cfg.rerollRPCost : 0), localContentStart);

            localContentStart -= 0.14f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Reward",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.5 0.8 0.0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);

            localContentStart -= 0.04f;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "#Claim        Blood [OR] XP<color=#dddddd>"
                             + "\n        [1]              1.500"
                             + "\n        [2]              3.000"
                             + "\n        [3]              4.500"
                             + "\n        [4]              6.000"
                             + "\n        [5]              7.500"
                             + "\n        [6]              9.000"
                             + "\n        [7]             10.500"
                             + "\n        [8]             12.000"
                             + "\n        [9]             13.500"
                             + "\n       [10]            15.000</color>",
                        FontSize = 12,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.3f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);

            //OptionsButton(elements, elUiId, "znquests.reroll", qm.CanReroll(), "    Reroll daily quests", "    - Can only be done once.\n    - All progress is lost!", "rp_img", Cfg.rerollRPCost, localContentStart);


            CuiHelper.AddUi(player, elements);
        }
        private void GUIMainElement(BasePlayer player, string elUiId, QuestManager qm)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.14 0.13 0.11 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalMainStart + " " + (globalBottomBoundary+eFootHeight+globalSpace),
                    AnchorMax = globalMainEnd + " " + (globalTopBoundary-eHeadHeight-globalSpace)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);
            float localTop = 0.995f;

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Your Quest Progress",
                        FontSize = baseFontSize+7,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.5 0.8 0.0 0.8",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.01 "+ (localTop-eSlotHeight),
                        AnchorMax = "0.99 "+ localTop
                    }
            }, elUiId);


            CuiHelper.AddUi(player, elements);
            Dictionary<string, int> rl = rewardList.ElementAt(qm.questsClaimed);
            foreach (ZQuest q in qm.questMap.Values)
            {
                localTop -= eSlotHeight + 0.007f;
                GUIQuestElement(player, elUiId + "_" + q.id(), elUiId, rl, q, localTop);
            }


        }
        private void GUIGlobalElement(BasePlayer player, string elUiId, QuestManager qm)
        {

            float leSlotHeight = 0.1f;
            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.14 0.13 0.11 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalClanStart + " " + (globalBottomBoundary+eFootHeight+globalSpace+0.3f),
                    AnchorMax = globalRighttBoundary + " " + (globalTopBoundary-eHeadHeight-globalSpace)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);
            float localTop = 0.995f;


            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Global Quest Bonus Challenge",
                        FontSize = baseFontSize+7,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.5 0.8 0.0 0.8",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.08f),
                        AnchorMax = "0.95 "+ localTop
                    }
            }, elUiId);

            localTop -= 0.05f;
            GlobalQuestManager gm = GlobalQuestManager.Get();

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Get an extra bonus the more players claim quests and the more quests you claim!",
                        FontSize = baseFontSize+3,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 0.8",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    }
            }, elUiId);

            localTop -= leSlotHeight;

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "To get a Global bonus " 
                            + "you need to claim at least <color=orange>"+Cfg.globalQuestClaimThreshold+"</color> quests."
                            + "\nThe more quests you claim, the higher your bonus share % gets. The more players claim quests, the higher the max bonus gets."
                            + "\nHighest possible Bonus is <color=orange>"+Cfg.globalRewardCap+"</color>.",

                        FontSize = baseFontSize,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 0.8",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-1.5f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    }
            }, elUiId);

            localTop -= 1.5f * leSlotHeight + 0.05f;

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "<color=orange>Current Contributors:</color> " + gm.todayContributors,

                        FontSize = baseFontSize+1,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 0.8",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.5f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    }
            }, elUiId);

            localTop -= 0.5f * leSlotHeight;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "<color=orange>Current Bonus (at 100% share):</color> " + numberCandy(gm.todayContributors * Cfg.globalRewardPerContributor),

                        FontSize = baseFontSize+1,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 0.8",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.5f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    }
            }, elUiId);

            CuiHelper.AddUi(player, elements);

            localTop -= leSlotHeight;

            GUIGlobalQuestElement(player, elUiId + "_prog", elUiId, gm, localTop);

            localTop -= 2 * eSlotHeight;

           
            elements = new CuiElementContainer();

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "<color=orange>Your Current Bonus:</color> " + numberCandy(gm.GetCurrentBonus(player)) +" XP",

                        FontSize = baseFontSize+5,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 0.8",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    }
            }, elUiId);


            localTop -= 0.06f;
            if (qm.hasGlobalClaimed)
            {
                elements.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.7f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    },
                    Text =
                    {
                        Text = "You have already claimed today's reward!",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
            }
            else if (gm.CanClaim(player))
            {
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "znquests.globalclaim",
                        Color = "0.5 0.8 0.0 0.8"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.7f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    },
                    Text =
                    {
                        Text = "Claim NOW!\n<color=#333333>(Or wait for more contributors)</color>",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId, elUiId + "_button");
            }
            else
            {
                elements.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.7f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    },
                    Text =
                    {
                        Text = "<color=lime>You need to claim at least " + Cfg.globalQuestClaimThreshold + " Quests!</color>",
                        FontSize = baseFontSize,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
            }

            localTop -= 0.7f * leSlotHeight +0.01f;
            // Yesterday Button 
            if (qm.hasGlobalClaimedYesterday)
            {
                elements.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.7f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    },
                    Text =
                    {
                        Text = "You have already claimed yesterday's reward!",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
            }
            else if (gm.CanClaimYesterday(player))
            {
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "znquests.globalclaimyesterday",
                        Color = "0.5 0.8 0.0 0.8"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.7f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    },
                    Text =
                    {
                        Text = "Claim yesterday's reward! ("+numberCandy(gm.GetCurrentBonus(player, true))+" XP)",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId, elUiId + "_button");
            }
            else
            {
                elements.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.05 "+ (localTop-0.7f*leSlotHeight),
                        AnchorMax = "0.95 "+ localTop
                    },
                    Text =
                    {
                        Text = "Nothing to claim for yesterday",
                        FontSize = baseFontSize,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
            }


            CuiHelper.AddUi(player, elements);

        }

        private void GUIInfoElement(BasePlayer player, string elUiId)
        {

            float leSlotHeight = 0.1f;
            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.14 0.13 0.11 0.4",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalClanStart + " " + (globalBottomBoundary+eFootHeight+globalSpace),
                    AnchorMax = globalRighttBoundary + " " + (globalBottomBoundary+eFootHeight+0.3f)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            float localLeftBoundary = 0.05f;
            float localRightBoundary = 0.95f;
            float localContentStart = 0.95f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Info",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.9 0.9 0.9 0.8",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.1f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);

            localContentStart -= 0.11f;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "- Quests collect automatically while playing"
                                +"\n- You get 10 random quests every day, 3 easy, 4 normal, 3 hard (24h server time)"
                                +"\n- You can reroll once for free every day"
                                +"\n- You can only reroll before claiming (progress will be lost!)"
                                +"\n- You can Claim rewards in Blood OR XP"
                                +"\n- Claiming rewards has a price (trade RP for XP or bleach for RP)"
                                +"\n- With each claimed quest the reward & price rises!"
                                +"\n- 'travel' quests only count distance between start and end (don't drive circles!)"
                                +"\n\nGLOBAL BONUS:\n- After "+Cfg.globalQuestClaimThreshold+" claims you can get a Bonus"
                                +"\n- The more players globally contribute, the more XP bonus everyone gets"
                                +"\n- The more quests you claim the higher your Global Bonus share"
                                +"\n- The global bonus is capped at "+ numberCandy( Cfg.globalRewardCap) +" XP per day",
                        FontSize = 10,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (0.1f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);

            CuiHelper.AddUi(player, elements);
        }

        private void GUIQuestElement(BasePlayer player, string elUiId, string parentUiId, Dictionary<string, int> rl, ZQuest q, float localTop)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = "0.01 "+ (localTop-eSlotHeight),
                    AnchorMax = "0.99 "+ localTop
                },
                CursorEnabled = true
            }, parentUiId, elUiId);

            string questColor = "lime";
            if (q.difficulty == "normal") questColor = "orange";
            if (q.difficulty == "hard") questColor = "#cc0000";

            string questTitle = "<color="+ questColor + ">["+q.difficulty+"]</color> " + q.desc;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = questTitle,
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.02 0.5",
                    AnchorMax = "0.98 0.995"
                }
            }, elUiId);

            questTitle ="<color=orange>Remaining: " + (q.amountGoal-q.amountDone) + "</color>";
            if (q.completed) questTitle = " <color=lime>COMPLETED!</color>";
            if (q.claimed) questTitle = " <color=darkgray>CLAIMED!</color>";

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = questTitle,
                    FontSize = 14,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.02 0.5",
                    AnchorMax = "0.98 0.995"
                }
            }, elUiId);

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.9",
                },
                RectTransform =
                {
                    AnchorMin = "0.015 0.1",
                    AnchorMax = "0.985 0.49"
                },
                CursorEnabled = true
            }, elUiId, elUiId + "_bar");

            if(q.claimed)
            {
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text =  "Reward Claimed",
                    FontSize = 9,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
                }, elUiId + "_bar", elUiId + "_bar_text");
            }
            else if (q.completed)
            {

                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "znquests.claim " + q.id() + " rp",
                        Color = "0.5 0.8 0.0 0.8"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.00 0",
                        AnchorMax = "0.45 1"
                    },
                    Text =
                    {
                        Text = "Claim as "+numberCandy(rl["rp_reward"])+" Blood <color=#333333>(Cost: "+numberCandy(rl["rp_reward_item"])+" "+Cfg.costItem+") </color>",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId + "_bar", elUiId + "_bar_buttonr");

                
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text =  "OR",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                            RectTransform =
                    {
                        AnchorMin = "0.451 0",
                        AnchorMax = "0.549 1"
                    }
                }, elUiId + "_bar", elUiId + "_bar_text");
                
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "znquests.claim " + q.id() + " xp",
                        Color = "0.5 0.8 0.0 0.8"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.55 0",
                        AnchorMax = "1 1"
                    },
                    Text =
                    {
                        Text = "Claim as "+numberCandy(rl["xp_reward"])+" XP <color=#333333>(Cost: "+numberCandy(rl["xp_reward_item"])+" "+Cfg.costItem+" + "+numberCandy(rl["xp_reward_rp"])+" RP)</color>",
                        FontSize = 9,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId + "_bar", elUiId + "_bar_buttonx");
            }
            else
            {
                float perOneWidth = 1f / q.amountGoal;
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0.5 0.8 0.0 0.9",
                },
                    RectTransform =
                {
                    AnchorMin = "0.004 0.1",
                    AnchorMax = q.amountDone*perOneWidth + " 0.9"
                },
                    CursorEnabled = true
                }, elUiId + "_bar", elUiId + "_bar_fill");

                int percentDone = (int)Math.Floor(((float)q.amountDone / (float)q.amountGoal) * 100f);
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text =  percentDone+ "%",
                    FontSize = 9,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
                }, elUiId + "_bar", elUiId + "_bar_text");
            }
            


            CuiHelper.AddUi(player, elements);
        }

        private void GUIGlobalQuestElement(BasePlayer player, string elUiId, string parentUiId, GlobalQuestManager gm, float localTop)
        {

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = "0.05 "+ (localTop-1.6f*eSlotHeight),
                    AnchorMax = "0.95 "+ localTop
                },
                CursorEnabled = true
            }, parentUiId, elUiId);
            string questTitle = "<color=orange>Your current Bonus Share: " +  gm.GetCurrentSharePercent(player) + "%</color>";

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = questTitle,
                    FontSize = 14,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.02 0.5",
                    AnchorMax = "0.98 0.995"
                }
            }, elUiId);

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.9",
                },
                RectTransform =
                {
                    AnchorMin = "0.015 0.1",
                    AnchorMax = "0.985 0.49"
                },
                CursorEnabled = true
            }, elUiId, elUiId + "_bar");

            
            float perOneWidth = 1f / 100f;
            int done = gm.GetCurrentSharePercent(player);

            elements.Add(new CuiPanel
            {
                Image =
            {
                Color = "0.5 0.8 0.0 0.9",
            },
                RectTransform =
            {
                AnchorMin = "0.004 0.1",
                AnchorMax = done*perOneWidth + " 0.9"
            },
                CursorEnabled = true
            }, elUiId + "_bar", elUiId + "_bar_fill");

            elements.Add(new CuiLabel
            {
                Text =
            {
                Text =  done+ "%",
                FontSize = 9,
                Align = TextAnchor.MiddleCenter,
                Color = "1 1 1 1"
            },
                RectTransform =
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            }
            }, elUiId + "_bar", elUiId + "_bar_text");
            



            CuiHelper.AddUi(player, elements);
        }

        private void OptionsButton(CuiElementContainer elements, string elUiId, string command, bool onOff, string text, string infotext, string iconname, int price, float localContentStart)
        {
            float optIcon = 0.2f;
            float localLeftBoundary = 0.05f;
            float localRightBoundary = 0.95f;
            float localHeight = 0.06f;

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = command,
                    Color = (onOff) ? "0.7 0.38 0 1" : "0.3 0.3 0.3 0.3"
                },
                RectTransform =
                {
                    AnchorMin = (localLeftBoundary) + " " + (localContentStart-localHeight),
                    AnchorMax = localRightBoundary+" "  + (localContentStart)
                },
                Text =
                {
                    Text = "\n"+text+"\n<color=#333333>"+infotext+"</color>",
                    FontSize = baseFontSize-3,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 1"
                }
            }, elUiId);
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage(iconname) },
                    new CuiRectTransformComponent {
                        AnchorMin = (localRightBoundary - optIcon - 0.01f) + " " + (localContentStart-localHeight+0.02f),
                        AnchorMax = (localRightBoundary- 0.01f) +" " + (localContentStart-0.01f)
                    }
                }
            });
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "x"+numberCandy(price)+"  ",
                    FontSize = baseFontSize-2,
                    Align = TextAnchor.LowerRight,
                    Color = "1 1 1 1",
                },
                RectTransform =
                {
                    AnchorMin = (localRightBoundary - 0.3f) + " " + (localContentStart-localHeight),
                    AnchorMax = (localRightBoundary) +" " + (localContentStart-localHeight+0.02f)
                }
            }, elUiId);

        }
        #endregion
        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }

        private NumberFormatInfo nfi = new CultureInfo("en-GB", false).NumberFormat;
        private string numberCandy(int number)
        {
            return Convert.ToDecimal(number).ToString("N", nfi);
        }
    }
}   
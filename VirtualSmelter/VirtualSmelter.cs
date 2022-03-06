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
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("VirtualSmelter", "DocValerian", "1.3.0")]
    class VirtualSmelter : RustPlugin
    {
        static VirtualSmelter Plugin;

        [PluginReference]
        private Plugin ImageLibrary, ServerRewards;
        private const string permUse = "virtualsmelter.use";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region DataConfig
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
            [JsonProperty(PropertyName = "Fuel consumption per slot level per minute")]
            public int FuelPerSlotLevel = 70;

            [JsonProperty(PropertyName = "Close UI after seconds")]
            public float UiAutoCloseSeconds = 60f;

            [JsonProperty(PropertyName = "Base resource capacity per slot")]
            public int BaseCapacity = 100000;

            [JsonProperty(PropertyName = "Base price per slot level")]
            public int BaseLevelPrice = 1;

            [JsonProperty(PropertyName = "Max Slot Level")]
            public int MaxLevel = 50;
            [JsonProperty(PropertyName = "Fuel consumption multiplier per Speed Upgrade")]
            public float SpeedFuelMultiplyer = 3.0f;

            [JsonProperty(PropertyName = "Base res per ore multiplier (int!)")]
            public Dictionary<string, int> BaseEfficiency = new Dictionary<string, int>()
            {
                ["wood"] = 1,
                ["hqm"] = 1,
                ["metal"] = 1,
                ["sulfur"] = 1
            };
            [JsonProperty(PropertyName = "Base output per minute for each resource (int)")]
            public Dictionary<string, int> OutputPerMinute = new Dictionary<string, int>()
            {
                ["wood"] = 70,
                ["hqm"] = 100,
                ["metal"] = 200,
                ["sulfur"] = 350
            };

            [JsonProperty(PropertyName = "Available upgrade types")]
            public string[] AvailableUpgrades = {"slots", "speed", "fuel", "efficiency"};

            [JsonProperty(PropertyName = "Available resources to use")]
            public string[] AvailableRes = {"hqm", "sulfur", "metal", "wood"};

            [JsonProperty(PropertyName = "Currency to use for upgrading (RP|tcomp|mill|furnace)")]
            public Dictionary<string, string> UpgradeCurrency = new Dictionary<string, string>()
            {
                ["slots"] = "RP",
                ["speed"] = "tcomp",
                ["fuel"] = "mill",
                ["efficiency"] = "RP",
                ["level"] = "furnace",
            };

            [JsonProperty(PropertyName = "Upgrade effect and price configuration")]
            public Dictionary<string, Dictionary<int, Dictionary<string, float>>> Upgrades = new Dictionary<string, Dictionary<int, Dictionary<string, float>>>()
            {
                ["slots"] = new Dictionary<int, Dictionary<string, float>>()
                {
                    [1] = new Dictionary<string, float>()
                    {
                        ["effect"] = 2f,
                        ["price"] = 4000f
                    },
                    [2] = new Dictionary<string, float>()
                    {
                        ["effect"] = 3f,
                        ["price"] = 5000f
                    },
                    [3] = new Dictionary<string, float>()
                    {
                        ["effect"] = 4f,
                        ["price"] = 6000f
                    },
                    [4] = new Dictionary<string, float>()
                    {
                        ["effect"] = 5f,
                        ["price"] = 7000f
                    },
                    [5] = new Dictionary<string, float>()
                    {
                        ["effect"] = 6f,
                        ["price"] = 8000f
                    }
                },
                ["speed"] = new Dictionary<int, Dictionary<string, float>>()
                {
                    [1] = new Dictionary<string, float>()
                    {
                        ["effect"] = 2f,
                        ["price"] = 10f
                    },
                    [2] = new Dictionary<string, float>()
                    {
                        ["effect"] = 3f,
                        ["price"] = 20f
                    },
                    [3] = new Dictionary<string, float>()
                    {
                        ["effect"] = 4f,
                        ["price"] = 30f
                    },
                    [4] = new Dictionary<string, float>()
                    {
                        ["effect"] = 5f,
                        ["price"] = 40f
                    },
                    [5] = new Dictionary<string, float>()
                    {
                        ["effect"] = 6f,
                        ["price"] = 50f
                    },
                    [6] = new Dictionary<string, float>()
                    {
                        ["effect"] =7f,
                        ["price"] = 60f
                    },
                    [7] = new Dictionary<string, float>()
                    {
                        ["effect"] = 8f,
                        ["price"] = 70f
                    },
                    [8] = new Dictionary<string, float>()
                    {
                        ["effect"] = 9f,
                        ["price"] = 80f
                    },
                    [9] = new Dictionary<string, float>()
                    {
                        ["effect"] = 10f,
                        ["price"] = 90f
                    },
                    [10] = new Dictionary<string, float>()
                    {
                        ["effect"] = 11f,
                        ["price"] = 100f
                    }
                },
                ["fuel"] = new Dictionary<int, Dictionary<string, float>>()
                {
                    [1] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.95f,
                        ["price"] = 5f
                    },
                    [2] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.90f,
                        ["price"] = 10f
                    },
                    [3] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.85f,
                        ["price"] = 15f
                    },
                    [4] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.8f,
                        ["price"] = 20f
                    },
                    [5] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.75f,
                        ["price"] = 25f
                    },
                    [6] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.65f,
                        ["price"] = 30f
                    },
                    [7] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.55f,
                        ["price"] = 35f
                    },
                    [8] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.45f,
                        ["price"] = 40f
                    },
                    [9] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.35f,
                        ["price"] = 45f
                    },
                    [10] = new Dictionary<string, float>()
                    {
                        ["effect"] = 0.25f,
                        ["price"] = 50f
                    }
                },
                ["efficiency"] = new Dictionary<int, Dictionary<string, float>>()
                {
                    [1] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.08f,
                        ["price"] = 5000f
                    },
                    [2] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.16f,
                        ["price"] = 5500f
                    },
                    [3] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.24f,
                        ["price"] = 6000f
                    },
                    [4] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.32f,
                        ["price"] = 6500f
                    },
                    [5] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.4f,
                        ["price"] = 7000f
                    },
                    [6] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.48f,
                        ["price"] = 7500f
                    },
                    [7] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.56f,
                        ["price"] = 8000f
                    },
                    [8] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.64f,
                        ["price"] = 8500f
                    },
                    [9] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.72f,
                        ["price"] = 9000f
                    },
                    [10] = new Dictionary<string, float>()
                    {
                        ["effect"] = 1.8f,
                        ["price"] = 9500f
                    }
                }
            };
        }

        private readonly string[] compatibleOvens =
        {
            "furnace",
            "furnace.static",
            "furnace.large"
        };
        private Dictionary<string, string> prefabNames = new Dictionary<string, string>()
        {
            ["stone"] = "stones",
            ["wood"] = "wood",
            ["charcoal"] = "charcoal",
            ["metal"] = "metal.ore",
            ["sulfur"] = "sulfur.ore",
            ["hqm"] = "hq.metal.ore",
            ["crude"] = "crude.oil",
            ["furnace"] = "furnace.large",
            ["tcomp"] = "targeting.computer",
            ["mill"] = "generator.wind.scrap",
            ["metal_out"] = "metal.fragments",
            ["sulfur_out"] = "sulfur",
            ["hqm_out"] = "metal.refined",
            ["wood_out"] = "charcoal",
        };
        
        private Dictionary<string, string> humanNames = new Dictionary<string, string>()
        {
            ["stone"] = "Stones",
            ["wood"] = "Wood",
            ["charcoal"] = "Charcoal",
            ["metal"] = "Metal Ore",
            ["sulfur"] = "Sulfur Ore",
            ["hqm"] = "High Quality Metal Ore",
            ["crude"] = "Crude Oil",
            ["furnace"] = "Large Furnace",
            ["tcomp"] = "Targeting Computer",
            ["mill"] = "Wind Turbine",
            ["RP"] = "RP",
            ["level"] = "Smelter Level",
            ["speed"] = "Extra Smelting Speed",
            ["fuel"] = "Fuel Consumption %",
            ["efficiency"] = "Ore->Resource Efficiency",
            ["slots"] = "Extra Smelting Slots",
            ["effect_level"] = "BaseStats* x",
            ["effect_speed"] = "Output & Fuel (x"+Cfg.SpeedFuelMultiplyer+") x",
            ["effect_fuel"] = "Fuel Consumption x",
            ["effect_efficiency"] = "Resource per 1 Ore x",
            ["effect_slots"] = "Smelting slots x",  
            ["metal_out"] = "Metal Fragments",
            ["sulfur_out"] = "Sulfur",
            ["hqm_out"] = "High Quality Metal",
            ["wood_out"] = "Charcoal",
        };

        private Dictionary<string, int> itemIDMap = new Dictionary<string, int>()
        {
            ["surveycharge"] = 1975934948,
            ["lgf"] = -946369541,
            ["wood"] = -151838493,
            ["charcoal"] = -1938052175,
            ["diesel"] = 1568388703,
            ["mill"] = 	-1819763926,
            ["tcomp"] = 1523195708,
            ["furnace"] = -1992717673,
            ["metal"] = -4031221,
            ["sulfur"] = -1157596551,
            ["hqm"] = -1982036270,
            ["metal_out"] = 69511070,
            ["sulfur_out"] = -1581843485,
            ["hqm_out"] = 317398316,
            ["wood_out"] = -1938052175,

        };
        private Dictionary<string, string> smeltingMap = new Dictionary<string, string>()
        {
            ["metal.ore"] = "metal.fragments",
            ["hq.metal.ore"] = "metal.refined",
            ["sulfur.ore"] = "sulfur",
            ["wood"] = "charcoal"
        };
        
        private string[] uiElements = 
        {
            "VirtualSmelter"+"_head", 
            "VirtualSmelter"+"_control",
            "VirtualSmelter"+"_foot",
            "VirtualSmelter"+"_slot_0",
            "VirtualSmelter"+"_slot_1",
            "VirtualSmelter"+"_slot_2",
            "VirtualSmelter"+"_slot_3",
            "VirtualSmelter"+"_slot_4",
            "VirtualSmelter"+"_slot_5",
            "VirtualSmelter"+"_slot_0_numbers",
            "VirtualSmelter"+"_slot_1_numbers",
            "VirtualSmelter"+"_slot_2_numbers",
            "VirtualSmelter"+"_slot_3_numbers",
            "VirtualSmelter"+"_slot_4_numbers",
            "VirtualSmelter"+"_slot_5_numbers",
            "VirtualSmelter"+"_furnace_info"
        };

        private const string globalNoErrorString = "none";
        
        private Dictionary<ulong, FurnaceManager> _furnaceManager = new Dictionary<ulong, FurnaceManager>();

        private DateTime _wipeTime; 

        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private Dictionary<ulong, Timer> playerTimer = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, DateTime> playerTimerRuntime = new Dictionary<ulong, DateTime>();

        private NumberFormatInfo nfi = new CultureInfo("en-GB", false).NumberFormat;

        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            nfi.NumberDecimalSeparator = ",";
            nfi.NumberGroupSeparator = ".";
            nfi.NumberDecimalDigits  = 0;
        }
        void OnPlayerDisconnected(BasePlayer player) => killUI(player);
        
        void Unload()
        {
            foreach (var player in UiPlayers.ToList())
            {
                killUI(player);
            }
        }
        private static void LoadData<T>(out T data, string filename = null) => 
        data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? "VirtualSmelter");

        private static void SaveData<T>(T data, string filename = null) => 
        Interface.Oxide.DataFileSystem.WriteObject(filename ?? "VirtualSmelter", data);

        #endregion

        #region Commands
        
        [ConsoleCommand("virtualsmelter.upgrade")]
        private void CmdUpgrade(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string errorMsg = globalNoErrorString;
            string upgradeType = arg.GetString(0);
            
            if(Cfg.UpgradeCurrency[upgradeType] == "RP")
            {
                errorMsg = upgradeWithRp(player, upgradeType);
            }
            else
            {
                errorMsg = upgradeWithItem(player, upgradeType);
            }
            reloadControlUI(player, errorMsg);

            if(upgradeType == "slots" || upgradeType == "level")
            {    
                FurnaceManager fm = FurnaceManager.Get(player.userID);
                if(upgradeType != "slots" && fm.currentLevel != 1) return;
                reloadSlotUI(player, (fm.Slots.Count()-1), errorMsg);
            }
        }

        private string upgradeWithRp(BasePlayer player, string upgradeType)
        {
            FurnaceManager fm = FurnaceManager.Get(player.userID);
            int buyPrice = fm.GetPrice(upgradeType);

            string errorMsg = globalNoErrorString;
            object bal = ServerRewards?.Call("CheckPoints", player.userID);
            if(bal == null) return "Error fetching RP balance";
            int playerRP = (int)bal;

            if(playerRP < buyPrice)
            {
                errorMsg = "This upgrade costs " + buyPrice + " RP, you only have " + playerRP;
            }
            else
            {   
                if(fm.doUpgrade(upgradeType))
                {
                    ServerRewards?.Call("TakePoints", player.userID, buyPrice);
                }
                else
                {
                    errorMsg = "Could not upgrade!";
                }
            }

            return errorMsg;
        }
        private string upgradeWithItem(BasePlayer player, string upgradeType)
        {
            string errorMsg = globalNoErrorString;
            FurnaceManager fm = FurnaceManager.Get(player.userID);
            
            int amountToTake = fm.GetPrice(upgradeType);
            string currency = Cfg.UpgradeCurrency[upgradeType];

            if(player.inventory.GetAmount(itemIDMap[currency]) < amountToTake){
                errorMsg = "You need "+humanNames[currency]+" (x"+amountToTake+") for that!";
            } 
            else 
            {
                 if(fm.doUpgrade(upgradeType))
                {
                    player.inventory.Take(null, itemIDMap[currency], amountToTake);
                }
                else
                {
                    errorMsg = "Could not upgrade!";
                }
            }

            return errorMsg;
        }

        [ConsoleCommand("virtualsmelter.smelt")]
        private void CmdSmelt(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string errorMsg = globalNoErrorString;
            int slot = arg.GetInt(0);
            string res = arg.GetString(1);

            FurnaceManager fm = FurnaceManager.Get(player.userID);

            errorMsg = fm.AddRes(player, slot, res);
            reloadSlotUI(player, slot, errorMsg);
        }

        
        [ConsoleCommand("virtualsmelter.addres")]
        private void CmdAddRes(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string errorMsg = globalNoErrorString;
            int slot = arg.GetInt(0);

            FurnaceManager fm = FurnaceManager.Get(player.userID);

            errorMsg = fm.AddRes(player, slot);
            reloadSlotUI(player, slot, errorMsg);
        }

        [ConsoleCommand("virtualsmelter.takeall")]
        private void CmdTakeAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string errorMsg = globalNoErrorString;
            int slot = arg.GetInt(0);

            FurnaceManager fm = FurnaceManager.Get(player.userID);

            errorMsg = fm.TakeAll(player, slot);
            reloadSlotUI(player, slot, errorMsg);
        }

        [ConsoleCommand("virtualsmelter.addfuel")]
        private void CmdAddFuel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string errorMsg = globalNoErrorString;
            int slot = arg.GetInt(0);
            int percent = arg.GetInt(1);

            FurnaceManager fm = FurnaceManager.Get(player.userID);

            errorMsg = fm.AddFuel(player, slot, percent);
            reloadSlotUI(player, slot, errorMsg);
        }

        [ConsoleCommand("virtualsmelter.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            killUI(player);
            var finaleffect = new Effect("assets/prefabs/deployable/rug/effects/rug-deploy.prefab", player, 0, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(finaleffect, player.net.connection);
        }

        [ChatCommand("smelt")]
        void SmeltCmd(BasePlayer player, string command, string[] args)
        {
            if(HasPermission(player.UserIDString, permUse))
            {
                FurnaceManager fm = FurnaceManager.Get(player.userID);
                _wipeTime = SaveRestore.SaveCreatedTime.ToLocalTime();
                //Autowipe contents on map wipe, keep levels.
                if(fm.lastCalculationTime < _wipeTime)
                {
                    Puts("INFO: wipetime " + _wipeTime + " last access " + fm.lastCalculationTime + " wiping smelter content for " + player);
                    fm.wipeContents();
                }
                reloadUI(player);
                var finaleffect = new Effect("assets/prefabs/deployable/campfire/effects/campfire-deploy.prefab", player, 0, Vector3.zero, Vector3.forward);
                EffectNetwork.Send(finaleffect, player.net.connection);
            }
            else
            {
                SendReply(player, "You don't have permission to use this command");
            }
        }

        #endregion
        #region hooks
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || !compatibleOvens.Contains(oven.ShortPrefabName))
                return;
            displayInfoUI(player);

        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            BaseOven oven = entity as BaseOven;

            if (oven == null || !compatibleOvens.Contains(oven.ShortPrefabName))
                return;
            killUI(player);
        }
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!UiPlayers.Contains(player)) return;
            if (input.WasJustPressed(BUTTON.JUMP))
            {
                killUI(player);
                var finaleffect = new Effect("assets/prefabs/deployable/rug/effects/rug-deploy.prefab", player, 0, Vector3.zero, Vector3.forward);
                EffectNetwork.Send(finaleffect, player.net.connection);
            }
            return;
        }
        #endregion

        #region Classes

        private class FurnaceManager
        {
            public ulong ownerID;
            public Dictionary<int,  FurnaceSlot> Slots = new Dictionary<int,  FurnaceSlot>();
            public DateTime lastCalculationTime = DateTime.Now;
            public int currentLevel = 0;
            public Dictionary<string, int> upgradeLevels = new Dictionary<string, int>(){
                ["slots"] = 0,
                ["speed"] = 0,
                ["fuel"] = 0,
                ["efficiency"] = 0,
            };

            public int currentFuelConsumptionPerMinute = Cfg.FuelPerSlotLevel;
            public Dictionary<string, int> currentSmeltingPerMinute = new Dictionary<string, int>()
            {
                ["wood"] = Cfg.OutputPerMinute["wood"],
                ["hqm"] = Cfg.OutputPerMinute["hqm"],
                ["metal"] = Cfg.OutputPerMinute["metal"],
                ["sulfur"] = Cfg.OutputPerMinute["sulfur"]
            };

             public FurnaceManager(ulong ownerId) : base()
            {
                ownerID = ownerId;
            }
            public static FurnaceManager Get(ulong id)
            {
                if (Plugin._furnaceManager.ContainsKey(id))
                    return Plugin._furnaceManager[id];
                
                var fileName = $"{"VirtualSmelter"}/{id}";

                FurnaceManager manager;
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(fileName))
                {
                    VirtualSmelter.LoadData(out manager, fileName);
                }
                else
                {
                    manager = new FurnaceManager(id);
                    manager.LevelUp();
                    VirtualSmelter.SaveData(manager, fileName);
                }

                Interface.Oxide.DataFileSystem.GetDatafile(fileName).Settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore
                };

                Plugin._furnaceManager.Add(id, manager);

                return manager;
            }
            public int getCapacity(){
                return currentLevel*Cfg.BaseCapacity;
            }
            public float getEfficiency(){
                if(upgradeLevels["efficiency"] == 0) return 1f;
                return Cfg.Upgrades["efficiency"][upgradeLevels["efficiency"]]["effect"];
            }
            private void calculateSmeltingPerMinute()
            {
                int speed = upgradeLevels["speed"] > 0 ? (int) Cfg.Upgrades["speed"][upgradeLevels["speed"]]["effect"] : 1;
                currentSmeltingPerMinute = new Dictionary<string, int>()
                {
                    ["wood"] = Cfg.OutputPerMinute["wood"] * currentLevel * speed,
                    ["hqm"] = Cfg.OutputPerMinute["hqm"] * currentLevel * speed,
                    ["metal"] = Cfg.OutputPerMinute["metal"] * currentLevel * speed,
                    ["sulfur"] = Cfg.OutputPerMinute["sulfur"] * currentLevel * speed
                };
                SaveData();
            }
            public Dictionary<string, int> getSmeltingPerMinute(){
                return currentSmeltingPerMinute;
            }
            private void calculateFuelConsumption()
            {                
                float fuelEfficiency = upgradeLevels["fuel"] == 0 ? 1f : Cfg.Upgrades["fuel"][upgradeLevels["fuel"]]["effect"];               
				float speed = upgradeLevels["speed"] > 0 ? (int) Cfg.Upgrades["speed"][upgradeLevels["speed"]]["effect"]*Cfg.SpeedFuelMultiplyer : 1f;

                fuelEfficiency = fuelEfficiency * speed;
                currentFuelConsumptionPerMinute = (int)Math.Ceiling(Cfg.FuelPerSlotLevel * currentLevel * fuelEfficiency);
                SaveData();
            }

            public float getFuelConsumption(){
                return currentFuelConsumptionPerMinute;
            }
            public int LevelUpPrice(){
                return Cfg.BaseLevelPrice + (int)Math.Floor((currentLevel+1)/10f)*(2*(int)Math.Floor((currentLevel+1)/10f));
            }
            
            public bool LevelUp(){
                currentLevel +=1;
                if(Slots.Count() == 0) AddSlot();
                calculateSmeltingPerMinute();
                calculateFuelConsumption();
                SaveData();
                return true;
            }

            public bool canUpgrade(string type)
            {    
                if(!upgradeLevels.ContainsKey(type)) return false;
                return upgradeLevels[type] < Cfg.Upgrades[type].Count();
            }
            public int GetPrice(string type)
            {
                if(type == "level") return LevelUpPrice();

                if(!upgradeLevels.ContainsKey(type) || upgradeLevels[type] >= Cfg.Upgrades[type].Count()) return 0;
                return (int)Math.Floor(Cfg.Upgrades[type][upgradeLevels[type]+1]["price"]);
            }

            public bool doUpgrade(string type)
            {    
                calculateContents(true);
                if(type == "level") return LevelUp();

                if(!upgradeLevels.ContainsKey(type) || upgradeLevels[type] >= Cfg.Upgrades[type].Count()) return false;
                switch(type)
                {
                    case "slots":
                        AddSlot();
                    break;
                    case "fuel":
                        calculateFuelConsumption();
                    break;
                    case "speed":
                        calculateFuelConsumption();
                        calculateSmeltingPerMinute();
                    break;
                    case "efficiency":
                    default:
                    break;
                }
                upgradeLevels[type]++;
                SaveData();
                return true;
            }
            
            public bool AddSlot(){
                int currentSlots = Slots.Count();
                FurnaceSlot slot = new FurnaceSlot(ownerID, currentSlots);
                Slots.Add(currentSlots, slot);
                SaveData();
                return true;
            }

            public string AddRes(BasePlayer player, int slotId, string res = "empty")
            {
                FurnaceSlot slot = Slots[slotId];
                // Smelting res
                if(res == "empty"){
                    if(slot.smeltingRes == "empty") return "No Res specified!";
                    calculateContents(true);
                    res = slot.smeltingRes;
                }
                int playerResAmount = player.inventory.GetAmount(Plugin.itemIDMap[res]);
                if(playerResAmount == 0) return "Not enough resources!";
                if(res == "wood") playerResAmount = (int)Math.Floor(0.5f*playerResAmount);
                int addAmount = (int)Math.Floor(getCapacity() - slot.smeltingResAmount);
                if(addAmount == 0) return "The slot is full!";
                if(playerResAmount < addAmount){
                    addAmount = playerResAmount;
                }
                slot.AddRes(res, addAmount);
                Plugin.takeItem(player, res, addAmount);

                // fuel
                int fuelNeeded = (int)Math.Ceiling(calculateResFuel(slotId) - slot.currentFuel);
                if(fuelNeeded > slot.currentFuel){
                    playerResAmount = player.inventory.GetAmount(Plugin.itemIDMap["wood"]);
                    if(playerResAmount < fuelNeeded){
                        fuelNeeded = playerResAmount;
                    }
                    Plugin.takeItem(player, "wood", fuelNeeded);
                    slot.currentFuel += fuelNeeded;
                }
                
                slot.lastAccessTime = DateTime.Now;

                SaveData();
                return VirtualSmelter.globalNoErrorString;
            }

            public float getFuelForHours(int slotId)
            {
                FurnaceSlot slot = Slots[slotId];
                if(slot.currentFuel == 0f) return 0f;

                return (slot.currentFuel / getFuelConsumption()) / 60;
            }
            public float getSmeltingForHours(int slotId)
            {
                FurnaceSlot slot = Slots[slotId];
                if(slot.smeltingRes == "empty") return 0;
                Dictionary<string, int> smelt = getSmeltingPerMinute();
                float effect = upgradeLevels["efficiency"] > 0 ?  Cfg.Upgrades["efficiency"][upgradeLevels["efficiency"]]["effect"] : 1f;

                return ((slot.smeltingResAmount*effect) / smelt[slot.smeltingRes]) / 60;
            }

            private int calculateResFuel(int slotId)
            {
                float hoursToSmelt =  getSmeltingForHours(slotId);
                int fuelRequired = (int)Math.Ceiling(getFuelConsumption()*hoursToSmelt*60);

                return fuelRequired;
            }

            public string TakeAll(BasePlayer player, int slotId)
            {
                FurnaceSlot slot = Slots[slotId];
                calculateContents(true);
                if(slot.smeltingRes == "empty") return "Slot is empty!";
                // smelting res
                Item res;
                if(slot.smeltingResAmount >= 1)
                {
                    res = ItemManager.CreateByItemID(Plugin.itemIDMap[slot.smeltingRes], (int) Math.Ceiling(slot.smeltingResAmount), 0UL);
                    player.GiveItem(res, BaseEntity.GiveItemReason.PickedUp);
                }
                // output res
                if(slot.productResAmount >= 1)
                {
                    res = ItemManager.CreateByItemID(Plugin.itemIDMap[slot.smeltingRes+"_out"], (int) Math.Ceiling(slot.productResAmount), 0UL);
                    player.GiveItem(res, BaseEntity.GiveItemReason.PickedUp);
                }
                // fuel
                if(slot.currentFuel >= 1f)
                {
                    res = ItemManager.CreateByItemID(Plugin.itemIDMap["wood"], (int) Math.Ceiling(slot.currentFuel), 0UL);
                    player.GiveItem(res, BaseEntity.GiveItemReason.PickedUp);

                }
                // biproduct
                if(slot.biproductResAmount >= 1)
                {
                    res = ItemManager.CreateByItemID(Plugin.itemIDMap["charcoal"], (int) Math.Ceiling(slot.biproductResAmount), 0UL);
                    player.GiveItem(res, BaseEntity.GiveItemReason.PickedUp);
                }

                slot.Empty();
                SaveData();
                return VirtualSmelter.globalNoErrorString;
            }

            
            public string AddFuel(BasePlayer player, int slotId, int percent)
            {
                FurnaceSlot slot = Slots[slotId];
                calculateContents(true);
                int playerResAmount = player.inventory.GetAmount(Plugin.itemIDMap["wood"]);
                if(playerResAmount == 0) return "You have no wood!";
                int fuelPlaced = (playerResAmount == 1) ? 1 : (int)Math.Floor((playerResAmount / 100f) * percent);                
                Plugin.takeItem(player, "wood", fuelPlaced);
                slot.currentFuel += fuelPlaced;
                slot.lastAccessTime = DateTime.Now;
                SaveData();
                return VirtualSmelter.globalNoErrorString;
            }

            public void wipeContents(){
                foreach(KeyValuePair<int,FurnaceSlot> slotKV in Slots){
                    FurnaceSlot slot = slotKV.Value;
                    slot.Empty();
                }
                lastCalculationTime = DateTime.Now;
                SaveData();
            }
            public void calculateContents(bool force = false){
                if(force == false && ((float)((DateTime.Now - lastCalculationTime).TotalSeconds) < 1.5f)) return;

                float fuelPerSecond = getFuelConsumption()/60f;
                Dictionary<string, int> smeltPerMinute = getSmeltingPerMinute();
                float efficiency = getEfficiency();
                
                foreach(KeyValuePair<int,FurnaceSlot> slotKV in Slots){
                    FurnaceSlot slot = slotKV.Value;
                    if(!slot.HasRes()) continue;

                    if(slot.IsSmelting())
                    {
                        float sinceUpdate = slot.SecondsSinceUpdate();

                        string res = slot.smeltingRes;
                        float usedFuel = fuelPerSecond*sinceUpdate;
                        if(usedFuel >= slot.currentFuel) usedFuel = slot.currentFuel;
                        slot.currentFuel = slot.currentFuel - usedFuel;
                        slot.biproductResAmount = slot.biproductResAmount + usedFuel;
                        //correct seconds if fuel ran out mid-smelting!
                        sinceUpdate = usedFuel/fuelPerSecond;

                        float usedRes = ((smeltPerMinute[res]*(1/efficiency))/60)*sinceUpdate;
                        if(usedRes >= slot.smeltingResAmount) usedRes = slot.smeltingResAmount;
                        slot.smeltingResAmount = slot.smeltingResAmount - usedRes;
                        
                        float productSmolten = (smeltPerMinute[res]/60f)*sinceUpdate;
                        if(productSmolten >= (usedRes*efficiency)) productSmolten = (usedRes*efficiency);
                        slot.productResAmount = slot.productResAmount + productSmolten;
                        
                        slot.lastAccessTime = DateTime.Now;
                    } 
                    else if(slot.HasFuel())
                    {
                        float sinceUpdate = slot.SecondsSinceUpdate();
                        float usedFuel = fuelPerSecond*sinceUpdate;
                        if(usedFuel >= slot.currentFuel) usedFuel = slot.currentFuel;
                        slot.currentFuel = slot.currentFuel - usedFuel;
                        slot.biproductResAmount = slot.biproductResAmount + usedFuel;

                        slot.lastAccessTime = DateTime.Now;
                    }                    
                }
                lastCalculationTime = DateTime.Now;
                SaveData();
            }

    
            public void SaveData()
            {
                VirtualSmelter.SaveData(this, $"{"VirtualSmelter"}/{ownerID}");
            }
        }

        

        private class FurnaceSlot
        {
            public ulong ownerId;
            public int entityId;
            public DateTime buyTime;
            public DateTime lastAccessTime;
            public string smeltingRes = "empty";
            public float smeltingResAmount = 0.0f;
            public float productResAmount = 0.0f;
            public float biproductResAmount = 0.0f;
            public float currentFuel = 0.0f;

            public  FurnaceSlot(ulong oId, int eID) : base()
            {
                ownerId = oId;
                entityId = eID;
                buyTime = DateTime.Now;
                lastAccessTime = DateTime.Now;
            }

            public bool HasRes()
            {
                return smeltingRes != "empty";
            }
            public bool HasFuel()
            {
                return currentFuel > 0f;
            }
            public bool IsSmelting()
            {
                return currentFuel > 0f && smeltingResAmount > 0f;
            }
            public void AddRes(string res, float amount)
            {
                smeltingRes = res;
                smeltingResAmount += amount;
                lastAccessTime = DateTime.Now;
            }
            public float SecondsSinceUpdate(){
                return (float)((DateTime.Now - lastAccessTime).TotalSeconds);
            }
            public void Empty()
            {
                smeltingRes = "empty";
                smeltingResAmount = 0f;
                productResAmount = 0f;
                biproductResAmount = 0f;
                currentFuel = 0f;
                lastAccessTime = DateTime.Now;
            }
        }

        #endregion


        #region Helpers

        private void takeItem(BasePlayer player, string res, int amount)
        {
            player.inventory.Take(null, itemIDMap[res], amount);
            player.Command("note.inv", itemIDMap[res], -amount);
        }
        
        private void reloadControlUI(BasePlayer player, string errorMsg = globalNoErrorString){
            if(!UiPlayers.Contains(player)){
                return;
            }
            
            upsertPlayerTimerRuntime(player);
            CuiHelper.DestroyUi(player, "VirtualSmelter_control");
         
            FurnaceManager fm = FurnaceManager.Get(player.userID);
            CuiHelper.DestroyUi(player, "VirtualSmelter_head");
            GUIHeaderElement(player, "VirtualSmelter_head", fm, errorMsg);
            GUIControlElement(player, "VirtualSmelter_control", fm);
        }
        private void reloadSlotUI(BasePlayer player, int slotId, string errorMsg = globalNoErrorString){
            if(!UiPlayers.Contains(player)){
                return;
            }
            
            upsertPlayerTimerRuntime(player);
            FurnaceManager fm = FurnaceManager.Get(player.userID);
            CuiHelper.DestroyUi(player, "VirtualSmelter_head");
            GUIHeaderElement(player, "VirtualSmelter_head", fm, errorMsg);
            CuiHelper.DestroyUi(player, "VirtualSmelter_slot_"+slotId);
            GUISlotElement(player, "VirtualSmelter_slot_"+slotId, slotId, fm, fm.Slots[slotId]);
            CuiHelper.DestroyUi(player, "VirtualSmelter_slot_"+slotId+"_numbers");
            GUISlotNumbersElement(player, "VirtualSmelter_slot_"+slotId+"_numbers", slotId, fm, fm.Slots[slotId]);
        }
        private void reloadUI(BasePlayer player, string errorMsg = globalNoErrorString){
            if(!UiPlayers.Contains(player)){
                UiPlayers.Add(player);
            }
            foreach(string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }
            
            FurnaceManager fm = FurnaceManager.Get(player.userID);
            displayUI(player, fm, errorMsg);
            Timer resTimer = timer.Every(2f, () =>
            {
                float timeSinceOpening = (float)(DateTime.Now -  playerTimerRuntime[player.userID]).TotalSeconds;
                if(Cfg.UiAutoCloseSeconds != 0f && timeSinceOpening > Cfg.UiAutoCloseSeconds)
                {
                    killUI(player);
                    return;
                }

                foreach(KeyValuePair<int, FurnaceSlot> slotKV in fm.Slots)
                {
                    if(slotKV.Value.IsSmelting() || slotKV.Value.HasFuel())
                    {   
                        CuiHelper.DestroyUi(player, "VirtualSmelter_slot_"+slotKV.Key+"_numbers");
                        GUISlotNumbersElement(player, "VirtualSmelter_slot_"+slotKV.Key+"_numbers", slotKV.Key, fm, fm.Slots[slotKV.Key]);
                    }
                }
            });
            if(playerTimer.ContainsKey(player.userID)){
                playerTimer[player.userID].Destroy();
                playerTimer[player.userID] = resTimer;
            }
            else
            {
                playerTimer.Add(player.userID, resTimer);
            }
            upsertPlayerTimerRuntime(player);

        }
        private void killUI(BasePlayer player){
            if(UiPlayers.Contains(player)){
                UiPlayers.Remove(player);
                foreach(string ui in uiElements)
                {
                    CuiHelper.DestroyUi(player, ui);
                }
            }
            if(playerTimer.ContainsKey(player.userID)){
                playerTimer[player.userID].Destroy();
                playerTimer.Remove(player.userID);
                playerTimerRuntime.Remove(player.userID);
            }
        }

        private void upsertPlayerTimerRuntime(BasePlayer player)
        {
            if(playerTimerRuntime.ContainsKey(player.userID)){
                playerTimerRuntime[player.userID] = DateTime.Now;
            }
            else
            {
                playerTimerRuntime.Add(player.userID, DateTime.Now);
            }
        }

        private string numberCandy(int number){
            return Convert.ToDecimal(number).ToString("N", nfi);
        }

        #endregion

        #region GUI

        private void displayInfoUI(BasePlayer player)
        {
            if (!UiPlayers.Contains(player))
            {
                UiPlayers.Add(player);
            }
            foreach (string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }
            var mainName = "VirtualSmelter";
            GUIInfoElement(player, mainName +"_furnace_info");

        }

        private float globalLeftBoundary = 0.1f;
        private float globalRighttBoundary = 0.9f;
        private float globalTopBoundary = 0.9f;
        private float globalBottomBoundary = 0.1f;
        private float globalSpace = 0.01f;
        private float eControlWidth = 0.15f;
        private float eHeadHeight = 0.05f;
        private float eFootHeight = 0.05f;
        private float eSlotHeight = 0.123f;

        private void displayUI(BasePlayer player, FurnaceManager fm, string errorMsg = globalNoErrorString)
        {
            var mainName = "VirtualSmelter";
            GUIHeaderElement(player, mainName+"_head", fm, errorMsg);
            GUIControlElement(player, mainName+"_control", fm);
            GUIFooterElement(player, mainName+"_foot", fm);
            
            foreach(KeyValuePair<int, FurnaceSlot> slot in fm.Slots)
            {
                GUISlotElement(player, mainName+"_slot_"+slot.Key, slot.Key, fm, slot.Value);
                GUISlotNumbersElement(player, mainName+"_slot_"+slot.Key+"_numbers", slot.Key, fm, slot.Value);
            }


        }

        private void GUIInfoElement(BasePlayer player, string elUiId)
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
                    AnchorMin = "0.7 0.55",
                    AnchorMax = "0.94 0.6"
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Check out <color=orange>/smelt</color> for more efficient smelting.",
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0.005",
                    AnchorMax = "0.95 0.995"
                }
            }, elUiId);

           

            CuiHelper.AddUi(player, elements);
        }

        private void GUIHeaderElement(BasePlayer player, string elUiId, FurnaceManager fm, string errorMsg  = globalNoErrorString)
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
                    Text = "Virtual Smelter (Level: " + fm.currentLevel +")",
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
        
        private void GUIControlElement(BasePlayer player, string elUiId, FurnaceManager fm)
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
                    AnchorMin = globalLeftBoundary + " " + (globalBottomBoundary + eFootHeight + globalSpace),
                    AnchorMax = (globalLeftBoundary+eControlWidth ) + " " + (globalTopBoundary - eHeadHeight - globalSpace)
                },
                CursorEnabled = true
            }, "Hud", elUiId);


            float buttonHeight = 0.04f;
            float contentStart = 0.985f;
            float leftBoundary = 0.03f;
            float leftWidth = 0.97f;
            float space = 0.01f;

            int playerFurnaces = player.inventory.GetAmount(itemIDMap["furnace"]);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Stats (per slot)",
                    FontSize = 16,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0 1 0 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);
            contentStart = contentStart-1.1f*buttonHeight;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Capacity:",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = numberCandy(fm.getCapacity()),
                    FontSize = 12,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);
            
            contentStart = contentStart-buttonHeight;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Efficiency (res/ore):",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = ""+fm.getEfficiency(),
                    FontSize = 12,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);

            contentStart = contentStart-buttonHeight;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Fuel (wood per minute):",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = numberCandy((int)Math.Ceiling(fm.getFuelConsumption())),
                    FontSize = 12,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);

            
            contentStart = contentStart-buttonHeight;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Output per minute:",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);
            Dictionary<string, int> smelt = fm.getSmeltingPerMinute();
            foreach(string res in Cfg.AvailableRes)
            {
                contentStart = contentStart-buttonHeight;
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "- "+humanNames[((res == "wood") ? "charcoal" : res)],
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                        AnchorMax = leftWidth + " "+ contentStart
                    }
                }, elUiId);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = numberCandy((int)Math.Ceiling(((res == "wood") ? (smelt[res]+fm.getFuelConsumption()) : smelt[res]))),
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                        AnchorMax = leftWidth + " "+ contentStart
                    }
                }, elUiId);
            }
            
            
            contentStart = contentStart-1.2f*buttonHeight;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Upgrades",
                    FontSize = 16,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0 1 0 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);

            contentStart = contentStart-1.2f*buttonHeight;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = humanNames["level"],
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0 0.90 0.0 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                    AnchorMax = leftWidth + " "+ contentStart
                }
            }, elUiId);
            contentStart = contentStart-buttonHeight;
            if(fm.currentLevel >= Cfg.MaxLevel)
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Levelcap reached!",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart-1.2*buttonHeight),
                        AnchorMax = leftWidth+" "+(contentStart)
                    }
                }, elUiId);
            }
            else
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Price: <color=orange>"+fm.GetPrice("level") +"x "+humanNames[Cfg.UpgradeCurrency["level"]] +"</color>\nEffect: +1 BaseStats*",
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary +" "+ (contentStart-1.2f*buttonHeight),
                        AnchorMax = leftWidth + " "+ contentStart
                    }
                }, elUiId);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualsmelter.upgrade level",
                        Color = "0.7 0.38 0 1"
                    },
                    RectTransform =
                    {
                    AnchorMin = (leftWidth - 0.2f) + " "+(contentStart-1.2*buttonHeight),
                    AnchorMax = leftWidth+" "+(contentStart)
                    },
                    Text =
                    {
                        Text = "UPGRADE",
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
            }

            
            if(fm.currentLevel > 0)
            {
                foreach(string upgradeType in Cfg.AvailableUpgrades)
                {
                    
                    int currentLevel = fm.upgradeLevels[upgradeType];
                    contentStart = contentStart-1.5f*buttonHeight;
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = humanNames[upgradeType] + " ("+currentLevel+"/"+Cfg.Upgrades[upgradeType].Count+")",
                            FontSize = 12,
                            Align = TextAnchor.MiddleLeft,
                            Color = "0 0.90 0.0 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = leftBoundary +" "+ (contentStart-buttonHeight),
                            AnchorMax = leftWidth + " "+ contentStart
                        }
                    }, elUiId);
                    contentStart = contentStart-buttonHeight-space;

                    float effect = currentLevel < Cfg.Upgrades[upgradeType].Count() ?  Cfg.Upgrades[upgradeType][currentLevel+1]["effect"] : Cfg.Upgrades[upgradeType][currentLevel]["effect"] ;
                    
                    if(!fm.canUpgrade(upgradeType))
                    {
                        
                        elements.Add(new CuiLabel
                        {
                            Text =
                            {
                                Text = "Max-Level!\nEffect: " + humanNames["effect_"+upgradeType] + effect,
                                FontSize = 10,
                                Align = TextAnchor.MiddleLeft,
                                Color = "1 1 1 1"
                            },
                            RectTransform =
                            {
                                AnchorMin = leftBoundary +" "+ (contentStart-1.2f*buttonHeight),
                                AnchorMax = leftWidth + " "+ contentStart
                            }
                        }, elUiId);
                    }
                    else
                    {
                        elements.Add(new CuiLabel
                        {
                            Text =
                            {
                                Text = "Price: <color=orange>"+fm.GetPrice(upgradeType) +"x "+humanNames[Cfg.UpgradeCurrency[upgradeType]] +"</color>\nEffect: " + humanNames["effect_"+upgradeType] + effect,
                                FontSize = 10,
                                Align = TextAnchor.MiddleLeft,
                                Color = "1 1 1 1"
                            },
                            RectTransform =
                            {
                                AnchorMin = leftBoundary +" "+ (contentStart-1.2f*buttonHeight),
                                AnchorMax = leftWidth + " "+ contentStart
                            }
                        }, elUiId);
                        elements.Add(new CuiButton
                        {
                            Button =
                            {
                                Command = "virtualsmelter.upgrade " + upgradeType,
                                Color = "0.7 0.38 0 1"
                            },
                            RectTransform =
                            {
                            AnchorMin = (leftWidth - 0.2f) + " "+(contentStart-1.2f*buttonHeight),
                            AnchorMax = leftWidth+" "+(contentStart)
                            },
                            Text =
                            {
                                Text = "UPGRADE",
                                FontSize = 8,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                        }, elUiId);
                    }

                }
            }

            CuiHelper.AddUi(player, elements);
        }
        
        private void GUIFooterElement(BasePlayer player, string elUiId, FurnaceManager fm)
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
                    AnchorMax = (globalLeftBoundary+eControlWidth ) + " " + (globalBottomBoundary+eFootHeight)
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "virtualsmelter.close",
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
        
        private void GUISlotElement(BasePlayer player, string elUiId, int slotId, FurnaceManager fm, FurnaceSlot slot)
        {
            
            var elements = new CuiElementContainer();
            float topBoundary = globalTopBoundary - eHeadHeight - globalSpace - (slotId*eSlotHeight);
            if(slotId > 0) topBoundary -= 0.5f*globalSpace;
            float botBoundary = globalTopBoundary - eHeadHeight - globalSpace - ((slotId+1)*eSlotHeight);

            float leftOffset = 0.05f;
            float imageWidth = 0.105f;
            float imageHeight = 0.105f;
            float resImageWidth = 0.075f;
            float resImageHeight = 0.6f;
            float detailsWidth = 0.11f;
            float bottomOffset = 0.15f;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5", 
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = (globalLeftBoundary+eControlWidth+globalSpace ) + " " + botBoundary,
                    AnchorMax = globalRighttBoundary + " " + topBoundary
                },
                CursorEnabled = true
            }, "Hud", elUiId);

                // Images
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = elUiId,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage(prefabNames["furnace"]) },
                        new CuiRectTransformComponent {
                            AnchorMin = "0 "+ (10f*globalSpace),
                            AnchorMax = (imageWidth) +" " + (1 - imageHeight + 5f*globalSpace)
                        }
                    }
                });
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Slot-"+slot.entityId,
                    FontSize = 10,
                    Align = TextAnchor.LowerCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                        AnchorMin = globalSpace + " 0",
                        AnchorMax = (imageWidth) +" 1"
                }
            }, elUiId);


            //empty smelter ui
            if(!slot.HasRes())
            {
                leftOffset = leftOffset + resImageWidth + globalSpace;
                foreach(string res in Cfg.AvailableRes)
                {
                    elements.Add(new CuiElement
                    {
                        Name = CuiHelper.GetGuid(),
                        Parent = elUiId,
                        Components =
                        {
                            new CuiRawImageComponent {Png = GetImage(prefabNames[res]) },
                            new CuiRectTransformComponent {
                            AnchorMin = leftOffset + " " + bottomOffset,
                            AnchorMax = (leftOffset + resImageWidth) +" " + (resImageHeight +bottomOffset)
                            }
                        }
                    });
                    
                    leftOffset = leftOffset + resImageWidth + globalSpace;
                    
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = humanNames[res] + "\n ADD MAX",
                            FontSize = 10,
                            Align = TextAnchor.MiddleLeft,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                                AnchorMin = leftOffset + " "+ bottomOffset,
                                AnchorMax = (leftOffset+detailsWidth) +" " + (resImageHeight +bottomOffset)
                        }
                    }, elUiId);
                    
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualsmelter.smelt " + slot.entityId + " " + res,
                            Color = "0 0 0 0"
                        },
                        RectTransform =
                        {
                            AnchorMin = leftOffset - resImageWidth - globalSpace + " " + 0.3f*bottomOffset,
                            AnchorMax = (leftOffset + detailsWidth) +" 1"
                        },
                        Text =
                        {
                            Text = "" ,
                            FontSize = 12,
                            Align = TextAnchor.LowerCenter,
                            Color = "1 1 1 1"
                        }
                    }, elUiId);
                    
                    leftOffset = leftOffset + detailsWidth +globalSpace;
                }
            }
            // filled smelter UI
            else
            {
                fm.calculateContents();
                var ts = TimeSpan.FromHours(fm.getFuelForHours(slot.entityId));
                string fuelText = " "+ts.Hours+"h "+ ts.Minutes +"min "+ ts.Seconds +"s of fuel";
                if(ts.Days != 0) fuelText = " "+ts.Days+"d" + fuelText;
                ts = TimeSpan.FromHours(fm.getSmeltingForHours(slot.entityId));
                string smeltText = " "+ts.Hours+"h "+ ts.Minutes +"min "+ ts.Seconds +"s to smelt";
                if(ts.Days != 0) smeltText = " "+ts.Days+"d" + smeltText;
                
                leftOffset = leftOffset + resImageWidth + globalSpace;
                // Images
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = elUiId,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage(prefabNames[slot.smeltingRes]) },
                        new CuiRectTransformComponent {
                            AnchorMin = leftOffset + " " + 2f*bottomOffset,
                            AnchorMax = (leftOffset + resImageWidth) +" " +  (2f*bottomOffset+resImageHeight)
                        }
                    }
                });
                leftOffset = leftOffset + detailsWidth +globalSpace;
                
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = elUiId,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage(prefabNames[slot.smeltingRes+"_out"]) },
                        new CuiRectTransformComponent {
                            AnchorMin = leftOffset + " " + 2f*bottomOffset,
                            AnchorMax = (leftOffset + resImageWidth) +" " + (2f*bottomOffset+resImageHeight)
                        }
                    }
                });
                leftOffset = leftOffset + detailsWidth +globalSpace;
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = elUiId,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage(prefabNames["wood"]) },
                        new CuiRectTransformComponent {
                            AnchorMin = leftOffset + " " + 2f*bottomOffset,
                            AnchorMax = (leftOffset + resImageWidth) +" " +  (2f*bottomOffset+resImageHeight)
                        }
                    }
                });
                leftOffset = leftOffset + detailsWidth +globalSpace;
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = elUiId,
                    Components =
                    {
                        new CuiRawImageComponent {Png = GetImage(prefabNames["charcoal"]) },
                        new CuiRectTransformComponent {
                            AnchorMin = leftOffset + " " + 2f*bottomOffset,
                            AnchorMax = (leftOffset + resImageWidth) +" " +  (2f*bottomOffset+resImageHeight)
                        }
                    }
                });
                leftOffset = leftOffset + detailsWidth +globalSpace;
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualsmelter.addres " + slot.entityId,
                        Color = "0.7 0.38 0 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " " + bottomOffset,
                        AnchorMax = (leftOffset + detailsWidth) +" " + (1f-bottomOffset)
                    },
                    Text =
                    {
                        Text = "Add "+humanNames[slot.smeltingRes]+" (max)" ,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
                leftOffset = leftOffset + detailsWidth +globalSpace;
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualsmelter.addfuel " + slot.entityId + " " + 50,
                        Color = "0.3 0.3 0.3 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " " + bottomOffset,
                        AnchorMax = (leftOffset + detailsWidth) +" " + (0.48f)
                    },
                    Text =
                    {
                        Text = "Add fuel (50%)" ,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualsmelter.addfuel " + slot.entityId + " " + 100,
                        Color = "0.3 0.3 0.3 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " " + (0.52f),
                        AnchorMax = (leftOffset + detailsWidth) +" " + (1-bottomOffset)
                    },
                    Text =
                    {
                        Text = "Add fuel (max)" ,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
                leftOffset = leftOffset + detailsWidth +globalSpace;
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualsmelter.takeall " + slot.entityId,
                        Color = "0.7 0.38 0 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " " + bottomOffset,
                        AnchorMax = (leftOffset + detailsWidth) +" " + (1-bottomOffset)
                    },
                    Text =
                    {
                        Text = "take out all" ,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, elUiId);
            }

            CuiHelper.AddUi(player, elements);
            
        }

        private void GUISlotNumbersElement(BasePlayer player, string elUiId, int slotId, FurnaceManager fm, FurnaceSlot slot)
        {
            
            var elements = new CuiElementContainer();
            float topBoundary = globalTopBoundary - eHeadHeight - globalSpace - (slotId*eSlotHeight);
            if(slotId > 0) topBoundary -= 0.5f*globalSpace;
            float botBoundary = globalTopBoundary - eHeadHeight - globalSpace - ((slotId+1)*eSlotHeight);

            float leftOffset = 0f;
            float contentWidth = 0.29f;
            float furnaceImageWidth = 0.08f;
            float detailsWidth = 0.25f;

            if(slot.HasRes())
            {
                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0",
                    },
                    RectTransform =
                    {
                        AnchorMin = (globalLeftBoundary+eControlWidth+globalSpace +furnaceImageWidth ) + " " + (botBoundary),
                        AnchorMax = (globalLeftBoundary+eControlWidth+globalSpace +furnaceImageWidth + contentWidth ) + " " + (topBoundary - 0.07f)
                    },
                    CursorEnabled = true
                }, "Hud", elUiId);

                fm.calculateContents();

                var ts = TimeSpan.FromHours(fm.getFuelForHours(slot.entityId));
                string fuelText = " "+ts.Hours+":"+ ts.Minutes +":"+ ts.Seconds +" of fuel";
                if(ts.Days != 0) fuelText = " "+ts.Days+"d" + fuelText;
                ts = TimeSpan.FromHours(fm.getSmeltingForHours(slot.entityId));
                string smeltText = " "+ts.Hours+":"+ ts.Minutes +":"+ ts.Seconds +" to smelt";
                if(ts.Days != 0) smeltText = " "+ts.Days+"d" + smeltText;
                string resAmount = numberCandy((int) Math.Ceiling(slot.smeltingResAmount));
                string currentFuel = numberCandy((int) Math.Ceiling(slot.currentFuel));
                string prodAmount = numberCandy((int) Math.Ceiling(slot.productResAmount));
                string biprodAmount = numberCandy((int) Math.Ceiling(slot.biproductResAmount));
                
                string resColor = (resAmount == "0") ? "1 0 0 1" : "1 1 1 1";
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = resAmount+"\n"+smeltText,
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = resColor
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " 0",
                        AnchorMax = (leftOffset+detailsWidth) +" 1"
                    }
                }, elUiId);
                leftOffset = leftOffset + detailsWidth +globalSpace;
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = prodAmount+"\n"+humanNames[slot.smeltingRes+"_out"],
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " 0",
                        AnchorMax = (leftOffset+detailsWidth) +" 1"
                    }
                }, elUiId);
                leftOffset = leftOffset + detailsWidth +globalSpace;
                string fuelColor = (currentFuel == "0") ? "1 0 0 1" : "1 1 1 1";
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = currentFuel+"\n"+fuelText,
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = fuelColor
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " 0",
                        AnchorMax = (leftOffset+detailsWidth) +" 1"
                    }
                }, elUiId);
                leftOffset = leftOffset + detailsWidth +globalSpace;
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = biprodAmount+"\n"+humanNames["charcoal"],
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftOffset + " 0",
                        AnchorMax = (leftOffset+detailsWidth) +" 1"
                    }
                }, elUiId);

            CuiHelper.AddUi(player, elements);
            }
            
            
        }

        #endregion

        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
    }
}   
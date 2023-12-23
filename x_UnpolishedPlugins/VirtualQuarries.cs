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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("VirtualQuarries", "DocValerian", "1.2.0")]
    class VirtualQuarries : RustPlugin
    {
        static VirtualQuarries Plugin;

        [PluginReference]
        private Plugin ServerRewards;
        private const string permUse = "virtualquarries.use";
        private const string permProt = "virtualquarries.protected";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region DataConfig
        static ConfigFile Cfg = new ConfigFile();
        class ConfigFile
        {
            public int CostPerQuarry = 5000;
            public Dictionary<int, int> CostPerQuarryNumber = new Dictionary<int, int>()
            {
                [0] = 2000,
                [1] = 7000,
                [2] = 15000,
                [3] = 25000,
                [4] = 45000,
                [5] = 65000,
                [6] = 850000,
                [7] = 1550000
            };
            public int RefundPerQuarry = 1000;
            public float SurveyEfficiency = 0.5f;
            public int EfficiencyLossAfterQuarryNr = 2;
            public float EfficiencyLossPerQuarry = 0.25f;
            public float DieselEfficiencyBoost = 1.5f;
            public int LgfPerHour = 360;
            public int DieselPerHour = 3;
            public int MaxQuarries = 7;
            public Dictionary<string, int> ResPerMinute = new Dictionary<string, int>()
            {
                ["stone_min"] = 80,
                ["stone_max"] = 150,
                ["metal_min"] = 20,
                ["metal_max"] = 60,
                ["sulfur_min"] = 10,
                ["sulfur_max"] = 50,
                ["hqm_min"] = 1,
                ["hqm_max"] = 1
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file...");
            Config.WriteObject(Cfg, true);
        }
        private Dictionary<string, string> resPrefabs = new Dictionary<string, string>()
        {
            ["stone"] = "stones",
            ["metal"] = "metal.ore",
            ["sulfur"] = "sulfur.ore",
            ["hqm"] = "hq.metal.ore",
            ["crude"] = "crude.oil",
        };
        private Dictionary<string, string> resNames = new Dictionary<string, string>()
        {
            ["stone"] = "Stones",
            ["metal"] = "Metal Ore",
            ["sulfur"] = "Sulfur Ore",
            ["hqm"] = "High Quality Metal Ore",
            ["crude"] = "Crude Oil",
        };
        private string[] resList = { "stone", "metal", "sulfur", "hqm" };

        private Dictionary<string, int> itemIDMap = new Dictionary<string, int>()
        {
            ["surveycharge"] = 1975934948,
            ["lgf"] = -946369541,
            ["diesel"] = 1568388703,

        };

        private Dictionary<ulong, MiningManager> _miningManager = new Dictionary<ulong, MiningManager>();

        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();

        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permProt, this);
        }
        void OnPlayerDisconnected(BasePlayer player) => killUI(player);
        void Unload()
        {
            foreach (var player in UiPlayers)
                CuiHelper.DestroyUi(player, Plugin.Name);
        }
        private static void LoadData<T>(out T data, string filename = null) =>
        data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? Plugin.Name);

        private static void SaveData<T>(T data, string filename = null) =>
        Interface.Oxide.DataFileSystem.WriteObject(filename ?? Plugin.Name, data);

        #endregion

        #region Commands
        [ConsoleCommand("virtualquarries.survey")]
        private void CmdSurvey(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string errorMsg = "none";

            if (player.inventory.GetAmount(itemIDMap["surveycharge"]) < 1)
            {
                errorMsg = "You need Survey Charges for that!";
            }
            else
            {
                player.inventory.Take(null, itemIDMap["surveycharge"], 1);
                MiningManager mm = MiningManager.Get(player.userID);
                mm.RunSurvey();
            }
            reloadUI(player, errorMsg);

        }

        [ConsoleCommand("virtualquarries.buyquarry")]
        private void CmdBuyQ(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            string errorMsg = "none";


            object bal = ServerRewards?.Call("CheckPoints", player.userID);
            if (bal == null) return;
            int playerRP = (int)bal;
            MiningManager mm = MiningManager.Get(player.userID);
            int buyPrice = Cfg.CostPerQuarryNumber[mm.Quarries.Count()];

            if (playerRP < buyPrice)
            {
                errorMsg = "A quarry costs " + buyPrice + " RP, you only have " + playerRP;
            }
            else
            {

                if (mm.addQuarry())
                {
                    ServerRewards?.Call("TakePoints", player.userID, buyPrice);
                }
                else
                {
                    errorMsg = "Could not add Quarry";
                }
            }

            reloadUI(player, errorMsg);

        }
        [ConsoleCommand("virtualquarries.sellquarry")]
        private void CmdSellQ(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse) || arg.Args.Length != 1)
                return;
            string errorMsg = "none";
            int quarryId = arg.GetInt(0);


            MiningManager mm = MiningManager.Get(player.userID);
            mm.takeFuel(player, quarryId);
            mm.takeRes(player, quarryId);
            if (!mm.removeQuarry(quarryId))
            {
                errorMsg = "Invalid Quarry ID!";
            }
            else
            {
                SendReply(player, "Quarry has been destroyed");
            }
            reloadUI(player, errorMsg);

        }

        [ConsoleCommand("virtualquarries.addfuel")]
        private void CmdAddFuel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse) || arg.Args.Length != 3)
                return;
            string errorMsg = "none";
            int quarryId = arg.GetInt(0);

            string fuelType = arg.GetString(1);
            int fuelAmount = arg.GetInt(2);

            int playerFuel = player.inventory.GetAmount(itemIDMap[fuelType]);
            MiningManager mm = MiningManager.Get(player.userID);
            if (playerFuel < fuelAmount)
            {
                errorMsg = "Not enough fuel in inventory!";
            }
            else
            {
                player.inventory.Take(null, itemIDMap[fuelType], fuelAmount);
                mm.addFuel(quarryId, fuelAmount, fuelType);
            }
            reloadUI(player, errorMsg);

        }

        [ConsoleCommand("virtualquarries.takefuel")]
        private void CmdTakeFuel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse) || arg.Args.Length != 1)
                return;
            string errorMsg = "none";
            int quarryId = arg.GetInt(0);

            MiningManager mm = MiningManager.Get(player.userID);
            mm.takeFuel(player, quarryId);

            reloadUI(player, errorMsg);

        }
        [ConsoleCommand("virtualquarries.takeres")]
        private void CmdTakeRes(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse) || arg.Args.Length != 1)
                return;
            string errorMsg = "none";
            int quarryId = arg.GetInt(0);

            MiningManager mm = MiningManager.Get(player.userID);
            mm.takeRes(player, quarryId);

            reloadUI(player, errorMsg);
        }

        [ConsoleCommand("virtualquarries.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            killUI(player);
        }

        [ChatCommand("mm")]
        void QuarryCmd(BasePlayer player, string command, string[] args)
        {
            if (HasPermission(player.UserIDString, permUse))
            {
                reloadUI(player);
            }
            else
            {
                SendReply(player, "You have no permission to use this command.");
            }
        }

        #endregion

        #region Helpers
        private void reloadUI(BasePlayer player, string errorMsg = "none")
        {
            if (!UiPlayers.Contains(player))
            {
                UiPlayers.Add(player);
            }
            CuiHelper.DestroyUi(player, Plugin.Name);
            displayInfo(player, errorMsg);
        }
        private void killUI(BasePlayer player)
        {
            if (UiPlayers.Contains(player))
            {
                UiPlayers.Remove(player);
            }
            CuiHelper.DestroyUi(player, Plugin.Name);
        }

        #endregion

        #region Classes

        private class MiningManager
        {
            public ulong ownerID;
            public Dictionary<int, MiningEntity> Quarries = new Dictionary<int, MiningEntity>();
            public DateTime lastAccessTime = DateTime.Now;
            private MiningEntity currentQuarry;
            public Dictionary<string, string> LastSurvey = new Dictionary<string, string>();

            public MiningManager(ulong ownerId) : base()
            {
                ownerID = ownerId;
            }
            public static MiningManager Get(ulong id)
            {
                if (Plugin._miningManager.ContainsKey(id))
                    return Plugin._miningManager[id];

                var fileName = $"{Plugin.Name}/{id}";

                MiningManager manager;
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(fileName))
                {
                    VirtualQuarries.LoadData(out manager, fileName);
                }
                else
                {
                    manager = new MiningManager(id);
                    VirtualQuarries.SaveData(manager, fileName);
                }

                Interface.Oxide.DataFileSystem.GetDatafile(fileName).Settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore
                };

                Plugin._miningManager.Add(id, manager);

                return manager;
            }

            public void RunSurvey()
            {
                Dictionary<string, string> survey = new Dictionary<string, string>();
                System.Random random = new System.Random();
                if (UnityEngine.Random.Range(0f, 1f) >= Cfg.SurveyEfficiency)
                {
                    survey.Add("SurveyFailed", "true");
                    LastSurvey = survey;
                    SaveData();
                    return;
                }
                // determine types
                string res1type = Plugin.resList[random.Next(0, Plugin.resList.Length)];
                string res2type = string.Empty;
                while (res2type == string.Empty || res2type == res1type)
                {
                    res2type = Plugin.resList[random.Next(0, Plugin.resList.Length)];
                }
                // get values
                float res1val = UnityEngine.Random.Range(Cfg.ResPerMinute[res1type + "_min"], Cfg.ResPerMinute[res1type + "_max"]);
                float res2val = UnityEngine.Random.Range(Cfg.ResPerMinute[res2type + "_min"], Cfg.ResPerMinute[res2type + "_max"]);

                if (Quarries.Count() > Cfg.EfficiencyLossAfterQuarryNr)
                {
                    int lossLevel = Quarries.Count() - Cfg.EfficiencyLossAfterQuarryNr;
                    for (int i = 1; i <= lossLevel; i++)
                    {
                        res1val *= (1 - Cfg.EfficiencyLossPerQuarry);
                        res2val *= (1 - Cfg.EfficiencyLossPerQuarry);
                    }
                }

                survey.Add("res1val", Math.Ceiling(res1val).ToString());
                survey.Add("res2val", Math.Ceiling(res2val).ToString());
                survey.Add("res1type", res1type);
                survey.Add("res2type", res2type);
                LastSurvey = survey;
                SaveData();
            }

            public bool addQuarry()
            {
                if (LastSurvey.Count < 2 || Quarries.Count() >= Cfg.MaxQuarries) return false;

                MiningEntity mine = new MiningEntity(ownerID, float.Parse(LastSurvey["res1val"]), float.Parse(LastSurvey["res2val"]), LastSurvey["res1type"], LastSurvey["res2type"]);
                Quarries.Add(mine.entityId, mine);
                LastSurvey = new Dictionary<string, string>();
                SaveData();
                return true;
            }
            public bool removeQuarry(int quarryId)
            {
                if (!Quarries.ContainsKey(quarryId)) return false;
                Quarries.Remove(quarryId);
                SaveData();
                return true;
            }

            public void addFuel(int quarryId, float amount, string type = "lgf")
            {
                MiningEntity currentQuarry = Quarries[quarryId];
                if (currentQuarry == null) return;
                updateMine(quarryId);
                switch (type)
                {
                    case "lgf":
                        currentQuarry.currentFuel += amount;
                        SaveData();
                        break;
                    case "diesel":
                        currentQuarry.currentDiesel += amount;
                        SaveData();
                        break;
                    default:
                        break;
                }
            }
            public void takeFuel(BasePlayer player, int quarryId)
            {
                MiningEntity currentQuarry = Quarries[quarryId];
                if (currentQuarry == null) return;
                updateMine(quarryId);

                Item fuel;
                if (currentQuarry.currentFuel > 0.0f)
                {
                    fuel = ItemManager.CreateByItemID(Plugin.itemIDMap["lgf"], (int)Math.Floor(currentQuarry.currentFuel), 0UL);
                    player.GiveItem(fuel, BaseEntity.GiveItemReason.PickedUp);
                    currentQuarry.currentFuel = 0.0f;
                }
                if (currentQuarry.currentDiesel > 0.0f)
                {
                    fuel = ItemManager.CreateByItemID(Plugin.itemIDMap["diesel"], (int)Math.Floor(currentQuarry.currentDiesel), 0UL);
                    player.GiveItem(fuel, BaseEntity.GiveItemReason.PickedUp);
                    currentQuarry.currentDiesel = 0.0f;
                }
                SaveData();
            }
            public void takeRes(BasePlayer player, int quarryId)
            {
                MiningEntity currentQuarry = Quarries[quarryId];
                if (currentQuarry == null) return;
                updateMine(quarryId);

                Item res;
                if (currentQuarry.currentAmountPrimary > 0.0f)
                {
                    res = ItemManager.CreateByName(Plugin.resPrefabs[currentQuarry.primaryResource], (int)Math.Floor(currentQuarry.currentAmountPrimary), 0UL);
                    player.GiveItem(res, BaseEntity.GiveItemReason.PickedUp);
                    currentQuarry.currentAmountPrimary = 0.0f;
                }
                if (currentQuarry.currentAmountSecondary > 0.0f)
                {
                    res = ItemManager.CreateByName(Plugin.resPrefabs[currentQuarry.secondaryResource], (int)Math.Floor(currentQuarry.currentAmountSecondary), 0UL);
                    player.GiveItem(res, BaseEntity.GiveItemReason.PickedUp);
                    currentQuarry.currentAmountSecondary = 0.0f;
                }
                SaveData();
            }

            public void updateMine(int quarryId)
            {
                MiningEntity currentQuarry = Quarries[quarryId];
                if (currentQuarry == null) return;

                currentQuarry.RunCalculation();
                SaveData();
            }

            public void SaveData()
            {
                VirtualQuarries.SaveData(this, $"{Plugin.Name}/{ownerID}");
            }

        }
        private class MiningEntity
        {
            public ulong ownerId;
            public int entityId;
            public DateTime lastAccessTime;
            public float miningPerMinutePrimary;
            public float miningPerMinuteSecondary;
            public string primaryResource;
            public string secondaryResource;
            public float currentAmountPrimary = 0.0f;
            public float currentAmountSecondary = 0.0f;
            public float currentFuel = 0.0f;
            public float currentDiesel = 0.0f;
            private float FuelPerHour = Cfg.LgfPerHour;
            private float DieselPerHour = Cfg.DieselPerHour;

            public MiningEntity(ulong oId, float mPrime, float mSec, string rPrime, string rSec) : base()
            {
                ownerId = oId;
                entityId = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                lastAccessTime = DateTime.Now;
                miningPerMinutePrimary = mPrime;
                miningPerMinuteSecondary = mSec;
                primaryResource = rPrime;
                secondaryResource = rSec;
            }
            public bool IsRunning()
            {
                return !(currentDiesel == 0f && currentFuel == 0f);
            }

            public float FuelForHours()
            {
                return (currentFuel / FuelPerHour + currentDiesel / DieselPerHour);
            }

            public void RunCalculation()
            {
                DateTime runtime = DateTime.Now;
                TimeSpan span = runtime.Subtract(lastAccessTime);
                int minutesSinceLastCalc = (int)Math.Floor(span.TotalMinutes);
                // update access time to now
                lastAccessTime = runtime;
                float dieselBonus = (currentDiesel > 0f) ? Cfg.DieselEfficiencyBoost : 1;

                // not running at all
                if (!IsRunning()) return;
                // we ran out of fuel somewhere
                if (FuelForHours() * 60 <= minutesSinceLastCalc)
                {
                    float fuelForMinutes;
                    if (currentDiesel > 0f)
                    {
                        fuelForMinutes = (currentDiesel / DieselPerHour) * 60;
                        currentDiesel = 0.0f;
                    }
                    else
                    {
                        fuelForMinutes = (currentFuel / FuelPerHour) * 60;
                        currentFuel = 0.0f;
                    }
                    currentAmountPrimary += fuelForMinutes * miningPerMinutePrimary * dieselBonus;
                    currentAmountSecondary += fuelForMinutes * miningPerMinuteSecondary * dieselBonus;
                }
                else
                { // enough fuel for the entire time.
                    currentAmountPrimary += minutesSinceLastCalc * miningPerMinutePrimary * dieselBonus;
                    currentAmountSecondary += minutesSinceLastCalc * miningPerMinuteSecondary * dieselBonus;
                    if (currentDiesel > 0f)
                    {
                        currentDiesel -= minutesSinceLastCalc * (DieselPerHour / 60);
                    }
                    else
                    {
                        currentFuel -= minutesSinceLastCalc * (FuelPerHour / 60);
                    }
                }
            }
        }

        #endregion

        #region GUI
        private void displayInfo(BasePlayer player, string errorMsg = "none")
        {
            var mainName = Plugin.Name;
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
                    AnchorMin = "0.1 0.1",
                    AnchorMax = "0.9 0.9"
                },
                CursorEnabled = true
            }, "Hud", mainName);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "virtualquarries.close",
                    Color = "0.56 0.12 0.12 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.005",
                    AnchorMax = "0.995 0.05"
                },
                Text =
                {
                    Text = "CLOSE",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(closeButton, mainName);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Mining Manager",
                    FontSize = 18,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.95",
                    AnchorMax = "0.995 0.995"
                }
            }, mainName);

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
                    AnchorMin = "0.005 0.95",
                    AnchorMax = "0.995 0.995"
                }
            }, mainName);

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
                        AnchorMin = "0.005 0.95",
                        AnchorMax = "0.995 0.995"
                    }
                }, mainName);
            }

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.0 0.949",
                    AnchorMax = "0.999 0.95"
                },
                CursorEnabled = true
            }, mainName);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Info:\n- UI wil not auto-refresh (yet)!\n- Use Survey Charge from anywhere!\n- max "+Cfg.MaxQuarries+" quarries\n- diminishing output after " +Cfg.EfficiencyLossAfterQuarryNr + " quarries\n  (each 20% less than prev. q)\n- diesel-boost of "+Cfg.DieselEfficiencyBoost+"\n- Destroy will NOT refund!",
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.005 0.005",
                    AnchorMax = "0.15 0.7"
                }
            }, mainName);

            MiningManager mm = MiningManager.Get(player.userID);
            GUIManagerWindow(player, elements, mainName, mm);
            GUIQuarryWindow(player, elements, mainName, mm);

            CuiHelper.AddUi(player, elements);
        }

        private void GUIManagerWindow(BasePlayer player, CuiElementContainer elements, string mainName, MiningManager mm)
        {
            float buttonHeight = 0.03f;
            float contentStart = 0.935f;
            float leftBoundary = 0.005f;
            float leftWidth = 0.15f;
            float space = 0.01f;

            int playerCharges = player.inventory.GetAmount(itemIDMap["surveycharge"]);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Controls",
                    FontSize = 14,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+(contentStart-buttonHeight),
                    AnchorMax = "0.5 "+contentStart
                }
            }, mainName);

            if (mm.Quarries.Count() >= Cfg.MaxQuarries)
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Quarry limit reached!",
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart-2*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart-buttonHeight-space)
                    }
                }, mainName);
                return;
            }

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "virtualquarries.survey",
                    Color = "0.7 0.38 0 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary + " "+(contentStart-2*buttonHeight-space),
                    AnchorMax = leftWidth+" "+(contentStart-buttonHeight-space)
                },
                Text =
                {
                    Text = "Use Survey Charge ("+playerCharges+")",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, mainName);

            string analysisInfoText = "You must run survey first! (click the button)";
            if (mm.LastSurvey.Count > 0 && !mm.LastSurvey.ContainsKey("SurveyFailed"))
            {
                analysisInfoText = "Analysis Results:";
                var res1type = mm.LastSurvey["res1type"];
                var res2type = mm.LastSurvey["res2type"];
                var res1val = mm.LastSurvey["res1val"];
                var res2val = mm.LastSurvey["res2val"];
                int res1max = Cfg.ResPerMinute[res1type + "_max"];
                int res2max = Cfg.ResPerMinute[res2type + "_max"];

                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = Plugin.resNames[(string)res1type],
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart-5*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart - 4*buttonHeight-space)
                    }
                }, mainName);
                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.19 0.19 0.19 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart-6*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart - 5*buttonHeight-space)
                    },
                    CursorEnabled = true
                }, mainName);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = res1val + " / min (max: "+res1max+")",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (leftBoundary+space) + " "+(contentStart-6*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart - 5*buttonHeight-space)
                    }
                }, mainName);

                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = Plugin.resNames[(string)res2type],
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart-7*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart - 6*buttonHeight-space)
                    }
                }, mainName);
                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.19 0.19 0.19 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart-8*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart - 7*buttonHeight-space)
                    },
                    CursorEnabled = true
                }, mainName);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = res2val + " / min (max: "+res2max+")",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (leftBoundary+space)  + " "+(contentStart-8*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart - 7*buttonHeight-space)
                    }
                }, mainName);
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualquarries.buyquarry",
                        Color = "0.7 0.38 0 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart-10*buttonHeight-space),
                        AnchorMax = leftWidth+" "+(contentStart-9*buttonHeight-space)
                    },
                    Text =
                    {
                        Text = "Buy Quarry ("+Cfg.CostPerQuarryNumber[mm.Quarries.Count()]+"RP)",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, mainName);
            }
            if (mm.LastSurvey.Count > 0 && mm.LastSurvey.ContainsKey("SurveyFailed"))
            {
                analysisInfoText = "Survey found nothing.";
            }
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = analysisInfoText,
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary + " "+(contentStart-4*buttonHeight-space),
                    AnchorMax = leftWidth+" "+(contentStart - 3*buttonHeight-space)
                }
            }, mainName);


        }

        private void GUIQuarryWindow(BasePlayer player, CuiElementContainer elements, string mainName, MiningManager mm)
        {
            float buttonHeight = 0.03f;
            float contentStart = 0.935f;
            float leftBoundary = 0.17f;
            float imgSize = 0.08f;
            float imgResSize = 0.04f;
            float leftWidth = 0.99f;
            float space = 0.02f;
            float lineHeight = imgSize + space;

            float fuelLeftBoundary = 0.505f;
            float contLeftBoundary = fuelLeftBoundary + imgSize + 0.02f + imgResSize;

            int playerCharges = player.inventory.GetAmount(itemIDMap["surveycharge"]);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Your Quarries",
                    FontSize = 14,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary +" "+(contentStart-buttonHeight),
                    AnchorMax = leftWidth+" "+contentStart
                }
            }, mainName);
            int i = 1;
            contentStart -= (buttonHeight + space);
            // main "Each Quarry" loop
            foreach (KeyValuePair<int, MiningEntity> mine in mm.Quarries)
            {
                MiningEntity currentMine = mine.Value;
                int mineId = mine.Key;
                mm.updateMine(mineId);

                string resPrime = Plugin.resPrefabs[currentMine.primaryResource];
                string resSec = Plugin.resPrefabs[currentMine.secondaryResource];
                string state = currentMine.IsRunning() ? "active" : "off";
                double primePerMin = Math.Round(currentMine.miningPerMinutePrimary, 2);
                double secPerMin = Math.Round(currentMine.miningPerMinuteSecondary, 2);

                string fuelType;
                if (currentMine.FuelForHours() == 0f)
                {
                    fuelType = "empty";
                }
                else if (currentMine.currentDiesel > 0f)
                {
                    fuelType = "diesel_barrel";
                    primePerMin = Math.Round(primePerMin * Cfg.DieselEfficiencyBoost, 2);
                    secPerMin = Math.Round(secPerMin * Cfg.DieselEfficiencyBoost, 2);
                }
                else
                {
                    fuelType = "lowgradefuel";
                }

                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.19 0.19 0.19 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " "+(contentStart - lineHeight),
                        AnchorMax = leftWidth+" "+ contentStart
                    },
                    CursorEnabled = true
                }, mainName);

                // Images
                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = mainName,
                    Components =
                    {
                        new CuiImageComponent { ItemId = ItemManager.itemDictionaryByName["mining.quarry"].itemid },
                        new CuiRectTransformComponent {
                            AnchorMin = leftBoundary +" "+(contentStart-lineHeight),
                            AnchorMax = (leftBoundary+imgSize)+" "+contentStart
                        }
                    }
                });

                //infos
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Quarry-"+mine.Key,
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (leftBoundary+imgSize) +" "+(contentStart-buttonHeight),
                        AnchorMax = leftWidth +" "+ contentStart
                    }
                }, mainName);
                var ts = TimeSpan.FromHours(currentMine.FuelForHours());
                string stateText = (state == "off") ? "(" + state + ")" : "(" + state + " - " + Math.Floor(currentMine.FuelForHours()) + "h " + ts.Minutes + "min remaining)";
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = stateText,
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Color = (state == "off") ? "0.56 0.12 0.12 1" : "0.09 0.87 0.09 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (leftBoundary+imgSize+0.12) +" "+(contentStart-buttonHeight),
                        AnchorMax = leftWidth +" "+ contentStart
                    }
                }, mainName);

                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualquarries.sellquarry " + mineId,
                        Color = "0.0 0.0 0.0 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (leftWidth-imgSize) +" "+(contentStart-buttonHeight),
                        AnchorMax = leftWidth +" "+ contentStart
                    },
                    Text =
                    {
                        Text = "Destroy",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, mainName);



                // resource panel
                float resLeftOverview = leftBoundary + imgSize + 0.002f;
                float resLeftBoundary = resLeftOverview + 0.18f;
                string dieselBonusString = (fuelType == "diesel_barrel") ? "(Diesel-Bonus x" + Cfg.DieselEfficiencyBoost + ")" : "";
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Output: "+dieselBonusString+"\n- " +resNames[currentMine.primaryResource] +": " + primePerMin + " / min\n- "+resNames[currentMine.secondaryResource] +": " + secPerMin + " / min\n ",
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (resLeftOverview)+" "+(contentStart-lineHeight),
                        AnchorMax = leftWidth +" "+ (contentStart-buttonHeight)
                    }
                }, mainName);

                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = resLeftBoundary +" "+(contentStart-lineHeight),
                        AnchorMax = (resLeftBoundary+imgResSize) +" "+ (contentStart-buttonHeight-0.002)
                    },
                    CursorEnabled = true
                }, mainName);

                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = mainName,
                    Components =
                    {
                        new CuiImageComponent { ItemId = ItemManager.itemDictionaryByName[resPrime].itemid },
                        new CuiRectTransformComponent {
                            AnchorMin = (resLeftBoundary) +" "+(contentStart-lineHeight),
                            AnchorMax = (resLeftBoundary+imgResSize) +" "+ (contentStart-buttonHeight)
                        }
                    }
                });
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "" + Math.Floor(currentMine.currentAmountPrimary),
                        FontSize = 10,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (resLeftBoundary) +" "+(contentStart-lineHeight),
                        AnchorMax = (resLeftBoundary+imgResSize-0.002) +" "+ (contentStart-2*buttonHeight-0.016)
                    }
                }, mainName);
                float resLeftBoundary2 = resLeftBoundary + imgResSize + 0.005f;
                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = resLeftBoundary2 +" "+(contentStart-lineHeight),
                        AnchorMax = (resLeftBoundary2+imgResSize) +" "+ (contentStart-buttonHeight-0.002)
                    },
                    CursorEnabled = true
                }, mainName);

                elements.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = mainName,
                    Components =
                    {
                        new CuiImageComponent { ItemId = ItemManager.itemDictionaryByName[resSec].itemid },
                        new CuiRectTransformComponent {
                            AnchorMin = (resLeftBoundary2) +" "+(contentStart-lineHeight),
                            AnchorMax = (resLeftBoundary2+imgResSize) +" "+ (contentStart-buttonHeight)
                        }
                    }
                });
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "" + Math.Floor(currentMine.currentAmountSecondary),
                        FontSize = 10,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (resLeftBoundary2) +" "+(contentStart-lineHeight),
                        AnchorMax = (resLeftBoundary2+imgResSize-0.002) +" "+ (contentStart-2*buttonHeight-0.016)
                    }
                }, mainName);


                float resLeftBoundary3 = resLeftBoundary2 + imgResSize + 0.005f;

                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "virtualquarries.takeres " + mineId,
                        Color = "0.7 0.38 0 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (resLeftBoundary3) +" "+(contentStart-lineHeight+0.002),
                        AnchorMax = (resLeftBoundary3 + 0.03)+" "+ (contentStart-2*buttonHeight-0.007)
                    },
                    Text =
                    {
                        Text = "Take",
                        FontSize = 10,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }, mainName);


                float buttonWidth = 0.09f;
                float contLeftBoundary2;
                float contLeftBoundary3;
                // info details
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Fuel",
                        FontSize = 10,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (fuelLeftBoundary+imgSize) +" "+(contentStart-2*buttonHeight),
                        AnchorMax = leftWidth +" "+ (contentStart-buttonHeight)
                    }
                }, mainName);


                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = (fuelLeftBoundary+imgSize+0.02f) +" "+(contentStart-lineHeight),
                        AnchorMax = (contLeftBoundary) +" "+ (contentStart-buttonHeight-0.002)
                    },
                    CursorEnabled = true
                }, mainName);

                if (fuelType == "empty")
                {

                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = "empty",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (fuelLeftBoundary+imgSize+0.02f) +" "+(contentStart-lineHeight),
                            AnchorMax = (contLeftBoundary) +" "+ (contentStart-buttonHeight)
                        }
                    }, mainName);

                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " lgf "+ Cfg.LgfPerHour,
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary+0.002) +" "+(contentStart-lineHeight+buttonHeight+0.007),
                            AnchorMax = (contLeftBoundary+buttonWidth)+" "+ (contentStart-buttonHeight-0.002)
                        },
                        Text =
                        {
                            Text = "Add 1h LGF ("+Cfg.LgfPerHour+")",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);

                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " diesel  "+ Cfg.DieselPerHour,
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary+0.002) +" "+(contentStart-lineHeight+0.002),
                            AnchorMax = (contLeftBoundary+buttonWidth)+" "+ (contentStart-2*buttonHeight-0.007)
                        },
                        Text =
                        {
                            Text = "Add 1h Diesel ("+ Cfg.DieselPerHour +")",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);
                }
                else if (fuelType == "lowgradefuel")
                {

                    elements.Add(new CuiElement
                    {
                        Name = CuiHelper.GetGuid(),
                        Parent = mainName,
                        Components =
                        {
                            new CuiImageComponent { ItemId = ItemManager.itemDictionaryByName[fuelType].itemid },
                            new CuiRectTransformComponent {
                                AnchorMin = (fuelLeftBoundary+imgSize+0.02f) +" "+(contentStart-lineHeight),
                                AnchorMax = (contLeftBoundary) +" "+ (contentStart-buttonHeight)
                            }
                        }
                    });
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = "" + Math.Ceiling(currentMine.currentFuel),
                            FontSize = 10,
                            Align = TextAnchor.MiddleRight,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (fuelLeftBoundary+imgSize+0.02f) +" "+(contentStart-lineHeight),
                            AnchorMax = (contLeftBoundary-0.002) +" "+ (contentStart-2*buttonHeight-0.016)
                        }
                    }, mainName);
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.takefuel " + mineId,
                            Color = "0.7 0.38 0 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary+0.002) +" "+(contentStart-lineHeight+buttonHeight+0.007),
                            AnchorMax = (contLeftBoundary+buttonWidth)+" "+ (contentStart-buttonHeight-0.002)
                        },
                        Text =
                        {
                            Text = "Take out fuel",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);

                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " lgf 100",
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary+0.002) +" "+(contentStart-lineHeight+0.002),
                            AnchorMax = (contLeftBoundary+buttonWidth)+" "+ (contentStart-2*buttonHeight-0.007)
                        },
                        Text =
                        {
                            Text = "Add 100",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);
                    contLeftBoundary2 = (float)(contLeftBoundary + buttonWidth + 0.002);
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " lgf 1000",
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary2)+" "+(contentStart-lineHeight+0.002),
                            AnchorMax = (contLeftBoundary2+buttonWidth)+" "+ (contentStart-2*buttonHeight-0.007)
                        },
                        Text =
                        {
                            Text = "Add 1000",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);
                    contLeftBoundary3 = (float)(contLeftBoundary2 + buttonWidth + 0.002);
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " lgf 10000",
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary3)+" "+(contentStart-lineHeight+0.002),
                            AnchorMax = (contLeftBoundary3+buttonWidth)+" "+ (contentStart-2*buttonHeight-0.007)
                        },
                        Text =
                        {
                            Text = "Add 10000",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);

                }
                else if (fuelType == "diesel_barrel")
                {

                    elements.Add(new CuiElement
                    {
                        Name = CuiHelper.GetGuid(),
                        Parent = mainName,
                        Components =
                        {
                            new CuiImageComponent { ItemId = ItemManager.itemDictionaryByName[fuelType].itemid },
                            new CuiRectTransformComponent {
                                AnchorMin = (fuelLeftBoundary+imgSize+0.02f) +" "+(contentStart-lineHeight),
                                AnchorMax = (contLeftBoundary) +" "+ (contentStart-buttonHeight)
                            }
                        }
                    });
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = "" + Math.Ceiling(currentMine.currentDiesel),
                            FontSize = 10,
                            Align = TextAnchor.MiddleRight,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (fuelLeftBoundary+imgSize+0.02f) +" "+(contentStart-lineHeight),
                            AnchorMax = (contLeftBoundary-0.002) +" "+ (contentStart-2*buttonHeight-0.016)
                        }
                    }, mainName);
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.takefuel " + mineId,
                            Color = "0.7 0.38 0 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary+0.002) +" "+(contentStart-lineHeight+buttonHeight+0.007),
                            AnchorMax = (contLeftBoundary+buttonWidth)+" "+ (contentStart-buttonHeight-0.002)
                        },
                        Text =
                        {
                            Text = "Take out fuel",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);


                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " diesel 2",
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary+0.002) +" "+(contentStart-lineHeight+0.002),
                            AnchorMax = (contLeftBoundary+buttonWidth)+" "+ (contentStart-2*buttonHeight-0.007)
                        },
                        Text =
                        {
                            Text = "Add 2",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);
                    contLeftBoundary2 = (float)(contLeftBoundary + buttonWidth + 0.002);
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " diesel 10",
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary2)+" "+(contentStart-lineHeight+0.002),
                            AnchorMax = (contLeftBoundary2+buttonWidth)+" "+ (contentStart-2*buttonHeight-0.007)
                        },
                        Text =
                        {
                            Text = "Add 10",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);
                    contLeftBoundary3 = (float)(contLeftBoundary2 + buttonWidth + 0.002);
                    elements.Add(new CuiButton
                    {
                        Button =
                        {
                            Command = "virtualquarries.addfuel " + mineId + " diesel 40",
                            Color = "0.19 0.19 0.19 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = (contLeftBoundary3)+" "+(contentStart-lineHeight+0.002),
                            AnchorMax = (contLeftBoundary3+buttonWidth)+" "+ (contentStart-2*buttonHeight-0.007)
                        },
                        Text =
                        {
                            Text = "Add 40",
                            FontSize = 10,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    }, mainName);

                }


                // next line
                contentStart -= lineHeight + space;
                i++;
            }
        }
        #endregion


    }
}
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
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("TimedRewards", "DocValerian", "1.2.0")]
    class TimedRewards : RustPlugin
    {
        static TimedRewards Plugin;
        
        [PluginReference]
        private Plugin ImageLibrary, PlaytimeTracker;
        
        const string permUse = "timedrewards.use";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region ConfigDataLoad
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
            [JsonProperty(PropertyName = "Reward Item:amount per minute playtime (max 6)")]
            public Dictionary<int, Dictionary<string, int>> MinuteRewardLists = new Dictionary<int, Dictionary<string, int>>() {
                [0] = new Dictionary<string, int>()
                {
                    ["blueberries"] = 5,
                    ["lowgradefuel"] = 10,
                    ["pookie.bear"] = 1,
                    ["wood"] = 5000
                },
                [30] = new Dictionary<string, int>()
                {
                    ["metal.refined"] = 50,
                    ["blueberries"] = 10,
                    ["generator.wind.scrap"] = 1,
                    ["metal.refined"] = 50,
                    ["blood"] = 2000
                },
                [60] = new Dictionary<string, int>()
                {
                    ["ammo.rifle"] = 200,
                    ["blueberries"] = 20,
                    ["targeting.computer"] = 6,
                    ["diesel_barrel"] = 20,
                    ["keycard_blue"] = 1
                },
                [120] = new Dictionary<string, int>()
                {
                    ["ammo.rocket.basic"] = 4,
                    ["blueberries"] = 20,
                    ["lowgradefuel"] = 300,
                    ["metal.fragments"] = 1500,
                    ["scrap"] = 1000,
                    ["supply.signal"] = 1
                },
                [240] = new Dictionary<string, int>()
                {
                    ["blueberries"] = 20,
                    ["targeting.computer"] = 10,
                    ["rifle.ak"] = 1,
                    ["metal.refined"] = 200,
                    ["supply.signal"] = 1,
                    ["sulfur"] = 2000
                },
                [480] = new Dictionary<string, int>()
                {
                    ["sticks"] = 500,
                    ["blueberries"] = 40,
                    ["blood"] = 3000,
                    ["metal.refined"] = 150,
                    ["supply.signal"] = 1,
                    ["explosive.timed"] = 5
                }
            };

            [JsonProperty(PropertyName = "Chat command aliases")]
            public string[] commandAliasList = { "dr", "daily" };
        }
        class StoredData
        {
            public DateTime currentDay = DateTime.Now;
            public Dictionary<ulong, double> PlayerDayStartTimes = new Dictionary<ulong, double>();
            public Dictionary<ulong, List<int>> PlayerClaimedRewards = new Dictionary<ulong, List<int>>();
        }
        
        StoredData storedData;

        private int highestRewardTime = 0; 
        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>(); 
            Plugin = this;
            permission.RegisterPermission(permUse, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("TimedRewards");
            SaveData();
            highestRewardTime = Cfg.MinuteRewardLists.Keys.Max();
            string[] commandAliases = Cfg.commandAliasList;
            foreach (string cmdAlias in commandAliases)
                cmd.AddChatCommand(cmdAlias, this, "CommandOpenUi");
        }
        void Unload()
        {
            SaveData();
            foreach (var player in UiPlayers.ToList())
            {
                killUI(player);
            }
        }
        private void OnServerInitialized()
        {
            if (!PlaytimeTracker)
            {
                Debug.LogError("[TimedRewards] Error! PlaytimeTracker not loaded! Please fix this error before loading TimedRewards again. Unloading...");
                Interface.Oxide.UnloadPlugin(this.Title);
                return;
            }
            ensureDayChange();
            // add starting playtime of the day to players
            foreach (var p in BasePlayer.activePlayerList)
            {
                addStartingPlaytime(p);
            }
            SaveData();
        }

        private void OnNewSave()
        {
            storedData = new StoredData();
            SaveData();
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("TimedRewards", storedData);
        }

        #endregion

        #region Hooks
        private void OnPlayerConnected(BasePlayer player)
        {
            ensureDayChange();
            //add playtime if first login of the day.
            addStartingPlaytime(player);
            SaveData();
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            killUI(player);
        }
        #endregion

        #region Commands

        [ConsoleCommand("timedrewards.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            killUI(player);
        }

        [ConsoleCommand("timedrewards.claim")]
        private void CmdClaim(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            int package = arg.GetInt(0);
            claimLoot(player, package);
            reloadUI(player);
        }

       
        void CommandOpenUi(BasePlayer player, string command, string[] args)
        {
            if (!permCheckVerbose(player, permUse)) return;
            ensureDayChange();
            reloadUI(player);
        }


        #endregion

        #region Functions
        private bool permCheckVerbose(BasePlayer player, string perm)
        {
            if (HasPermission(player.UserIDString, perm)) return true;
            SendReply(player, "No permission to use this command!");
            return false;
        }

        private void claimLoot(BasePlayer player, int package)
        {
            if (!Cfg.MinuteRewardLists.ContainsKey(package)) return;
            if (storedData.PlayerClaimedRewards.ContainsKey(player.userID))
            {
                if (storedData.PlayerClaimedRewards[player.userID].Contains(package))
                {
                    SendReply(player, "You already claimed the " + package + "min reward!");
                    return;
                }
            }
            else
            {
                storedData.PlayerClaimedRewards.Add(player.userID, new List<int>());
            }
            storedData.PlayerClaimedRewards[player.userID].Add(package);

            Item item;
            foreach (KeyValuePair<string, int> loot in Cfg.MinuteRewardLists[package])
            {
                item = ItemManager.CreateByName(loot.Key, loot.Value);
                player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);   
            }
            SendReply(player, "You have claimed the " + package + "min reward!");
            SaveData();
        }

        private int getTodayPlayTime(BasePlayer player)
        {
            if (!storedData.PlayerDayStartTimes.ContainsKey(player.userID))
            {
                addStartingPlaytime(player);
                SaveData();
                return 0;
            }
            var playerTime = PlaytimeTracker?.Call("GetPlayTime", player.UserIDString);
            if (playerTime == null) return 0;

            double playTime = (double)playerTime - storedData.PlayerDayStartTimes[player.userID];

            return (int)Math.Floor(playTime);
        }

        private void addStartingPlaytime(BasePlayer player)
        {
            // add only if player didn't login sooner today
            if (storedData.PlayerDayStartTimes.ContainsKey(player.userID))
            {
                return;
            }

            var playerTime = PlaytimeTracker?.Call("GetPlayTime", player.UserIDString);
            if (playerTime == null)
            {
                storedData.PlayerDayStartTimes.Add(player.userID, 0.0d);
            }
            else
            {
                storedData.PlayerDayStartTimes.Add(player.userID, (double)playerTime);
            }
        }
       
        private void ensureDayChange()
        {
            // reset every calendar day
            if (storedData.currentDay.Day != DateTime.Now.Day)
            {
                Puts("INFO: Daily rewards reset");
                storedData = new StoredData();
                // add starting playtime of the day to players
                foreach (var p in BasePlayer.activePlayerList)
                {
                    addStartingPlaytime(p);
                }
                SaveData();
            }
        }
        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
        #endregion

            #region GUI

        private const string globalNoErrorString = "none";
        private const string mainName = "TimedRewards";
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements =
        {
            mainName+"_head",
            mainName+"_content",
            mainName+"_foot"
        };
        private float globalLeftBoundary = 0.1f;
        private float globalRighttBoundary = 0.9f;
        private float globalTopBoundary = 0.90f;
        private float globalBottomBoundary = 0.2f;
        private float globalSpace = 0.01f;
        private float eContentWidth = 0.395f;
        private float eHeadHeight = 0.05f;
        private float eFootHeight = 0.05f;
        private float eSlotHeight = 0.123f;


        private void reloadUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (!UiPlayers.Contains(player))
            {
                UiPlayers.Add(player);
            }
            foreach (string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
            }

            displayUI(player, errorMsg);

        }
        private void killUI(BasePlayer player)
        {
            if (UiPlayers.Contains(player))
            {
                UiPlayers.Remove(player);
                foreach (string ui in uiElements)
                {
                    CuiHelper.DestroyUi(player, ui);
                }
            }
        }

        private void displayUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            GUIHeaderElement(player, mainName + "_head", errorMsg);
            GUIContentElement(player, mainName+"_content");
            GUIFooterElement(player, mainName + "_foot");
        }
        private void GUIHeaderElement(BasePlayer player, string elUiId, string errorMsg = globalNoErrorString)
        {
            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = globalLeftBoundary  +" " + (globalTopBoundary-eHeadHeight),
                    AnchorMax = globalRighttBoundary + " " + globalTopBoundary
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            TimeSpan untilMidnight = DateTime.Today.AddDays(1.0) - DateTime.Now;
            string stateText = "Daily Timed Rewards (Day reset in: " + untilMidnight.Hours + "h " + untilMidnight.Minutes + "min)";
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = stateText,
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


        private void GUIContentElement(BasePlayer player, string elUiId)
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
                    AnchorMin = globalLeftBoundary + " " + (globalBottomBoundary+eHeadHeight+globalSpace),
                    AnchorMax = globalRighttBoundary + " " + (globalTopBoundary-eHeadHeight-globalSpace)
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            int rewardCount = Cfg.MinuteRewardLists.Count();
            float localSpace = 0.005f;
            float localboxWidth = (1f/rewardCount) - 1.5f*localSpace;
            float localLeftBound = 1.5f * localSpace;
            float localRightBound = rewardCount * (localboxWidth + localSpace);
            float perMinuteWidth = 0.95f / highestRewardTime;
            float playerTime = (float)Math.Floor(getTodayPlayTime(player)/60f);
            float localRewardTimeLine = 0;
            string rewardPanelName = "";
            float localLootRowTop = 0f;
            float localLootLeft = 0.01f;
            bool isEvenRow = true;

            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Your daily (active) playtime: <color=lime>" + playerTime + " minutes</color> ",
                        FontSize = 18,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBound + " 0.85",
                        AnchorMax = (localRightBound) + " 0.95"
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "* Time is counted over all game sessions between midnight->midnight Server Time!",
                        FontSize = 10,
                        Align = TextAnchor.UpperRight,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBound + " 0.85",
                        AnchorMax = (localRightBound) + " 0.95"
                    }
            }, elUiId);
            // for visualization, we need to cap that value
            if (playerTime > highestRewardTime) playerTime = highestRewardTime;


            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.9",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBound + " 0.74",
                    AnchorMax = (localRightBound) + " 0.84"
                },
                CursorEnabled = true
            }, elUiId, elUiId + "_bar");


            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.5 0.8 0.0 0.9",
                },
                RectTransform =
                {
                    AnchorMin = "0.004 0.1",
                    AnchorMax = playerTime*perMinuteWidth + " 0.9"
                },
                CursorEnabled = true
            }, elUiId + "_bar", elUiId + "_bar_fill");

            foreach (KeyValuePair<int, Dictionary<string, int>> reward in Cfg.MinuteRewardLists.OrderBy(x => x.Key))
            {

                // the loading bar elements
                localRewardTimeLine = (reward.Key * perMinuteWidth);
                if (localRewardTimeLine == 0) localRewardTimeLine = 0.004f;
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "1 1 1 0.9",
                },
                    RectTransform =
                {
                    AnchorMin = localRewardTimeLine + " 0.1",
                    AnchorMax = (localRewardTimeLine+0.001) + " 0.9"
                },
                    CursorEnabled = true
                }, elUiId + "_bar", elUiId + "_bar_line_" + reward.Key);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = reward.Key +"min",
                        FontSize = 11,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = (localRewardTimeLine+0.003) + " 0.1",
                        AnchorMax = (localRewardTimeLine+0.1) + " 0.9"
                    }
                }, elUiId + "_bar");


                // the loot panels
                rewardPanelName = elUiId + "r_" + reward.Key;
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0.7",
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBound + " 0.01",
                    AnchorMax = (localLeftBound+localboxWidth) +" 0.7"
                },
                    CursorEnabled = true
                }, elUiId, rewardPanelName);

                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Reward for " + reward.Key +"min",
                        FontSize = 14,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.05 0.9",
                        AnchorMax = "1 0.995"
                    }
                }, rewardPanelName);

                //loot preview
                localLootRowTop = 0.89f;
                isEvenRow = true;
                foreach (KeyValuePair<string, int> loot in reward.Value)
                {
                    if (isEvenRow)
                    {
                        localLootLeft = 0.01f;
                        isEvenRow = false;
                    }
                    else
                    {
                        localLootLeft = 0.5f;
                        isEvenRow = true;
                    }
                    CuiRawImageComponent img;

                    img = new CuiRawImageComponent { Png = GetImage(ItemManager.itemDictionaryByName[loot.Key].shortname) };
                    
                    elements.Add(new CuiElement
                    {
                        Name = CuiHelper.GetGuid(),
                        Parent = rewardPanelName,
                        Components =
                        {
                            img,
                            new CuiRectTransformComponent {
                                AnchorMin = localLootLeft +" "+ (localLootRowTop-0.20f),
                                AnchorMax = (localLootLeft + 0.35f) + " " + localLootRowTop
                            }
                        }
                    });
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = "x" + loot.Value,
                            FontSize = 10,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = localLootLeft +" "+ (localLootRowTop-0.20f),
                            AnchorMax = (localLootLeft + 0.35f) + " " + localLootRowTop
                        }
                    }, rewardPanelName);

                    if(isEvenRow) localLootRowTop -= 0.25f;
                }

                // claiming button

                // already claimed!
                if(storedData.PlayerClaimedRewards.ContainsKey(player.userID) && storedData.PlayerClaimedRewards[player.userID].Contains(reward.Key))
                {
                    elements.Add(new CuiLabel
                    {
                        Text =
                    {
                        Text = "claimed",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.5 0.8 0.0 1"
                    },
                        RectTransform =
                    {
                        AnchorMin = "0.05 0.01",
                        AnchorMax = "0.95 0.1"
                    }
                    }, rewardPanelName);

                }
                // not unlocked
                else if(playerTime < reward.Key)
                {
                    elements.Add(new CuiLabel
                    {
                        Text =
                    {
                        Text = "unlocked in " + (int)Math.Ceiling(reward.Key-playerTime) +"min",
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    },
                        RectTransform =
                    {
                        AnchorMin = "0.05 0.01",
                        AnchorMax = "0.95 0.1"
                    }
                    }, rewardPanelName);
                }
                // allow claiming
                else
                {
                    var claimButton = new CuiButton
                    {
                        Button =
                        {
                            Command = "timedrewards.claim " + reward.Key,
                            Color = "0.5 0.8 0.0 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0.05 0.01",
                            AnchorMax = "0.95 0.1"
                        },
                        Text =
                        {
                            Text = "Claim!",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                    };
                    elements.Add(claimButton, rewardPanelName);
                }

                localLeftBound = localLeftBound + localboxWidth + localSpace;

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
                    Command = "timedrewards.close",
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

        #endregion

    }
}   
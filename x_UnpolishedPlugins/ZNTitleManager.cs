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
using Oxide.Core.Libraries.Covalence;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("ZNTitleManager", "DocValerian", "1.0.4")]
    class ZNTitleManager : RustPlugin
    {
        static ZNTitleManager Plugin;
        
        [PluginReference]
        private Plugin BetterChat, ServerRewards, InfoAPI;
        
        const string permUse = "zntitlemanager.use";
        private enum titleType {
            WIPE,
            ETERNAL,
            NONE
        };
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
        #endregion
        #region Classes
        class Configuration
        {
            public string titleTagPrefix = "\"";
            public string titleTagSuffix = "\"";
            public string titleTagColor = "#DDDDDD";
            public string titleEternalTagColor = "#FFD700";
        }
        class WipeData
        {
            public Dictionary<string, ZNTitle> titles = new Dictionary<string, ZNTitle>();
            public Dictionary<ulong, string> playerTitles = new Dictionary<ulong, string>();
        }
        WipeData wipeData;
        class EternalData
        {
            public Dictionary<string, ZNTitle> titles = new Dictionary<string, ZNTitle>();
        }
        EternalData eternalData;

        class ZNTitle
        {
            public string achievement { get; set; }
            public string tag_m { get; set; }
            public string tag_f { get; set; }
            public string description { get; set; }
            public string scoreUnit { get; set; }
            public titleType type { get; set; }
            public float holderScore { get; set; }
            public ulong holderID { get; set; }
            public string holderName { get; set; }
            public string holderTag { get; set; }
            public DateTime changeDate { get; set; }
            public ZNTitle() : base()
            {
                holderScore = 0f;
                holderID = 0;
                holderName = "n/a";
                holderTag = "";
                changeDate = DateTime.Now;
            }
        }
        void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);

            wipeData = Interface.Oxide.DataFileSystem.ReadObject<WipeData>("ZNTitleManager_wipe");
            eternalData = Interface.Oxide.DataFileSystem.ReadObject<EternalData>("ZNTitleManager_eternal");
            SaveData();

            string[] commandAliases = { "hs", "score", "pc"};
            foreach (string cmdAlias in commandAliases)
                cmd.AddChatCommand(cmdAlias, this, "OpenTitleUI");
        }

        void Unload()
        {
            foreach (var player in UiPlayers.ToList())
            {
                killUI(player);
            }
        }

        private void OnServerInitialized()
        {
            RegisterTitles();
        }

        private void OnNewSave()
        {
            // reset wipe highscore automatically on wipe
            wipeData = new WipeData();
            SaveData();
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZNTitleManager_wipe", wipeData);
            Interface.Oxide.DataFileSystem.WriteObject("ZNTitleManager_eternal", eternalData);
        }

        #endregion

        #region Hooks
        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Title == "Better Chat")
                RegisterTitles();
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            killUI(player);
        }
        #endregion

        #region Commands

        [ConsoleCommand("zntitles.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            killUI(player);
        }
        [ConsoleCommand("zntitles.wtitle")]
        private void CmdWipeTitle(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, mainName + "_content");
            GUIScoreElement(player, mainName + "_content", wipeData.titles, "Wipe");
        }
        [ConsoleCommand("zntitles.etitle")]
        private void CmdEternalTitle(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, mainName + "_content");
            GUIScoreElement(player, mainName + "_content", eternalData.titles, "All-Time");
        }
        [ConsoleCommand("zntitles.mytitle")]
        private void CmdMyTitle(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, mainName + "_content");
            GUITitleElement(player, mainName + "_content");
        }
        [ConsoleCommand("zntitles.setmytitle")]
        private void CmdSetMyTitle(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            string titleId = arg.GetString(0);
            string titleVersion = arg.GetString(1);
            ZNTitle title = null;
            if(wipeData.titles.ContainsKey(titleId))
            {
                title = wipeData.titles[titleId];
            }
            else if(eternalData.titles.ContainsKey(titleId))
            {
                title = eternalData.titles[titleId];
            }
            else
            {
                return;
            }
            //can't add titles you don't hold
            if (title.holderID != player.userID) return;
            string tVariant = (titleVersion == "m") ? title.tag_m : title.tag_f;
            title.holderTag = tVariant;
            wipeData.playerTitles[player.userID] = titleId;
            SaveData();
            InfoAPI.Call("ShowInfoPopup", player, "Title set to <color=orange>"+tVariant+"</color>.");

            CuiHelper.DestroyUi(player, mainName + "_content");
            GUITitleElement(player, mainName + "_content");
        }

        [ConsoleCommand("zntitles.resetmytitle")]
        private void CmdReSetMyTitle(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            wipeData.playerTitles.Remove(player.userID);
            SaveData();
            InfoAPI.Call("ShowInfoPopup", player, "Title <color=orange>reset</color>.");

            CuiHelper.DestroyUi(player, mainName + "_content");
            GUITitleElement(player, mainName + "_content");
        }

        [ConsoleCommand("zntitles.admadd")]
        private void CmdAdmAdd(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !player.IsAdmin)
                return;
            string titleId = arg.GetString(0);
            float score = arg.GetFloat(1);
            ulong pId = ulong.Parse(arg.GetString(2));
            bool lower = arg.GetBool(3);
            CheckScore(titleId, score, pId, lower);
        }
        void OpenTitleUI(BasePlayer player, string command, string[] args)
        {
            reloadUI(player);
        }
        /*
        [ChatCommand("ll")]
        void TESTcommand(BasePlayer player, string command, string[] args)
        {
            reloadUI(player, "test");
        }
        */

        #endregion

        #region API

        private void CheckScore(string id, float score, ulong playerID, bool lowIsBetter = false)
        {
            ZNTitle t;
            string eId = id + "_eternal";
            titleType gotTitle = titleType.NONE;
            BasePlayer p = BasePlayer.FindAwakeOrSleeping(playerID.ToString());
            string oldHolderName = "";

            if (wipeData.titles.ContainsKey(id))
            {
                t = wipeData.titles[id];
                if((t.holderScore < score && !lowIsBetter) || (lowIsBetter && (t.holderScore > score || t.holderScore == 0)))
                {
                    oldHolderName = t.holderName;
                    gotTitle = titleType.WIPE;
                    t.holderScore = score;
                    t.holderID = playerID;
                    t.holderName = (p != null) ? p.displayName : "";
                    t.holderTag = t.tag_m;
                    t.changeDate = DateTime.Now;
                }
            }
            // Eternal Variant
            if (eternalData.titles.ContainsKey(eId))
            {
                t = eternalData.titles[eId];
                if ((t.holderScore < score && !lowIsBetter) || (lowIsBetter && (t.holderScore > score || t.holderScore == 0)))
                {
                    oldHolderName = t.holderName;
                    gotTitle = titleType.ETERNAL;
                    t.holderScore = score;
                    t.holderID = playerID;
                    t.holderName = (p != null) ? p.displayName : "";
                    t.holderTag = t.tag_m;
                    t.changeDate = DateTime.Now;
                }
            }
            if(gotTitle == titleType.WIPE)
            {
                t = wipeData.titles[id];
                SaveData();
                Puts("DEBUG: New High-Score: " + id + " for " + playerID);
                if (p != null) InfoAPI.Call("ShowInfoPopup", p, "You've got a new Wipe Highscore!\nCheck <color=orange>/hs</color> to get a title.");
                if(oldHolderName != t.holderName && oldHolderName != "n/a")
                {
                    Server.Broadcast("<color=orange>Highscores:</color> - "+t.description+" -\n<color=green>" + t.holderName+ "</color> took the title from <color=green>" + oldHolderName + "</color>");
                }
            }
            if (gotTitle == titleType.ETERNAL)
            {
                t = eternalData.titles[eId];
                SaveData(); 
                Puts("DEBUG: New ETERNAL High-Score: " + eId + " for " + playerID);
                if (p != null) InfoAPI.Call("ShowInfoPopup", p, "You've got a new ALL-TIME Highscore!\nCheck <color=orange>/hs</color> to get a title.");
                if (oldHolderName != t.holderName && oldHolderName != "n/a")
                {
                    Server.Broadcast("<color=orange>Highscores:</color> - " + t.description + " -\n<color=green>" + t.holderName + "</color> took the title from <color=green>" + oldHolderName + "</color>");
                }
            }
        }

        private void UpsertTitle(string id, string description, string scoreUnit, string tag_e_m, string tag_e_f, string tag_m, string tag_f)
        {
            ZNTitle t;
            string tId = id;
            // Wipe title
            if (wipeData.titles.ContainsKey(tId))
            {
                t = wipeData.titles[tId];
            }
            else
            {
                t = new ZNTitle();
                t.achievement = tId;
                t.type = titleType.WIPE;
            }
            t.description = description + " (High Score)";
            t.tag_f = tag_f;
            t.tag_m = tag_m;
            t.scoreUnit = scoreUnit;
            wipeData.titles[tId] = t;
            // Eternal Variant
            tId = id + "_eternal";
            if (eternalData.titles.ContainsKey(tId))
            {
                t = eternalData.titles[tId];
            }
            else
            {
                t = new ZNTitle();
                t.achievement = tId;
                t.type = titleType.ETERNAL;
            }
            t.description = description + " (All-Time High Score)";
            t.tag_f = tag_e_f;
            t.tag_m = tag_e_m;
            t.scoreUnit = scoreUnit;
            eternalData.titles[tId] = t;
                    
            Puts("DEBUG: upserted title: " + tId);
            SaveData();
        }
        #endregion

        #region Functions
       

        private void RegisterTitles()
        {
            //if (!BetterChat) return;
            BetterChat?.Call("API_RegisterThirdPartyTitle", new object[] { this, new Func<IPlayer, string>(getChatTitle) });
        }

        private string getChatTitle(IPlayer player)
        {
            
            ulong pid = 0;
            if (!ulong.TryParse(player.Id, out pid))
            {
                Puts("ERROR: parsing id for " + player);
                return String.Empty;
            }
            return GetPlayerTitle(pid);
            
        }
        private string GetPlayerTitle(ulong pid, bool decoration = true)
        {
            string playerTitle = string.Empty;
            if (wipeData.playerTitles.ContainsKey(pid))
            {
                string titleId = wipeData.playerTitles[pid];
                if (wipeData.titles.ContainsKey(titleId))
                {
                    if (wipeData.titles[titleId].holderID != pid) return playerTitle;
                    if (decoration)
                    {
                        playerTitle = "[" + Cfg.titleTagColor + "]" + Cfg.titleTagPrefix + wipeData.titles[titleId].holderTag + Cfg.titleTagSuffix + "[/#]";
                    }
                    else
                    {
                        playerTitle = wipeData.titles[titleId].holderTag;
                    }
                }
                if (eternalData.titles.ContainsKey(titleId))
                {

                    if (eternalData.titles[titleId].holderID != pid) return playerTitle;
                    if (decoration)
                    {
                        playerTitle = "[" + Cfg.titleEternalTagColor + "]" + Cfg.titleTagPrefix + eternalData.titles[titleId].holderTag + Cfg.titleTagSuffix + "[/#]";
                    }
                    else
                    {
                        playerTitle = eternalData.titles[titleId].holderTag;
                    }
                }
            }
            return playerTitle;

        }

        private List<ZNTitle> getPlayerTitles(BasePlayer p)
        {
            List<ZNTitle> l = new List<ZNTitle>();
            foreach (ZNTitle t in eternalData.titles.Values)
            {
                if (t.holderID == p.userID) l.Add(t);
            }
            foreach (ZNTitle t in wipeData.titles.Values)
            {
                if (t.holderID == p.userID) l.Add(t);
            }
            return l;
        }
        #endregion

        #region GUI

        private const string globalNoErrorString = "none";
        private const string mainName = "ZNTitles";
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements = 
        {
            mainName+"_head", 
            mainName+"_menu",
            mainName+"_foot",
            mainName+"_content"
        };
        private float globalLeftBoundary = 0.1f;
        private float globalRighttBoundary = 0.9f;
        private float globalTopBoundary = 0.90f;
        private float globalBottomBoundary = 0.1f;
        private float globalSpace = 0.01f;
        private float eContentWidth = 0.395f;
        private float eHeadHeight = 0.05f;
        private float eFootHeight = 0.05f;
        private float eSlotHeight = 0.123f;


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
            GUIHeaderElement(player, mainName+"_head");
            GUIMenuElement(player, mainName + "_menu");
            GUIScoreElement(player, mainName + "_content", wipeData.titles, "Wipe");
            //GUIStatsElement(player, mainName+"_attacker", "attacker", storedData.Attackers);
        }
        private void GUIHeaderElement(BasePlayer player, string elUiId, string errorMsg  = globalNoErrorString)
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
                    AnchorMin = "0 " + (1f-eHeadHeight),
                    AnchorMax = "1 " + (1f)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ZN Title Manager",
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

       
        private void GUIMenuElement(BasePlayer player, string elUiId)
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
                    AnchorMin = "0 0.9",
                    AnchorMax = "1 0.95"
                },
                CursorEnabled = true
            }, "Overlay", elUiId);
            float leftBoundary = 0.01f;

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "zntitles.wtitle",
                    Color = "0.1 0.1 0.1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary+" 0.1",
                    AnchorMax = (leftBoundary + 0.22f)+" 0.9"
                },
                Text =
                {
                    Text = "Wipe Highscores",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, elUiId);
            leftBoundary += 0.25f;

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "zntitles.etitle",
                    Color = "0.1 0.1 0.1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary+" 0.1",
                    AnchorMax = (leftBoundary + 0.22f)+" 0.9"
                },
                Text =
                {
                    Text = "All-Time Highscores",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, elUiId);
            leftBoundary += 0.25f;
            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "zntitles.mytitle",
                    Color = "0.1 0.1 0.1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary+" 0.1",
                    AnchorMax = (leftBoundary + 0.22f)+" 0.9"
                },
                Text =
                {
                    Text = "Your Titles",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, elUiId);

            leftBoundary += 0.25f;
            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "zntitles.close",
                    Color = "0.56 0.12 0.12 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary+" 0.1",
                    AnchorMax = (leftBoundary + 0.23f)+" 0.9"
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



        private void GUIScoreElement(BasePlayer player, string elUiId, Dictionary<string, ZNTitle> d, string type)
        {

            var elements = new CuiElementContainer();
            float leftBoundary =  globalSpace;
            float rightBoundary = 1f - globalSpace;

            float topBoundary = globalTopBoundary - globalSpace;
            float botBoundary = globalBottomBoundary;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary + " " + (botBoundary),
                    AnchorMax = rightBoundary + " " + (topBoundary)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = type + " Highscore",
                    FontSize = 20,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.93",
                    AnchorMax = "0.99 0.99"
                }
            }, elUiId);
            float buttonHeight = 0.2f;
            float contentStart = 0.9f;
            float itemsPerRow = 10f;
            float cellWidth = 0.93f / (itemsPerRow);
            float space = 0.005f;
            leftBoundary = 0.01f;

            int numItems = 0;
            string achievement = "";
            foreach (ZNTitle t in d.Values)
            {
                int rowNum = numItems % (int)itemsPerRow;
                int lastRowNum = rowNum;
                int colNum = (int)Math.Floor(numItems / itemsPerRow);

                float localContentStart = (float)(contentStart - (Math.Floor(numItems / itemsPerRow) * (buttonHeight + 2*space)));
                float localLeftBoundary = (float)(leftBoundary + (numItems % itemsPerRow * (cellWidth + space)));
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0.0",
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary +" "+ (localContentStart-buttonHeight),
                    AnchorMax = (localLeftBoundary + cellWidth) + " "+ localContentStart
                },
                    CursorEnabled = true
                }, elUiId, t.achievement);
                int index = t.description.IndexOf("(");
                achievement = (index > 0) ? t.description.Substring(0, index) : t.description;
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = achievement,
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.01 0.82",
                    AnchorMax = "0.99 0.96"
                }
                }, t.achievement);



                float subLineStart = 0.96f;
                float subLineHeight = 0.175f;
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0.7",
                },
                    RectTransform =
                {
                    AnchorMin = "0.0 0.0",
                    AnchorMax = "1.0 0.81"
                },
                    CursorEnabled = true
                }, t.achievement, t.achievement + "_l1");

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "Holder:",
                    FontSize = 8,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");
                string holderName = Regex.Replace(t.holderName, @"^\[.+\]\s", "");
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = holderName,
                    FontSize = 10,
                    Align = TextAnchor.MiddleRight,
                    Color = (t.holderID == player.userID) ? "0 0.8 0 1" : "0.8 0.7 0.1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");

                subLineStart -= (subLineHeight + 0.02f);
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = t.scoreUnit,
                    FontSize = 8,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = t.holderScore.ToString(),
                    FontSize = 8,
                    Align = TextAnchor.MiddleRight,
                    Color = (t.holderID == player.userID) ? "0 0.8 0 1" : "0.8 0.7 0.1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");

                subLineStart -= (subLineHeight + 0.02f);
                

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = t.changeDate.ToLongDateString(),
                    FontSize = 8,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");
                subLineStart -= (subLineHeight + 0.02f);
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "Title (m):",
                    FontSize = 8,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = t.tag_m,
                    FontSize = 8,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");

                subLineStart -= (subLineHeight + 0.02f);
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "Title (f):",
                    FontSize = 8,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = t.tag_f,
                    FontSize = 8,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                }
                }, t.achievement + "_l1");






                numItems++;
            }




            CuiHelper.AddUi(player, elements);
        }

        private void GUITitleElement(BasePlayer player, string elUiId)
        {

            var elements = new CuiElementContainer();
            float leftBoundary = globalSpace;
            float rightBoundary = 1f - globalSpace;

            float topBoundary = globalTopBoundary - globalSpace;
            float botBoundary = globalBottomBoundary;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary + " " + (botBoundary),
                    AnchorMax = rightBoundary + " " + (topBoundary)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Your Titles (" + GetPlayerTitle(player.userID, false)+")",
                    FontSize = 20,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.93",
                    AnchorMax = "0.99 0.99"
                }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "If you hold any titles, you can select ONE to be your main title.\nClick the button to change titles.",
                    FontSize = 11,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0.93",
                    AnchorMax = "0.99 0.99"
                }
            }, elUiId);

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "zntitles.resetmytitle",
                    Color = "0.7 0.38 0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.8 0.88",
                    AnchorMax = "0.99 0.92"
                },
                Text =
                {
                    Text = "Reset Title",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            }, elUiId);

            float buttonHeight = 0.1f;
            float contentStart = 0.9f;
            float itemsPerRow = 5f;
            float cellWidth = 0.93f / (itemsPerRow);
            float space = 0.005f;
            leftBoundary = 0.01f;

            int numItems = 0;
            List<ZNTitle> d = getPlayerTitles(player);
            string achievement = "";
            foreach (ZNTitle t in d)
            {
                int rowNum = numItems % (int)itemsPerRow;
                int lastRowNum = rowNum;
                int colNum = (int)Math.Floor(numItems / itemsPerRow);

                float localContentStart = (float)(contentStart - (Math.Floor(numItems / itemsPerRow) * (buttonHeight + 2 * space)));
                float localLeftBoundary = (float)(leftBoundary + (numItems % itemsPerRow * (cellWidth + space)));
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0.0",
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary +" "+ (localContentStart-buttonHeight),
                    AnchorMax = (localLeftBoundary + cellWidth) + " "+ localContentStart
                },
                    CursorEnabled = true
                }, elUiId, t.achievement);
                int index = t.description.IndexOf("(");
                achievement = (index > 0) ? t.description.Substring(0, index) : t.description;
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = achievement,
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0.01 0.7",
                    AnchorMax = "0.99 0.96"
                }
                }, t.achievement);
                float subLineStart = 0.9f;
                float subLineHeight = 0.38f;
                elements.Add(new CuiPanel
                {
                    Image =
                {
                    Color = "0 0 0 0.7",
                },
                    RectTransform =
                {
                    AnchorMin = "0.0 0.0",
                    AnchorMax = "1.0 0.7"
                },
                    CursorEnabled = true
                }, t.achievement, t.achievement + "_l1");
                elements.Add(new CuiButton
                {
                    Button =
                {
                    Command = "zntitles.setmytitle "+t.achievement+" m",
                    Color = "0.7 0.38 0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                },
                    Text =
                {
                    Text = t.tag_m,
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
                }, t.achievement + "_l1");

                subLineStart -= (subLineHeight + 0.07f);
                elements.Add(new CuiButton
                {
                    Button =
                {
                    Command = "zntitles.setmytitle "+t.achievement+" f",
                    Color = "0.7 0.38 0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 "+(subLineStart - subLineHeight),
                    AnchorMax = "0.95 "+subLineStart
                },
                    Text =
                {
                    Text = t.tag_f,
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
                }, t.achievement + "_l1");
               
                numItems++;
            }




            CuiHelper.AddUi(player, elements);
        }

        #endregion

    }
}   
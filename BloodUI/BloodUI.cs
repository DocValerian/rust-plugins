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
using Network;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("BloodUI", "DocValerian", "1.1.1")]
    class BloodUI : RustPlugin
    {
        static BloodUI Plugin;

        [PluginReference]
        private Plugin ImageLibrary, ServerRewards;


        #region ConfigDataLoad
        private int bloodID;
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
            public int[] options = { 10, 100, 1000, 10000 };
            public bool onlyAtHome = false;
            public string infoText = "Some Info here";
            public string rpImgUrl = "http://m2v.eu/i/zn/rp1.png";
        }
        
        void Loaded()
        {
            Plugin = this;
        }
        private void OnServerInitialized()
        {
            bloodID = ItemManager.itemDictionaryByName["blood"].itemid;
            ImageLibrary?.Call("ImportImageList", "bloodui", new Dictionary<string, string>() { ["rp_img"] = Cfg.rpImgUrl});
        }
        void Unload()
        {
            foreach (var player in UiPlayers.ToList())
            {
                killUI(player);
            }
        }
        #endregion

        #region Hooks

        void OnPlayerDisconnected(BasePlayer player)
        {
            killUI(player);
        }
        #endregion

        #region Commands

        [ConsoleCommand("bloodui.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            killUI(player);
        }
        [ConsoleCommand("bloodui.getblood")]
        private void CmdGetBlood(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            BuildingPrivlidge priv = player.GetBuildingPrivilege();
            if (Cfg.onlyAtHome && (priv == null || (priv != null && !priv.IsAuthed(player))))
            {
                SendReply(player, "This can only be done within Authed TC range");
                return;
            }
            int amount = arg.GetInt(0);
            
            object bal = ServerRewards?.Call("CheckPoints", player.userID);
            if (bal == null) return;
            int playerRP = (int)bal;
                
            if (playerRP < amount)
            {
                SendReply(player, "You don't have enough RP to get " + amount + " Blood.");
                return;
            }
            else
            {
                ServerRewards?.Call("TakePoints", player.userID, amount);
                Item blood = ItemManager.CreateByName("blood", amount);
                player.GiveItem(blood, BaseEntity.GiveItemReason.PickedUp);
                reloadUI(player);
            }
            SendReply(player, "You exchanged " + amount + " RP for Blood.");
            Puts("INFO: " + player + " exchanged " + amount + " RP ---> Blood");
        }
        [ConsoleCommand("bloodui.storeblood")]
        private void CmdStoreBlood(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            BuildingPrivlidge priv = player.GetBuildingPrivilege();
            if (Cfg.onlyAtHome && (priv == null || (priv != null && !priv.IsAuthed(player))))
            {
                SendReply(player, "This can only be done within Authed TC range");
                return;
            }

            Item blood = player.inventory.FindItemID(bloodID);
            int bloodAmount = player.inventory.containerMain.GetAmount(bloodID, true);
            // safety from overbyuing
            if (blood == null || bloodAmount < 1)
            {
                SendReply(player, "You don't have any Blood in your inventory!");
                return;
            }
            else
            {
                int takeAmt = player.inventory.Take(null, bloodID, bloodAmount);
                Puts("INFO: " + player + " converted " + takeAmt + " Blood ---> RP");
                ServerRewards?.Call("AddPoints", player.userID, takeAmt);
                reloadUI(player);
                SendReply(player, "You transferred " + takeAmt + " Blood into RP.");
            }
        }


        [ChatCommand("blood")]
        void CmdBlood(BasePlayer player, string command, string[] args)
        {
            BuildingPrivlidge priv = player.GetBuildingPrivilege();
            if (Cfg.onlyAtHome && (priv == null || (priv != null && !priv.IsAuthed(player))))
            {
                SendReply(player, "This can only be done within Authed TC range");
                return;
            }
            reloadUI(player);
        }

        #endregion

        #region Functions

        
        #endregion

        #region GUI

        private const string globalNoErrorString = "none";
        private const string mainName = "BloodUI";
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements =
        {
            mainName+"_head",
            mainName+"_content",
            mainName+"_foot"
        };
        private float globalLeftBoundary = 0.3f;
        private float globalRighttBoundary = 0.7f;
        private float globalTopBoundary = 0.7f;
        private float globalBottomBoundary = 0.4f;
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
            }
            foreach (string ui in uiElements)
            {
                CuiHelper.DestroyUi(player, ui);
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
                    Text = "/Blood <-> RP exchange UI",
                    FontSize = 18,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0.005",
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
                    AnchorMin = globalLeftBoundary + " " + (globalBottomBoundary+eFootHeight) ,
                    AnchorMax = globalRighttBoundary + " " + (globalTopBoundary - eHeadHeight)
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            
            // icons
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                        {
                            new CuiRawImageComponent {Png = GetImage("blood") },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.05 0.3",
                                AnchorMax = "0.25 0.8"
                            }
                        }
            });

            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "x"+ player.inventory.containerMain.GetAmount(bloodID, true),
                            FontSize = 12,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                                AnchorMin = "0.05 0.25",
                                AnchorMax = "0.25 0.8"
                        }
            }, elUiId);

            elements.Add(new CuiButton
            {
                Button =
                    {
                        Command = "bloodui.storeblood",
                        Color = "0.7 0.38 0 1"
                    },
                RectTransform =
                    {
                        AnchorMin = 0.27f +" "+ 0.65f,
                        AnchorMax = 0.72f +" "+ 0.85f
                    },
                Text =
                    {
                        Text = "Transfer all Blood to RP (1:1)    --->>   ",
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    }
            }, elUiId);
            elements.Add(new CuiButton
            {
                Button =
                    {
                        Command = "bloodui.getblood 100",
                        Color = "0.1 0.1 0.1 0.8"
                    },
                RectTransform =
                    {
                        AnchorMin = 0.27f +" "+ 0.5f,
                        AnchorMax = 0.72f +" "+ 0.6f
                    },
                Text =
                    {
                        Text = "   <<--- Get 100 Blood for RP",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    }
            }, elUiId);
            elements.Add(new CuiButton
            {
                Button =
                    {
                        Command = "bloodui.getblood 1000",
                        Color = "0.1 0.1 0.1 0.8"
                    },
                RectTransform =
                    {
                        AnchorMin = 0.27f +" "+ 0.37f,
                        AnchorMax = 0.72f +" "+ 0.47f
                    },
                Text =
                    {
                        Text = "   <<--- Get 1.000 Blood for RP",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    }
            }, elUiId);

            elements.Add(new CuiButton
            {
                Button =
                    {
                        Command = "bloodui.getblood 10000",
                        Color = "0.1 0.1 0.1 0.8"
                    },
                RectTransform =
                    {
                        AnchorMin = 0.27f +" "+ 0.24f,
                        AnchorMax = 0.72f +" "+ 0.34f
                    },
                Text =
                    {
                        Text = "   <<--- Get 10.000 Blood for RP",
                        FontSize = 12,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    }
            }, elUiId);


            object bal = ServerRewards?.Call("CheckPoints", player.userID);
            if (bal == null) return;
            int playerRP = (int)bal;
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                        {
                            new CuiRawImageComponent {Png = GetImage("rp_img") },
                            new CuiRectTransformComponent {
                                AnchorMin = "0.75 0.3",
                                AnchorMax = "0.95 0.8"
                            }
                        }
            });
            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "x"+ playerRP,
                            FontSize = 12,
                            Align = TextAnchor.LowerRight,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                                AnchorMin = "0.75 0.25",
                                AnchorMax = "0.95 0.8"
                        }
            }, elUiId);


            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = Cfg.infoText,
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.2 0.8 0 1"
                    },
                RectTransform =
                    {
                        AnchorMin = "0.005 0.005",
                        AnchorMax = "0.995 0.2"
                    }
            }, elUiId);



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
                    Command = "bloodui.close",
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
        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
    }
}
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
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("InfoAPI", "DocValerian", "1.0.0")]
    class InfoAPI : RustPlugin
    {

        static InfoAPI Plugin;
        #region commands
        /*
        [ChatCommand("ll")]
        void TESTcommand(BasePlayer player, string command, string[] args)
        {
            //ProfileManager.Get(player.userID, true);
            CreateAnnouncement(player, "Test");
            return;
        }
        */
        #endregion

        #region API
        private void ShowInfoPopup(BasePlayer player, string msg, bool isError = false)
        {

            timer.Once(1f, () => {
                CuiElementContainer GUITEXT = new CuiElementContainer();
                string color = (isError) ? "1 0 0 1" : "0 9 0 1";
                //GUITEXT = new CuiElementContainer();

                GUITEXT.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        FadeIn = 0.5f
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.3 0.7", 
                        AnchorMax = "0.7 0.8"
                    },
                    CursorEnabled = false
                }, "Overlay", "AnnouncementText");
                GUITEXT.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = msg,
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = color,
                        FadeIn = 0.9f
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }, "AnnouncementText");

                CuiHelper.DestroyUi(player, "AnnouncementText");
                CuiHelper.AddUi(player, GUITEXT);

                timer.Once(5f, () => {
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                });

            });
        }
        #endregion
    }
}

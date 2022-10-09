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
    [Info("SimpleInfoNote", "DocValerian", "1.0.0")]
    class SimpleInfoNote : RustPlugin
    {
        static SimpleInfoNote Plugin;


        #region ConfigDataLoad
        private int noteID;
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

            [JsonProperty(PropertyName = "Chat command aliases")]
            public string[] commandAliasList = { "discord" };

            [JsonProperty(PropertyName = "Text to add to Notes")]
            public string notesText = "Discord URL: https://example.gg/abcdefg";

            [JsonProperty(PropertyName = "Global Note Cooldown (s) - Spam protection")]
            public double antiSpamSeconds = 5.0;
        }

        private DateTime lastNoteTime;

        void Loaded()
        {
            Plugin = this;
            string[] commandAliases = Cfg.commandAliasList;
            foreach (string cmdAlias in commandAliases)
                cmd.AddChatCommand(cmdAlias, this, "CommandGiveNote");
        }
        private void OnServerInitialized()
        {
            noteID = ItemManager.itemDictionaryByName["note"].itemid;
            lastNoteTime = DateTime.Now;
        }
        void Unload()
        {
            
        }
        #endregion

        #region Commands

        void CommandGiveNote(BasePlayer player, string command, string[] args)
        {
            double timeDiff = (DateTime.Now - lastNoteTime).TotalSeconds;
            if(timeDiff < Cfg.antiSpamSeconds)
            {
                SendReply(player, "Cooldown, please try again in a few moments");
                return;
            }
            Item item = ItemManager.CreateByItemID(noteID, 1);
            if (item == null) return;
            item.text = Cfg.notesText;
            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            lastNoteTime = DateTime.Now;
        }

        #endregion


    }
}
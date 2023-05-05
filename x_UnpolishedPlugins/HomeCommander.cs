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

namespace Oxide.Plugins
{
    [Info("HomeCommander", "DocValerian", "1.0.14")]
    class HomeCommander : RustPlugin
    {
        static HomeCommander Plugin;

        [PluginReference]
        private Plugin Clans;

        private const string permUse = "homecommander.use";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);
        }

        [ChatCommand("hcmd")]
        void HomeCommand(BasePlayer player, string command, string[] args)
        {
            // you have to be in a building zone
            BuildingPrivlidge privilege = player.GetBuildingPrivilege();
            if (!privilege)
            {
                SendReply(player, "You are not within a building privlege.");
                return;
            }
            // if you are not authorized, you will only get a generic error, before stuff happens
            if (!privilege.IsAuthed(player) && privilege.OwnerID != player.userID && !player.IsAdmin)
            {
                SendReply(player, "You are not authorized in/owner of this building privlege.");
                return;
            }

            if (args.Length == 0)
            {
                showInfoText(player);
                return;
            }

            string cmd = String.Join("_", args);

            Dictionary<ulong, AnimatedBuildingBlock> doors = getDoors(player);
            int locks = 0;
            string clanTag;
            switch (cmd)
            {
                case "help":
                    showInfoText(player);
                    break;
                case "auth":
                    addToTc(player, player, privilege);
                    addToDoors(player, player, doors);
                    break;
                case "auth_team":
                    if (player.currentTeam != 0UL)
                    {
                        RelationshipManager.PlayerTeam theTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                        foreach (var member in theTeam.members)
                        {
                            BasePlayer x = FindPlayer(member);
                            if (x == null) continue;
                            addToTc(player, x, privilege);
                            addToDoors(player, x, doors);
                        }
                    }
                    else
                    {
                        SendReply(player, "You are not in a team!");
                    }
                    break;
                case "auth_clan":
                    if (Clans == null)
                    {
                        SendReply(player, "Clan Plugin not enabled!");
                        break;
                    }
                    clanTag = Clans?.Call<string>("GetClanOf", player.userID);
                    if (!String.IsNullOrEmpty(clanTag))
                    {
                        object clan = Clans?.Call<JObject>("GetClan", clanTag);
                        if (clan != null && clan is JObject)
                        {
                            JToken members = (clan as JObject).GetValue("members");
                            if (members != null && members is JArray)
                            {
                                foreach (ulong member in (JArray)members)
                                {
                                    BasePlayer x = FindPlayer(member);
                                    if (x == null) continue;
                                    addToTc(player, x, privilege);
                                    addToDoors(player, x, doors);
                                }
                            }
                            else
                            {
                                SendReply(player, "Your Clan has no members!");
                            }
                        }
                        else
                        {
                            SendReply(player, "Couldn't load Clan data!");
                        }
                    }
                    else
                    {
                        SendReply(player, "You are not in a Clan!");
                    }
                    break;
                case "auth_ally":
                    if (Clans == null)
                    {
                        SendReply(player, "Clan Plugin not enabled!");
                        break;
                    }
                    clanTag = Clans?.Call<string>("GetClanOf", player.userID);
                    if (!String.IsNullOrEmpty(clanTag))
                    {
                        object clan = Clans?.Call<JObject>("GetClan", clanTag);
                        if (clan != null && clan is JObject)
                        {
                            JToken members = (clan as JObject).GetValue("members");
                            if (members != null && members is JArray)
                            {
                                foreach (ulong member in (JArray)members)
                                {
                                    BasePlayer x = FindPlayer(member);
                                    if (x == null) continue;
                                    addToTc(player, x, privilege);
                                    addToDoors(player, x, doors);
                                }
                            }
                            else
                            {
                                SendReply(player, "Your Clan has no members!");
                            }
                            JToken allies = (clan as JObject).GetValue("allies");
                            if (allies != null && allies is JArray)
                            {
                                foreach (string allyClan in allies)
                                {
                                    object oAllyClan = Clans?.Call<JObject>("GetClan", allyClan);
                                    if (oAllyClan != null && oAllyClan is JObject)
                                    {
                                        JToken aMembers = (oAllyClan as JObject).GetValue("members");
                                        if (aMembers != null && aMembers is JArray)
                                        {
                                            foreach (ulong member in (JArray)aMembers)
                                            {
                                                BasePlayer x = FindPlayer(member);
                                                if (x == null) continue;
                                                addToTc(player, x, privilege);
                                                addToDoors(player, x, doors);
                                            }
                                        }
                                        else
                                        {
                                            SendReply(player, "Your Ally has no members!");
                                        }
                                    }

                                }
                            }
                            else
                            {
                                SendReply(player, "Your Clan has no allies!");
                            }
                        }
                        else
                        {
                            SendReply(player, "Couldn't load Clan data!");
                        }
                    }
                    else
                    {
                        SendReply(player, "You are not in a Clan!");
                    }
                    break;
                case "clear":
                    privilege.authorizedPlayers.Clear();
                    privilege.SendNetworkUpdateImmediate();
                    SendReply(player, "All TC auths cleared");
                    clearDoors(player, doors);
                    break;
                case "info":
                    locks = 0;
                    string msg = "Current doors:\t" + doors.Count();
                    foreach (KeyValuePair<ulong, AnimatedBuildingBlock> d in doors)
                    {
                        if (d.Value.GetSlot(BaseEntity.Slot.Lock) != null)
                        {
                            locks++;
                        }
                    }
                    msg += "\nCurrent locks:\t" + locks;
                    msg += "\nLocks needed:\t" + (doors.Count() - locks);
                    SendReply(player, msg);
                    break;
                case "lock":
                    locks = 0;
                    foreach (KeyValuePair<ulong, AnimatedBuildingBlock> d in doors)
                    {
                        if (d.Value.GetSlot(BaseEntity.Slot.Lock) == null)
                        {
                            locks++;
                        }
                    }
                    if (!HasCodeLock(player, locks))
                    {
                        SendReply(player, "You need " + locks + " code locks for this command!");
                        break;
                    }
                    foreach (KeyValuePair<ulong, AnimatedBuildingBlock> d in doors)
                    {
                        addCodeLock(player, d.Value);
                        CodeLock Code = d.Value.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
                        if (Code == null) continue;
                        Code.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                    SendReply(player, "All Doors (" + doors.Count() + ") locked! (that were placed by you)");
                    break;
                case "unlock":
                    locks = 0;
                    foreach (KeyValuePair<ulong, AnimatedBuildingBlock> d in doors)
                    {
                        if (d.Value.GetSlot(BaseEntity.Slot.Lock) != null)
                        {
                            CodeLock Code = d.Value.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
                            Code.SetFlag(BaseEntity.Flags.Locked, false);
                            locks++;
                        }
                    }
                    SendReply(player, "All Doors (" + locks + ") unlocked! (that were placed by you)");
                    break;
                default:
                    if (cmd.Contains("auth_add_"))
                    {
                        if (args[2] == null) return;
                        string playerName = args[2];
                        BasePlayer targetPlayer = BasePlayer.FindAwakeOrSleeping(playerName);

                        if (targetPlayer != null)
                        {
                            addToTc(player, targetPlayer, privilege);
                            addToDoors(player, targetPlayer, doors);
                        }
                        else
                        {
                            SendReply(player, "Player: " + playerName + " not found!");
                        }
                    }
                    else
                    {
                        showInfoText(player);
                    }
                    break;
            }
        }
        private void addCodeLock(BasePlayer Player, AnimatedBuildingBlock door)
        {
            if (door.GetSlot(BaseEntity.Slot.Lock) != null) return;

            var Code = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
            Code.Spawn();
            Code.code = "" + RandomCode();
            Code.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
            door.SetSlot(BaseEntity.Slot.Lock, Code);
            Code.SetFlag(BaseEntity.Flags.Locked, true);
            Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab", Code.transform.position);
            Code.whitelistPlayers.Add(Player.userID);
            TakeCodeLock(Player);
        }

        //int layerMasks = LayerMask.GetMask("Default");
        private Dictionary<ulong, AnimatedBuildingBlock> getDoors(BasePlayer player)
        {
            float range = 100f;
            List<AnimatedBuildingBlock> list = new List<AnimatedBuildingBlock>();
            Vis.Entities<AnimatedBuildingBlock>(player.transform.position, range, list);
            Dictionary<ulong, AnimatedBuildingBlock> doors = new Dictionary<ulong, AnimatedBuildingBlock>();
            List<string> clanMates = Clans?.Call<List<string>>("GetClanMembers", player.userID);

            foreach (AnimatedBuildingBlock entity in list)
            {
                // vis will find the entity multiple times
                if (doors.ContainsKey(entity.net.ID.Value) || entity.OwnerID == 0) continue;
                // only works on player owned entities
                if (entity.OwnerID == player.userID || (clanMates != null && clanMates.Contains(entity.OwnerID.ToString())) || player.IsAdmin)
                {
                    doors.Add(entity.net.ID.Value, entity);
                }

            }
            return doors;
        }

        public int RandomCode()
        {
            System.Random random = new System.Random();
            return random.Next(1000, 9999);
        }
        void TakeCodeLock(BasePlayer Player)
        {
            Player.inventory.Take(null, 1159991980, 1);
        }



        private void clearDoors(BasePlayer owner, Dictionary<ulong, AnimatedBuildingBlock> doors)
        {
            int locks = 0;
            foreach (KeyValuePair<ulong, AnimatedBuildingBlock> d in doors)
            {
                CodeLock Code = d.Value.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
                if (Code == null) continue;
                locks++;
                Code.whitelistPlayers.Clear();
            }
            SendReply(owner, "Auth cleared on " + locks + " CodeLocks.");
        }

        private void addToDoors(BasePlayer owner, BasePlayer player, Dictionary<ulong, AnimatedBuildingBlock> doors)
        {
            int locks = 0;
            foreach (KeyValuePair<ulong, AnimatedBuildingBlock> d in doors)
            {
                CodeLock Code = d.Value.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
                if (Code == null) continue;
                locks++;
                if (!Code.whitelistPlayers.Contains(player.userID))
                {
                    Code.whitelistPlayers.Add(player.userID);
                }
            }
            SendReply(owner, player.displayName + " authed on " + locks + " CodeLocks.");
        }
        private void addToTc(BasePlayer owner, BasePlayer player, BuildingPrivlidge priv)
        {
            if (!priv.IsAuthed(player))
            {
                priv.authorizedPlayers.Add(new ProtoBuf.PlayerNameID()
                {
                    userid = player.userID,
                    username = player.displayName
                });
                priv.SendNetworkUpdateImmediate();
            }
            SendReply(owner, player.displayName + " authed on TC.");
        }

        private bool HasCodeLock(BasePlayer Player, int amount)
        {
            Item i = Player.inventory.FindItemID(1159991980);
            if (i == null) return false;
            return i.amount >= amount;
        }
        private void showInfoText(BasePlayer player)
        {
            string infoText = "<color=orange>============ Home Command (v" + Plugin.Version + ") ============</color>";
            infoText += "\n All functions apply to the TC and ALL doors in range, ";
            infoText += "\n that are owned by you (or your clanmates)!";
            infoText += "\n - Will NOT affect the TC-Lock, only building priv!";

            infoText += "\n\n<color=green>Usage:</color>";
            infoText += "\n/hcmd \t\t\t\tdisplays the command info";
            infoText += "\n/hcmd auth \t\t\tAuth yourself";
            infoText += "\n/hcmd auth add <name> \tAuth <name>";
            infoText += "\n/hcmd auth team \t\tAuth team";
            infoText += "\n/hcmd auth clan \t\t\tAuth clan";
            infoText += "\n/hcmd auth ally \t\t\tAuth clan + allies";
            infoText += "\n/hcmd clear \t\t\tClear auth";
            infoText += "\n/hcmd info \t\t\t\tShow number of doors and locks";
            infoText += "\n/hcmd lock \t\t\tLock all doors (+add locks) \n\t\t\t\t\t- May require CodeLocks!";
            infoText += "\n/hcmd unlock \t\t\tUnlock all doors";
            SendReply(player, infoText);

        }

        private BasePlayer FindPlayer(ulong userID)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            if (player == null)
            {
                player = BasePlayer.FindSleeping(userID);
            }
            return player;
        }
    }
}
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
using Facepunch;
using Network;
using System;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Oxide.Plugins
{
    [Info("TurretCommander", "DocValerian", "1.2.16")]
    class TurretCommander : RustPlugin
    {
        static TurretCommander Plugin;

        [PluginReference]
        private Plugin Clans, PersonalPvp;

        void Loaded()
        {
            LoadMessages();
            Plugin = this;
        }

        void LoadMessages()
        {
            Dictionary<string, string> msg = new Dictionary<string, string>
            {
                ["Line"] = "----------------------------------",
                ["NoAmmoAmt"] = "You do not have sufficient ammo to fill {1} turrets with {0} bullets!",
                ["TrapNoAmmoAmt"] = "You do not have sufficient ammo to fill {1} traps with {0} bullets!",
                ["TrapFillSuccess"] = "You added {0} ammo to {1} traps!",
                ["FillUsage"] = "Usage: /tcmd fill|filltrap <amount>",
                ["EmptySuccess"] = "You successtully emptied your turrets!",
                ["EmptyTrapSuccess"] = "You successtully emptied your traps!",
                ["InventoryFull"] = "Your Inventory is full!",
                ["Debug"] = "debug: {0}"
            };
            lang.RegisterMessages(msg, this);
        }

        [ChatCommand("tcmd")]
        void TurretCommand(BasePlayer player, string command, string[] args)
        {
            ItemDefinition ammoDefinition;

            if (args.Length == 0)
            {
                showInfoText(player);
                return;
            }
            if (args.Length == 1)
            {
                List<AutoTurret> turrets = getTurrets(player);

                switch (args[0])
                {
                    case "status":
                        string msg = "<color=orange>============= Turret Command - Status =============</color>";
                        int tcount = turrets.Count;
                        Dictionary<int, int> ammoTypes = getAmmoTypes(turrets);
                        msg += "\nTotal Turrets:\t" + tcount;
                        msg += "\nUnarmed:\t\t" + ammoTypes[0];
                        msg += "\n\n<color=green>=== Loaded Ammo ===</color>";
                        foreach (KeyValuePair<int, int> amt in ammoTypes)
                        {
                            if (amt.Key == 0) continue;
                            msg += "\n" + amt.Value + "x \t" + ItemManager.itemDictionary[amt.Key].displayName.translated;
                        }
                        SendReply(player, msg);
                        break;
                    case "help":
                        showInfoText(player);
                        break;
                    case "on":
                        if (player.IsAdmin)
                        {
                            foreach (AutoTurret at in turrets)
                            {
                                at.SetIsOnline(true);
                                at.SendNetworkUpdateImmediate();
                            }
                        }
                        SendReply(player, "Command no longer available (electricity patch)!");
                        break;

                    case "arm":
                        if (player.IsAdmin)
                        {
                            foreach (AutoTurret at in turrets)
                            {
                                at.AttachedWeapon = null;
                                Item slot = at.inventory.GetSlot(0);

                                if (slot != null && (slot.info.category == ItemCategory.Weapon || slot.info.category == ItemCategory.Fun))
                                {
                                    slot.RemoveFromContainer();
                                    slot.Remove();
                                }
                                timer.Once(0.1f, () =>
                                {
                                    var shortname = "rifle.ak";
                                    var itemToCreate = ItemManager.FindItemDefinition(shortname);
                                    //Puts("Debug create" + itemToCreate);
                                    if (itemToCreate != null)
                                    {
                                        Item item = ItemManager.Create(itemToCreate, 1);

                                        if (!item.MoveToContainer(at.inventory, 0, false))
                                        {
                                            item.Remove();
                                        }
                                        else
                                        {
                                            item.SwitchOnOff(true);
                                        }
                                    }

                                    if (at.AttachedWeapon == null) return;
                                    if (!(at.AttachedWeapon is BaseProjectile)) return;
                                    BaseProjectile gun = at.AttachedWeapon as BaseProjectile;
                                    Item ammo = ItemManager.CreateByItemID(gun.primaryMagazine.ammoType.itemid, 1);
                                    if (at.inventory.CanAcceptItem(ammo, 1) != ItemContainer.CanAcceptResult.CanAccept) return;


                                    player.inventory.containerMain.Take(null, gun.primaryMagazine.ammoType.itemid, 600);
                                    at.inventory.AddItem(gun.primaryMagazine.ammoType, 600);

                                });
                                at.InitiateStartup();
                                at.SetIsOnline(true);
                                at.SendNetworkUpdateImmediate();
                            }
                        }
                        SendReply(player, "armed!");
                        break;
                    case "off":
                        if (player.IsAdmin)
                        {
                            foreach (AutoTurret at in turrets)
                            {
                                at.SetIsOnline(false);
                                at.SendNetworkUpdateImmediate();
                            }
                        }
                        SendReply(player, "Command no longer available (electricity patch)!");
                        break;
                    case "peace":
                        foreach (AutoTurret at in turrets)
                        {
                            at.SetPeacekeepermode(true);
                            at.SendNetworkUpdateImmediate();
                        }
                        SendReply(player, "All turrets set to peacekeeping!");
                        break;
                    case "war":
                        foreach (AutoTurret at in turrets)
                        {
                            at.SetPeacekeepermode(false);
                            at.SendNetworkUpdateImmediate();
                        }
                        SendReply(player, "All turrets set to attack all!");
                        break;
                    case "auth":
                        addPlayerToTurrets(player, player, turrets);
                        break;
                    case "empty":
                        ammoDefinition = ItemManager.FindItemDefinition("ammo.rifle");
                        if (ammoDefinition != null)
                        {
                            // this requires a double loop, since GiveItem automatically modifies the turret 
                            // object, thus triggering a C# exception
                            List<Item> itmList = new List<Item>();
                            foreach (AutoTurret at in turrets)
                            {
                                foreach (Item itm in at.inventory.itemList.ToList())
                                {
                                    //itmList.Add(itm);
                                    if (itm.info.category == ItemCategory.Weapon) continue;
                                    player.GiveItem(itm, BaseEntity.GiveItemReason.PickedUp);
                                }
                            }
                            player.ChatMessage(LangMsg("EmptySuccess", player.UserIDString));
                        }
                        else
                        {
                            Puts("Rifle ammo definition is null,Please contact the plugin author if this happens again.");
                        }
                        break;
                    case "emptytrap":
                        ammoDefinition = ItemManager.FindItemDefinition("ammo.handmade.shell");
                        List<GunTrap> traps = getTraps(player);
                        if (ammoDefinition != null)
                        {
                            // this requires a double loop, since GiveItem automatically modifies the turret 
                            // object, thus triggering a C# exception
                            List<Item> itmList = new List<Item>();
                            foreach (GunTrap at in traps)
                            {
                                foreach (Item itm in at.inventory.itemList)
                                {
                                    itmList.Add(itm);
                                }
                            }
                            foreach (Item itm in itmList)
                            {
                                // this will take care of full inventory and remove stuff from the turret
                                player.GiveItem(itm, BaseEntity.GiveItemReason.PickedUp);
                            }
                            player.ChatMessage(LangMsg("EmptyTrapSuccess", player.UserIDString));
                        }
                        else
                        {
                            Puts("Rifle ammo definition is null,Please contact the plugin author if this happens again.");
                        }
                        break;
                    default:
                        break;
                }
            }
            if (args.Length == 2)
            {
                List<AutoTurret> turrets = getTurrets(player);
                int fillAmount = 0;
                int playerAmmo;
                Dictionary<int, int> playerAmmoDict = new Dictionary<int, int>();
                int tCount;

                switch (args[0])
                {
                    case "help":
                        showInfoText(player, args[1]);
                        break;
                    case "fill":
                        try
                        {
                            fillAmount = int.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            player.ChatMessage(LangMsg("FillUsage", player.UserIDString));
                            return;
                        }
                        Dictionary<int, int> ammoTypes = getAmmoTypes(turrets);
                        string missingAmmo = "";
                        foreach (KeyValuePair<int, int> amt in ammoTypes)
                        {
                            if (amt.Key == 0) continue;
                            playerAmmoDict.Add(amt.Key, player.inventory.containerMain.GetAmount(amt.Key, true));
                            if (playerAmmoDict[amt.Key] < amt.Value * fillAmount)
                            {
                                missingAmmo += "\n" + amt.Value * fillAmount + "x\t" + ItemManager.itemDictionary[amt.Key].displayName.translated;
                            }
                        }
                        if (missingAmmo != "")
                        {
                            SendReply(player, "<color=red>You are missing Ammo:</color>" + missingAmmo);
                            return;
                        }
                        int filledTurrets = 0;
                        foreach (AutoTurret at in turrets)
                        {
                            if (at.AttachedWeapon == null) continue;
                            if (!(at.AttachedWeapon is BaseProjectile)) continue;
                            BaseProjectile gun = at.AttachedWeapon as BaseProjectile;
                            Item ammo = ItemManager.CreateByItemID(gun.primaryMagazine.ammoType.itemid, 1);
                            if (at.inventory.CanAcceptItem(ammo, 1) != ItemContainer.CanAcceptResult.CanAccept) continue;


                            player.inventory.containerMain.Take(null, gun.primaryMagazine.ammoType.itemid, fillAmount);
                            at.inventory.AddItem(gun.primaryMagazine.ammoType, fillAmount);
                            filledTurrets++;
                        }
                        SendReply(player, $"You added {fillAmount} ammo to {filledTurrets} armed turrets!");

                        break;
                    case "auth":
                        string clanTag;
                        switch (args[1])
                        {
                            case "clear":
                                foreach (AutoTurret at in turrets)
                                {
                                    at.SetIsOnline(false);
                                    at.authorizedPlayers.Clear();
                                    at.SendNetworkUpdateImmediate();
                                }
                                SendReply(player, "Turret auth cleared, ALL TURRETS ARE OFFLINE! (" + turrets.Count + ")");
                                break;
                            case "list":
                                string response = "Authorized:";
                                foreach (AutoTurret at in turrets)
                                {
                                    response += "\n(";
                                    string authPlayerNames = String.Empty;
                                    foreach (ProtoBuf.PlayerNameID p in at.authorizedPlayers)
                                    {
                                        if (!String.IsNullOrEmpty(authPlayerNames))
                                            authPlayerNames += ", ";
                                        authPlayerNames += p.username;
                                    }
                                    response += authPlayerNames + ")";
                                }
                                SendReply(player, response);
                                break;
                            case "team":
                                if (player.currentTeam != 0UL)
                                {
                                    RelationshipManager.PlayerTeam theTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                                    foreach (ulong member in theTeam.members)
                                    {

                                        BasePlayer x = FindPlayer(member);
                                        if (x == null) continue;
                                        addPlayerToTurrets(player, x, turrets);
                                    }
                                }
                                else
                                {
                                    SendReply(player, "You are not in a team!");
                                }
                                break;
                            case "pvp":
                                if (PersonalPvp == null)
                                {
                                    SendReply(player, "PersonalPvp Plugin not enabled!");
                                    break;
                                }
                                HashSet<ulong> pvpGroup = PersonalPvp?.Call<HashSet<ulong>>("getPVPGroupMembers", player);
                                foreach (ulong member in pvpGroup)
                                {
                                    BasePlayer x = FindPlayer(member);
                                    if (x == null) continue;
                                    addPlayerToTurrets(player, x, turrets);
                                }

                                break;
                            case "clan":
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
                                                addPlayerToTurrets(player, x, turrets);
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
                            case "ally":
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
                                                addPlayerToTurrets(player, x, turrets);
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
                                                            addPlayerToTurrets(player, x, turrets);
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
                        }
                        break;
                    case "filltrap":
                        ammoDefinition = ItemManager.FindItemDefinition("ammo.handmade.shell");
                        playerAmmo = player.inventory.containerMain.GetAmount(ammoDefinition.itemid, true);
                        List<GunTrap> traps = getTraps(player);
                        tCount = traps.Count;

                        try
                        {
                            fillAmount = int.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            player.ChatMessage(LangMsg("FillUsage", player.UserIDString));
                            return;
                        }

                        if (playerAmmo < (fillAmount * tCount))
                        {
                            player.ChatMessage(LangMsg("TrapNoAmmoAmt", player.UserIDString, fillAmount, tCount));
                            return;
                        }

                        if (ammoDefinition != null)
                        {
                            foreach (GunTrap at in traps)
                            {
                                player.inventory.containerMain.Take(null, ammoDefinition.itemid, fillAmount);
                                at.inventory.AddItem(ammoDefinition, fillAmount);
                            }
                            player.ChatMessage(LangMsg("TrapFillSuccess", player.UserIDString, fillAmount, tCount));
                        }
                        else
                        {
                            Puts("Handmade Shell definition is null,Please contact the plugin author if this happens again.");
                        }
                        break;
                    default:
                        break;
                }
            }
            if (args.Length == 3)
            {
                List<AutoTurret> turrets = getTurrets(player);
                if (args[0] != "auth" || args[1] != "add")
                {
                    showInfoText(player);
                    return;
                }
                string playerName = args[2];
                BasePlayer targetPlayer = BasePlayer.FindAwakeOrSleeping(playerName);

                if (targetPlayer != null)
                {
                    addPlayerToTurrets(player, targetPlayer, turrets);
                }
                else
                {
                    SendReply(player, "Player: " + playerName + " not found!");
                }

            }


        }

        private void addPlayerToTurrets(BasePlayer owner, BasePlayer player, List<AutoTurret> turrets)
        {
            foreach (AutoTurret at in turrets)
            {
                if (!at.authorizedPlayers.Any(x => x.userid == player.userID))
                {
                    at.authorizedPlayers.Add(new ProtoBuf.PlayerNameID { userid = player.userID, username = player.displayName });
                    at.SendNetworkUpdateImmediate();
                }
            }
            SendReply(owner, player.displayName + " authed on all owned turrets in 200m radius. (" + turrets.Count + ")");
        }

        private void showInfoText(BasePlayer player, string category = "default")
        {
            string infoText = "<color=orange>================ Turret Command ================</color>"
                            + "\n /tcmd [help] \t\t\topens the info panel";
            switch (category)
            {
                case "auth":
                    infoText += "\n All Functions only apply to turrets in <color=green>200m</color> range, that are"
                            + "\n owned by you (or your clanmates)!";
                    infoText += "\n\n<color=green>=== Auth tools ===</color>"
                            + "\n /tcmd auth \t\t\tAuth yourself"
                            + "\n /tcmd auth list \t\t\tShow Auth List"
                            + "\n /tcmd auth clear  \t\tClear all auths & shuts \n\t\t\t\t\tdown all turrets!"
                            + "\n\n /tcmd auth add <player>  \tAuths <player>"
                            + "\n /tcmd auth team  \t\tAuths all your teammates";
                    if (Clans != null)
                    {
                        infoText += "\n /tcmd auth clan \t\t\tAuths all your clanmates";
                        infoText += "\n /tcmd auth ally \t\t\tAuths all allied members";
                    }
                    break;
                case "turret":
                    infoText += "\n All Functions only apply to turrets in <color=green>200m</color> range, that are"
                            + "\n owned by you!";
                    infoText += "\n\n<color=green> === Turret Controlls ===</color>"
                            + "\n /tcmd status \t\t\tTurret Info"
                            + "\n /tcmd peace \t\t\tSet all turrets to peacekeeper"
                            + "\n /tcmd war \t\t\t\tSet all turrets to attack all mode"
                            + "\n /tcmd fill <amount>  \t\tFills up all your turrets by"
                            + "\n \t \t \t\t\t <amount> bullets from inventory"
                            + "\n /tcmd empty \t\t\tEmpty all turrets (take ammo)";
                    break;
                case "trap":
                    infoText += "\n All Functions apply to all shotgun traps in <color=green>200m</color> range,"
                            + "\n that are owned by you!";
                    infoText += "\n\n<color=green> === Trap Controlls ===</color>"
                            + "\n /tcmd filltrap <amount>  \tFills up all your traps by"
                            + "\n \t \t \t\t\t <amount> shells from inventory"
                            + "\n /tcmd emptytrap \t\tEmpty all traps (take ammo)";
                    break;
                default:
                    infoText += "\n\n<color=green> === Detailed Commands ===</color>"
                            + "\n /tcmd help auth \t\t\tList all auth commands"
                            + "\n /tcmd help turret \t\tList all turret commands"
                            + "\n /tcmd help trap \t\t\tList all trap commands";
                    break;
            }
            infoText += "\n<color=orange>===============================================</color>";
            SendReply(player, infoText);

        }
        private Dictionary<int, int> getAmmoTypes(List<AutoTurret> turrets)
        {
            Dictionary<int, int> ammoTypes = new Dictionary<int, int>();
            ammoTypes.Add(0, 0);
            foreach (AutoTurret at in turrets)
            {
                if (!(at.AttachedWeapon is BaseProjectile)) continue;
                BaseProjectile gun = at.AttachedWeapon as BaseProjectile;

                if (at.AttachedWeapon == null || gun.primaryMagazine == null)
                {
                    ammoTypes[0]++;
                    continue;
                }
                int atype = gun.primaryMagazine.ammoType.itemid;
                if (ammoTypes.ContainsKey(atype))
                {
                    ammoTypes[atype]++;
                }
                else
                {
                    ammoTypes.Add(atype, 1);
                }

            }
            return ammoTypes;
        }
        private List<AutoTurret> getTurrets(BasePlayer player)
        {

            List<AutoTurret> tList = new List<AutoTurret>();
            int tcMasks = LayerMask.GetMask("Deployed");
            Vis.Entities<AutoTurret>(player.transform.position, 200, tList, tcMasks);
            List<AutoTurret> list = new List<AutoTurret>();
            List<string> clanMates = Clans?.Call<List<string>>("GetClanMembers", player.userID);
            foreach (AutoTurret entity in tList)
            {
                // vis will find the entity multiple times
                if (list.Contains(entity) || entity.OwnerID == 0) continue;
                if (entity.OwnerID == player.userID || (clanMates != null && clanMates.Contains(entity.OwnerID.ToString())) || player.IsAdmin)
                {
                    list.Add(entity);
                }
            }
            return list;
        }

        private List<GunTrap> getTraps(BasePlayer player)
        {
            List<GunTrap> tList = new List<GunTrap>();
            int tcMasks = LayerMask.GetMask("Deployed");
            Vis.Entities<GunTrap>(player.transform.position, 200, tList, tcMasks);
            List<GunTrap> list = new List<GunTrap>();
            List<string> clanMates = Clans?.Call<List<string>>("GetClanMembers", player.userID);
            foreach (GunTrap entity in tList)
            {
                // vis will find the entity multiple times
                if (list.Contains(entity) || entity.OwnerID == 0) continue;
                if (entity.OwnerID == player.userID || (clanMates != null && clanMates.Contains(entity.OwnerID.ToString())) || player.IsAdmin)
                {
                    list.Add(entity);
                }
            }
            return list;
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

        private string LangMsg(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
    }
}
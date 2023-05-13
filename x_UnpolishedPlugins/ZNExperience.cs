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
//Requires: Kits
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using System;
using Network;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using System.Globalization;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("ZNExperience", "DocValerian", "2.0.7")]
    public class ZNExperience : RustPlugin
    {
        static ZNExperience Plugin;

        [PluginReference]
        private Plugin BetterChat, ServerRewards, ZNTitleManager, ImageLibrary, PlaytimeTracker, InfoAPI, ZNFarming, PrivateBuyday;

        #region globalVars
        const string permUse = "znexperience.use";
        const string permAdm = "znexperience.admin";
        const string permNoLvl = "znexperience.nolevel";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        private Dictionary<ulong, ProfileManager> _pm = new Dictionary<ulong, ProfileManager>();
        private Dictionary<int, int> xpPerLevel = new Dictionary<int, int>();
        private Dictionary<int, int> xpAtLevel = new Dictionary<int, int>();
        private ZNSkillTree _tree;
        private List<string> allPermissions = new List<string>();
        private NumberFormatInfo nfi = new CultureInfo("en-GB", false).NumberFormat;
        private List<string> expPerms = new List<string>()
        {
            "znexperience.xpboost_10",
            "znexperience.xpboost_20",
            "znexperience.xpboost_30",
            "znexperience.xpboost_40",
            "znexperience.xpboost_50",
        };
        private const int PAPERID = -544317637;
        #endregion

        #region plugin load

        void Loaded()
        {
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAdm, this);
            permission.RegisterPermission(permNoLvl, this);

            foreach (string p in expPerms)
            {
                permission.RegisterPermission(p, this);
            }
            //storedData = new StoredData();
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ZNExperience");
            expPerLevelCache();
            initSkills();
            initSkillGroups();
            SaveData(storedData);

            nfi.NumberDecimalSeparator = ",";
            nfi.NumberGroupSeparator = ".";
            nfi.NumberDecimalDigits = 0;
        }
        private void OnServerInitialized()
        {
            _tree = new ZNSkillTree();
            foreach (var p in BasePlayer.activePlayerList)
            {
                ProfileManager.Get(p.userID);
                reloadLiveUI(p);
            }
            RegisterTitles();
            ImageLibrary?.Call("ImportImageList", "xpimg", new Dictionary<string, string>() { ["rp_img"] = "http://m2v.eu/i/zn/rp1.png", ["xp_img"] = "http://m2v.eu/i/zn/xp.png" });
        }
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(player);
                killLiveUI(player);
                killUI(player);

            }
        }

        #endregion

        #region Config
        protected override void LoadDefaultConfig()
        {
            Cfg = new Configuration();
            Puts("Loaded default configuration file");
        }
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

        static Configuration Cfg = new Configuration();
        public class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public PluginSettings Settings = new PluginSettings();
            [JsonProperty(PropertyName = "Experience for killing")]
            public ExperienceMap xp = new ExperienceMap();
            [JsonProperty(PropertyName = "Is pvp")]
            public bool isPvp = false;
        }
        public class PluginSettings
        {
            public int minSP = 8;
            public int SPPerLevel = 1;
            public int apocalypseSPPerPrestige = 1;
            public int skillPointsPerPrestige = 10;
            //public int expPerLevel = 20000;
            public int baseXPPerLevel = 5000;
            public int xpGrowPerLevel = 400;
            public float xpPerPrestigeMulti = 0.1f;
            public DateTime lastApocalypse = DateTime.Now;
            public DateTime nextApocalypse = DateTime.Now;
            public int levelCap = 99;
            public string xpName = "Experience";
            public int reskillRP = 30000;
            public int buyLevelRP = 50000;
            public int buyTenLevelRP = 450000;
            public int reskillPapers = 20000;
            public float prestigeLevelBuyFactor = 0.15f;
        }
        public class ExperienceMap
        {
            public Dictionary<string, int> onDeath = new Dictionary<string, int>()
            {
                ["scientistnpc_heavy"] = 25,
                ["scientist"] = 40,
                ["npc_tunneldweller"] = 40,
                ["npc_underwaterdweller"] = 60,
                ["scientistnpc_roam_nvg_variant"] = 60,
                ["scientistnpc_junkpile_pistol"] = 40,
                ["scientistnpc_full_lr300"] = 60,
                ["scientistnpc_full_mp5"] = 60,
                ["scientistnpc_full_pistol"] = 60,
                ["scientistnpc_full_any"] = 20,
                ["scientistnpc_full_shotgun"] = 60,
                ["scientistnpc"] = 40,
                ["scientistnpc_oilrig"] = 30,
                ["scientistnpc_cargo"] = 30,
                ["scientistnpc_roamtethered"] = 40,
                ["scientistnpc_roam"] = 40,
                ["scientistnpc_patrol"] = 40,
                ["scarecrow"] = 35,
                ["heavyscientist"] = 40,
                ["heavyscientistad"] = 40,
                ["chicken"] = 200,
                ["boar"] = 40,
                ["wolf"] = 40,
                ["bear"] = 100,
                ["polarbear"] = 100,
                ["stag"] = 100,
                ["loot_barrel_1"] = 40,
                ["loot_barrel_2"] = 40,
                ["loot-barrel-1"] = 40,
                ["loot-barrel-2"] = 40,
                ["oil_barrel"] = 40,
                ["trash-pile-1"] = 40,
                ["roadsign1"] = 40,
                ["roadsign2"] = 40,
                ["roadsign3"] = 40,
                ["roadsign4"] = 40,
                ["roadsign5"] = 40,
                ["roadsign6"] = 40,
                ["roadsign7"] = 40,
                ["roadsign8"] = 40,
                ["roadsign9"] = 40,
            };
            public Dictionary<string, int> onLoot = new Dictionary<string, int>()
            {
                ["crate_normal"] = 50,
                ["crate_normal_2"] = 50,
                ["crate_normal_2_medical"] = 150,
                ["vehicle_parts"] = 50,
                ["crate_tools"] = 50,
                ["crate_underwater_advanced"] = 300,
                ["crate_underwater_basic"] = 150,
                ["crate_elite"] = 300,
                ["codelockedhackablecrate"] = 500,
                ["codelockedhackablecrate_oilrig"] = 500,
                ["foodbox"] = 50,
                ["trash-pile-1"] = 50,
                ["crate_mine"] = 70,
                ["minecart"] = 70,
            };
        }
        class StoredData
        {
            public Dictionary<string, ZNSkill> Skills = new Dictionary<string, ZNSkill>()
            { };

            public Dictionary<string, ZNSkillGroup> SkillGroups = new Dictionary<string, ZNSkillGroup>()
            {
                ["farming"] = new ZNSkillGroup { name = "Farming Skills", description = "Skills that influence your resource farming ability", id = "farming", skillIDs = new List<string>() },
                ["abilities"] = new ZNSkillGroup { name = "Commands/Abilities", description = "Skills that give you extra powers", id = "abilities", skillIDs = new List<string>() },
            };

            public HashSet<ulong> noUIPlayers = new HashSet<ulong>();

        };

        StoredData storedData;

        private static void LoadData<T>(out T data, string filename = null) =>
        data = Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? "ZNExperience");

        private static void SaveData<T>(T data, string filename = null) =>
        Interface.Oxide.DataFileSystem.WriteObject(filename ?? "ZNExperience", data);

        #endregion
        #region Hooks
        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Title == "Better Chat")
                RegisterTitles();
        }
        private void OnPermissionRegistered(string name, Plugin owner)
        {

        }

        private void OnPluginUnloaded(Plugin plugin)
        {

        }

        private void OnServerSave()
        {
            foreach (ProfileManager pm in _pm.Values)
            {
                pm.SaveData();
            }

        }
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (_pm.ContainsKey(player.userID))
            {
                ProfileManager pm = _pm[player.userID];
                pm.SaveData();
                _pm.Remove(player.userID);
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player is NPCPlayer || player is HumanNPC || player.IsNpc) return;
            if (!_pm.ContainsKey(player.userID))
            {
                _pm[player.userID] = ProfileManager.Get(player.userID);
                if (!(storedData.noUIPlayers.Contains(player.userID))) reloadLiveUI(player);
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {


            if (entity == null || entity.IsDestroyed) return;
            if (entity == null || String.IsNullOrWhiteSpace(entity?.ShortPrefabName)) return;
            if (info == null || info.Initiator == null) return;

            if (!(info.Initiator is BasePlayer) || (info.Initiator as BasePlayer).IsNpc)
                return;

            BasePlayer bplayer = info?.Initiator?.ToPlayer();
            if (bplayer == null) return;

            //Puts("DEBUG: Player " + bplayer + " KILLED " + entity.ShortPrefabName);
            if (Cfg.xp.onDeath.ContainsKey(entity.ShortPrefabName))
            {
                AddXp(bplayer.userID, Cfg.xp.onDeath[entity.ShortPrefabName]);
            }
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            //Puts("DEBUG: OnLootEntityEnd " + player + " looted " + entity + " owner " + entity.OwnerID);

            if (entity == null || entity.IsDestroyed) return;
            if (entity == null || String.IsNullOrWhiteSpace(entity?.ShortPrefabName)) return;

            if (Cfg.xp.onLoot.ContainsKey(entity.ShortPrefabName))
            {
                AddXp(player.userID, Cfg.xp.onLoot[entity.ShortPrefabName]);
            }
        }
        
        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if (player == null || player.IsNpc) return;
            if (item.info.shortname == "researchpaper" && !HasPermission(player.UserIDString, permAdm))
            {
                if (_pm.ContainsKey(player.userID))
                {
                    Item papers = player.inventory.FindItemID(PAPERID);
                    // safety from overbyuing
                    if (papers == null || papers.amount < 1)
                    {
                        return;
                    }
                    player.inventory.Take(null, PAPERID, papers.amount);
                    Puts("INFO: " + player + " auto-converted " + papers.amount + " Research Papers to XP (with multiplier)");
                    if (papers.amount >= 10000)
                    {
                        AddXp(player.userID, papers.amount, false);
                    }
                    else
                    {
                        AddXp(player.userID, papers.amount);
                    }
                    SendReply(player, "<color=green>ZN-XP:</color> You gained XP from looting " + papers.amount + " research papers (XP-Boosts apply!)");
                }
            }
        }
        
        #endregion

        #region Commands

        [ConsoleCommand("znexp.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)// || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            killUI(player);
        }


        [ConsoleCommand("znexp.prestige")]
        private void CmdPrestige(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)// || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            float scale = arg.GetFloat(0);
            //check & Update ProfileManager
            if (_pm.ContainsKey(player.userID))
            {
                _pm[player.userID].Prestige();
            }

            reloadUI(player);
        }

        [ConsoleCommand("znexp.skilldetail")]
        private void CmdOpenDetail(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)// || !HasPermission(arg.Player().UserIDString, permUse))
                return;

            string skillId = arg.GetString(0);
            //check & Update ProfileManager
            if (_pm.ContainsKey(player.userID) && storedData.Skills.ContainsKey(skillId))
            {

                CuiHelper.DestroyUi(player, mainName + "_skillDetail");
                GUIskillDetailElement(player, mainName + "_skillDetail", _pm[player.userID], storedData.Skills[skillId]);
            }


        }
        [ConsoleCommand("znexp.closedetail")]
        private void CmdCloseDetail(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, mainName + "_skillDetail");

        }

        [ConsoleCommand("znexp.addskill")]
        private void CmdAddSkill(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            string skillId = arg.GetString(0);
            //check & Update ProfileManager
            if (_pm.ContainsKey(player.userID) && storedData.Skills.ContainsKey(skillId))
            {
                ZNResponse r = _pm[player.userID].AddSkill(storedData.Skills[skillId]);
                if (r.success)
                {
                    InfoAPI.Call("ShowInfoPopup", player, r.msg);
                    reloadUI(player);
                }
                else
                {
                    InfoAPI.Call("ShowInfoPopup", player, r.msg, true);
                }
            }
        }

        [ConsoleCommand("znexp.prestigeunlock")]
        private void CmdPrestigeUnlock(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            string skillId = arg.GetString(0);
            //check & Update ProfileManager
            if (_pm.ContainsKey(player.userID) && storedData.Skills.ContainsKey(skillId))
            {
                ZNResponse r = _pm[player.userID].PUnlock(storedData.Skills[skillId].prestigeUnlockId);
                if (r.success)
                {
                    InfoAPI.Call("ShowInfoPopup", player, r.msg);
                    reloadUI(player);
                }
                else
                {
                    InfoAPI.Call("ShowInfoPopup", player, r.msg, true);
                }
            }
        }
        [ConsoleCommand("znexp.reskillpaper")]
        private void CmdReskillPaper(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            if (_pm.ContainsKey(player.userID))
            {
                Item papers = player.inventory.FindItemID(PAPERID);
                ProfileManager p = _pm[player.userID];
                // safety from overbyuing
                if (papers == null || papers.amount < 1)
                {
                    InfoAPI.Call("ShowInfoPopup", player, "You don't have any Research Papers!", true);
                    return;
                }

                if (papers.amount < Cfg.Settings.reskillPapers)
                {
                    InfoAPI.Call("ShowInfoPopup", player, "It costs " + Cfg.Settings.reskillPapers + " Research Papers to use this command,\nyou only have " + papers.amount, true);
                    return;
                }
                else
                {
                    player.inventory.Take(null, PAPERID, Cfg.Settings.reskillPapers);
                    p.Reskill();
                    reloadUI(player);
                }
                InfoAPI.Call("ShowInfoPopup", player, "You paid " + Cfg.Settings.reskillPapers + " Research Papers.\nSkills & Unlocks reset!");
                Puts("INFO: " + player + " reset their skills for Research Papers");
            }
        }


        [ConsoleCommand("znexp.transferpapers")]
        private void CmdTransferPaper(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            if (_pm.ContainsKey(player.userID))
            {
                ProfileManager p = _pm[player.userID];
                int amount = 0;
                var slots = player.inventory.FindItemIDs(PAPERID);
                foreach (var slot in slots)
                {
                    if (slot == null)
                    {
                        continue;
                    }
                    amount += slot.amount;
                    slot.Drop(Vector3.zero, Vector3.zero);
                }
                if (amount < 1)
                {
                    InfoAPI.Call("ShowInfoPopup", player, "You don't have any Research Papers!", true);
                    return;
                }

                amount = (int)Math.Floor(p.xpMulti * amount);
                Puts("INFO: " + player + " converted " + amount + " Research Papers to XP (with boosts)");  
                p.AddExperience(amount);
                reloadUI(player);
            }
        }
        [ConsoleCommand("znexp.closeconfirm")]
        private void CmdCloseConfirm(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, mainName + "_confirm");
        }

        [ConsoleCommand("znexp.confirm")]
        private void CmdConfirm(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, mainName + "_confirm");
            if (_pm.ContainsKey(player.userID))
            {
                ProfileManager p = _pm[player.userID];
                // safety from overbyuing
                if (!p.CanReskill())
                {
                    InfoAPI.Call("ShowInfoPopup", player, "You don't have anything allocated yet!", true);
                    return;
                }
                object bal = ServerRewards?.Call("CheckPoints", player.userID);
                if (bal == null) return;
                int playerRP = (int)bal;
                if (playerRP < Cfg.Settings.reskillRP)
                {
                    InfoAPI.Call("ShowInfoPopup", player, "It costs " + numberCandy(Cfg.Settings.reskillRP) + " RP to use this command, you only have " + playerRP, true);

                    return;
                }
                else
                {
                    ServerRewards?.Call("TakePoints", player.userID, Cfg.Settings.reskillRP);
                    p.Reskill();
                    reloadUI(player);
                }
                InfoAPI.Call("ShowInfoPopup", player, "You paid " + numberCandy(Cfg.Settings.reskillRP) + " RP.\nSkills & Unlocks reset!");
                Puts("INFO: " + player + " reset their skills for RP");
            }
        }
        [ConsoleCommand("znexp.reskill")]
        private void CmdReskill(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            GUIconfirmElement(player, mainName + "_confirm");
        }

        [ConsoleCommand("znexp.noskillbuylevel")]
        private void CmdBuyLevel(ConsoleSystem.Arg arg)
        {
            return; //deprecated!
            /*
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            int levels = arg.GetInt(0);

            if (_pm.ContainsKey(player.userID))
            {
                ProfileManager p = _pm[player.userID];
                // safety from overbyuing

                if (p.level + levels > Cfg.Settings.levelCap)
                {
                    levels = Cfg.Settings.levelCap - p.level;
                }
                if (levels == 0)
                {
                    InfoAPI?.Call("ShowInfoPopup", player, "You must prestige first!", true);
                    return;
                }
                object bal = ServerRewards?.Call("CheckPoints", player.userID);
                if (bal == null) return;
                int playerRP = (int)bal;
                int price = (int) Math.Floor(Cfg.Settings.prestigeLevelBuyFactor * p.prestigeLevel * (levels * Cfg.Settings.buyLevelRP) + (levels * Cfg.Settings.buyLevelRP));
                if (playerRP < price)
                {
                    InfoAPI?.Call("ShowInfoPopup", player, "It costs " + price + " RP to buy " + levels + " levels, you only have " + playerRP, true);
                    return;
                }
                else
                {
                    ServerRewards?.Call("TakePoints", player.userID, price);
                    p.AddExperience(levels * Cfg.Settings.expPerLevel);
                    reloadUI(player);
                }
                SendReply(player, "<color=green>ZN-XP:</color> You paid " + price + " RP.\n" + levels + " levels added!");
                Puts("INFO: " + player + " purchased " + levels + " levels for " + price + " RP");
            }
            */
        }
        [ChatCommand("atpt")]
        void CmdAdmTest(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permAdm)) return;
            
        }


        [ChatCommand("atxp")]
        void CmdAdmInspect(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permAdm)) return;
            BasePlayer p;
            if(args.Length == 0)
            {
                p = player;
            }
            else
            {
                var findPlayers = FindPlayer(args[0]);
                if (findPlayers.Count == 0 )
                {
                    SendReply(player, "No players found." );
                    return;
                }

                if (findPlayers.Count > 1 )
                {
                    SendReply(player, "Multiple players found." );
                    return;
                }
                 p = findPlayers[0];

                
            }

            ProfileManager pm = _pm[p.userID];
            if(args.Length == 2 && args[1] != null)
            {
                switch(args[1]){
                    case "demote":
                        int levels = 0;
                        int.TryParse(args[2], out levels);
                        if (levels == 0) return;
                        pm.Demote(player, levels);
                        reloadLiveUI(p);
                        break;
                    default:
                        break;
                }
            }
            string msg = "<color=orange>----- " + Plugin.Name + " - ADMIN /atxp -----</color>";
            msg += "\nInspecting: \t" + p.displayName + " " + pm.formatedDisplayLevel;
            msg += "\nLifetime XP:\t" + numberCandy(pm.eternalXpPoints);
            msg += "\nLevel-Up XP:\t" + (pm.xpPoints - getExpATLevel(pm.displayLevel)) + "/" + getExpForLevel(pm.displayLevel + 1) + " (" + getPercentToNextLevel(pm.displayLevel, pm.xpPoints) + "%)";
            msg += "\nSkillpoints:\t\t" + (pm.maxSkillPoints - pm.allocatedSkillPoints) + "/" + pm.maxSkillPoints;
            msg += "\nPrestige-pts:\t" + (pm.prestigeLevel - pm.prestigeUnlocks.Count()) + "/" + pm.prestigeLevel;
            msg += "\nPlaytime:\t\t" + GetPlaytimeFor(player);

            msg += "\n\n<color=green>Skills:</color>\n";
            msg += string.Join(", ", pm.playerSkills);
            msg += "\n\n<color=green>Prestige Unlocks:</color>\n";
            msg += string.Join(", ", pm.prestigeUnlocks);

            SendReply(player, msg);

        }

        [ChatCommand("pui")]
        void CmdUiToggle(BasePlayer player, string command, string[] args)
        {
            toggleUI(player);
        }

        [ChatCommand("p")]
        void CmdProfileMain(BasePlayer player, string command, string[] args)
        {
            reloadUI(player);
            return;
        }
        /*
        [ChatCommand("ll")]
        void TESTcommand(BasePlayer player, string command, string[] args)
        {
            //ProfileManager.Get(player.userID, true);
            reloadUI(player);
            return;
        }
        */
        #endregion
        #region API

        private int GetLevel(ulong playerId)
        {
            ProfileManager p;
            if (!_pm.TryGetValue(playerId, out p)) return 0;

            return p.level;
        }
        private int GetDisplayLevel(ulong playerId)
        {
            ProfileManager p;
            if (!_pm.TryGetValue(playerId, out p)) return 0;

            return p.displayLevel;
        }

        private string GetLevelString(ulong playerId)
        {
            ProfileManager p;
            if(!_pm.TryGetValue(playerId, out p)) return "";
            return p.formatedDisplayLevel;
        }
        private string GetLifetimeXPString(ulong playerId)
        {
            ProfileManager p;
            if (!_pm.TryGetValue(playerId, out p)) return "";

            return numberCandy(p.eternalXpPoints);
        }

        private Dictionary<ulong, string> GetLowLevelPlayers()
        {
            Dictionary<ulong, string> ret = new Dictionary<ulong, string>();

            foreach(ProfileManager p in _pm.Values)
            {
                if(p.displayLevel <= 10000)
                {
                    ret.Add(p.playerID, p.getPlayerName());
                }
            }
            ret.Add(1, "Debug PlAyA 1");
            ret.Add(2, "Debug g 23");
            ret.Add(3, "De3bug 5 6u");
            ret.Add(4, "Dedrgbug PlAyA 1");
            ret.Add(5, "Debug g 23");
            ret.Add(6, "De3bug 5 6u");
            ret.Add(7, "Debug PlAyA 1");
            ret.Add(8, "Debug g 23");
            ret.Add(9, "De3bug 5 6u");
            ret.Add(0, "Deb435tug PlAyA 1");
            ret.Add(11, "Debug g 23");
            ret.Add(12, "De3bug 5 6u");
            ret.Add(13, "Debug PlAyA 1");
            ret.Add(14, "Debug g 23");
            ret.Add(15, "De3bug 5 6u");
            ret.Add(16, "Deb erug PlAyA 1");
            ret.Add(17, "Debug g 23");
            ret.Add(18, "De3bug 5 6u");
            ret.Add(19, "Deberge ug PlAyA 1");
            ret.Add(20, "Debug g 23");
            ret.Add(21, "De3be ug 5 6u");

            return ret;
        }

        private void AddXp(ulong playerId, int amount, bool bonus = true)
        {
            ProfileManager p;
            if (!_pm.TryGetValue(playerId, out p)) return;
            if (bonus)
            {
                amount = (int) Math.Ceiling(p.xpMulti * amount);
                /* DEPRECATED
                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    amount = (int)Math.Ceiling(1.5f * amount);
                }
                */
            }

            //Puts("DEBUG: P: "+ playerId + " adding XP: " + amount);
            p?.AddExperience(amount);
        }
        private void AddLevel(ulong playerId, int amount)
        {
            ProfileManager p;
            if (!_pm.TryGetValue(playerId, out p)) return;
            int value = xpPerLevel[_pm[playerId].displayLevel];

            //Puts("DEBUG: P: " + playerId + " adding Levels: " + amount + " (XP: " + value+")");
            p?.AddExperience(value);
        }

        private bool CanAddLevel(ulong playerId, int amount)
        {
            ProfileManager p;
            if (!_pm.TryGetValue(playerId, out p)) return false;

            return (p.level + amount) <= Cfg.Settings.levelCap;
        }

        private void RegisterSkill(
            string id,
            string name, 
            string groupId,
            int spCost,
            List<string> permissionEffect,
            string description,
            string prerequisiteSkillId,
            string followUpSkillId,
            bool isDefault,
            string prestigeUnlockId,
            string iconURL 
            )
        {
            if (storedData.Skills.ContainsKey(id)) { 
                storedData.Skills[id] = new ZNSkill
                {
                    id = id,
                    name = name,
                    groupId = groupId,
                    spCost = spCost,
                    permissionEffect = permissionEffect,
                    description = (description != "") ? description : null,
                    prerequisiteSkillId = (prerequisiteSkillId != "") ? prerequisiteSkillId : null,
                    followUpSkillId = (followUpSkillId != "") ? followUpSkillId : null,
                    isDefault = isDefault,
                    prestigeUnlockId = (prestigeUnlockId != "") ? prestigeUnlockId : null,
                    iconURL = (iconURL != "") ? iconURL : null,
                };
            }
            else
            {
                ZNSkill s = new ZNSkill
                {
                    id                  = id,
                    name                = name,
                    groupId             = groupId,
                    spCost              = spCost,
                    permissionEffect    = permissionEffect,
                    description         = (description != "") ? description : null,
                    prerequisiteSkillId = (prerequisiteSkillId != "") ? prerequisiteSkillId : null,
                    followUpSkillId     = (followUpSkillId != "") ? followUpSkillId : null,
                    isDefault           = isDefault,
                    prestigeUnlockId    = (prestigeUnlockId != "") ? prestigeUnlockId : null,
                    iconURL             = (iconURL != "") ? iconURL : null,
                };
                storedData.Skills.Add(s.id, s);
            }
            SaveData(storedData);
            //Puts("DEBUG: RegisterSkill (upsert) - " + id);
        }

        private void SaveSkillSetup()
        {
            initSkillGroups();
            SaveData(storedData);
            _tree = new ZNSkillTree();
        }
        #endregion
        #region helpers/formatters
        private int calculateXPForLevel(int prestige, int level)
        {
            int xp = -1;
            xp = (int) Math.Floor((Cfg.Settings.baseXPPerLevel + level*Cfg.Settings.xpGrowPerLevel) * (1 + prestige * Cfg.Settings.xpPerPrestigeMulti));
            return xp;
        }

        private void expPerLevelCache()
        {
            //string lvls = "";
            for(int p = 0; p < 50; p++)
            {
                for( int i = 1; i <= Cfg.Settings.levelCap; i++)
                {
                    xpPerLevel[(p*100+i)] = calculateXPForLevel(p, i);
                    if(i == 1)
                    {
                        xpAtLevel[(p * 100 + i)] = xpPerLevel[(p * 100 + i)];
                    }
                    else
                    {
                        xpAtLevel[(p * 100 + i)] = xpAtLevel[(p * 100 + (i-1))] + xpPerLevel[(p * 100 + i)];
                    }
                    //lvls += " --- Level: " + (p * 100 + i) + " xp: " + xpPerLevel[(p * 100 + i)];
                }
            }
            //Puts("DEBUG: " + lvls);
        }
        private string skillNameFromId(string skillID)
        {
            if (storedData.Skills.ContainsKey(skillID))
            {
                return storedData.Skills[skillID].name;
            }
            return skillID;
        }
        private int getExpATLevel(int level)
        {
            int xp = 0;
            xpAtLevel.TryGetValue(level, out xp);
            return xp;
        }
        private int getExpForLevel(int level)
        {
            int xp = 0;
            xpPerLevel.TryGetValue(level, out xp);
            return xp;
        }
        private float getPercentToNextLevel(int displaylevel, int xp)
        {
            int reqXp = getExpForLevel(displaylevel+1);
            int baseXp = getExpATLevel(displaylevel);
            int levelXP = 1;
            xpPerLevel.TryGetValue(displaylevel+1, out levelXP);
            //Puts("base for current level " + baseXp + " next level base " + reqXp + " per level " + levelXP );
            return (float)(xp - baseXp ) / levelXP * 100f;
        }

        private string GetImage(string fileName, ulong skin = 0)
        {
            string imageId = (string)ImageLibrary.Call("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }

        #endregion

        #region Functions

        private void initSkills()
        {

            storedData.Skills["autoloot.lv0"] = new ZNSkill
            {
                id = "autoloot.lv0",
                name = "Autoloot Blood",
                groupId = "farming",
                spCost = 1,
                permissionEffect = new List<string> { "znfarming.blood.pickup" },
                description = "Passive skill that will automatically pick up Blood gained from farming etc. for you."
            };
            storedData.Skills["instanttp.lv0"] = new ZNSkill
            {
                id = "instanttp.lv0",
                name = "Instant Teleport",
                groupId = "abilities",
                spCost = 5,
                permissionEffect = new List<string> { "warpsgui.none", "teleportgui.none", "homesgui.none" },
                description = "Allows you to instant Teleport with /home /tp /warp.\nAlso removes TP cooldowns."
            };
            storedData.Skills["sil.lv0"] = new ZNSkill
            {
                id = "sil.lv0",
                name = "/sil(t) command",
                groupId = "abilities",
                spCost = 2,
                permissionEffect = new List<string> { "signartist.url", "signartist.text" },
                description = "Allows you to put images and text on signs with /sil and /silt"
            };
            storedData.Skills["quarry.lv0"] = new ZNSkill
            {
                id = "quarry.lv0",
                name = "/mm command",
                groupId = "abilities",
                spCost = 2,
                permissionEffect = new List<string> { "virtualquarries.use" },
                description = "Allows you to use virtual Quarries with /mm"
            };
            storedData.Skills["smelt.lv0"] = new ZNSkill
            {
                id = "smelt.lv0",
                name = "/smelt command",
                groupId = "abilities",
                spCost = 2,
                permissionEffect = new List<string> { "virtualsmelter.use" },
                description = "Allows you to use our efficient super smelter with /smelt"
            };
            storedData.Skills["kits.lv0"] = new ZNSkill
            {
                id = "kits.lv0",
                name = "VIP Starter /kit",
                groupId = "abilities",
                spCost = 3,
                permissionEffect = new List<string> { "kits.vipstarter" },
                followUpSkillId = "kits.lv1",
                description = "Gives you a special once per wipe starter /kit"
            };
            storedData.Skills["kits.lv1"] = new ZNSkill
            {
                id = "kits.lv1",
                name = "Daily Horse /kit",
                groupId = "abilities",
                spCost = 3,
                permissionEffect = new List<string> { "kits.dailyhorse" },
                prerequisiteSkillId = "kits.lv0",
                followUpSkillId = "kits.lv2",
                description = "Gives you a daily Horse & Boat /kit"
            };
            storedData.Skills["kits.lv2"] = new ZNSkill
            {
                id = "kits.lv2",
                name = "Vet Starter /kit",
                groupId = "abilities",
                spCost = 5,
                permissionEffect = new List<string> { "kits.vetstarter" },
                prerequisiteSkillId = "kits.lv1",
                followUpSkillId = "kits.lv3",
                description = "Gives you another special once per wipe starter /kit"
            };
            storedData.Skills["kits.lv3"] = new ZNSkill
            {
                id = "kits.lv3",
                name = "Daily Heli /kit",
                groupId = "abilities",
                spCost = 5,
                permissionEffect = new List<string> { "kits.vetcopter" },
                prerequisiteSkillId = "kits.lv2",
                description = "Gives you a daily RHIB and Minicopter /kit"
            };
            storedData.Skills["buyday.lv0"] = new ZNSkill { id = "buyday.lv0", name = "/day command", groupId = "abilities", spCost = 3, permissionEffect = new List<string> { "privatebuyday.use" }, followUpSkillId = "buyday.lv1", description = "Allows you to use /day and buy daylight whenever you want" };
            storedData.Skills["buyday.lv1"] = new ZNSkill { id = "buyday.lv1", name = "/day FREE", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "privatebuyday.free" }, prerequisiteSkillId = "buyday.lv0", description = "Allows you to use /day without RP cost at any time" };
            storedData.Skills["getmystuff.lvz"] = new ZNSkill { id = "getmystuff.lvz", name = "/getmystuff Base", groupId = "abilities", spCost = 0, permissionEffect = new List<string> { "usrtools.getmystuff.base" }, isDefault = true, followUpSkillId = "getmystuff.lv0",  description = "Allows you to use /getmystuff to help find your corpse or bag" };
            storedData.Skills["getmystuff.lv0"] = new ZNSkill { id = "getmystuff.lv0", name = "/getmystuff BUY", groupId = "abilities", spCost = 2, permissionEffect = new List<string> { "usrtools.getmystuff" }, followUpSkillId = "getmystuff.lv1", prerequisiteSkillId = "getmystuff.lvz", description = "Allows you to use /getmystuff buy and teleport loot from your corpse to you (for RP!). Only if your corpse hasn't despawned! Not a 100% guarantee!" };
            storedData.Skills["getmystuff.lv1"] = new ZNSkill { id = "getmystuff.lv1", name = "/getmystuff FREE", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "usrtools.getmystuff.free" }, prerequisiteSkillId = "getmystuff.lv0", followUpSkillId = "getmystuff.lv2", prestigeUnlockId = "pGetStuff", description = "Allows you to use /getmystuff buy for FREE. Not a 100% guarantee to not lose some stuff!" };
            storedData.Skills["getmystuff.lv2"] = new ZNSkill { id = "getmystuff.lv2", name = "/getmystuff AUTO", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "restoreupondeath.admin" }, prerequisiteSkillId = "getmystuff.lv1", prestigeUnlockId = "pGetStuff", description = "With this skill active you will automatically get your stuff back & equipped. !!! NOT IN RAIDS !!! Not a 100% guarantee to not lose some stuff!" };
            storedData.Skills["nodurability.lv0"] = new ZNSkill { id = "nodurability.lv0", name = "Items don't break", groupId = "abilities", spCost = 10, permissionEffect = new List<string> { "nodurability.allowed" }, description = "Your Items will be repaired on use\nOnly if not fully broken and NOT within Raids!" };
            storedData.Skills["agrade.lv0"] = new ZNSkill { id = "agrade.lv0", name = "/agrade command", groupId = "abilities", spCost = 2, permissionEffect = new List<string> { "buildmanager.agrade.use" }, followUpSkillId = "agrade.lv1", description = "Allows you to use /agrade to upgrade the entire building at once (Costs RP and 1.5x Res)" };
            storedData.Skills["agrade.lv1"] = new ZNSkill { id = "agrade.lv1", name = "/agrade FREE", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "buildmanager.agrade.free" }, prerequisiteSkillId = "agrade.lv0", description = "Allows you to use /agrade without RP cost and with 1.1x res cost." };
            storedData.Skills["repairall.lv0"] = new ZNSkill { id = "repairall.lv0", name = "/repairall command", groupId = "abilities", spCost = 2, permissionEffect = new List<string> { "buildmanager.repairall.use" }, followUpSkillId = "repairall.lv1", description = "Allows you to use /repairall to repair the entire building at once (Costs RP and 1.5x Res)" };
            storedData.Skills["repairall.lv1"] = new ZNSkill { id = "repairall.lv1", name = "/repairall FREE", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "buildmanager.repairall.free" }, prerequisiteSkillId = "repairall.lv0", description = "Allows you to use /repairall without RP cost and with 1.1x res cost." };
            storedData.Skills["xpboost.lv0"] = new ZNSkill { id = "xpboost.lv0", name = "5% XP boost", groupId = "abilities", spCost = 7, permissionEffect = new List<string> { "znexperience.xpboost_10" }, followUpSkillId = "xpboost.lv1", description = "All experience gained will be increased by 5% (Including Research Paper transfer)" };
            storedData.Skills["xpboost.lv1"] = new ZNSkill { id = "xpboost.lv1", name = "12% XP boost", groupId = "abilities", spCost = 10, permissionEffect = new List<string> { "znexperience.xpboost_20" }, prerequisiteSkillId = "xpboost.lv0", followUpSkillId = "xpboost.lv2", description = "All experience gained will be increased by 12% (Including Research Paper transfer)" };
            storedData.Skills["xpboost.lv2"] = new ZNSkill { id = "xpboost.lv2", name = "20% XP boost", groupId = "abilities", spCost = 10, permissionEffect = new List<string> { "znexperience.xpboost_30" }, prerequisiteSkillId = "xpboost.lv1", followUpSkillId = "xpboost.lv3", description = "All experience gained will be increased by 20% (Including Research Paper transfer)" };
            storedData.Skills["xpboost.lv3"] = new ZNSkill { id = "xpboost.lv3", name = "30% XP boost", groupId = "abilities", spCost = 10, permissionEffect = new List<string> { "znexperience.xpboost_40" }, prerequisiteSkillId = "xpboost.lv2", followUpSkillId = "xpboost.lv4", prestigeUnlockId = "pXpboost", description = "All experience gained will be increased by 30% (Including Research Paper transfer)" };
            storedData.Skills["xpboost.lv4"] = new ZNSkill { id = "xpboost.lv4", name = "40% XP boost", groupId = "abilities", spCost = 10, permissionEffect = new List<string> { "znexperience.xpboost_50" }, prerequisiteSkillId = "xpboost.lv3", prestigeUnlockId = "pXpboost", description = "All experience gained will be increased by 40% (Including Research Paper transfer)" };
            storedData.Skills["backpack.lv0"] = new ZNSkill { id = "backpack.lv0", name = "Backpack Base", groupId = "abilities", spCost = 0, permissionEffect = new List<string> { "backpacks.use", "backpacks.size.12" }, followUpSkillId = "backpack.lv1", isDefault = true, description = "Gives you a Backpack with <color=green>2</color> rows" };
            storedData.Skills["backpack.lv1"] = new ZNSkill { id = "backpack.lv1", name = "Backpack Lv. 1", groupId = "abilities", spCost = 1, permissionEffect = new List<string> { "backpacks.size.18" }, prerequisiteSkillId = "backpack.lv0", followUpSkillId = "backpack.lv2", description = "Gives you a Backpack with <color=green>3</color> rows" };
            storedData.Skills["backpack.lv2"] = new ZNSkill { id = "backpack.lv2", name = "Backpack Lv. 2", groupId = "abilities", spCost = 2, permissionEffect = new List<string> { "backpacks.size.24" }, prerequisiteSkillId = "backpack.lv1", followUpSkillId = "backpack.lv3", description = "Gives you a Backpack with <color=green>4</color> rows" };
            storedData.Skills["backpack.lv3"] = new ZNSkill { id = "backpack.lv3", name = "Backpack Lv. 3", groupId = "abilities", spCost = 3, permissionEffect = new List<string> { "backpacks.size.30" }, prerequisiteSkillId = "backpack.lv2", followUpSkillId = "backpack.lv4", description = "Gives you a Backpack with <color=green>5</color> rows" };
            storedData.Skills["backpack.lv4"] = new ZNSkill { id = "backpack.lv4", name = "Backpack Lv. 4", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "backpacks.size.36" }, prerequisiteSkillId = "backpack.lv3", followUpSkillId = "backpack.lv5", description = "Gives you a Backpack with <color=green>6</color> rows" };
            storedData.Skills["backpack.lv5"] = new ZNSkill { id = "backpack.lv5", name = "Backpack Lv. 5", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "backpacks.size.42" }, prerequisiteSkillId = "backpack.lv4", followUpSkillId = "backpack.lv6", prestigeUnlockId = "pBackpack", description = "Gives you a Backpack with <color=green>7</color> rows" };
            storedData.Skills["backpack.lv6"] = new ZNSkill { id = "backpack.lv6", name = "Backpack Lv. 6", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "backpacks.size.48" }, prerequisiteSkillId = "backpack.lv5", followUpSkillId = "backpack.lv7", prestigeUnlockId = "pBackpack", description = "Gives you a Backpack with <color=green>8</color> rows" };
            storedData.Skills["backpack.lv7"] = new ZNSkill { id = "backpack.lv7", name = "Super Backpack Lv. 7", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "backpacks.size.96" }, prerequisiteSkillId = "backpack.lv6", followUpSkillId = "backpack.lv8", prestigeUnlockId = "pBackpack", description = "Gives you a Backpack with <color=green>2x8</color> rows" };
            storedData.Skills["backpack.lv8"] = new ZNSkill { id = "backpack.lv8", name = "Super Backpack Lv. 8", groupId = "abilities", spCost = 4, permissionEffect = new List<string> { "backpacks.size.144" }, prerequisiteSkillId = "backpack.lv7", prestigeUnlockId = "pBackpack", description = "Gives you a Backpack with <color=green>3x8</color> rows" };
            storedData.Skills["treeplanter.lv0"] = new ZNSkill { id = "treeplanter.lv0", name = "/tree command", groupId = "abilities", spCost = 3, permissionEffect = new List<string> { "treeplanter.use" }, description = "Allows you to plant trees near your base, that can't be farmed by others. (BETA!)" };
            storedData.Skills["craftmulti.lv0"] = new ZNSkill { id = "craftmulti.lv0", name = "/cm (craft multiplier)", groupId = "abilities", spCost = 2, permissionEffect = new List<string> { "craftmultiplier.use" }, description = "Allows you to use the <color=orange>/cm 10</color> command to multiply increase your default craft stack. Use <color=orange>/cm</color> to deactivate (BETA!)" };

            storedData.Skills["workbench.lv0"] = new ZNSkill { 
                id = "workbench.lv0", 
                name = "Base Workbench", 
                groupId = "abilities", 
                spCost = 3, 
                permissionEffect = new List<string> { "buildingworkbench.use" }, 
                followUpSkillId = "workbench.lv1", 
                isDefault = false, 
                description = "Gives you a Workbench effect in your entire building. Based on the WorkBenches in the building!" 
            };
            storedData.Skills["workbench.lv1"] = new ZNSkill
            {
                id = "workbench.lv1",
                name = "Global Workbench Lv. 1",
                groupId = "abilities",
                spCost = 2,
                permissionEffect = new List<string> { "buildingworkbench.global1" },
                prerequisiteSkillId = "workbench.lv0",
                followUpSkillId = "workbench.lv2",
                description = "Gives you a global Workbench 1 effect.\nBuilding effect may overrule this within buildings!"
            };
            storedData.Skills["workbench.lv2"] = new ZNSkill
            {
                id = "workbench.lv2",
                name = "Global Workbench Lv. 2",
                groupId = "abilities",
                spCost = 2,
                prestigeUnlockId = "pGlobalBench",
                permissionEffect = new List<string> { "buildingworkbench.global2" },
                prerequisiteSkillId = "workbench.lv1",
                followUpSkillId = "workbench.lv3",
                description = "Gives you a global Workbench 2 effect.\nBuilding effect may overrule this within buildings!"
            };
            storedData.Skills["workbench.lv3"] = new ZNSkill
            {
                id = "workbench.lv3",
                name = "Global Workbench  Lv. 3",
                groupId = "abilities",
                spCost = 2,
                prestigeUnlockId = "pGlobalBench",
                permissionEffect = new List<string> { "buildingworkbench.global3" },
                prerequisiteSkillId = "workbench.lv2",
                description = "Gives you a global Workbench 3 effect.\nOCraft everything, everywhere!"
            };
            
            SaveData(storedData);
        }

        private List<BasePlayer> FindPlayer(string arg)
        {
            var listPlayers = Pool.GetList<BasePlayer>();

            ulong steamid;
            ulong.TryParse(arg, out steamid);
            var lowerarg = arg.ToLower();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (steamid != 0L)
                {
                    if (player.userID == steamid)
                    {
                        listPlayers.Clear();
                        listPlayers.Add(player);
                        return listPlayers;
                    }
                }

                var lowername = player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    listPlayers.Add(player);
                }
            }

            return listPlayers;
        }

        private string GetPlaytimeFor(BasePlayer player)
        {
            var playerTime = PlaytimeTracker?.Call("GetPlayTime", player.UserIDString);
            if (playerTime == null) return "";

            double minutes = (double)playerTime / 60d;
            int hours = 0;

            if (minutes > 60d)
            {
                hours = (int)Math.Floor(minutes / 60d);
                minutes = Math.Floor(minutes - (hours * 60d));
            }
            return hours + "h " + minutes + "min";
        }

        private string infoGetPlayerInfo(BasePlayer player)
        {
            ProfileManager pm = _pm[player.userID];
            string msg = "Player Experience ALPHA";
            msg += "\nLevel: " + pm.displayLevel;
            msg += "\nxp: " + pm.xpPoints;
            msg += "\n% of next level: " + getPercentToNextLevel(pm.displayLevel, pm.xpPoints).ToString("0.00");
            msg += "\nskills: " + string.Join(",", pm.playerSkills);
            return msg;
        }

        private void RegisterTitles()
        {
            //if (!BetterChat) return;
            BetterChat?.Call("API_RegisterThirdPartyTitle", new object[] { this, new Func<IPlayer, string>(GetPlayerTitles) });
        }

        private string GetPlayerTitles(IPlayer player)
        {
            ulong pid = 0;
            if(!ulong.TryParse(player.Id, out pid))
            {
                Puts("ERROR: parsing id for " + player);
                return String.Empty;
            }
            return GetLevelString(pid);
            }
        private void initSkillGroups()
        {
            foreach(ZNSkillGroup sg in storedData.SkillGroups.Values)
            {
                sg.skillIDs.Clear();
            };
            foreach(ZNSkill s in storedData.Skills.Values)
            {
                if (storedData.SkillGroups.ContainsKey(s.groupId))
                {
                    storedData.SkillGroups[s.groupId].skillIDs.Add(s.id);
                }
            }

        }
       
        private string numberCandy(int number)
        {
            return Convert.ToDecimal(number).ToString("N", nfi);
        }

        // gets called whenever Skills are added or reset to trigger updates
        private void notifyRelatedPlugins(BasePlayer player)
        {
            ZNFarming?.Call("updatePlayerCache", player);
            PrivateBuyday?.Call("verifyDayVision", player);
        }


        private List<string> sortSkills(List<string> skillIdList)
        {
            List<string> retVal = new List<string>();
            if (skillIdList.Count == 0) return retVal;
            ZNSkill s;
            Dictionary<string, string> temD = new Dictionary<string, string>();
            foreach(string str in skillIdList)
            {
                if (!storedData.Skills.TryGetValue(str, out s)) continue;
                temD.Add(str, s.name);

            }
            foreach (KeyValuePair<string, string> d in temD.OrderBy(x => x.Value))
            {
                retVal.Add(d.Key);
            }
            return retVal;
        }

        private void toggleUI(BasePlayer player)
        {
            string msg = "<color=green>ZN-XP:</color> Profile & XP UI ";
            if (storedData.noUIPlayers.Contains(player.userID))
            {
                storedData.noUIPlayers.Remove(player.userID);
                msg += "<color=green>Activated!</color>";
                reloadLiveUI(player);
            }
            else
            {
                storedData.noUIPlayers.Add(player.userID);
                msg += "<color=red>Disabled!</color>";
                killLiveUI(player);
            }
            SendReply(player, msg);
        }
        #endregion

        #region Classes 
        public class ZNResponse
        {
            public bool success { get; set; }
            public string msg { get; set; }
            public ZNResponse (bool s, string m = "") : base()
            {
                success = s;
                msg = m;
            }
        }
        public class ZNSkillTree
        {
            public string id { get; set; }
            public Dictionary<string, List<ZNSkill>> tree = new Dictionary<string, List<ZNSkill>>();
            public List<string> permissions = new List<string>();


            public ZNSkillTree() : base()
            {
                //Plugin.Puts("DEBUG: generating skill & perm tree");
                permissions = new List<string>();
                foreach(ZNSkill s in Plugin.storedData.Skills.Values)
                {
                    if(s.prerequisiteSkillId == null)
                    {
                        tree.Add(s.getBaseId(), new List<ZNSkill> { s });
                        addPermissionList(s);
                        _followUp(s);
                    }
                }
                Plugin.Puts("INFO: Update skill tree with " + tree.Count() +" nodes");
            }

            private void _followUp(ZNSkill s, int cnt = 0)
            {
                if (cnt >= 11) return;
                //Plugin.Puts("DEBUG: follow up on " + s.id);
                if (s.followUpSkillId != null && Plugin.storedData.Skills.ContainsKey(s.followUpSkillId))
                {
                    ZNSkill fs = Plugin.storedData.Skills[s.followUpSkillId];
                    tree[s.getBaseId()].Add(fs);
                    addPermissionList(fs);
                    _followUp(fs, cnt+1);
                }
            }

            private void addPermissionList(ZNSkill s)
            {
                foreach(string p in s.permissionEffect)
                {
                    permissions.Add(p);
                }
            }

        }
        public class ZNSkillGroup
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public List<string> skillIDs { get; set; }

        }
        public class ZNSkill
        {
            public string id { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string groupId { get; set; }
            public int spCost { get; set; }
            public List<string> permissionEffect { get; set; }
            public string prerequisiteSkillId { get; set; }
            public string followUpSkillId { get; set; }
            public bool isDefault { get; set; }
            public string prestigeUnlockId { get; set; }
            public string iconURL { get; set; }
            public string getBaseId()
            {
                int index = id.LastIndexOf(".");
                if (index > 0)
                {
                    return id.Substring(0, index);
                }
                return id;
                    
            }
        }

        private class ProfileManager
        {
            public ulong playerID;
            public int level = 0;
            public int xpPoints = 0;
            public int eternalXpPoints = 0;
            public int prestigeLevel = 0;
            public int displayLevel = 0;
            public int allocatedSkillPoints = 0;
            public int maxSkillPoints = 0;
            public string formatedDisplayLevel = "0";
            public HashSet<string> playerSkills = new HashSet<string>();
            public HashSet<string> prestigeUnlocks = new HashSet<string>();

            public DateTime profileCreated = DateTime.Now;
            public DateTime lastApocalypse = Cfg.Settings.lastApocalypse;
            public int apocalypseCounter = 0;
            public int lastApocalypseDisplayLevel = 0;
            public int apocalypseBaseSP = 0;

            private BasePlayer bPlayer;
            private string cGrey = "#DDDDDD";
            private string cGold = "#FFD700";
            public float xpMulti = 1.0f;

            public ProfileManager(ulong playerId) : base()
            {
                playerID = playerId;
            }
            public static ProfileManager Get(ulong playerId, bool wipe = false)
            {
                if (Plugin._pm.ContainsKey(playerId) && !wipe)
                    return Plugin._pm[playerId];

                var fileName = $"{Plugin.Name}/{playerId}";

                ProfileManager manager;
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(fileName) && !wipe)
                {
                    // Load existing Data
                    ZNExperience.LoadData(out manager, fileName);

                    // Apocalypse compatibility
                    if (manager.profileCreated == DateTime.MinValue)
                    {
                        manager.profileCreated = Cfg.Settings.lastApocalypse;
                    }
                    if (manager.lastApocalypse == DateTime.MinValue)
                    {
                        manager.lastApocalypse = Cfg.Settings.lastApocalypse;
                    }

                }
                else
                {
                    // Create a completely new Playerdataset
                    manager = new ProfileManager(playerId);
                    manager.loadDefaultSkills();
                    manager.updateSkillPoints();
                    ZNExperience.SaveData(manager, fileName);
                }

                Interface.Oxide.DataFileSystem.GetDatafile(fileName).Settings = new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Populate
                };
                manager.bPlayer = BasePlayer.FindAwakeOrSleeping(playerId.ToString());
                Plugin._pm[playerId] = manager;
                manager.checkApocalypse();

                manager.SyncSkillPermissions();
                manager.updateFormatString();
                manager.updateXPMulti();
                return manager;
            }

            public void SaveData()
            {
                ZNExperience.SaveData(this, $"{Plugin.Name}/{playerID}");
            }

            private void loadDefaultSkills()
            {
                foreach(KeyValuePair<string,ZNSkill> s in Plugin.storedData.Skills)
                {
                    if (s.Value.isDefault)
                    {
                        playerSkills.Add(s.Key);
                    }
                }
            }
            private void updateXPMulti()
            {
                if (HasSkill("xpboost.lv4"))
                {
                    xpMulti = 1.4f;
                }
                else if (HasSkill("xpboost.lv3"))
                {
                    xpMulti = 1.3f;
                }
                else if (HasSkill("xpboost.lv2"))
                {
                    xpMulti = 1.2f;
                }
                else if (HasSkill("xpboost.lv1"))
                {
                    xpMulti = 1.12f;
                }
                else if (HasSkill("xpboost.lv0"))
                {
                    xpMulti = 1.05f;
                }
                else
                {
                    xpMulti = 1.0f;
                }
            }

            private void updateSkillPoints()
            {
                int skillPoints =  level * Cfg.Settings.SPPerLevel;
                skillPoints += Cfg.Settings.minSP; //give everyone a small boost
                maxSkillPoints = skillPoints + (prestigeLevel * Cfg.Settings.skillPointsPerPrestige) + apocalypseBaseSP;
            }
            
            private void inform(string msg)
            {
                if (bPlayer != null)
                {
                    Plugin.SendReply(bPlayer, "<color=green>ZN-XP:</color> "+msg);
                    Plugin.InfoAPI?.Call("ShowInfoPopup", bPlayer, msg);
                }

            }

            private void checkApocalypse()
            {
                if(DateTime.Now < Cfg.Settings.nextApocalypse || lastApocalypse >= Cfg.Settings.nextApocalypse)
                {
                    //Plugin.Puts("DEBUG: Apocalypse is in the future");
                }
                else
                {
                    Plugin.Puts("DEBUG: Run --- Apocalypse --- for " + bPlayer);
                    runApocalypse();
                }
            }

            private void runApocalypse()
            {
                apocalypseCounter++;
                lastApocalypse = Cfg.Settings.nextApocalypse;
                lastApocalypseDisplayLevel = displayLevel;
                Reskill();
                // higher prestige Players get harder reset
                if(prestigeLevel > 0)
                {
                    apocalypseBaseSP = prestigeLevel * Cfg.Settings.apocalypseSPPerPrestige;
                    prestigeLevel = 1;
                    xpPoints = 0;
                    LevelUp(0);
                }
                else
                {
                    // adjust vor XP changes in restructure
                    xpPoints = Plugin.getExpATLevel(displayLevel);
                }
                SaveData();
                Plugin.reloadLiveUI(bPlayer);
            }
            #region title formatting
            private string fN(string s)
            {
                return "<color=" + cGrey + ">" + s + "</color>";
            }
            public string fG(string s)
            {
                return "<color=" + cGold + ">" + s + "</color>";
            }
            public void updateFormatString()
            {
                formatedDisplayLevel = generateFormatString(displayLevel, prestigeLevel);
            }
            private string generateFormatString(int lvl, int prestige)
            {
                string flevel = " "+ lvl + " ";

                string openBracket = "{";
                string closeBracket = "}";
                if(prestige > 20)
                {
                    //gngnn g nngng
                    flevel = fG("<<<") + fG(flevel) + fG(">>>");
                    return flevel;
                }
                switch (prestige)
                {
                    case 0:
                        //n n n
                        flevel = fN(openBracket + flevel + closeBracket);
                        break;
                    case 1:
                        //g g g
                        flevel = fG(openBracket + flevel + closeBracket);
                        break;
                    case 2:
                        //gn g ng
                        flevel = fG(openBracket) + fN(openBracket) + fG(flevel) + fN(closeBracket) + fG(closeBracket);
                        break;
                    case 3:
                        //gg g gg
                        flevel = fG(openBracket + openBracket + flevel + closeBracket + closeBracket);
                        break;
                    case 4:
                        //gnn g nng
                        flevel = fG(openBracket) + fN(openBracket + openBracket) + fG(flevel) + fN(closeBracket + closeBracket) + fG(closeBracket);
                        break;
                    case 5:
                        //gng g gng
                        flevel = fG(openBracket) + fN(openBracket) + fG(openBracket + flevel + closeBracket) + fN(closeBracket ) + fG(closeBracket);
                        break;
                    case 6:
                        //ggn g ngg
                        flevel = fG(openBracket + openBracket) + fN(openBracket) + fG(flevel) + fN(closeBracket ) + fG(closeBracket + closeBracket);
                        break;
                    case 7:
                        //ggg g ggg
                        flevel = fG(openBracket + openBracket + openBracket + flevel + closeBracket + closeBracket + closeBracket);
                        break;
                    case 8:
                        //gnnn g nnng
                        flevel = fG(openBracket) + fN(openBracket + openBracket + openBracket) + fG(flevel) + fN(closeBracket + closeBracket + closeBracket) + fG(closeBracket);
                        break;
                    case 9:
                        //gnng g gnng
                        flevel = fG(openBracket) + fN(openBracket + openBracket) + fG(openBracket + flevel+ closeBracket) + fN(closeBracket  + closeBracket) + fG(closeBracket);
                        break;
                    case 10:
                        //gngn g ngng
                        flevel = fG(openBracket) + fN(openBracket) + fG(openBracket) + fN(openBracket) + fG(flevel) + fN(closeBracket) + fG(closeBracket) + fN(closeBracket) + fG(closeBracket);
                        break;
                    case 11:
                        //gngg g ggng
                        flevel = fG(openBracket) + fN(openBracket) + fG(openBracket + openBracket + flevel + closeBracket + closeBracket) + fN(closeBracket) + fG(closeBracket);
                        break;
                    case 12:
                        //ggnn g nngg
                        flevel = fG(openBracket + openBracket) + fN(openBracket + openBracket) + fG(flevel) + fN(closeBracket + closeBracket) + fG(closeBracket + closeBracket);
                        break;
                    case 13:
                        //ggng g gngg
                        flevel = fG(openBracket + openBracket) + fN(openBracket) + fG(openBracket + flevel+ closeBracket) + fN(closeBracket ) + fG(closeBracket + closeBracket);
                        break;
                    case 14:
                        //gggn g nggg
                        flevel = fG(openBracket + openBracket + openBracket) + fN(openBracket) + fG(flevel) + fN(closeBracket) + fG(closeBracket + closeBracket + closeBracket);
                        break;
                    case 15:
                        //gggg g gggg
                        flevel = fG(openBracket + openBracket + openBracket + openBracket + flevel + closeBracket + closeBracket + closeBracket + closeBracket);
                        break;
                    case 16:
                        //gnnnn g nnnng
                        flevel = fG(openBracket) + fN(openBracket + openBracket + openBracket + openBracket) + fG(flevel) + fN(closeBracket + closeBracket + closeBracket + closeBracket) + fG(closeBracket);
                        break;
                    case 17:
                        //gnnng g gnnng                        
                        flevel = fG(openBracket) + fN(openBracket + openBracket + openBracket) + fG(openBracket) + fG(flevel) + fG(closeBracket) + fN(closeBracket + closeBracket + closeBracket) + fG(closeBracket);
                        break;
                    case 18:
                        //gnngn g ngnng
                        flevel = fG(openBracket) + fN(openBracket + openBracket) + fG(openBracket) + fN(openBracket) + fG(flevel) + fN(closeBracket) + fG(closeBracket) + fN(closeBracket + closeBracket) + fG(closeBracket);
                        break;
                    case 19:
                        //gnngg g ggnng
                        flevel = fG(openBracket) + fN(openBracket + openBracket) + fG(openBracket + openBracket) + fG(flevel) +  fG(closeBracket + closeBracket) + fN(closeBracket + closeBracket) + fG(closeBracket);
                        break;
                    case 20:
                        //gngnn g nngng
                        flevel = fG("<<<") + fG(flevel) + fG(">>>");
                        break;
                    default:
                        flevel = displayLevel.ToString();
                        break;

                }

                return flevel;
            }
            public string getPlayerName()
            {
                if(bPlayer != null)
                {
                    return bPlayer.displayName;
                }
                return "E:unknown";
            }
            public string getFormatFor(int lvl, int prestige)
            {
                return generateFormatString(lvl, prestige);
            }
            #endregion
            #region leveling
            private void LevelUp(int newLevel)
            {
                level = newLevel;
                displayLevel = prestigeLevel * 100 + level;
                updateFormatString();
                updateSkillPoints();
                inform("You LEVELED UP to " + formatedDisplayLevel + "!");
                SaveData();
                Plugin.Puts("INFO: "+bPlayer+" leveled up to " + displayLevel);
            }
            private void ClearSkills()
            {
                playerSkills.Clear();
                allocatedSkillPoints = 0;
                loadDefaultSkills();
                SyncSkillPermissions();
                updateXPMulti();
                Plugin.notifyRelatedPlugins(bPlayer);
            }
            private void ClearUnlocks()
            {
                prestigeUnlocks.Clear();
            }
            #endregion
            #region SkillManagement
            private void SyncSkillPermissions()
            {
                foreach(string perm in Plugin._tree.permissions)
                {
                    Plugin.permission.RevokeUserPermission(playerID.ToString(), perm);
                }
                foreach (string sID in playerSkills)
                {
                    addPermForSkill(sID);
                }
            }

            private void addPermForSkill(string skillID)
            {
                if (!Plugin.storedData.Skills.ContainsKey(skillID)) return;
                ZNSkill s = Plugin.storedData.Skills[skillID];
                foreach (string perm in s.permissionEffect)
                {
                    Plugin.permission.GrantUserPermission(playerID.ToString(), perm, null);
                }
            }

            #endregion
            #region API

            public void Demote(BasePlayer admin, int levels = 1)
            {
                if (!admin.IsAdmin) return;

                level = level - levels;
                displayLevel = prestigeLevel * 100 + level;
                xpPoints = Plugin.xpAtLevel[displayLevel] + 1;
                if (xpPoints < 0) xpPoints = 0;
                updateFormatString();
                updateSkillPoints();
                ClearSkills();
                inform("You got DEMOTED to " + formatedDisplayLevel + " by " + admin.displayName + "!\nSkills have been reset!");
                SaveData();
                Plugin.Puts("INFO: " + admin.displayName + " demoted " + bPlayer + " down to " + displayLevel);
            }
            public bool HasSkill(string skillId)
            {
                return playerSkills.Contains(skillId);
            }
            public void AddExperience(int points)
            {
                eternalXpPoints += points;
                if (level >= Cfg.Settings.levelCap)
                {
                    level = Cfg.Settings.levelCap;
                    return;
                }

                xpPoints += points;
                int newLevel = level;
                for (int i = level; i <= Cfg.Settings.levelCap; i++)
                {
                    int lvl = (prestigeLevel * 100) + (i + 1);
                    if(i+1 > Cfg.Settings.levelCap)
                    {
                        newLevel = i;
                        break;
                    }
                    //Plugin.Puts("DEBUG: add " + points + " to " +xpPoints + " level check " + lvl + " at lvl " + Plugin.xpAtLevel[lvl]);
                    if (xpPoints >= Plugin.xpAtLevel[lvl]) 
                    { 
                        continue; 
                    }
                    newLevel = i;
                    break;
                }
                //int newLevel = (int)Math.Floor((double)(xpPoints / Cfg.Settings.expPerLevel));
                if (newLevel >= Cfg.Settings.levelCap)
                {
                    newLevel = Cfg.Settings.levelCap;
                }
                // handle levelUp
                if (newLevel > level)
                {
                    LevelUp(newLevel);
                }
                Plugin.reloadLiveUI(bPlayer);
            }
            public ZNResponse AddSkill(ZNSkill skill)
            {
                if (playerSkills.Contains(skill.id))
                {
                    return new ZNResponse(false, "You already have the skill: " + skill.id);
                }
                if (skill.prerequisiteSkillId != null && !playerSkills.Contains(skill.prerequisiteSkillId))
                {
                    return new ZNResponse(false, "You are missing skill: " + skill.prerequisiteSkillId);
                }
                updateSkillPoints();
                if (GetSkillPoints() < skill.spCost)
                {
                    return new ZNResponse(false, "You do not have enough skill points (" + skill.spCost+") left");
                }

                foreach (string perm in skill.permissionEffect)
                {
                    Plugin.permission.GrantUserPermission(playerID.ToString(), perm, null);
                }
                playerSkills.Add(skill.id);
                allocatedSkillPoints += skill.spCost;
                addPermForSkill(skill.id);
                updateXPMulti();
                Plugin.notifyRelatedPlugins(bPlayer);
                SaveData();
                return new ZNResponse(true, "Successfully unlocked skill: " + skill.name);
            }

            public int GetSkillPoints()
            {
                return maxSkillPoints - allocatedSkillPoints;
            }
            public void Prestige()
            {
                if (level != Cfg.Settings.levelCap) return;
                prestigeLevel = prestigeLevel + 1;
                xpPoints = 0;
                ClearSkills();
                LevelUp(0);
                SaveData();
                string msg = "<color=green>ZN-XP:</color> <color=orange>" + bPlayer.displayName + "</color> reached Prestige Level " + formatedDisplayLevel;
                Plugin.Server.Broadcast(msg);
                Plugin.reloadLiveUI(bPlayer);
            }
            public bool CanReskill()
            {
                return allocatedSkillPoints != 0 || prestigeUnlocks.Count() != 0;
            }
            public void Reskill()
            {
                ClearSkills();
                ClearUnlocks();
                SaveData();
            }
            public ZNResponse PUnlock(string prestigeSkillGroup)
            {
                Plugin.Puts("INFO: "+bPlayer+" prestige unlocked " + prestigeSkillGroup);
                if (prestigeLevel <= prestigeUnlocks.Count())
                {
                    return new ZNResponse(false, "You don't have enough prestige unlocks.");
                }
                if (prestigeSkillGroup == null)
                {
                    return new ZNResponse(false, "Invalid Unlock Tree.");
                }
                prestigeUnlocks.Add(prestigeSkillGroup);
                SaveData();
                return new ZNResponse(true, prestigeSkillGroup + " Tree has been unlocked!");
            }
            #endregion

        }

        
        #endregion

        #region GUI
        private HashSet<BasePlayer> LiveUiPlayers = new HashSet<BasePlayer>();
        private void reloadLiveUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (player == null || !player.IsConnected || !player.IsValid()) return;
            if (storedData.noUIPlayers.Contains(player.userID)) return;
            if (!LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Add(player);
            }
            CuiHelper.DestroyUi(player, mainName + "_live");
            CuiHelper.DestroyUi(player, mainName + "_live_bar");
            displayLiveUI(player, errorMsg);
        }
        private void killLiveUI(BasePlayer player)
        {
            if (LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Remove(player);
                CuiHelper.DestroyUi(player, mainName + "_live");
                CuiHelper.DestroyUi(player, mainName + "_live_bar");
            }
        }

        private void displayLiveUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            GUILiveElement(player, mainName + "_live", errorMsg);
        }

        private void GUILiveElement(BasePlayer player, string name, string errorMsg = "none")
        {
            if (!player.IsValid() || !player.IsConnected) return;
            if (!_pm.ContainsKey(player.userID)) return;

            var mainName = name;
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.0",
                   // Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin =  "1 0" ,
                    AnchorMax = "1 0",
                    OffsetMin = "-450 15",
                    OffsetMax = "-215 80"
                },
                CursorEnabled = false
            }, "Hud", mainName);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ZN Profile (/p)",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "0 0 0 1"
                },
                RectTransform =
                {
                    AnchorMin = 0.05f + " 0.052",
                    AnchorMax = 0.973f + " 0.945"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ZN Profile (/p)",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = 0.05f + " 0.05",
                    AnchorMax = 0.97f + " 0.95"
                }
            }, mainName);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "type /p to open your profile and skills.",
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0 0 0 1"
                },
                RectTransform =
                {
                    AnchorMin = 0.05f + " 0.23",
                    AnchorMax = 0.973f + " 0.95"
                }
            }, mainName);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "type <color=yellow>/p</color> to open your profile and skills.",
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = 0.05f + " 0.25",
                    AnchorMax = 0.97f + " 0.95"
                }
            }, mainName);

            ProfileManager p = _pm[player.userID];
            if (p == null) return;
            string xpString = "Level-Up XP: "+ (p.xpPoints - getExpATLevel(p.displayLevel)) + "/"+getExpForLevel(p.displayLevel+1) + " ("+ getPercentToNextLevel(p.displayLevel, p.xpPoints).ToString("0.00") + "%)";
            float levelProgress = getPercentToNextLevel(p.displayLevel, p.xpPoints);
            int cols = 3;
            float colWidth = (1f / cols) - 0.01f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Level: " + p.formatedDisplayLevel,
                    FontSize = 12,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = 0.05f + " 0.05",
                    AnchorMax = 0.97f + " 0.95"
                }
            }, mainName);


            //XP Bar
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.35"
                },
                CursorEnabled = false
            }, mainName, mainName + "_bar");
            if (p.level < Cfg.Settings.levelCap)
            {


                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.5 0.8 0.0 0.8",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.055",
                        AnchorMax = (levelProgress/100) + " 0.9"
                    },
                    CursorEnabled = false
                }, mainName + "_bar", mainName + "_bar_fill");

                

                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = xpString,
                    FontSize = 9,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
                }, mainName + "_bar");


            }
            else
            {
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "max level -> prestige to gain more",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 0 0 1"
                },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                },  mainName + "_bar");

            }





            CuiHelper.AddUi(player, elements);
        }


        private void reloadUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (!player.IsValid()) return;
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
            if (_pm.ContainsKey(player.userID))
            {
                ProfileManager pm = _pm[player.userID];
                pm.updateFormatString();
                //GUIBackgroundElement(player, mainName + "_bg");
                GUILeftPlayerElement(player, mainName + "_playerBot", pm);
                GUIFunctionsElement(player, mainName + "_function", pm);
                GUIinfoElement(player, mainName + "_info");
                GUIHeaderElement(player, mainName + "_head", pm, errorMsg);
                GUIFooterElement(player, mainName + "_foot", pm);
            }
        }

        private const string globalNoErrorString = "none";
        private const string mainName = "ZNExperience";
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements =
        {
            mainName+"_bg",
            mainName+"_head",
            mainName+"_foot",
            mainName + "_player",
            mainName + "_playerBot",
            mainName + "_function",
            mainName + "_skillDetail",
            mainName + "_info",
            mainName + "_confirm",
        };
        private float globalLeftBoundary = 0.0f;
        private float globalRighttBoundary = 1f;
        private float globalTopBoundary = 1f;
        private float globalBottomBoundary = 0f;
        private float globalSpace = 0.01f;
        private float eHeadHeight = 0.04f;
        private float eFootHeight = 0.14f;
        private float eplayerWidth = 0.34f;
        private float efunctionWidth = 0.305f;
        private int baseFontSize = 12;

        private void GUIBackgroundElement(BasePlayer player, string elUiId)
        {
            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 1",
                },
                RectTransform =
                {
                    AnchorMin = "0 0 ",
                    AnchorMax = "1 1"
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            CuiHelper.AddUi(player, elements);
        }
        private void GUIHeaderElement(BasePlayer player, string elUiId, ProfileManager pm, string errorMsg = globalNoErrorString)
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

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ZN Profile (/p)",
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
                    Text = "Zombie Nation Experience Manager v" + Plugin.Version + " by " + Plugin.Author,
                    FontSize = 10,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.205 0.005",
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
            /* DEBUG button
            elements.Add(new CuiButton
            {
                Button =
                    {
                        Command = "znexp.testreset",
                        Color = "0.8 0.8 0.8 0.2"
                    },
                RectTransform =
                    {
                        AnchorMin = 0.45f +" "+ 0.05f,
                        AnchorMax = 0.55f +" "+ 0.95f
                    },
                Text =
                    {
                        Text = "TEST reset",
                        FontSize = baseFontSize-2,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
            }, elUiId);
            */

            CuiHelper.AddUi(player, elements);
        }
        private void GUIFooterElement(BasePlayer player, string elUiId, ProfileManager pm)
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
                    AnchorMin = string.Format("{0} {1}", 0.8f, 0.9f),
                    AnchorMax = string.Format("{0} {1}", 1f, 1f-eHeadHeight - globalSpace)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "znexp.close",
                    Color = "0.56 0.12 0.12 1"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Text = "☒ CLOSE ",
                    FontSize = (baseFontSize + 8),
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(closeButton, elUiId);



            CuiHelper.AddUi(player, elements);
        }

        private void GUILeftPlayerElement(BasePlayer player, string elUiId, ProfileManager pm)
        {

            var elements = new CuiElementContainer();

            float topBoundary = 1f - eHeadHeight;
            float botBoundary = 0f;
            float leftBoundary = globalLeftBoundary;
            float rightBoundary = 0.2f;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.14 0.13 0.11 1",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = string.Format("{0} {1}", leftBoundary, botBoundary),
                    AnchorMax = string.Format("{0} {1}", rightBoundary, topBoundary)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            float localLeftBoundary = 0.05f;
            float localRightBoundary = 0.95f;
            float localContentStart = 0.98f;



            float levelProgress = getPercentToNextLevel(pm.displayLevel, pm.xpPoints);
            int playerRP = 0;
            object bal = ServerRewards?.Call("CheckPoints", player.userID);
            if (bal != null) playerRP = (int)bal;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = pm.fG(player.displayName),
                    FontSize = baseFontSize+8,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.05f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);


            string title = ZNTitleManager?.Call<string>("GetPlayerTitle", player.userID, false);
            if(title != null && title != string.Empty)
            {
                localContentStart -= 0.03f;
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "--- " + title + " ---",
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.05f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
                }, elUiId);

            }
               
            localContentStart -= 0.05f;
            elements.Add(new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = elUiId,
                Components =
                {
                    new CuiRawImageComponent {Png = GetImage(player.UserIDString) },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.35 " + (localContentStart - 0.1f),
                        AnchorMax = "0.65 " + (localContentStart)
                    }
                }
            });

            localContentStart -= 0.12f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Level:",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
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
                    Text =  pm.formatedDisplayLevel,
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 0.9"
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
                        Text = "Prestige:",
                        FontSize = baseFontSize-2,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 0.9"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" "  + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = pm.prestigeLevel.ToString(),
                        FontSize = baseFontSize-2,
                        Align = TextAnchor.UpperRight,
                        Color = "1 1 1 0.9"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" "  + localContentStart
                    }
            }, elUiId);
            localContentStart -= 0.04f;

            //XP Bar
            string xpString = "LevelUp XP: " + numberCandy(pm.xpPoints - getExpATLevel(pm.displayLevel)) + "/" + numberCandy(getExpForLevel(pm.displayLevel + 1)) + " (" + getPercentToNextLevel(pm.displayLevel, pm.xpPoints).ToString("0.00") + "%)";
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Your Experience",
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
            if (pm.level < Cfg.Settings.levelCap)
            {
                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0.8",
                    },
                    RectTransform =
                    {
                        AnchorMin = localLeftBoundary +" " + (localContentStart - 0.05f),
                        AnchorMax = localRightBoundary +" " + localContentStart
                    },
                    CursorEnabled = false
                }, elUiId, elUiId + "_bar");


                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.5 0.8 0.0 0.8",
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.1",
                        AnchorMax = (levelProgress/100) + " 0.9"
                    },
                    CursorEnabled = false
                }, elUiId + "_bar", elUiId + "_bar_fill");



                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = xpString,
                    FontSize = baseFontSize-2,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
                }, elUiId + "_bar");
                localContentStart -= 0.06f;

            }
            else
            {
                //Prestige mode?
                string prestigeString = " !!! Click to PRESTIGE !!!";
                prestigeString += "\n---> reset XP, SP, skills";
                prestigeString += "\n---> gain +1 Level, +" + Cfg.Settings.skillPointsPerPrestige + " base SP";
                prestigeString += "\n---> gain new Levelframe {} & special Skills unlock points";

                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "znexp.prestige",
                        Color = "0.5 0.8 0.0 0.8"
                    },
                    RectTransform =
                    {
                        AnchorMin = localLeftBoundary +" " + (localContentStart - 0.08f),
                        AnchorMax = localRightBoundary +" " + localContentStart
                    },
                    Text =
                    {
                        Text = prestigeString,
                        FontSize = baseFontSize-3,
                        Align = TextAnchor.MiddleLeft,
                        Color = "0.9 0.9 0.9 1"
                    }
                }, elUiId);
                localContentStart -= 0.1f;
            }

            // Player Level and Point Info

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Lifetime XP:",
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = ""+numberCandy(pm.eternalXpPoints),
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.04f;
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "RP/Blood:",
                        FontSize = baseFontSize,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 0.9"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" "  + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = numberCandy(playerRP),
                        FontSize = baseFontSize,
                        Align = TextAnchor.UpperRight,
                        Color = "1 1 1 0.9"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" "  + localContentStart
                    }
            }, elUiId);
            localContentStart -= 0.04f;
            string playtimeInfo = GetPlaytimeFor(player);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = "Playtime:",
                        FontSize = baseFontSize,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 0.9"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" "  + localContentStart
                    }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                    {
                        Text = playtimeInfo,
                        FontSize = baseFontSize,
                        Align = TextAnchor.UpperRight,
                        Color = "1 1 1 0.9"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary+" "  + localContentStart
                    }
            }, elUiId);
            localContentStart -= 0.04f;


            localContentStart -= 0.02f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Your Unlock Points",
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
            string skillString = (pm.maxSkillPoints - pm.allocatedSkillPoints) + "/" + pm.maxSkillPoints;


            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Skill Points (SP):",
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = skillString,
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.04f;

            //Prestige
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Prestige Unlocks:",
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = (pm.prestigeLevel-pm.prestigeUnlocks.Count())+"/"+pm.prestigeLevel,
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.04f;

            // APOCALYPSE UI
            if(pm.apocalypseCounter > 0)
            {
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "Your Last Apocalypse",
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
                    Text = "Level before Apocalypse:",
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
                }, elUiId);
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "" + pm.lastApocalypseDisplayLevel,
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 0.9"
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
                }, elUiId);
                localContentStart -= 0.04f;
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "Base SP from Apocalypse:",
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "1 1 1 0.9"
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
                }, elUiId);
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = "" + pm.apocalypseBaseSP,
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperRight,
                    Color = "1 1 1 0.9"
                },
                    RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
                }, elUiId);
                localContentStart -= 0.04f;
            }
            

            localContentStart -= 0.02f;
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

            int currentPapers = 0;
            var slots = player.inventory.FindItemIDs(PAPERID);
            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }
                currentPapers += slot.amount;
            }
            //int price = (int) Math.Floor((Cfg.Settings.prestigeLevelBuyFactor * pm.prestigeLevel * Cfg.Settings.buyLevelRP) + Cfg.Settings.buyLevelRP);
            
            OptionsButton(elements, elUiId, "znexp.reskill", pm.CanReskill(), "    Reset Skills & Unlocks", "rp_img", Cfg.Settings.reskillRP, localContentStart);
            localContentStart -= 0.05f;
            //OptionsButton(elements, elUiId, "znexp.transferpapers", (currentPapers > 0), "    Transfer Research Paper -> XP (1:1)", "researchpaper", currentPapers, localContentStart);

            //OptionsButton(elements, elUiId, "znexp.noskillbuylevel 1", (playerRP >= price), "    Buy 1 Level", "rp_img", price, localContentStart);
            //localContentStart -= 0.05f;
            //OptionsButton(elements, elUiId, "znexp.noskillbuylevel 10", (playerRP >= Cfg.Settings.buyTenLevelRP), "    Buy 10 Level", "rp_img", Cfg.Settings.buyTenLevelRP, localContentStart);

            CuiHelper.AddUi(player, elements);
        }

        private void GUIFunctionsElement(BasePlayer player, string elUiId, ProfileManager pm)
        {

            var elements = new CuiElementContainer();

            float topBoundary = globalTopBoundary - eHeadHeight;
            float botBoundary = globalBottomBoundary;
            float leftBoundary = 0.2f;
            float rightBoundary = 1f;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.26 0.25 0.23 0.8",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = string.Format("{0} {1}", leftBoundary,botBoundary),
                    AnchorMax = string.Format("{0} {1}", rightBoundary, topBoundary)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);



            float localLeftBoundary = 0.02f;
            float localRightBoundary = 0.98f;
            float localContentStart = 1f;

           

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Your Skills",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.5 0.8 0.0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = (localLeftBoundary) +" "+ (localContentStart-0.06f),
                    AnchorMax = (localRightBoundary) + " "+ localContentStart
                }
            }, elUiId);

            localContentStart -= 0.07f;


            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.26 0.25 0.23 0"
                },
                RectTransform =
                {
                    AnchorMin = string.Format("{0} {1}", 0, botBoundary),
                    AnchorMax = string.Format("{0} {1}", 0.745, localContentStart)
                },
                CursorEnabled = true
            }, elUiId, elUiId+"_skills");

            generateSkillsUI(elements, player, pm, elUiId + "_skills");


            CuiHelper.AddUi(player, elements);
        }

        private void generateSkillsUI(CuiElementContainer elements, BasePlayer player, ProfileManager pm, string parentElId)
        {
            float localLeftBoundary = 0.02f;
            float localRightBoundary = 0.98f;
            float localContentStart = 1f;
            int itemnum = 0;
            float[] dimensions = { localLeftBoundary, localContentStart};

            /*
             * Player Skills that are currently active
             */

            List<string> sortedSkills = sortSkills(pm.playerSkills.ToList());
            foreach (string s in sortedSkills)
            {
                ZNSkill skill;
                if (!storedData.Skills.TryGetValue(s, out skill)) continue;
                if (pm.playerSkills.Contains(skill.followUpSkillId)) continue;
                
                // individual Skill Element
                dimensions = skillUIElement(elements, skill, pm, parentElId, dimensions[0], dimensions[1], true);

                // new row
                if (itemnum == 4)
                {
                    dimensions[0] = localLeftBoundary;
                    itemnum = 0;
                    localContentStart = dimensions[1];
                }
                else
                {
                    dimensions[1] = localContentStart;
                    itemnum++;
                }
            }
            if(itemnum != 0) localContentStart -= 0.09f;

            /*
             * Available skills to unlock
             */

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Available Skills",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.5 0.8 0.0 0.8",
                },
                RectTransform =
                {
                    AnchorMin = (localLeftBoundary) +" "+ (localContentStart-0.08f),
                    AnchorMax = (localRightBoundary) + " "+ localContentStart
                }
            }, parentElId);

            localContentStart -= 0.07f;

            itemnum = 0;
            foreach (ZNSkillGroup sg in storedData.SkillGroups.Values)
            {
                elements.Add(new CuiLabel
                {
                    Text =
                {
                    Text = sg.name,
                    FontSize = baseFontSize,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                    RectTransform =
                {
                    AnchorMin = (localLeftBoundary) +" "+ (localContentStart-0.04f),
                    AnchorMax = (localRightBoundary) + " "+ localContentStart
                }
                }, parentElId);

                localContentStart -= 0.05f;
                itemnum = 0;
                dimensions[0] = localLeftBoundary;
                dimensions[1] = localContentStart;
                sortedSkills = sortSkills(sg.skillIDs);
                foreach(string str in sortedSkills)
                {
                    ZNSkill s;
                    if (!storedData.Skills.TryGetValue(str, out s)) continue;
                    if (pm.playerSkills.Contains(s.id) || (s.prerequisiteSkillId != null && !pm.playerSkills.Contains(s.prerequisiteSkillId))) continue;
                    

                    // individual Skill Element
                    dimensions = skillUIElement(elements, s, pm, parentElId, dimensions[0], dimensions[1], pm.playerSkills.Contains(s.id));
                   
                    // new row
                    if(itemnum == 4)
                    {
                        dimensions[0] = localLeftBoundary;
                        itemnum = 0;
                        localContentStart = dimensions[1];
                    }
                    else
                    {
                        dimensions[1] = localContentStart;
                        itemnum++;
                    }
                }
                localContentStart -= 0.09f;
            }
        }

        private float[] skillUIElement(CuiElementContainer elements, ZNSkill s, ProfileManager pm, string parentElId, float left, float top, bool active = false, string currentSkillId = "")
        {
            string activeColor = "0.5 0.8 0.0 1";
            string defautColor = "0.8 0.8 0.8 1";
            string currentColor = "0.7 0.38 0 1";
            int rowitems = (currentSkillId != "") ? 1 : 5;
            float elwidth = ((1f / rowitems) - 0.03f);
            float newLeft = left + elwidth;
            float elheight = 0.04f;
            float newTop = top - elheight;
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5",
                    //Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                        AnchorMin = left +" "+ (newTop),
                        AnchorMax = newLeft + " "+ top
                },
                CursorEnabled = true
            }, parentElId, parentElId+"_itm_"+s.id);

            string label = s.name;
            if (active)
            {
                label += " ✓";
            }
            else
            {
                label += " <color=yellow>[" + s.spCost + "SP]</color>";
                if (s.prestigeUnlockId != null && !(pm.prestigeUnlocks.Contains(s.prestigeUnlockId))){
                    label += " (PRESTIGE)";
                }
            }

            elements.Add(new CuiButton
            {
                Button =
                    {
                        Command = "znexp.skilldetail " + s.id,
                        Color = "0 0 0 0.8"
                    },
                RectTransform =
                    {
                        AnchorMin = left +" "+ (newTop),
                        AnchorMax = newLeft + " "+ top
                    },
                Text =
                    {
                        Text = label,
                        FontSize = baseFontSize-2,
                        Align = TextAnchor.MiddleCenter,
                        Color = (s.id == currentSkillId) ? currentColor : (active) ? activeColor : defautColor
                    },
            }, parentElId);

            float[] ret = { newLeft+0.02f, newTop -0.02f };
            return ret;
        }



        private void GUIskillDetailElement(BasePlayer player, string elUiId, ProfileManager pm, ZNSkill s)
        {
            var elements = new CuiElementContainer();

            float topBoundary = 0.89f;
            float botBoundary = 0f;
            float leftBoundary = 0.8f;
            float rightBoundary = 1f;
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.14 0.13 0.11 1",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = string.Format("{0} {1}", leftBoundary, botBoundary),
                    AnchorMax = string.Format("{0} {1}", rightBoundary, topBoundary)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            float localTop = 0.98f;
            float lineHeight = 0.07f;
            float localContentStart = 0.98f;
            float localLeftBoundary = 0.05f;
            float localRightBoundary = 0.95f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text =  "" + s.name,
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.7 0.38 0 1",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.05f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.05f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Description",
                    FontSize = baseFontSize+4,
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
                        Text = ""+s.description,
                        FontSize = baseFontSize,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                RectTransform =
                    {
                        AnchorMin = localLeftBoundary + " " + (localContentStart-0.1f),
                        AnchorMax = localRightBoundary+" " + localContentStart
                    }
            }, elUiId);
            localContentStart -= 0.1f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Skill Tree",
                    FontSize = baseFontSize+4,
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
            if (_tree.tree.ContainsKey(s.getBaseId()))
            {
                foreach (ZNSkill ts in _tree.tree[s.getBaseId()])
                {
                    skillUIElement(elements, ts, pm, elUiId, 0.03f, localContentStart, pm.playerSkills.Contains(ts.id), s.id);
                    localContentStart -= 0.05f;
                }

            }


            /*
             * Funcional Buttons
             */
            string buttonMin = localLeftBoundary + " 0.01";
            string buttonMax = "0.8 0.15";
            if (pm.playerSkills.Contains(s.id))
            {
                elements.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = "ACTIVE",
                            FontSize = baseFontSize-4,
                            Align = TextAnchor.MiddleCenter,
                            Color = "0.5 0.8 0.0 1"
                        },
                    RectTransform =
                        {
                            AnchorMin = buttonMin,
                            AnchorMax = buttonMax
                        }
                }, elUiId);
                localTop = localTop - lineHeight - 0.05f;

            }
            else
            {
                if(pm.GetSkillPoints() >= s.spCost 
                    && (s.prerequisiteSkillId == null || (s.prerequisiteSkillId != null && pm.playerSkills.Contains(s.prerequisiteSkillId)))
                    && (s.prestigeUnlockId == null || (s.prestigeUnlockId != null && pm.prestigeUnlocks.Contains(s.prestigeUnlockId))))
                {
                    elements.Add(new CuiButton
                    {
                        Button =
                            {
                                Command = "znexp.addSkill " + s.id,
                                Color = "0.7 0.38 0 1"
                            },
                        RectTransform =
                            {
                                AnchorMin = buttonMin,
                                AnchorMax = buttonMax
                            },
                        Text =
                            {
                                Text = "Unlock Skill ("+s.spCost+"SP)",
                                FontSize = baseFontSize-4,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                    }, elUiId);
                }
                else if (pm.playerSkills.Contains(s.prerequisiteSkillId)
                    && (s.prestigeUnlockId != null && !(pm.prestigeUnlocks.Contains(s.prestigeUnlockId)))
                    && pm.prestigeUnlocks.Count < pm.prestigeLevel)
                {
                    elements.Add(new CuiButton
                    {
                        Button =
                            {
                                Command = "znexp.prestigeunlock " + s.id,
                                Color = "0.7 0.38 0 1"
                            },
                        RectTransform =
                            {
                                AnchorMin = buttonMin,
                                AnchorMax = buttonMax
                            },
                        Text =
                            {
                                Text = "Prestige unlock this Tree",
                                FontSize = baseFontSize-4,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            }
                    }, elUiId);
                }
                else 
                {
                    string errMsg = "Can't afford (" + s.spCost + "SP)";
                    if (s.prestigeUnlockId != null && !(pm.prestigeUnlocks.Contains(s.prestigeUnlockId)))
                    {
                        errMsg = "Needs Prestige unlock";
                    }
                    if (s.prerequisiteSkillId != null && !pm.playerSkills.Contains(s.prerequisiteSkillId))
                    { 
                        errMsg = "Missing prerequisite Skill " + skillNameFromId(s.prerequisiteSkillId); 
                    }
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = errMsg,
                            FontSize = baseFontSize-4,
                            Align = TextAnchor.MiddleCenter,
                            Color = "0.8 0.0 0.0 1"
                        },
                        RectTransform =
                        {
                                AnchorMin = buttonMin,
                                AnchorMax = buttonMax
                        }
                    }, elUiId);
                    localTop = localTop - lineHeight - 0.05f;
                }
                
                
            }

            buttonMin = "0.85 0.01";
            buttonMax = "0.95 0.15";
            elements.Add(new CuiButton
            {
                Button =
                    {
                        Command = "znexp.closedetail",
                        Color = "0.56 0.12 0.12 1"
                    },
                RectTransform =
                    {
                        AnchorMin = buttonMin,
                        AnchorMax = buttonMax
                    },
                Text =
                    {
                        Text = "hide",
                        FontSize = 8,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
            }, elUiId);

            CuiHelper.AddUi(player, elements);
        }

        private void GUIinfoElement(BasePlayer player, string elUiId)
        {
            var elements = new CuiElementContainer();

            float topBoundary = 0.89f;
            float botBoundary = 0f;
            float leftBoundary = 0.8f;
            float rightBoundary = 1f;
            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.14 0.13 0.11 1",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = string.Format("{0} {1}", leftBoundary, botBoundary),
                    AnchorMax = string.Format("{0} {1}", rightBoundary, topBoundary)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            float localContentStart = 0.98f;
            float localLeftBoundary = 0.05f;
            float localRightBoundary = 0.95f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Info",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.8 0.8 0.8 0.8",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.05f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.02f;
            string infoStr = "";
            infoStr += "\n- Levels, XP & Skills don't wipe!";
            infoStr += "\n- Breaking rules can result in level loss!";
            infoStr += "\n- Max Level = x99";
            infoStr += "\n- Prestige = unlimited";
            infoStr += "\n- Levels give you + 1 SP";
            infoStr += "\n\n- After max lvl you can prestige";

            infoStr += "\n\n<b>Prestige Reset:</b>";
            infoStr += "\n- Resets your LevelUp XP to 0";
            infoStr += "\n- Resets your Skill Points to "+Cfg.Settings.minSP+" + Bonus";
            infoStr += "\n- Resets your Skills";
            infoStr += "\n- Does NOT reset Lifetime XP";
            infoStr += "\n- Does NOT reset Prestige Unlocks";
            infoStr += "\n\n<b>Prestige Bonus:</b>";
            infoStr += "\n- 1 Level up";
            infoStr += "\n- "+Cfg.Settings.skillPointsPerPrestige+" extra starting SP per Prestige";
            infoStr += "\n- 1 Prestige Unlock Point";
            infoStr += "\n- New Rank Frame {{ lvl }}";
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = infoStr ,
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.6 0.6 0.6 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.5f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.47f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "<color=green>Usage</color>",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.8 0.8 0.8 0.8",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.05f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.05f;
            string usageStr = "";
            usageStr += "- Select one of the 'Available Skills'";
            usageStr += "\n- 'Unlock' it with SP";
            usageStr += "\n- You must have all prior skills in a tree";
            usageStr += "\n- (Prestige) Skills need to be Unlocked";
            usageStr += "\n- 'Prestige Unlock' unlocks ONE entire tree";
            usageStr += "\n\n- Buying a level is "+Cfg.Settings.prestigeLevelBuyFactor*Cfg.Settings.buyLevelRP+" more expensive for each Prestige";

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = usageStr,
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.6 0.6 0.6 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.4f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);

            localContentStart -= 0.2f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "<color=green>Apocalypse</color>",
                    FontSize = baseFontSize+7,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.8 0.8 0.8 0.8",
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.05f),
                    AnchorMax = localRightBoundary+" " + localContentStart
                }
            }, elUiId);
            localContentStart -= 0.05f;
            string apoStr = "";
            apoStr += "- Apocalypse is a rare (yearly) event";
            apoStr += "\n- It brings lots of changes and potential resets";
            apoStr += "\n- Level < 100 were not changed";
            apoStr += "\n- Level > 100 were reset to 100 & gained +1 base SP per Prestige";

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = apoStr,
                    FontSize = baseFontSize,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.6 0.6 0.6 0.9"
                },
                RectTransform =
                {
                    AnchorMin = localLeftBoundary + " " + (localContentStart-0.4f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                }
            }, elUiId);


            CuiHelper.AddUi(player, elements);
        }

        private void OptionsButton(CuiElementContainer elements, string elUiId, string command, bool onOff, string text, string iconname, int price, float localContentStart)
        {
            float optIcon = 0.1f;
            float localLeftBoundary = 0.05f;
            float localRightBoundary = 0.95f;

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = command,
                    Color = (onOff) ? "0.7 0.38 0 1" : "0.3 0.3 0.3 0.3"
                },
                RectTransform =
                {
                    AnchorMin = (localLeftBoundary) + " " + (localContentStart-0.04f),
                    AnchorMax = localRightBoundary+" "  + localContentStart
                },
                Text =
                {
                    Text = text,
                    FontSize = baseFontSize-4,
                    Align = TextAnchor.MiddleLeft,
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
                        AnchorMin = (localRightBoundary - optIcon) + " " + (localContentStart-0.04f),
                        AnchorMax = localRightBoundary +" " + (localContentStart-0.01f)
                    }
                }
            });
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "x"+numberCandy(price)+"  ",
                    FontSize = baseFontSize-2,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1",
                },
                RectTransform =
                {
                    AnchorMin = (localRightBoundary - 2.5f*optIcon) + " " + (localContentStart-0.04f),
                    AnchorMax = (localRightBoundary - optIcon) +" " + (localContentStart)
                }
            }, elUiId);

        }


        private void GUIconfirmElement(BasePlayer player, string elUiId)
        {
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.9",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = string.Format("{0} {1}", 0.1f, 0.4f),
                    AnchorMax = string.Format("{0} {1}", 0.9f, 0.6f)
                },
                CursorEnabled = true
            }, "Overlay", elUiId);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Do you really want to pay " + numberCandy(Cfg.Settings.reskillRP) + "RP for a full skill reset?",
                    FontSize = baseFontSize+15,
                    Align = TextAnchor.UpperCenter,
                    Color = "1 1 1 1",
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 0.9"
                }
            }, elUiId);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "znexp.closeconfirm",
                    Color = "0.3 0.3 0.3 0.3"
                },
                RectTransform =
                {
                    AnchorMin = "0.1 0.1",
                    AnchorMax = "0.48 0.45"
                },
                Text =
                {
                    Text = "Cancel",
                    FontSize = (baseFontSize + 8),
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(closeButton, elUiId);
            var confirmButton = new CuiButton
            {
                Button =
                {
                    Command = "znexp.confirm",
                    Color = "0.7 0.38 0 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.52 0.1",
                    AnchorMax = "0.9 0.45"
                },
                Text =
                {
                    Text = "Accept",
                    FontSize = (baseFontSize + 8),
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                }
            };
            elements.Add(confirmButton, elUiId);


            CuiHelper.AddUi(player, elements);
        }
        #endregion

    }
}   
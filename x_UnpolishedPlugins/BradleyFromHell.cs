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
// Requires: PathFinding

using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using UnityEngine.AI;
using Oxide.Game.Rust.Cui;
using Random = UnityEngine.Random;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("BradleyFromHell", "DocValerian", "1.6.1")]
    class BradleyFromHell : RustPlugin
    {
        static BradleyFromHell Plugin { get; set; }

        [PluginReference] Plugin ServerRewards, PathFinding, Clans, LootBoxSpawner, ZNui, ZNTitleManager, ZNExperience;
        private const string permUse = "bradleyfromhell.use";
        private const string permConsole = "bradleyfromhell.ticket";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region Config
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
        static ConfigFile Cfg = new ConfigFile();
        class ConfigFile
        {
            public int BradleySpawnRadius = 30;
            public int BradleyQueueSeconds = 120;
            public float SoloDamageReduction = 0.2f;
            public float TeamHealthPercentPerPlayer = 0.2f;
            public float TeamRewartPercentPerPlayer = 0.05f;
            public Dictionary<string, Dictionary<string, float>> BradleyLevels = new Dictionary<string, Dictionary<string, float>>
            {
                ["normal"] = new Dictionary<string, float>
                {
                    ["bradleyHealth"] = 1500f,
                    ["damageMultiplyer"] = 1.0f,
                    ["rewardPoints"] = 500f,
                    ["rewardPointsHighscore"] = 1000f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 2f,
                    ["maxRewardMultiplyer"] = 5f,
                    ["experience"] = 500f,
                },
                ["noloot_normal"] = new Dictionary<string, float>
                {
                    ["bradleyHealth"] = 1500f,
                    ["damageMultiplyer"] = 1.0f,
                    ["rewardPoints"] = 500f,
                    ["rewardPointsHighscore"] = 1000f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 0f,
                    ["maxRewardMultiplyer"] = 0f,
                    ["experience"] = 500f,
                },
                ["hardcore"] = new Dictionary<string, float>
                {
                    ["bradleyHealth"] = 5000f,
                    ["damageMultiplyer"] = 1.8f,
                    ["rewardPoints"] = 1000f,
                    ["rewardPointsHighscore"] = 1500f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 6f,
                    ["maxRewardMultiplyer"] = 15f,
                    ["experience"] = 1000f,
                },
                ["ultra"] = new Dictionary<string, float>
                {
                    ["bradleyHealth"] = 10000f,
                    ["damageMultiplyer"] = 2.2f,
                    ["rewardPoints"] = 1500f,
                    ["rewardPointsHighscore"] = 2000f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 16f,
                    ["maxRewardMultiplyer"] = 25f,
                    ["experience"] = 2000f,
                },
                ["bradleyfromhell"] = new Dictionary<string, float>
                {
                    ["bradleyHealth"] = 25000f,
                    ["damageMultiplyer"] = 3.0f,
                    ["rewardPoints"] = 2000f,
                    ["rewardPointsHighscore"] = 2500f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 30f,
                    ["maxRewardMultiplyer"] = 50f,
                    ["experience"] = 4000f,
                }
            };
            public Dictionary<string, Dictionary<string, string>> Titles = new Dictionary<string, Dictionary<string, string>>()
            {
                ["normal"] = new Dictionary<string, string>()
                {
                    ["m"] = "TankBuster",
                    ["f"] = "TankBuster",
                    ["m_eternal"] = "TankDestroyer",
                    ["f_eternal"] = "TankDestroyer",
                },
                ["hardcore"] = new Dictionary<string, string>()
                {
                    ["m"] = "TankHunter",
                    ["f"] = "TankHuntress",
                    ["m_eternal"] = "Master TankHunter",
                    ["f_eternal"] = "Master TankHuntress",
                },
                ["ultra"] = new Dictionary<string, string>()
                {
                    ["m"] = "Ultra TankHunter",
                    ["f"] = "Ultra TankHuntress",
                    ["m_eternal"] = "UltraBrad Legend",
                    ["f_eternal"] = "UltraBrad Legend",
                },
                ["bradleyfromhell"] = new Dictionary<string, string>()
                {
                    ["m"] = "Tank Hellraiser",
                    ["f"] = "Tank Hellraiser",
                    ["m_eternal"] = "God of Tanks",
                    ["f_eternal"] = "Goddess of Tanks",
                }
            };
        }

        #endregion


        #region Data  
        private BradleyAPC currentBradley;
        private string currentBradleyLevel;
        private BasePlayer currentBradleyOwner;
        private HashSet<ulong> currentTeamMemberIDs = new HashSet<ulong>();
        private DateTime currentBradleySpawnTime = DateTime.Now;
        private DateTime lastBradleyKillTime = DateTime.Now;
        private DateTime lastDamageTime = DateTime.Now;
        private List<string> teamSizeName = new List<string>{
            "none",
            "solo",
            "duo",
            "trio",
            "4P",
            "5P",
            "6P",
            "7P",
            "8P",
            "Zerg_9",
            "Zerg_10",
            "Zerg_11",
            "Zerg_12",
            "Zerg_13",
            "Zerg_14",
            "Zerg_15",
            "Zerg_16",
            "Zerg_17",
            "Zerg_18",
            "Zerg_19",
            "Full_20"
        };
        private Dictionary<ulong, float> currentDamage = new Dictionary<ulong, float>();
        private bool currentIsHighscore = false;
        private bool currentIsForceKill = false;
        private Timer bradleyRedirectionTimer;
        private Timer bradleyFireTimer;
        private Timer currentUITimer;
        public static int blockLayer;
        private Dictionary<ulong, DateTime> lastPlayerBradley = new Dictionary<ulong, DateTime>();
        private string[] cooldownBrads = { "normal", "hardcore" };
        private string lastWinStat = "";
        private float lastWinTime = 0.0f;

        class StoredData
        {
            public List<ulong> BradleyQueue = new List<ulong>();
            public Dictionary<ulong, string> TicketData = new Dictionary<ulong, string>();
        }
        class HighscoreData
        {
            public Dictionary<string, Dictionary<int, float>> HighscoreTimes = new Dictionary<string, Dictionary<int, float>>();
            public Dictionary<string, Dictionary<int, string>> HighscorePlayer = new Dictionary<string, Dictionary<int, string>>();
        }
        StoredData storedData;
        HighscoreData highscoreData;
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("BradleyFromHell", storedData);
            Interface.Oxide.DataFileSystem.WriteObject("BradleyFromHell_Highscores", highscoreData);
        }
        #endregion
        void Loaded()
        {

            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permConsole, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("BradleyFromHell");
            highscoreData = Interface.Oxide.DataFileSystem.ReadObject<HighscoreData>("BradleyFromHell_Highscores");
            SaveData();
        }

        private void Unload()
        {
            Unsubscribe(nameof(OnEntityKill));
            if (currentBradley != null)
            {
                currentIsForceKill = true;
                currentBradley.Kill();
            }
            SaveData();
            resetUI();
        }
        private void OnNewSave()
        {
            // reset highscore automatically on wipe
            // keep purchases (storedData) alive
            highscoreData = new HighscoreData();
            SaveData();
        }
        private void OnServerInitialized()
        {
            blockLayer = LayerMask.GetMask("World", "Construction", "Tree", "Deployed", "Default");
            initTitles();
        }

        #region Commands
        [ConsoleCommand("bradleyfromhell.highscore")]
        private void CmdUIkill(ConsoleSystem.Arg arg)
        {

            if (arg.Player() != null)
            {
                CuiHelper.DestroyUi(arg.Player(), "BradleyUI");
                displayHighscore(arg.Player(), arg.Args[0]);
            }
        }

        [ConsoleCommand("bradleyfromhell.ticket")]
        private void CmdBuyHeliconsole(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 2) return;
            if (arg.Player() != null)
            {
                if (!HasPermission(arg.Player().UserIDString, permConsole))
                {
                    PrintToConsole(arg.Player(), "No Permission");
                    return;
                }
            }

            Action<string> printToConsole;
            if (arg.Player() == null)
            {
                printToConsole = (str) => Puts(str);
            }
            else
            {
                printToConsole = (str) => PrintToConsole(arg.Player(), str);
            }
            if (!arg.Args[0].IsSteamId())
            {
                printToConsole("InvalidSteamId " + arg.Args[0]);
                return;
            }

            var player = FindPlayer(ulong.Parse(arg.Args[0]));
            if (player == null)
            {
                printToConsole("Player Not Found");
                return;
            }
            string bradleyLevel = arg.Args[1];
            if (!Cfg.BradleyLevels.ContainsKey(bradleyLevel))
            {
                printToConsole("Invalid bradley Level " + bradleyLevel);
                return;
            }

            if (addToQueue(player, bradleyLevel))
            {
                printToConsole("Added ticket for " + player + " at level " + bradleyLevel);
            }
            else
            {
                printToConsole("Player " + player + " already has a ticket!");
            }

        }
        private void showUsageMessage(BasePlayer player)
        {
            string msg = "<color=orange>============ Bradley From Hell (v" + Plugin.Version + ") ============</color>";
            msg += "\n- The Tank can be spawned everywhere.\n- It only attacks the caller(+team)\n- Only the caller(+team) can damage it!";
            msg += "\n- It can only be attacked in an authed base!";
            msg += "\n- Auto-Surrender if not damaged! (120s + 2s per %damaged)";
            msg += "\n\n<color=green>Main commands:</color>";
            msg += "\n/brad info \t\t\tShow the Bradley queue and wait time";
            msg += "\n/brad levels \t\tShow brad levels info";
            msg += "\n/brad get <level> \tGet a Bradley Ticket (free)  \n\t\t\t\tcheck <color=orange>/brad levels</color>";
            msg += "\n/brad spawn \t\tSpawn your Bradley";
            msg += "\n\n<color=green>Other commands:</color>";
            msg += "\n/brad \t\t\tShow this info";
            msg += "\n/brad score \t\tOpen highscores";
            msg += "\n/brad last \t\t\tShow last fight report";
            msg += "\n/brad cancel \t\tRemove your Bradley ticket";
            msg += "\n<color=green>/brad surrender</color> \t\tSend Bradley away";
            msg += "\n<color=green>/loot</color> \t\t\t\tAfter the round: \n\t\t\t\t - spawn crates with all your loot!";

            if (player.IsAdmin)
            {

                msg += "\n\n<color=red>=== Admin Commands</color>";
                msg += "\n/brad kill \t\t\tKill the Bradley";
                msg += "\n/brad kick <steamid> \tKick player from queue";
            }
            SendReply(player, msg);
        }

        private void showInfoMessage(BasePlayer player)
        {
            var secondsSinceLastKill = Math.Floor((DateTime.Now - lastBradleyKillTime).TotalSeconds);
            string msg = "";
            if (currentBradley != null && currentBradleyOwner != null)
            {
                var secondsFighting = Math.Floor((DateTime.Now - currentBradleySpawnTime).TotalSeconds);
                msg += "<color=green>" + currentBradleyOwner.displayName + " is currently fighting a " + currentBradleyLevel + " Bradley</color>";
                msg += "\nSeconds since spawn: \t\t\t" + secondsFighting;
                msg += "\nCurrent Bradley health: \t\t" + Math.Ceiling(currentBradley.health) + "/" + currentBradley._maxHealth + "\n\n";
            }
            msg += "Seconds since last Bradley kill: \t" + secondsSinceLastKill;
            msg += "\nSkip queue possible at: \t\t" + Cfg.BradleyQueueSeconds;
            msg += "\n\n<color=orange>=============== Bradley Queue ================</color>";
            if (storedData.BradleyQueue.Count == 0)
            {
                msg += "\nNoone waiting to fight a Bradley!";
            }
            else
            {

                int i = 1;
                foreach (ulong ticket in storedData.BradleyQueue)
                {
                    BasePlayer ticketPlayer = FindPlayer(ticket);
                    if (ticketPlayer != null)
                    {
                        msg += "\n[" + i + "] <color=green>" + storedData.TicketData[ticket] + "</color>\t - " + ticketPlayer.displayName;
                        i++;
                    }
                }
            }
            SendReply(player, msg);
        }

        private void showLevelsMessage(BasePlayer player)
        {
            string msg = "<color=orange>============ Bradley From Hell (v" + Plugin.Version + ") ============</color>";
            foreach (KeyValuePair<string, Dictionary<string, float>> level in Cfg.BradleyLevels)
            {
                msg += "\n<color=green>" + level.Key + "</color>";
                msg += "\nHealth: \t\t" + level.Value["bradleyHealth"] + "<color=#999999> (+" + (Cfg.TeamHealthPercentPerPlayer * 100) + "% per player)</color>";
                msg += "\nDamage: \t\tx" + level.Value["damageMultiplyer"];
                msg += "\nRP: \t\t\t" + level.Value["rewardPoints"] + ", Highscore: +" + level.Value["rewardPointsHighscore"];
                msg += "\nBaseLoot: \t\t" + level.Value["minRewardMultiplyer"] + "-" + level.Value["maxRewardMultiplyer"] + "<color=#999999> (+" + (Cfg.TeamRewartPercentPerPlayer * 100) + "% per player)</color>";
            }
            SendReply(player, msg);
        }

        [ChatCommand("brad")]
        private void CmdBradleyMain(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse))
            {
                SendReply(player, "You don't have permission to use this command");
                return;
            }
            if (args.Length == 0)
            {
                showUsageMessage(player);
                return;
            }
            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "info":
                        showInfoMessage(player);
                        break;
                    case "levels":
                        showLevelsMessage(player);
                        break;
                    case "cancel":
                        if (!storedData.TicketData.ContainsKey(player.userID))
                        {
                            SendReply(player, "You do not have a bradley ticket, use <color=blue>/brad get ...</color>");
                            break;
                        }
                        removeFromQueue(player);
                        SendReply(player, "Your Bradley ticket has been canceled!");
                        break;
                    case "surrender":
                        if (currentBradley != null && player == currentBradleyOwner)
                        {
                            if (currentBradley.health == currentBradley.MaxHealth())
                            {
                                SendReply(currentBradleyOwner, "You can't surrender an undamaged Bradley!");
                                return;
                            }
                            currentIsForceKill = true;
                            SendReply(currentBradleyOwner, "Your Bradley has been sent away!");
                            currentBradley.Kill();
                        }
                        break;
                    case "last":
                        SendReply(player, "Last Brad Fight:\n" + lastWinStat);
                        break;
                    case "spawn":
                        // only one bradley at a time
                        if (currentBradley != null)
                        {
                            SendReply(player, "There is already a Bradley in the air, wait for your turn!");
                            return;
                        }
                        // check if player has permission
                        if (storedData.BradleyQueue.Count == 0 || !storedData.TicketData.ContainsKey(player.userID))
                        {
                            SendReply(player, "You don't have a Bradley ticket, use <color=blue>/brad get ...</color>");
                            break;
                        }
                        var secondsSinceLastKill = (DateTime.Now - lastBradleyKillTime).TotalSeconds;
                        if (storedData.BradleyQueue[0] != player.userID && secondsSinceLastKill < Cfg.BradleyQueueSeconds && !player.IsAdmin)
                        {
                            SendReply(player, "It's not your turn to call a Bradley. Check <color=blue>/brad info</color>");
                            break;
                        }
                        if (!TCAuthVerification(player))
                        {
                            SendReply(player, "Bradley can only be spawned from a base you're authed on!");
                            break;
                        }
                        // re-position skipped player
                        if (storedData.BradleyQueue[0] != player.userID && secondsSinceLastKill >= Cfg.BradleyQueueSeconds && !player.IsAdmin)
                        {
                            ulong firstSpot = storedData.BradleyQueue[0];
                            removeFromQueue(firstSpot);
                            //storedData.BradleyQueue.Remove(firstSpot);
                            //storedData.BradleyQueue.Add(firstSpot);
                            //SaveData();
                        }
                        CallBradleyForPlayer(player, storedData.TicketData[player.userID]);

                        break;
                    case "score":
                        displayHighscore(player, "normal");
                        break;
                    case "kill":
                        if (currentBradley != null && player.IsAdmin)
                        {
                            SendReply(currentBradleyOwner, "Your Bradley has been sent away by ADMIN: <color=blue>" + player.displayName + "</color>");
                            SendReply(player, currentBradleyOwner + " had their Bradley sent away, was informed!");
                            currentIsForceKill = true;
                            currentBradley.Kill();
                        }
                        break;
                    default:
                        showUsageMessage(player);
                        break;
                }

            }
            if (args.Length == 2)
            {
                switch (args[0])
                {
                    case "kick":
                        if (player.IsAdmin)
                        {
                            if (!args[1].IsSteamId())
                            {
                                SendReply(player, "InvalidSteamId " + args[1]);
                                break;
                            }
                            BasePlayer targetPlayer = FindPlayer(ulong.Parse(args[1]));
                            if (targetPlayer == null || !storedData.BradleyQueue.Contains(ulong.Parse(args[1])))
                            {
                                SendReply(player, "Player not Ffound (in queue");
                                break;
                            }
                            removeFromQueue(targetPlayer);
                            SendReply(targetPlayer, "You have been removed from the queue by ADMIN: <color=blue>" + player.displayName + "</color>");
                            SendReply(player, targetPlayer + " was kicked from queue, informed!");

                        }
                        break;
                    case "get":
                        int getCooldown = 10;
                        if (!Cfg.BradleyLevels.ContainsKey(args[1]))
                        {
                            SendReply(player, "You must use a valid level with get (normal, hardcore, ultra, hell)");
                            break;
                        }
                        if (cooldownBrads.Contains(args[1]) && lastPlayerBradley.ContainsKey(player.userID) && (int)Math.Floor((DateTime.Now - lastPlayerBradley[player.userID]).TotalMinutes) < getCooldown)
                        {
                            SendReply(player, "You must wait " + (getCooldown - (int)Math.Floor((DateTime.Now - lastPlayerBradley[player.userID]).TotalMinutes)) + " more minutes to do another " + args[1] + " Bradley!\nYou can do 'noloot_...', 'ultra' and 'bradleyfromhell' any time ");
                            break;
                        }
                        if (addToQueue(player, args[1]))
                        {
                            SendReply(player, "Added /brad ticket at level " + args[1]);
                        }
                        break;
                    default:
                        showUsageMessage(player);
                        break;
                }
            }
        }

        #endregion

        #region Hooks

        private object CanBuild(Planner planner, Construction entity, Construction.Target target)
        {
            if (currentBradley == null || planner == null || entity == null)
            {
                return null;
            }

            // check if we care about what we are trying to build
            string name = entity?.fullName ?? string.Empty;
            //Puts("can Build: " + name + " at " + planner.GetOwnerPlayer().transform.position);
            if (!name.Contains("foundation")) return null;

            float distance = Vector3Ex.Distance2D(currentBradley.transform.position, planner.GetOwnerPlayer().transform.position);
            if (distance <= 40f)
            {
                SendReply(planner.GetOwnerPlayer(), "You can't build new foundations so close to a Bradley");
                return false;
            }

            return null;
        }
        private void OnEntityKill(BaseEntity entity)
        {
            if (entity == currentBradley)
            {
                if (!currentIsForceKill)
                {
                    checkHighscore();
                    rewardTeam();
                    createRewardCrates(entity.transform.position);
                }
                cleanupAfterBradley();
            }
        }


        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return null;
            if (currentBradley == null) return null;
            float damageScale = Cfg.BradleyLevels[currentBradleyLevel]["damageMultiplyer"];
            //solo player boost
            if (currentTeamMemberIDs.Count() == 1) damageScale -= Cfg.SoloDamageReduction;

            if (entity == currentBradley || info?.Initiator == currentBradley)
            {
                BasePlayer player = info?.Initiator as BasePlayer;
                BradleyAPC bradley = info?.Initiator as BradleyAPC;
                if (player != null)
                {
                    if (!HasSteaksInBradley((BaseEntity)player))
                    {
                        info.damageTypes.ScaleAll(0.0f);
                    }
                    if (!TCAuthVerification(player))
                    {
                        SendReply(player, "You can only attack Bradley in a base you're authed on!");
                        info.damageTypes.ScaleAll(0.0f);
                    }
                    currentDamage[player.userID] += info.damageTypes.Total();
                    currentDamage[0] += info.damageTypes.Total();
                    lastDamageTime = DateTime.Now;
                    //Puts("DEBUG: damaging bradley for "+ info.damageTypes.Total());
                }
                if (bradley != null)
                {
                    //don't allow bradley to damage innocents
                    if (!HasSteaksInBradley((BaseEntity)entity))
                    {
                        if (IsTCHacking((BaseEntity)entity))
                        {
                            info.damageTypes.ScaleAll(200000.0f);
                        }
                        else
                        {
                            //there are no innocents if they get hit.
                            //info.damageTypes.ScaleAll(0.0f); 
                            if (entity is BuildingBlock || entity is DecayEntity)
                            {
                                info.damageTypes.ScaleAll(0.0f);
                            }
                            else
                            {
                                info.damageTypes.ScaleAll(damageScale);
                            }
                        }
                    }
                    else
                    {
                        info.damageTypes.ScaleAll(damageScale);
                    }
                    //Puts("DEBUG: Bradley is attacking " +entity+ " for "+ info.damageTypes.Total());
                }
            }

            //Puts("DEBUG: DAMAGE" + info.damageTypes.Total() + " --- " + info.WeaponPrefab + " ... " + info.Initiator);
            // mitigate rocket damage to bystanderes
            if (info.WeaponPrefab != null && info.WeaponPrefab.name.Contains("MainCannonShell"))
            {
                //Puts("DEBUG: bradley cannon?" + info.damageTypes.Total() + " --- " + info.WeaponPrefab + " ... " + entity);
                if (!HasSteaksInBradley((BaseEntity)entity))
                {
                    if (IsTCHacking((BaseEntity)entity))
                    {
                        info.damageTypes.ScaleAll(50000.0f);
                    }
                    else
                    {
                        //Puts("DEBUG: rocket hit innocent, no damage" + entity);    
                        if (entity is BuildingBlock || entity is DecayEntity)
                        {
                            info.damageTypes.ScaleAll(0.0f);
                        }
                        else
                        {
                            info.damageTypes.ScaleAll(damageScale);
                        }
                    }
                }
                else
                {
                    info.damageTypes.ScaleAll(damageScale);
                    if (entity is BuildingBlock || entity is DecayEntity || entity.name.Contains("shopfront.metal") || entity.name.Contains("door"))
                    {
                        //Puts("Attacking building wall, make it destructive");   
                        info.damageTypes.ScaleAll(450.0f * damageScale);
                    }
                }
                //Puts("DEBUG: Bradley is canon attacking " +entity+ " for "+ info.damageTypes.Total());

            }
            return null;
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info == null || info.HitEntity == null)
                return null;

            if (info.HitEntity == currentBradley)
            {
                if (!HasSteaksInBradley(attacker))
                {
                    SendReply(attacker, "You don't have permission to attack this Bradley!");
                    info.damageTypes.ScaleAll(0.0f);
                    return false;
                }
                if (!TCAuthVerification(attacker))
                {
                    SendReply(attacker, "You can only attack Bradley in a base you're authed on!");
                    info.damageTypes.ScaleAll(0.0f);
                    return false;
                }
            }
            return null;
        }


        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity target)
        {
            if (apc != currentBradley)
                return null;
            return HasSteaksInBradley(target) ? null : (object)false;
        }

        // loot protection
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;

            //remove fire from bradley (and possible bradley), cause it is no use...
            if (entity.name != null && entity.name.Contains("oilfireball") && !entity.IsDestroyed)
            {
                entity.Kill();
            }
            if (entity is BradleyAPC && (currentBradley == null || currentBradley != entity))
            {
                Puts("Killing public Bradley with no owner");
                entity.Kill();
            }
            if (entity is HelicopterDebris)
            {
                var debris = entity as HelicopterDebris;
                if (debris == null || entity.IsDestroyed) return;
                debris.Kill();
            }
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            killUI(player);
            if (player && player == currentBradleyOwner && currentBradley != null)
            {
                currentIsForceKill = true;
                currentBradley.Kill();
                Puts(player + " disconnected, active Bradley was surrendered");
            }
            if (player && storedData.TicketData.ContainsKey(player.userID))
            {
                removeFromQueue(player);
                Puts(player + " disconnected, Bradley ticket was removed");
            }
        }

        #endregion
        #region BradleyMods
        class BradleyModifyer : MonoBehaviour
        {
            public BradleyAPC APC { get; set; }
            bool IsBoosted { get; set; }
            void Awake()
            {
                APC = GetComponent<BradleyAPC>();

                APC.myRigidBody.useGravity = true;
                //APC.coaxFireRate = 0.03667f;
            }

            private float hoverHeight = 1.0f;
            private float terrainHeight;
            private Vector3 pos;
            private RaycastHit hit;
            public Transform raycastPoint;
            void Update()
            {

            }


            void OnCollisionEnter(Collision collision)
            {
                var obj = collision.gameObject;
                //print("DEBUG: collision with name: " + collision.gameObject.name);
                if (obj.name.Contains("wall.frame") || obj.name.Contains("refinery_small") || obj.name.Contains("gate") || obj.name.Contains("ollider") || obj.name.Contains("table") || obj.name.Contains("fence") || obj.name.Contains("furnace") || obj.name.Contains("barricade") || obj.name.Contains("bench") || obj.name.Contains("bed") || obj.name.Contains("box") || obj.name.Contains("water") || obj.name.Contains("locker") || obj.name.Contains("barbeque") || obj.name.Contains("chair") || obj.name.Contains("sign") || obj.name.Contains("wall.low"))
                {
                    Physics.IgnoreCollision(collision.collider, APC.myCollider);
                    Vector3 newPosition = Plugin.GetNewAPCTarget();
                    APC.transform.position = newPosition;
                        /*var ent = obj.GetComponentInParent<BaseEntity>();
                        if (ent != null)
                        {
                            ent.Kill(BaseNetworkable.DestroyMode.Gib);
                            return;
                        }*/
                    }
                if (obj.name.Contains("building core"))
                {
                    var ent = obj.GetComponentInParent<BaseEntity>();
                    if (ent != null)
                    {
                        Vector3 newPosition = Plugin.GetNewAPCTarget();
                        APC.transform.position = newPosition;
                        return;
                        if (!Plugin.HasSteaksInBradley(ent)) return;
                        ((BuildingBlock)ent).health -= 1201f;
                        if (((BuildingBlock)ent).health <= 1) ent.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                }

                if (obj.name.Contains("foundation") || obj.name.Contains("ramp") || obj.name.Contains("roof") || obj.name.Contains("wall.low"))
                {
                    var ent = obj.GetComponentInParent<BaseEntity>();
                    if (ent != null)
                    {
                        Vector3 newPosition = Plugin.GetNewAPCTarget();
                        APC.transform.position = newPosition;
                        return;
                        if (!Plugin.HasSteaksInBradley(ent)) return;
                        ent.Kill(BaseNetworkable.DestroyMode.Gib);
                        return;
                    }
                }
                if (collision.gameObject.layer == 16 || obj.name.Contains("foundation"))
                {
                    if (!collision.gameObject.name.Contains("perimeter_wall") || obj.name.Contains("foundation"))
                    {
                        Physics.IgnoreCollision(collision.collider, APC.myCollider);
                    }

                    if (!IsBoosted)
                    {
                        var force = APC.moveForceMax;
                        APC.moveForceMax *= 4.5f;

                        Plugin.timer.Once(1f, () =>
                        {
                            if (APC != null && !APC.IsDead())
                            {
                                APC.moveForceMax = force;
                                IsBoosted = false;
                            }


                        });

                        IsBoosted = true;
                    }

                    return;
                }



                var res = obj.GetComponentInParent<ResourceEntity>();
                if (res != null)
                {
                    var fakeInfo = new HitInfo();
                    fakeInfo.PointStart = APC.transform.position - APC.myRigidBody.velocity * 0.5f;
                    fakeInfo.PointEnd = res.transform.position;
                    res.OnKilled(fakeInfo);
                    return;
                }

                /* 
                var dent = obj.GetComponentInParent<DecayEntity>();
                if(dent != null)
                {
                    dent.Kill(BaseNetworkable.DestroyMode.Gib);
                    return;
                } 
                */

                var jpile = obj.GetComponent<JunkPile>();
                if (jpile != null)
                {
                    jpile.SinkAndDestroy();

                }
            }
        }


        #endregion

        #region HelperFunctions
        private void initTitles()
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> d in Cfg.Titles)
            {
                ZNTitleManager?.Call("UpsertTitle", getTitleID(d.Key), "Brad '" + d.Key + "'", "Seconds", d.Value["m_eternal"], d.Value["f_eternal"], d.Value["m"], d.Value["f"]);
            }
        }

        private string getTitleID(string difficulty)
        {
            int index = difficulty.IndexOf("_");
            difficulty = (index > 0) ? difficulty.Substring(index + 1) : difficulty;
            return Plugin.Name + "_" + difficulty;
        }
        private void surrenderEnemy()
        {
            if (currentBradley != null)
            {
                currentIsForceKill = true;
                SendReply(currentBradleyOwner, "Your Bradley has been sent away.");
                currentBradley.Kill();
            }
        }

        private void createRewardCrates(Vector3 targetPos)
        {
            if (LootBoxSpawner == null)
            {
                Puts("ERROR: LootBoxSpawner not loaded!");
                return;
            }
            else
            {
                int minRewardMultiplyer = (int)Math.Ceiling(Cfg.BradleyLevels[currentBradleyLevel]["minRewardMultiplyer"]);
                minRewardMultiplyer += (int)Math.Floor((currentTeamMemberIDs.Count - 1) * Cfg.TeamRewartPercentPerPlayer * minRewardMultiplyer);
                int maxRewardMultiplyer = (int)Math.Ceiling(Cfg.BradleyLevels[currentBradleyLevel]["maxRewardMultiplyer"]);
                maxRewardMultiplyer += (int)Math.Floor((currentTeamMemberIDs.Count - 1) * Cfg.TeamRewartPercentPerPlayer * maxRewardMultiplyer);
                string msg;
                if (maxRewardMultiplyer == 0)
                {
                    msg = "You finished a 'noloot' variant, there is only an RP reward.";
                }
                else
                {
                    LootBoxSpawner.Call("storeRewardClaim", currentBradleyOwner.userID, currentBradleyLevel, minRewardMultiplyer, maxRewardMultiplyer, "brad");
                    msg = "Use <color=green>/loot</color> to get your reward.";
                }
                SendReply(currentBradleyOwner, msg);
            }

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

        private void adjustBradleyPower(BradleyAPC bradley)
        {
            string bradleyLevel = currentBradleyLevel;
            int currentTeamSize = currentTeamMemberIDs.Count();

            float bradleyHealth = Cfg.BradleyLevels[bradleyLevel]["bradleyHealth"];
            bradleyHealth += (float)(bradleyHealth * Cfg.TeamHealthPercentPerPlayer * (currentTeamSize - 1f));
            bradley._maxHealth = bradleyHealth;
            bradley.health = bradleyHealth;
            bradley.maxCratesToSpawn = 0;
            bradley.moveForceMax *= 1.2f;
        }
        private static BradleyAPC InstantiateEntity(Vector3 position)
        {
            var prefabName = "assets/prefabs/npc/m2bradley/bradleyapc.prefab";
            var prefab = GameManager.server.FindPrefab(prefabName);
            var go = Facepunch.Instantiate.GameObject(prefab, position, default(Quaternion));

            go.name = prefabName;
            SceneManager.MoveGameObjectToScene(go, Rust.Server.EntityScene);

            if (go.GetComponent<Spawnable>())
            {
                UnityEngine.Object.Destroy(go.GetComponent<Spawnable>());
            }

            if (!go.activeSelf)
            {
                go.SetActive(true);
            }
            return go.GetComponent<BradleyAPC>();
        }
        private bool CallBradleyForPlayer(BasePlayer player, string bradleyLevel = "normal")
        {

            currentBradleyOwner = player;


            currentBradley = InstantiateEntity(GetNewAPCTarget());
            if (!currentBradley) return false;
            currentBradley.Spawn();
            currentBradley.SendNetworkUpdateImmediate();
            currentBradley.UpdateMovement_Hunt();
            //currentBradley.transform.position = GetNewAPCTarget();
            currentBradleyOwner = player;
            currentBradleySpawnTime = DateTime.Now;
            if (cooldownBrads.Contains(bradleyLevel)) lastPlayerBradley[player.userID] = DateTime.Now;
            currentBradleyLevel = bradleyLevel;
            currentDamage.Add(0, 0f); // total damage counter
            lastDamageTime = DateTime.Now;
            // team related additions
            if (currentBradleyOwner.currentTeam != 0UL)
            {
                RelationshipManager.PlayerTeam theTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                foreach (var member in theTeam.members)
                {
                    BasePlayer p = BasePlayer.Find("" + member);
                    if (p != null)
                    {
                        currentTeamMemberIDs.Add(member);
                        currentDamage.Add(member, 0f);
                        currentBradley.AddOrUpdateTarget(player, player.transform.position);
                    }
                }
            }
            else
            {
                currentTeamMemberIDs.Add(player.userID);
                currentDamage.Add(player.userID, 0f);
                currentBradley.AddOrUpdateTarget(player, player.transform.position);
            }
            adjustBradleyPower(currentBradley);
            removeFromQueue(player);
            currentBradley.gameObject.AddComponent<BradleyModifyer>();

            // add movement timer
            SetupGameTimer();
            currentBradley.SendNetworkUpdateImmediate();

            foreach (var p in BasePlayer.activePlayerList)
            {
                SendReply(p, "Personal <color=#63ff64>" + bradleyLevel + "</color> BradleyAPC was spawned for <color=#63ff64>" + player.displayName + " </color>.\nIt will not damage bystanders.");
            }
            ZNui?.Call("ToggleEvent", "tankEvent", true);
            return true;
        }
        private Vector3 FindTeamPosition()
        {
            if (currentDamage.Count <= 2 || currentDamage[0] <= 0)
                return currentBradleyOwner.transform.position;
            int rand = Random.Range(1, currentDamage.Count);
            ulong playerID = currentDamage.ElementAt(rand).Key;
            BasePlayer p = FindPlayer(playerID);
            if (p != null)
            {
                return p.transform.position;
            }
            else
            {
                return currentBradleyOwner.transform.position;
            }

        }

        private void FireGun()
        {
            currentBradley.FireGun(currentBradleyOwner.eyes.position, 3f, true);
            //currentBradley.FireGun(currentBradleyOwner.eyes.position, 3f, false);
            currentBradley.FireGunTest();
            currentBradley.mainGunTarget = (BaseCombatEntity)currentBradleyOwner;
            currentBradley.DoWeaponAiming();

            currentBradley.FireGun(currentBradleyOwner.eyes.position, 3f, true);
        }
        private void AirStrike()
        {
            return;
            if (currentDamage.Count <= 2) return; 
            timer.Repeat(1f, 2, () =>
            {

                float randX = Random.Range(-8, 8);
                float randZ = Random.Range(-8, 8);
                float randY = Random.Range(5f, 20f);
                Vector3 spawnPoint2 = FindTeamPosition() + new Vector3(randX, randY, randZ);

                string cratePrefab = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
                var entity = GameManager.server.CreateEntity(cratePrefab, spawnPoint2);
                entity.OwnerID = currentBradley.net.ID;
                if (entity == null) { return; }

                entity.Spawn();
            });
        }

        private void SetupGameTimer()
        {
            Vector3 lastBradPosition = currentBradley.transform.position;
            currentUITimer = timer.Every(1f, () =>
            {
                if (UiPlayers.Count > 0)
                {
                    foreach (BasePlayer p in UiPlayers)
                    {
                        reloadUI(p);
                    }
                }
                else
                {
                    foreach (ulong pID in currentTeamMemberIDs)
                    {
                        reloadUI(FindPlayer(pID));
                    }
                }
                if (!currentBradleyOwner.IsConnected || (DateTime.Now - lastDamageTime).TotalSeconds > getNoDamageSurrenderTime())
                {
                    surrenderEnemy();
                }
            });

            //bradleyFireTimer = timer.Every(6f, () =>
            //{
            //});
            bradleyRedirectionTimer = timer.Every(7f, () =>
            {
                float disty = Math.Abs(FindTeamPosition().y - currentBradley.transform.position.y);
                if(disty > 10f || currentBradley.health < (currentBradley._maxHealth/ 3f))
                {
                    timer.Once(0.5f, () => { FireGun(); });
                    timer.Once(1.5f, () => { FireGun(); });
                }
                if(currentBradley.health < (currentBradley._maxHealth / 5f))
                {
                    AirStrike();
                }
                FireGun();
                timer.Once(1f, () => { FireGun(); });
                List<Vector3> newPath = GetNewAPCPath(currentBradley.transform.position);
                currentBradley.currentPathIndex = 0;
                currentBradley.AddOrUpdateTarget(currentBradleyOwner, FindTeamPosition());
                Vector3 newPosition = GetNewAPCTarget();
                float distance = Vector3Ex.Distance2D(currentBradley.transform.position, lastBradPosition);
                if (distance <= 2f)
                {
                    currentBradley.transform.position = newPosition;
                    SendReply(currentBradleyOwner, "Brad used the teleporter to get unstuck!");
                    Puts("DEBUG: brad TP to: " + currentBradley.transform.position + " from: " + lastBradPosition);
                    return;
                }
                lastBradPosition = currentBradley.transform.position;


                // bradley is stuck
                if (newPath == null)
                    return;


                if (newPath.Count > 120 || currentBradley.transform.position.y < (TerrainMeta.HeightMap.GetHeight(currentBradley.transform.position) - 5))
                {

                    newPath = GetNewAPCPath(newPosition);
                    if (newPath != null)
                    {
                        currentBradley.transform.position = newPosition;
                        //Puts("DEBUG: Bradley uses Blink ability ");
                    }
                    else
                    {
                        //Puts("Player " + currentBradleyOwner + " too far away or hiding from Bradley. Waiting for them to move.");
                        return;
                    }
                }
                currentBradley.currentPath = newPath;
                //Puts("DEBUG: update path " + currentBradley.currentPath + " ___ " + currentBradley.currentPath?.Count);
            });
        }

        private int getNoDamageSurrenderTime()
        {
            float health = currentBradley.health;
            float maxHealth = currentBradley._maxHealth;

            int healthPercent = (int)Math.Ceiling((health / maxHealth) * 100);
            return 120 + ((100 - healthPercent) * 2);
        }
        private List<Vector3> GetNewAPCPath(Vector3 position)
        {
            List<Vector3> path = (List<Vector3>)PathFinding?.Call("FindBestPath", position, GetNewAPCTarget());
            return path;
        }
        private Vector3 GetNewAPCTarget(int rec = 0, float range = 20f)
        {
            float rand1 = Random.Range(-range, range);
            float rand2 = Random.Range(-range, range);
            float minDistance = 20f;
            float randX = (rand1 > 0 ? (rand1 + minDistance) : (rand1 - minDistance));
            float randZ = (rand2 > 0 ? (rand2 + minDistance) : (rand2 - minDistance));
            Vector3 node = FindTeamPosition() + new Vector3(randX, 200f, randZ);
            node = SampleHeightWithRaycast(node);
            // check if the path is blocked by something
            NavMeshHit navMeshHit;

            if (!NavMesh.SamplePosition(node, out navMeshHit, 5f, NavMesh.AllAreas))
            {
                //Puts("DEBUG: retry navmesh " + node);
                // break recursion
                if (rec > 15)
                    return node;
                // try to find an unblocked random
                rec++;
                return GetNewAPCTarget(rec, range + 5);
            }
            else
            {
                return navMeshHit.position + new Vector3(0, 0.1f, 0);
            }
        }
        private void notifyNextPlayer()
        {
            if (storedData.BradleyQueue.Count > 0)
            {
                ulong nextTicket = storedData.BradleyQueue[0];
                BasePlayer nextPlayer = FindPlayer(nextTicket);
                if (nextPlayer != null)
                {
                    SendReply(nextPlayer, "<color=green>Your bradley</color> can now be called!\nUse <color=blue>/brad spawn</color> now.");
                }
                return;
            }
        }

        private void cleanupAfterBradley()
        {
            currentUITimer?.Destroy();
            resetUI();
            bradleyRedirectionTimer?.Destroy();
            bradleyFireTimer?.Destroy();
            currentBradley = (BradleyAPC)null;
            currentBradleyOwner = (BasePlayer)null;
            currentBradleyLevel = string.Empty;
            currentBradleySpawnTime = DateTime.Now;
            currentTeamMemberIDs.Clear();
            currentDamage = new Dictionary<ulong, float>();
            lastBradleyKillTime = DateTime.Now;
            currentIsHighscore = false;
            currentIsForceKill = false;
            notifyNextPlayer();
            ZNui?.Call("ToggleEvent", "tankEvent", false);
        }

        private bool TCAuthVerification(BasePlayer player)
        {
            BuildingPrivlidge privilege = player.GetBuildingPrivilege();
            if (!privilege) return false;
            if (!privilege.IsAuthed(player))
            {
                return false;
            }
            return true;
        }

        private bool IsTCHacking(BaseEntity entity)
        {
            if (!(entity is BuildingBlock))
            {
                return false;
            }
            // public safety
            if (currentBradleyOwner == null || currentBradley == null)
            {
                return false;
            }
            var building = BuildingManager.server.GetBuilding(((DecayEntity)entity).buildingID);
            var tc = building?.GetDominatingBuildingPrivilege();
            if (tc == null)
            {
                // a building block without TC is hit - exploit!
                return true;
            }
            return false;

        }

        private bool HasSteaksInBradley(BaseEntity entity)
        {
            //return false;
            if (!(entity is BasePlayer) && !(entity is DecayEntity))
            {
                return false;
            }
            // public safety
            if (currentBradleyOwner == null || currentBradley == null)
            {
                return false;
            }

            if (entity is BasePlayer)
            {
                // owner can attack / be attacked
                if (entity == currentBradleyOwner)
                {
                    return true;
                }

                if (currentTeamMemberIDs.Contains(((BasePlayer)entity).userID))
                {
                    return true;
                }
            }
            // allow damage to player and team entities
            if (entity is DecayEntity)
            {
                if (entity.OwnerID == currentBradleyOwner.userID)
                {
                    return true;
                }
                if (currentTeamMemberIDs.Contains(entity.OwnerID))
                {
                    return true;
                }
                // allow damage to entities in a TC-range that the team is authed on
                var building = BuildingManager.server.GetBuilding(((DecayEntity)entity).buildingID);
                var tc = building?.GetDominatingBuildingPrivilege();
                if (tc != null)
                {
                    foreach (var entry in tc.authorizedPlayers)
                    {
                        if (entry.userid == currentBradleyOwner.userID)
                        {
                            return true;
                        }
                        if (currentTeamMemberIDs.Contains(entry.userid))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool addToQueue(BasePlayer player, string level)
        {
            // only allow a new ticket if there is not already one
            if (!storedData.BradleyQueue.Contains(player.userID))
            {
                storedData.BradleyQueue.Add(player.userID);
                storedData.TicketData.Add(player.userID, level);
                SaveData();
                return true;
            }
            else
            {
                SendReply(player, "You can only have one bradley ticket at a time.");
                return false;
            }

        }
        private void removeFromQueue(BasePlayer player)
        {
            storedData.BradleyQueue.Remove(player.userID);
            storedData.TicketData.Remove(player.userID);
            SaveData();
        }
        private void removeFromQueue(ulong playerId)
        {
            storedData.BradleyQueue.Remove(playerId);
            storedData.TicketData.Remove(playerId);
            SaveData();
        }
        private void refundTicket(BasePlayer player, string bradleyLevel)
        {
            if (!ServerRewards)
                return;

            ServerRewards?.Call("AddPoints", player.userID, (int)Cfg.BradleyLevels[bradleyLevel]["refundPrice"]);
            SendReply(player, "Bradley ticket price refunded.");
        }
        private void refundSurrender(BasePlayer player, string bradleyLevel)
        {
            if (!ServerRewards)
                return;

            ServerRewards?.Call("AddPoints", player.userID, (int)(Math.Ceiling(Cfg.BradleyLevels[bradleyLevel]["refundPrice"] / 2)));
            SendReply(player, "50% of bradley price refunded for surrender.");
        }

        private void rewardTeam()
        {
            if (currentDamage.Count == 0 || currentDamage[0] <= 0)
                return;

            string msg = "<color=orange>You took down your Bradley!</color>";
            msg += "\n<color=green>Time taken:</color>\t\t" + lastWinTime + "s";
            msg += "\n<color=green>Total Damage dealt:</color>\t" + Math.Ceiling(currentDamage[0]);
            List<BasePlayer> tempNotifyPlayers = new List<BasePlayer>();
            float topDamage = 0f;
            foreach (KeyValuePair<ulong, float> dmg in currentDamage.OrderByDescending(key => key.Value))
            {
                BasePlayer player = FindPlayer(dmg.Key);
                if (player != null)
                {
                    tempNotifyPlayers.Add(player);
                    // calculate and display reward and damage
                    var ratio = dmg.Value / currentDamage[0];

                    int reward = (int)Math.Floor(Cfg.BradleyLevels[currentBradleyLevel]["rewardPoints"] * ratio);
                    if (currentIsHighscore)
                    {
                        reward += (int)Math.Floor(Cfg.BradleyLevels[currentBradleyLevel]["rewardPointsHighscore"] * ratio);
                    }
                    if (reward < 0)
                    {
                        reward = 0;
                    }
                    else
                    {
                        // add reward
                        ServerRewards?.Call("AddPoints", player.userID, reward);
                    }

                    // calculate XP ratio
                    if (topDamage == 0f)
                    {
                        topDamage = dmg.Value;
                        ratio = 1;
                    }
                    else
                    {
                        ratio = dmg.Value / topDamage;
                    }
                    int xpreward = (int)Math.Floor(Cfg.BradleyLevels[currentBradleyLevel]["experience"] * ratio);
                    ZNExperience?.Call("AddXp", player.userID, xpreward);

                    msg += "\n" + player.displayName + " (damage: " + Math.Ceiling(dmg.Value) + ", RP: " + reward + " XP: " + xpreward + ")";
                }
            }
            foreach (BasePlayer p in tempNotifyPlayers)
            {
                SendReply(p, msg);
            }
            lastWinStat = msg;
        }

        private void checkHighscore()
        {
            if (currentBradleySpawnTime == null || currentIsForceKill)
                return;

            float fightDuration = (float)((DateTime.Now - currentBradleySpawnTime).TotalSeconds);
            lastWinTime = fightDuration;
            int teamSize = ((currentTeamMemberIDs.Count > 0) ? currentTeamMemberIDs.Count : 1);

            if (teamSize == 1)
            {
                ZNTitleManager?.Call("CheckScore", getTitleID(currentBradleyLevel), fightDuration, currentBradleyOwner.userID, true);
            }
            if (highscoreData.HighscoreTimes.ContainsKey(currentBradleyLevel)
                && highscoreData.HighscoreTimes[currentBradleyLevel].ContainsKey(teamSize)
                && highscoreData.HighscoreTimes[currentBradleyLevel][teamSize] <= fightDuration)
            {
                // no highscore
                return;
            }

            // New Highscore!!!
            string playerNamesString = currentBradleyOwner.displayName;
            currentIsHighscore = true;
            // team player
            if (teamSize > 1)
            {
                foreach (ulong teamMember in currentTeamMemberIDs)
                {
                    if (teamMember != currentBradleyOwner.userID)
                    {
                        playerNamesString += " - " + FindPlayer(teamMember).displayName;
                    }
                }
            }

            if (!highscoreData.HighscoreTimes.ContainsKey(currentBradleyLevel))
            {
                highscoreData.HighscoreTimes.Add(currentBradleyLevel, new Dictionary<int, float>());
                highscoreData.HighscorePlayer.Add(currentBradleyLevel, new Dictionary<int, string>());
                SaveData();
            }
            if (!highscoreData.HighscoreTimes[currentBradleyLevel].ContainsKey(teamSize))
            {
                // first entry is automatic highscore
                highscoreData.HighscoreTimes[currentBradleyLevel].Add(teamSize, fightDuration);
                highscoreData.HighscorePlayer[currentBradleyLevel].Add(teamSize, playerNamesString);
            }
            else
            {
                highscoreData.HighscoreTimes[currentBradleyLevel][teamSize] = fightDuration;
                highscoreData.HighscorePlayer[currentBradleyLevel][teamSize] = playerNamesString;
            }
            SaveData();

            foreach (var p in BasePlayer.activePlayerList)
            {
                SendReply(p, "NEW HIGHSCORE: <color=#63ff64>" + currentBradleyLevel + "</color> bradley (" + teamSize + "-Player): " + fightDuration + "s \nby: <color=#63ff64>" + playerNamesString + "</color>");
            }
            return;
        }

        private void displayHighscore(BasePlayer player, string bradleyLevel)
        {
            //NextTick(() =>
            //{
            // Destroy existing UI
            var mainName = "BradleyUI";

            int i = 0;
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel
            {
                Image =
                    {
                        Color = "0 0 0 0.9"
                    },
                RectTransform =
                    {
                        AnchorMin = "0.2 0.2",
                        AnchorMax = "0.8 0.8"
                    },
                CursorEnabled = true
            }, "Hud", mainName);
            foreach (string levelCfg in Cfg.BradleyLevels.Keys)
            {
                if (levelCfg == "demo")
                    continue;
                i++;
                elements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = "bradleyfromhell.highscore "+ levelCfg,
                            Color = "0.3 0.3 0.3 1"
                        },
                    RectTransform =
                        {
                            AnchorMin = $"0.85 {0.92 - 0.08*i + 0.01}",
                            AnchorMax = $"0.99 {0.92 - 0.08*(i - 1.0)}"
                        },
                    Text =
                        {
                            Text = levelCfg,
                            FontSize = 22,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        }
                }, mainName);
            }



            var closeButton = new CuiButton
            {
                Button =
                    {
                        Close = mainName,
                        Color = "0.47 0.31 0.31 1"
                    },
                RectTransform =
                    {
                        AnchorMin = "0.01 0.01",
                        AnchorMax = "0.99 0.1"
                    },
                Text =
                    {
                        Text = "close",
                        FontSize = 22,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
            };
            elements.Add(closeButton, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "Bradley Highscore (Level: " + bradleyLevel +")",
                            FontSize = 20,
                            Align = TextAnchor.MiddleCenter,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                            AnchorMin = "0.01 0.92",
                            AnchorMax = "0.99 0.99"
                        }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "Teamsize",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                            AnchorMin = "0.01 0.85",
                            AnchorMax = "0.15 0.92"
                        }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "Best Time",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                            AnchorMin = "0.16 0.85",
                            AnchorMax = "0.3 0.92"
                        }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                        {
                            Text = "Player / Team",
                            FontSize = 15,
                            Align = TextAnchor.MiddleLeft,
                            Color = "1 1 1 1"
                        },
                RectTransform =
                        {
                            AnchorMin = "0.31 0.85",
                            AnchorMax = "0.85 0.92"
                        }
            }, mainName);
            // only work if there is data
            if (highscoreData.HighscoreTimes.ContainsKey(bradleyLevel))
            {
                i = 1;
                foreach (KeyValuePair<int, float> highscore in highscoreData.HighscoreTimes[bradleyLevel])
                {
                    i++;
                    elements.Add(new CuiLabel
                    {
                        Text =
                            {
                                Text = teamSizeName[highscore.Key],
                                FontSize = 12,
                                Align = TextAnchor.MiddleCenter,
                                Color = "1 1 1 1"
                            },
                        RectTransform =
                            {
                                AnchorMin = $"0.01 {0.92 - 0.08*i + 0.01}",
                                AnchorMax = $"0.15 {0.92 - 0.08*(i - 1.0)}"
                            }
                    }, mainName);
                    elements.Add(new CuiLabel
                    {
                        Text =
                            {
                                Text = highscore.Value+"s",
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Color = "1 1 1 1"
                            },
                        RectTransform =
                            {
                                AnchorMin = $"0.16 {0.92 - 0.08*i + 0.01}",
                                AnchorMax = $"0.3 {0.92 - 0.08*(i - 1.0)}"
                            }
                    }, mainName);
                    elements.Add(new CuiLabel
                    {
                        Text =
                            {
                                Text = highscoreData.HighscorePlayer[bradleyLevel][highscore.Key],
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Color = "1 1 1 1"
                            },
                        RectTransform =
                            {
                                AnchorMin = $"0.31 {0.92 - 0.08*i + 0.01}",
                                AnchorMax = $"0.85 {0.92 - 0.08*(i - 1.0)}"
                            }
                    }, mainName);
                }
            }

            CuiHelper.AddUi(player, elements);
            //});
        }
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string infoUIname = "GameRoundHeli_infoHUD";
        private void reloadUI(BasePlayer player, string errorMsg = "none")
        {
            if (!UiPlayers.Contains(player))
            {
                UiPlayers.Add(player);
            }
            CuiHelper.DestroyUi(player, infoUIname);
            displayInfo(player, errorMsg);
        }
        private void killUI(BasePlayer player)
        {
            if (UiPlayers.Contains(player))
            {
                UiPlayers.Remove(player);
            }
            CuiHelper.DestroyUi(player, infoUIname);
        }

        private void resetUI()
        {
            foreach (var player in UiPlayers)
                CuiHelper.DestroyUi(player, infoUIname);

            UiPlayers.Clear();
        }
        private void displayInfo(BasePlayer player, string errorMsg = "none")
        {
            var mainName = infoUIname;
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
                    AnchorMin = "0.0 0.8",
                    AnchorMax = "0.15 0.9"
                },
                CursorEnabled = false
            }, "Hud", mainName);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Duration:",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0.66",
                    AnchorMax = "0.5 1"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Health:",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0.33",
                    AnchorMax = "0.5 0.66"
                }
            }, mainName);

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "NoDamageSurrender:",
                    FontSize = 8,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0",
                    AnchorMax = "0.5 0.33"
                }
            }, mainName);
            if (currentBradley != null)
            {

                var secondsFighting = Math.Floor((DateTime.Now - currentBradleySpawnTime).TotalSeconds);
                var secondsTilSurrender = Math.Floor(getNoDamageSurrenderTime() - (DateTime.Now - lastDamageTime).TotalSeconds);

                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = secondsFighting+"s",
                        FontSize = 14,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.66",
                        AnchorMax = "0.95 1"
                    }
                }, mainName);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = Math.Ceiling(currentBradley.health) + "/" + currentBradley._maxHealth,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.33",
                        AnchorMax = "0.95 0.66"
                    }
                }, mainName);


                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = secondsTilSurrender+"s",
                        FontSize = 8,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.95 0.33"
                    }
                }, mainName);
            }

            CuiHelper.AddUi(player, elements);
        }

        private Vector3 SampleHeightWithRaycast(Vector3 playerPos)
        {
            float groundDistOffset = 2f;
            RaycastHit hit;
            //Raycast down to terrain
            if (Physics.Raycast(playerPos, -Vector3.up, out hit))
            {
                //Get y position
                playerPos.y = (hit.point + Vector3.up * groundDistOffset).y;
            }
            return playerPos;
        }
        #endregion
    }
}
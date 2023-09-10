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
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using System.Text;
using UnityEngine;
using Rust;
using Oxide.Game.Rust.Cui;
using Random = UnityEngine.Random;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.SceneManagement;

namespace Oxide.Plugins
{
    [Info("HeliFromHell", "DocValerian", "1.5.4")]
    class HeliFromHell : RustPlugin
    {
        static HeliFromHell Plugin;

        [PluginReference] Plugin ServerRewards, Clans, LootBoxSpawner, ZNui, ZNTitleManager, ZNExperience;
        private const string permUse = "helifromhell.use";
        private const string permConsole = "helifromhell.ticket";
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
            public int HeliSpawnRadius = 30;
            public int HeliQueueSeconds = 120;
            public float SoloDamageReduction = 0.2f;
            public float TeamHealthPercentPerPlayer = 0.2f;
            public float TeamRewartPercentPerPlayer = 0.05f;
            public Dictionary<string, Dictionary<string, float>> HeliLevels = new Dictionary<string, Dictionary<string, float>>
            {
                ["normal"] = new Dictionary<string, float>
                {
                    ["heliHealth"] = 10000f,
                    ["heliWeakspotHealthPercent"] = 40f,
                    ["damageMultiplyer"] = 1.0f,
                    ["rewardPoints"] = 500f,
                    ["rewardPointsHighscore"] = 1000f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 2f,
                    ["maxRewardMultiplyer"] = 4f,
                    ["experience"] = 500f,
                },
                ["noloot_normal"] = new Dictionary<string, float>
                {
                    ["heliHealth"] = 10000f,
                    ["heliWeakspotHealthPercent"] = 40f,
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
                    ["heliHealth"] = 25000f,
                    ["heliWeakspotHealthPercent"] = 40f,
                    ["damageMultiplyer"] = 1.5f,
                    ["rewardPoints"] = 1000f,
                    ["rewardPointsHighscore"] = 1500f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 5f,
                    ["maxRewardMultiplyer"] = 14f,
                    ["experience"] = 1000f,
                },
                ["noloot_hardcore"] = new Dictionary<string, float>
                {
                    ["heliHealth"] = 25000f,
                    ["heliWeakspotHealthPercent"] = 40f,
                    ["damageMultiplyer"] = 1.5f,
                    ["rewardPoints"] = 1000f,
                    ["rewardPointsHighscore"] = 1500f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 0f,
                    ["maxRewardMultiplyer"] = 0f,
                    ["experience"] = 1000f,
                },
                ["ultra"] = new Dictionary<string, float>
                {
                    ["heliHealth"] = 50000f,
                    ["heliWeakspotHealthPercent"] = 50f,
                    ["damageMultiplyer"] = 2.0f,
                    ["rewardPoints"] = 1500f,
                    ["rewardPointsHighscore"] = 2000f,
                    ["refundPrice"] = 0f,
                    ["minRewardMultiplyer"] = 15f,
                    ["maxRewardMultiplyer"] = 25f,
                    ["experience"] = 2000f,
                },
                ["helifromhell"] = new Dictionary<string, float>
                {
                    ["heliHealth"] = 100000f,
                    ["heliWeakspotHealthPercent"] = 50f,
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
                    ["m"] = "HeliCrasher",
                    ["f"] = "HeliCrasher",
                    ["m_eternal"] = "HeliDestroyer",
                    ["f_eternal"] = "HeliDestroyer",
                },
                ["hardcore"] = new Dictionary<string, string>()
                {
                    ["m"] = "HeliHunter",
                    ["f"] = "HeliHuntress",
                    ["m_eternal"] = "Master HeliHunter",
                    ["f_eternal"] = "Master HeliHuntress",
                },
                ["ultra"] = new Dictionary<string, string>()
                {
                    ["m"] = "Ultra HeliHunter",
                    ["f"] = "Ultra HeliHuntress",
                    ["m_eternal"] = "UltraHeli Legend",
                    ["f_eternal"] = "UltraHeli Legend",
                },
                ["helifromhell"] = new Dictionary<string, string>()
                {
                    ["m"] = "Heli Hellraiser",
                    ["f"] = "Heli Hellraiser",
                    ["m_eternal"] = "Lord of HeliHell",
                    ["f_eternal"] = "Queen of HeliHell",
                }
            };
        }

        #endregion


        #region Data  
        private PatrolHelicopter currentHeli;
        private PatrolHelicopterAI currentHeliAI;
        private string currentHeliLevel;
        private BasePlayer currentHeliOwner;
        private HashSet<ulong> currentTeamMemberIDs = new HashSet<ulong>();
        private FieldInfo tooHotUntil = typeof(HelicopterDebris).GetField("tooHotUntil", (BindingFlags.Instance | BindingFlags.NonPublic));
        private DateTime currentHeliSpawnTime = DateTime.Now;
        private DateTime lastHeliKillTime = DateTime.Now;
        private DateTime lastHeliCallTime = DateTime.Now;
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

        private Timer heliRedirectionTimer;
        private Timer currentUITimer;
        private Dictionary<ulong, DateTime> lastPlayerHeli = new Dictionary<ulong, DateTime>();
        private string[] cooldownHelis = { "normal", "hardcore" };
        private string lastWinStat = "";
        private float lastWinTime = 0.0f;

        class StoredData
        {
            public List<ulong> HeliQueue = new List<ulong>();
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
            Interface.Oxide.DataFileSystem.WriteObject("HeliFromHell", storedData);
            Interface.Oxide.DataFileSystem.WriteObject("HeliFromHell_Highscores", highscoreData);
        }
        #endregion
        void Loaded()
        {

            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permConsole, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("HeliFromHell");
            highscoreData = Interface.Oxide.DataFileSystem.ReadObject<HighscoreData>("HeliFromHell_Highscores");
            SaveData();
        }
        private void OnServerInitialized()
        {
            initTitles();
        }

        private void Unload()
        {
            Unsubscribe(nameof(OnEntityKill));
            if (currentHeli != null)
            {
                currentIsForceKill = true;
                currentHeli.Kill();
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

        #region Commands
        [ConsoleCommand("helifromhell.highscore")]
        private void CmdUIkill(ConsoleSystem.Arg arg)
        {

            if (arg.Player() != null)
            {
                CuiHelper.DestroyUi(arg.Player(), "HeliUI");
                displayHighscore(arg.Player(), arg.Args[0]);
            }
        }

        [ConsoleCommand("helifromhell.ticket")]
        private void CmdBuyHeliConsole(ConsoleSystem.Arg arg)
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
            string heliLevel = arg.Args[1];
            if (!Cfg.HeliLevels.ContainsKey(heliLevel))
            {
                printToConsole("Invalid heli Level " + heliLevel);
                return;
            }
            if (heliLevel.Contains("solo") && !SoloCheckSuccess(player))
            {
                refundTicket(player, heliLevel);
                printToConsole("Ticket was refunded!");
                return;
            }
            if (addToQueue(player, heliLevel))
            {
                printToConsole("Added ticket for " + player + " at level " + heliLevel);
            }
            else
            {
                refundTicket(player, heliLevel);
                printToConsole("Player " + player + " already has a ticket!");
            }

        }
        private void showUsageMessage(BasePlayer player)
        {
            string msg = "<color=orange>============ Heli From Hell (v" + Plugin.Version + ") ============</color>";
            msg += "\n- The heli can be spawned everywhere.\n- It only attacks the caller(+team)\n- Only the caller(+team) can damage it!";
            msg += "\n- It can only be attacked in public space or an authed base!";
            msg += "\n- Auto-Surrender if not damaged! (120s + 2s per %damaged)";
            msg += "\n\n<color=green>Main commands:</color>";
            msg += "\n/heli info \t\t\tShow the heli queue and wait time";
            msg += "\n/heli levels \t\tShow heli levels info";
            msg += "\n/heli get <level> \t\tGet a heli Ticket (free)  \n\t\t\t\tcheck <color=orange>/heli levels</color>";
            msg += "\n\n/heli spawn \t\tSpawn your heli";
            msg += "\n\n<color=green>Other commands:</color>";
            msg += "\n/heli \t\t\t\tShow this info";
            msg += "\n/heli score \t\t\tOpen highscores";
            msg += "\n/heli last \t\t\tShow last fight report";
            msg += "\n/heli cancel \t\tRemove your heli ticket";
            msg += "\n/heli call \t\t\tForce heli to return (200s cooldown)";
            msg += "\n<color=green>/heli surrender</color> \t\tSend heli away";
            msg += "\n<color=green>/loot</color> \t\t\t\tAfter the round: \n\t\t\t\t - spawn crates with all your loot!";

            if (player.IsAdmin)
            {

                msg += "\n\n<color=red>=== Admin Commands</color>";
                msg += "\n/heli kill \t\t\tKill the heli";
                msg += "\n/heli kick <steamid> \tKick player from queue";
            }
            SendReply(player, msg);
        }

        private void showInfoMessage(BasePlayer player)
        {
            var secondsSinceLastKill = Math.Floor((DateTime.Now - lastHeliKillTime).TotalSeconds);
            string msg = "";
            if (currentHeli != null && currentHeliOwner != null)
            {
                var secondsFighting = Math.Floor((DateTime.Now - currentHeliSpawnTime).TotalSeconds);
                msg += "<color=green>" + currentHeliOwner.displayName + " is currently fighting a " + currentHeliLevel + " heli</color>";
                msg += "\nSeconds since spawn: \t\t\t" + secondsFighting;
                msg += "\nCurrent heli health (core): \t\t\t" + Math.Ceiling(currentHeli.health) + "/" + currentHeli._maxHealth;


                foreach (PatrolHelicopter.weakspot weakspot in currentHeli.weakspots)
                {
                    msg += "\nWeakspot health: \t\t\t" + Math.Ceiling(weakspot.health) + "/" + weakspot.maxHealth;
                }
                msg += "\n\n";
            }
            msg += "Seconds since last heli kill: \t\t" + secondsSinceLastKill;
            msg += "\nSkip queue possible at: \t\t" + Cfg.HeliQueueSeconds;
            msg += "\n\n<color=orange>=============== Heli Queue ================</color>";
            if (storedData.HeliQueue.Count == 0)
            {
                msg += "\nNoone waiting to fight a heli!";
            }
            else
            {

                int i = 1;
                foreach (ulong ticket in storedData.HeliQueue)
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
            string msg = "<color=orange>============ Heli From Hell (v" + Plugin.Version + ") ============</color>";
            foreach (KeyValuePair<string, Dictionary<string, float>> level in Cfg.HeliLevels)
            {
                msg += "\n<color=green>" + level.Key + "</color>";
                msg += "\nHealth: \t\t" + level.Value["heliHealth"] + "<color=#999999> (+" + (Cfg.TeamHealthPercentPerPlayer * 100) + "% per player)</color>";
                msg += "\nDamage: \t\tx" + level.Value["damageMultiplyer"];
                msg += "\nRP: \t\t\t" + level.Value["rewardPoints"] + ", Highscore: +" + level.Value["rewardPointsHighscore"];
                msg += "\nBaseLoot: \t\t" + level.Value["minRewardMultiplyer"] + "-" + level.Value["maxRewardMultiplyer"] + "<color=#999999> (+" + (Cfg.TeamRewartPercentPerPlayer * 100) + "% per player)</color>";
            }
            SendReply(player, msg);
        }

        [ChatCommand("heli")]
        private void CmdHeliMain(BasePlayer player, string command, string[] args)
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
                    case "cancel":
                        if (!storedData.TicketData.ContainsKey(player.userID))
                        {
                            SendReply(player, "You do not have a heli ticket, use <color=blue>/heli get ...</color>");
                            break;
                        }
                        refundTicket(player, storedData.TicketData[player.userID]);
                        removeFromQueue(player);
                        SendReply(player, "Your heli ticket has been canceled!");
                        break;
                    case "surrender":
                        if (currentHeli != null && player == currentHeliOwner)
                        {
                            surrenderEnemy();
                        }
                        break;
                    case "levels":
                        showLevelsMessage(player);
                        break;
                    case "call":
                        if (currentHeliOwner == player)
                        {
                            forceHeliToPlayer(player);
                        }
                        else
                        {
                            SendReply(player, "There is no heli you can call right now!");
                        }
                        break;
                    case "last":
                        SendReply(player, "Last Heli Fight:\n" + lastWinStat);
                        break;
                    case "spawn":
                        // only one heli at a time
                        if (currentHeli != null)
                        {
                            SendReply(player, "There is already a heli in the air, wait for your turn!");
                            return;
                        }
                        // check if player has permission
                        if (storedData.HeliQueue.Count == 0 || !storedData.TicketData.ContainsKey(player.userID))
                        {
                            SendReply(player, "You don't have a heli ticket, use <color=blue>/heli get ...</color>");
                            break;
                        }
                        var secondsSinceLastKill = (DateTime.Now - lastHeliKillTime).TotalSeconds;
                        if (storedData.HeliQueue[0] != player.userID && secondsSinceLastKill < Cfg.HeliQueueSeconds && !player.IsAdmin)
                        {
                            SendReply(player, "It's not your turn to call a heli. Check <color=blue>/heli info</color>");
                            break;
                        }
                        if (!TCAuthVerification(player))
                        {
                            SendReply(player, "Heli can only be spawned from a base you're authed on!");
                            break;
                        }
                        if (storedData.TicketData[player.userID].Contains("solo") && !SoloCheckSuccess(player))
                        {
                            break;
                        }
                        // re-position skipped player
                        if (storedData.HeliQueue[0] != player.userID && secondsSinceLastKill >= Cfg.HeliQueueSeconds && !player.IsAdmin)
                        {
                            ulong firstSpot = storedData.HeliQueue[0];
                            removeFromQueue(firstSpot);
                            //storedData.HeliQueue.Remove(firstSpot);
                            //storedData.HeliQueue.Add(firstSpot);
                            //SaveData();
                        }
                        CallHeliForPlayer(player, storedData.TicketData[player.userID]);

                        break;
                    case "score":
                        displayHighscore(player, "normal");
                        break;
                    case "kill":
                        if (currentHeli != null && player.IsAdmin)
                        {
                            refundTicket(currentHeliOwner, currentHeliLevel);
                            currentIsForceKill = true;
                            SendReply(currentHeliOwner, "Your heli has been sent away by ADMIN: <color=blue>" + player.displayName + "</color>\nTicket was refunded!");
                            SendReply(player, currentHeliOwner + " had their heli sent away, was refunded & informed!");
                            currentHeli.Kill();
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
                            if (targetPlayer == null || !storedData.HeliQueue.Contains(ulong.Parse(args[1])))
                            {
                                SendReply(player, "Player not Ffound (in queue");
                                break;
                            }
                            refundTicket(targetPlayer, storedData.TicketData[targetPlayer.userID]);
                            removeFromQueue(targetPlayer);
                            SendReply(targetPlayer, "You have been removed from the queue by ADMIN: <color=blue>" + player.displayName + "</color>\nTicket was refunded!");
                            SendReply(player, targetPlayer + " was kicked from queue, refunded, informed!");

                        }
                        break;
                    case "get":
                        int getCooldown = 10;
                        if (!Cfg.HeliLevels.ContainsKey(args[1]))
                        {
                            SendReply(player, "You must use a valid level with get (normal, hardcore, ultra, hell)");
                            break;
                        }
                        if (cooldownHelis.Contains(args[1]) && lastPlayerHeli.ContainsKey(player.userID) && (int)Math.Floor((DateTime.Now - lastPlayerHeli[player.userID]).TotalMinutes) < getCooldown)
                        {
                            SendReply(player, "You must wait " + (getCooldown - (int)Math.Floor((DateTime.Now - lastPlayerHeli[player.userID]).TotalMinutes)) + " more minutes to do another " + args[1] + " heli!\nYou can do 'noloot_...', 'ultra' and 'helifromhell' any time ");
                            break;
                        }
                        if (addToQueue(player, args[1]))
                        {
                            SendReply(player, "Added /heli ticket at level " + args[1]);
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
        private void OnEntityKill(BaseEntity entity)
        {
            if (entity == currentHeli)
            {
                if (!currentIsForceKill)
                {
                    checkHighscore();
                    rewardTeam();
                    createRewardCrates(entity.transform.position);
                }
                cleanupAfterHeli();
            }

        }


        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return null;

            if (currentHeli == null) return null;
            float damageScale = Cfg.HeliLevels[currentHeliLevel]["damageMultiplyer"];
            //solo player boost
            if (currentTeamMemberIDs.Count() == 1) damageScale -= 0.2f;

            if (entity == currentHeli || info?.Initiator == currentHeli)
            {
                BasePlayer player = info?.Initiator as BasePlayer;
                PatrolHelicopter heli = info?.Initiator as PatrolHelicopter;
                if (player != null)
                {
                    if (!HasSteaksInHeli((BaseEntity)player))
                    {
                        info.damageTypes.ScaleAll(0.0f);
                    }
                    if (!TCAuthVerification(player))
                    {
                        SendReply(player, "You can only attack Heli in a base you're authed on!");
                        info.damageTypes.ScaleAll(0.0f);
                    }
                    currentDamage[player.userID] += info.damageTypes.Total();
                    currentDamage[0] += info.damageTypes.Total();
                    lastDamageTime = DateTime.Now;
                    //Puts("DEBUG: damaging heli for "+ info.damageTypes.Total());
                }
                if (heli != null)
                {
                    //don't allow heli to damage innocents
                    if (!HasSteaksInHeli((BaseEntity)entity))
                    {
                        if (IsTCHacking((BaseEntity)entity))
                        {
                            info.damageTypes.ScaleAll(200000.0f);
                        }
                        else
                        {
                            info.damageTypes.ScaleAll(0.0f);
                        }
                    }
                    //Puts("DEBUG: Heli is attacking " +entity+ " for "+ info.damageTypes.Total());
                }
            }

            //Puts("DEBUG: DAMAGE" + info.damageTypes.Total() + " --- " + info.WeaponPrefab + " ... " + info.Initiator);

            // mitigate napalm/fire damage to bystanderes
            if (info.Initiator != null && (info.Initiator.name.Contains("napalm") || info.Initiator.name.Contains("oilfireball")))
            {

                //Puts("DEBUG: heli fire? " + info.Initiator?.name);
                if (!HasSteaksInHeli((BaseEntity)entity))
                {
                    info.damageTypes.ScaleAll(0.0f);
                }
                return null;
            }

            // mitigate rocket damage to bystanderes
            if (info.WeaponPrefab != null && info.WeaponPrefab.name.Contains("rocket_heli"))
            {
                if (!HasSteaksInHeli((BaseEntity)entity))
                {
                    if (IsTCHacking((BaseEntity)entity))
                    {
                        info.damageTypes.ScaleAll(500.0f);
                    }
                    else
                    {
                        //Puts("DEBUG: rocket hit innocent, no damage" + entity);
                        info.damageTypes.ScaleAll(0.0f);
                    }
                }
                else
                {
                    info.damageTypes.ScaleAll(damageScale);
                }
                //Puts("DEBUG: heli rocket?" + info.damageTypes.Total() + " --- " + info.WeaponPrefab + " ... " + info.Initiator);
            }
            return null;
        }

        private object OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (info == null || info.HitEntity == null)
                return null;

            if (info.HitEntity == currentHeli)
            {
                if (!HasSteaksInHeli(attacker))
                {
                    SendReply(attacker, "You don't have permission to attack this heli!");
                    info.damageTypes.ScaleAll(0.0f);
                    return false;
                }
                if (!TCAuthVerification(attacker))
                {
                    SendReply(attacker, "You can only attack Heli in a base you're authed on!");
                    info.damageTypes.ScaleAll(0.0f);
                    return false;
                }
            }
            return null;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heliAi, BasePlayer target)
        {
            if (heliAi != currentHeliAI)
                return null;
            return HasSteaksInHeli(target) ? null : (object)false;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heliAi, BasePlayer target)
        {
            if (heliAi != currentHeliAI)
                return null;
            return HasSteaksInHeli(target) ? null : (object)false;
        }
        private object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity target)
        {
            if (turret?._heliAI != currentHeliAI)
                return null;
            return HasSteaksInHeli(target) ? null : (object)false;
        }

        // loot protection
        void OnEntitySpawned(BaseNetworkable entity)
        {
            //remove fire from heli (and possible heli), cause it is no use...
            if (entity.name.Contains("oilfireball") && !entity.IsDestroyed)
            {
                entity.Kill();
            }
            if (entity is PatrolHelicopter && (currentHeli == null || currentHeli != entity))
            {
                Puts("Killing public Heli with no owner");
                entity.Kill();
            }
            if (entity is HelicopterDebris)
            {
                var debris = entity as HelicopterDebris;
                if (debris == null) return;
                debris.Kill();
            }
        }
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            killUI(player);
            if (player && player == currentHeliOwner && currentHeli != null)
            {
                surrenderEnemy();
                Puts(player + " disconnected, active heli was surrendered");
            }
            if (player && storedData.TicketData.ContainsKey(player.userID))
            {
                refundTicket(player, storedData.TicketData[player.userID]);
                removeFromQueue(player);
                Puts(player + " disconnected, heli ticket was removed");
            }
        }

        // napalm will be fired with 25% chance, only on unauthed TCs it is 100
        // make sure owner has TC, to prevent team-hacks
        private bool CanHelicopterUseNapalm(PatrolHelicopterAI heli)
        {
            return TCAuthVerification(currentHeliOwner);
        }

        #endregion

        #region HelperFunctions
        private void initTitles()
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> d in Cfg.Titles)
            {
                ZNTitleManager?.Call("UpsertTitle", getTitleID(d.Key), "Heli '" + d.Key + "'", "Seconds", d.Value["m_eternal"], d.Value["f_eternal"], d.Value["m"], d.Value["f"]);
            }
        }

        private string getTitleID(string difficulty)
        {
            int index = difficulty.IndexOf("_");
            difficulty = (index > 0) ? difficulty.Substring(index + 1) : difficulty;
            return Plugin.Name + "_" + difficulty;
        }
        private void surrenderEnemy(bool force = false)
        {
            if (currentHeli != null)
            {
                if (!force && currentHeli.health == currentHeli.MaxHealth())
                {
                    SendReply(currentHeliOwner, "You can't surrender an undamaged Heli!");
                    return;
                }
                currentIsForceKill = true;
                SendReply(currentHeliOwner, "Your heli has been sent away.");
                currentHeli.Kill();
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
                int minRewardMultiplyer = (int)Math.Ceiling(Cfg.HeliLevels[currentHeliLevel]["minRewardMultiplyer"]);
                minRewardMultiplyer += (int)Math.Floor((currentTeamMemberIDs.Count - 1) * Cfg.TeamRewartPercentPerPlayer * minRewardMultiplyer);
                int maxRewardMultiplyer = (int)Math.Ceiling(Cfg.HeliLevels[currentHeliLevel]["maxRewardMultiplyer"]);
                maxRewardMultiplyer += (int)Math.Floor((currentTeamMemberIDs.Count - 1) * Cfg.TeamRewartPercentPerPlayer * maxRewardMultiplyer);
                string msg;
                if (maxRewardMultiplyer == 0)
                {
                    msg = "You finished a 'noloot' variant, there is only an RP reward.";
                }
                else
                {
                    msg = "Use <color=green>/loot</color> to get your reward.";
                    LootBoxSpawner.Call("storeRewardClaim", currentHeliOwner.userID, currentHeliLevel, minRewardMultiplyer, maxRewardMultiplyer, "heli");

                }
                SendReply(currentHeliOwner, msg);
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

        private void adjustHeliPower(PatrolHelicopter heli)
        {
            string heliLevel = currentHeliLevel;

            float damageScale = Cfg.HeliLevels[currentHeliLevel]["damageMultiplyer"];
            //solo player boost
            if (currentTeamMemberIDs.Count() == 1) damageScale -= 0.2f;
            int currentTeamSize = currentTeamMemberIDs.Count();

            float heliHealth = Cfg.HeliLevels[heliLevel]["heliHealth"];
            heliHealth += (float)(heliHealth * 0.2f * (currentTeamSize - 1f));
            heli._maxHealth = heliHealth;
            heli.health = heliHealth;
            heli.maxCratesToSpawn = 0;
            heli.bulletDamage *= damageScale;

            foreach (PatrolHelicopter.weakspot weakspot in heli.weakspots)
            {
                float weakhealth = (heliHealth / 100) * Cfg.HeliLevels[heliLevel]["heliWeakspotHealthPercent"];
                weakspot.maxHealth = weakhealth;
                weakspot.health = weakhealth;
            }

        }
        private Vector3 getRandomSpawn(BasePlayer player)
        {
            Vector3 playerPos = player.transform.position;
            int range = Cfg.HeliSpawnRadius;
            float spawnX = Random.Range(-range, range);
            spawnX = (spawnX < 0) ? spawnX - range : spawnX + range;
            float spawnZ = Random.Range(-range, range);
            spawnZ = (spawnZ < 0) ? spawnZ - range : spawnZ + range;

            return playerPos + new Vector3(spawnX, 40, spawnZ);
        }

        private static PatrolHelicopter InstantiateEntity(Vector3 position)
        {
            var prefabName = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
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
            return go.GetComponent<PatrolHelicopter>();
        }
        private bool CallHeliForPlayer(BasePlayer player, string heliLevel = "normal")
        {
            var heliPos = getRandomSpawn(player);

            //currentHeli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", new Vector3(), new Quaternion(), true) as PatrolHelicopter;
            currentHeli = InstantiateEntity(heliPos);
            if (!currentHeli) return false;
            currentHeliAI = currentHeli.GetComponent<PatrolHelicopterAI>();
            currentHeliOwner = player;
            currentHeli.Spawn();
            currentHeli.transform.position = heliPos;
            currentHeliSpawnTime = DateTime.Now;
            if (cooldownHelis.Contains(heliLevel)) lastPlayerHeli[player.userID] = DateTime.Now;
            currentHeliLevel = heliLevel;
            currentDamage.Add(0, 0f); // total damage counter
            lastDamageTime = DateTime.Now;
            lastHeliCallTime = DateTime.Now;
            // team related additions
            if (currentHeliOwner.currentTeam != 0UL)
            {
                RelationshipManager.PlayerTeam theTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                foreach (var member in theTeam.members)
                {
                    BasePlayer memberPlayer = BasePlayer.Find("" + member);
                    if (memberPlayer != null)
                        currentTeamMemberIDs.Add(member);
                    currentDamage.Add(member, 0f);
                    currentHeliAI._targetList.Add(new PatrolHelicopterAI.targetinfo((BaseEntity)memberPlayer, memberPlayer));
                }
            }
            else
            {
                currentTeamMemberIDs.Add(player.userID);
                currentDamage.Add(player.userID, 0f);
                currentHeliAI._targetList.Add(new PatrolHelicopterAI.targetinfo((BaseEntity)player, player));
            }
            adjustHeliPower(currentHeli);
            // make sure we always focus on the calling player and never go patrolling.
            currentHeliAI.SetTargetDestination(currentHeliOwner.transform.position);

            removeFromQueue(player);
            SetupGameTimer();
            foreach (var p in BasePlayer.activePlayerList)
            {
                SendReply(p, "Personal <color=#63ff64>" + heliLevel + "</color> helicopter was spawned for <color=#63ff64>" + player.displayName + " </color>.\nIt will not damage bystanders.");
            }
            ZNui?.Call("ToggleEvent", "heliEvent", true);
            return true;
        }


        private void SetupGameTimer()
        {
            heliRedirectionTimer = timer.Every(15f, () =>
            {
                //Puts("DEBUG: Timer run " + currentHeliAI._targetList.Count());
                currentHeliAI._targetList.Add(new PatrolHelicopterAI.targetinfo((BaseEntity)currentHeliOwner, currentHeliOwner));
            });

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
                if (!currentHeliOwner.IsConnected || (DateTime.Now - lastDamageTime).TotalSeconds > getNoDamageSurrenderTime())
                {
                    surrenderEnemy(true);
                }
            });

        }

        private int getNoDamageSurrenderTime()
        {
            float health = currentHeli.health;
            float maxHealth = currentHeli._maxHealth;

            foreach (PatrolHelicopter.weakspot weakspot in currentHeli.weakspots)
            {
                health += weakspot.health;
                maxHealth += weakspot.maxHealth;
            }

            int healthPercent = (int)Math.Ceiling((health / maxHealth) * 100);
            return 120 + ((100 - healthPercent) * 2);
        }
        private void forceHeliToPlayer(BasePlayer player)
        {
            if (currentHeli == null)
                return;

            int MaxHeliDistanceToPlayer = 200;
            currentHeliAI._targetList.Clear();
            currentHeliAI._targetList.Add(new PatrolHelicopterAI.targetinfo(player, player));
            float heliDistance = Vector3Ex.Distance2D(currentHeli.transform.position, player.transform.position);

            if (heliDistance > MaxHeliDistanceToPlayer)
            {
                if (currentHeliAI._currentState != PatrolHelicopterAI.aiState.MOVE || Vector3Ex.Distance2D(currentHeliAI.destination, player.transform.position) > MaxHeliDistanceToPlayer)
                {
                    currentHeliAI.ExitCurrentState();
                    var heliTarget = player.transform.position.XZ() + Vector3.up * 250;
                    RaycastHit hit;
                    if (Physics.SphereCast(player.transform.position.XZ() + Vector3.up * 600, 50, Vector3.down, out hit, 1500, Layers.Solid))
                    {
                        heliTarget = hit.point + Vector3.up * 20;
                    }
                    Puts($"Forcing helicopter {currentHeli.transform.position} to player {player.displayName}, pos {heliTarget}");
                    currentHeliAI.State_Move_Enter(heliTarget);
                    currentHeli.transform.position = heliTarget;
                    lastHeliCallTime = DateTime.Now;
                }
                SendReply(player, "Heli was called to you!");
            }
            else
            {
                SendReply(player, "Heli is still to close to be called. (" + heliDistance + "m, needs " + MaxHeliDistanceToPlayer + "m)");
            }
        }

        private void notifyNextPlayer()
        {
            if (storedData.HeliQueue.Count > 0)
            {
                ulong nextTicket = storedData.HeliQueue[0];
                BasePlayer nextPlayer = FindPlayer(nextTicket);
                if (nextPlayer != null)
                {
                    SendReply(nextPlayer, "<color=green>Your heli</color> can now be called!\nUse <color=blue>/heli spawn</color> now.");
                }
                return;
            }
        }

        private void cleanupAfterHeli()
        {
            currentUITimer?.Destroy();
            resetUI();
            heliRedirectionTimer.Destroy();
            currentHeli = (PatrolHelicopter)null;
            currentHeliOwner = (BasePlayer)null;
            currentHeliAI = (PatrolHelicopterAI)null;
            currentHeliLevel = string.Empty;
            currentHeliSpawnTime = DateTime.Now;
            currentTeamMemberIDs.Clear();
            currentDamage = new Dictionary<ulong, float>();
            lastHeliKillTime = DateTime.Now;
            lastHeliCallTime = DateTime.Now;
            currentIsHighscore = false;
            currentIsForceKill = false;
            notifyNextPlayer();
            ZNui?.Call("ToggleEvent", "heliEvent", false);
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

        private bool SoloCheckSuccess(BasePlayer player)
        {
            if (!IsSoloPlayer(player))
            {
                SendReply(player, "You can't have a team or clan of more than 3 for this!");
                return false;
            }
            return true;
        }
        private bool IsSoloPlayer(BasePlayer player)
        {
            if (player.currentTeam != 0UL) return false;
            if (Clans != null)
            {
                string clanTag = Clans?.Call<string>("GetClanOf", player.userID);
                if (!String.IsNullOrEmpty(clanTag))
                {
                    object clan = Clans?.Call<JObject>("GetClan", clanTag);
                    if (clan != null && clan is JObject)
                    {
                        JToken members = (clan as JObject).GetValue("members");
                        if (members != null && members is JArray)
                        {
                            if (members.Count() > 3) return false;
                        }
                    }
                }
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
            if (currentHeliOwner == null || currentHeli == null)
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

        private bool HasSteaksInHeli(BaseEntity entity)
        {
            //return false;
            if (!(entity is BasePlayer) && !(entity is DecayEntity))
            {
                return false;
            }
            // public safety
            if (currentHeliOwner == null || currentHeli == null)
            {
                return false;
            }

            if (entity is BasePlayer)
            {
                // owner can attack / be attacked
                if (entity == currentHeliOwner)
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
                if (entity.OwnerID == currentHeliOwner.userID)
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
                        if (entry.userid == currentHeliOwner.userID)
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
            if (!storedData.HeliQueue.Contains(player.userID))
            {
                storedData.HeliQueue.Add(player.userID);
                storedData.TicketData.Add(player.userID, level);
                SaveData();
                return true;
            }
            else
            {
                SendReply(player, "You can only have one heli ticket at a time.");
                return false;
            }

        }
        private void removeFromQueue(BasePlayer player)
        {
            storedData.HeliQueue.Remove(player.userID);
            storedData.TicketData.Remove(player.userID);
            SaveData();
        }
        private void removeFromQueue(ulong playerId)
        {
            storedData.HeliQueue.Remove(playerId);
            storedData.TicketData.Remove(playerId);
            SaveData();
        }
        private void refundTicket(BasePlayer player, string heliLevel)
        {
            if (!ServerRewards)
                return;

            ServerRewards?.Call("AddPoints", player.userID, (int)Cfg.HeliLevels[heliLevel]["refundPrice"]);
            SendReply(player, "Heli ticket price refunded.");
        }
        private void refundSurrender(BasePlayer player, string heliLevel)
        {
            if (!ServerRewards)
                return;

            ServerRewards?.Call("AddPoints", player.userID, (int)(Math.Ceiling(Cfg.HeliLevels[heliLevel]["refundPrice"] / 2)));
            SendReply(player, "50% of heli price refunded for surrender.");
        }

        private void rewardTeam()
        {
            if (currentDamage.Count == 0 || currentDamage[0] <= 0 || currentHeliLevel == null)
                return;

            string msg = "<color=orange>You took down your Heli!</color>";
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
                    int reward = (int)Math.Floor(Cfg.HeliLevels[currentHeliLevel]["rewardPoints"] * ratio);
                    if (currentIsHighscore)
                    {
                        reward += (int)Math.Floor(Cfg.HeliLevels[currentHeliLevel]["rewardPointsHighscore"] * ratio);
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
                    int xpreward = (int)Math.Floor(Cfg.HeliLevels[currentHeliLevel]["experience"] * ratio);
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
            if (currentHeliSpawnTime == null || currentIsForceKill || currentHeliLevel == null)
                return;

            float fightDuration = (float)((DateTime.Now - currentHeliSpawnTime).TotalSeconds);
            lastWinTime = fightDuration;
            int teamSize = ((currentTeamMemberIDs.Count > 0) ? currentTeamMemberIDs.Count : 1);

            if (teamSize == 1)
            {
                ZNTitleManager?.Call("CheckScore", getTitleID(currentHeliLevel), fightDuration, currentHeliOwner.userID, true);
            }


            if (highscoreData.HighscoreTimes.ContainsKey(currentHeliLevel)
                && highscoreData.HighscoreTimes[currentHeliLevel].ContainsKey(teamSize)
                && highscoreData.HighscoreTimes[currentHeliLevel][teamSize] <= fightDuration)
            {
                // no highscore
                return;
            }

            // New Highscore!!!
            string playerNamesString = currentHeliOwner.displayName;
            currentIsHighscore = true;
            // team player
            if (teamSize > 1)
            {
                foreach (ulong teamMember in currentTeamMemberIDs)
                {
                    if (teamMember != currentHeliOwner.userID)
                    {
                        playerNamesString += " - " + FindPlayer(teamMember).displayName;
                    }
                }
            }

            if (!highscoreData.HighscoreTimes.ContainsKey(currentHeliLevel))
            {
                highscoreData.HighscoreTimes.Add(currentHeliLevel, new Dictionary<int, float>());
                highscoreData.HighscorePlayer.Add(currentHeliLevel, new Dictionary<int, string>());
                SaveData();
            }
            if (!highscoreData.HighscoreTimes[currentHeliLevel].ContainsKey(teamSize))
            {
                // first entry is automatic highscore
                highscoreData.HighscoreTimes[currentHeliLevel].Add(teamSize, fightDuration);
                highscoreData.HighscorePlayer[currentHeliLevel].Add(teamSize, playerNamesString);
            }
            else
            {
                highscoreData.HighscoreTimes[currentHeliLevel][teamSize] = fightDuration;
                highscoreData.HighscorePlayer[currentHeliLevel][teamSize] = playerNamesString;
            }
            SaveData();

            foreach (var p in BasePlayer.activePlayerList)
            {
                SendReply(p, "NEW HIGHSCORE: <color=#63ff64>" + currentHeliLevel + "</color> heli (" + teamSize + "-Player): " + fightDuration + "s \nby: <color=#63ff64>" + playerNamesString + "</color>");
            }
            return;
        }

        private void displayHighscore(BasePlayer player, string heliLevel)
        {
            //NextTick(() =>
            //{
            // Destroy existing UI
            var mainName = "HeliUI";

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
            foreach (string levelCfg in Cfg.HeliLevels.Keys)
            {
                if (levelCfg == "demo")
                    continue;
                i++;
                elements.Add(new CuiButton
                {
                    Button =
                        {
                            Command = "helifromhell.highscore "+ levelCfg,
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
                            Text = "Heli Highscore (Level: " + heliLevel +")",
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
            if (highscoreData.HighscoreTimes.ContainsKey(heliLevel))
            {
                i = 1;
                foreach (KeyValuePair<int, float> highscore in highscoreData.HighscoreTimes[heliLevel])
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
                                Text = highscoreData.HighscorePlayer[heliLevel][highscore.Key],
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
                    AnchorMin = "0.0 0.74",
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
                    AnchorMin = "0.05 0.75",
                    AnchorMax = "0.5 1"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Core Health:",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0.50",
                    AnchorMax = "0.5 0.75"
                }
            }, mainName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Weakpoints:",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.05 0.25",
                    AnchorMax = "0.5 0.50"
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
                    AnchorMax = "0.5 0.25"
                }
            }, mainName);
            if (currentHeli != null)
            {

                var secondsFighting = Math.Floor((DateTime.Now - currentHeliSpawnTime).TotalSeconds);
                var secondsTilSurrender = Math.Floor(getNoDamageSurrenderTime() - (DateTime.Now - lastDamageTime).TotalSeconds);
                string msg = "";
                foreach (PatrolHelicopter.weakspot weakspot in currentHeli.weakspots)
                {
                    msg += "(" + Math.Ceiling(weakspot.health) + "/" + weakspot.maxHealth + ") \n";
                }

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
                        AnchorMin = "0.5 0.75",
                        AnchorMax = "0.95 1"
                    }
                }, mainName);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = Math.Ceiling(currentHeli.health) + "/" + currentHeli._maxHealth,
                        FontSize = 12,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.50",
                        AnchorMax = "0.95 0.75"
                    }
                }, mainName);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = msg,
                        FontSize = 8,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.25",
                        AnchorMax = "0.95 0.50"
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
                        AnchorMax = "0.95 0.25"
                    }
                }, mainName);
            }

            CuiHelper.AddUi(player, elements);
        }

        #endregion
    }
}
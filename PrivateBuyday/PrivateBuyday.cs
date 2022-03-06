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
using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PrivateBuyday", "DocValerian", "1.3.0")]
    class PrivateBuyday : RustPlugin
    {
        static PrivateBuyday Plugin;

        [PluginReference] Plugin NightVision, ServerRewards;

        private const string permUse = "privatebuyday.use";
        private const string permFree = "privatebuyday.free";
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
            [JsonProperty(PropertyName = "Server Rewards Price per time_stack")]
            public int CostPerTimeIncrement = 666;
            [JsonProperty(PropertyName = "Time in Minutes per time_stack")]
            public int TimeIncremetMinutes = 10;

            [JsonProperty(PropertyName = "default /day set time to (0.0 -> 24.0)")]
            public float dayTime = 12.0f;

            [JsonProperty(PropertyName = "default /night set time to (0.0 -> 24.0)")]
            public float nightTime = 0.0f;
        }

        class StoredData
        {
            public Dictionary<ulong, DateTime> BuyDayBudget = new Dictionary<ulong, DateTime>();
        }
        Timer cleanupTimer;

        StoredData storedData;
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("PrivateBuyDay_Budget", storedData);
        }

        #endregion

        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permFree, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("PrivateBuyDay_Budget");
            StartCleanupTimer();
            SaveData();
        }
        private void OnServerInitialized()
        {
            if (!NightVision)
            {
                Debug.LogError("[PrivateBuyday] Error! NightVision not loaded! Please fix this error before loading PrivateBuyday again. Unloading...");
                Interface.Oxide.UnloadPlugin(this.Title);
                return;
            }
        }
        private void Unload() {
            cleanupTimer.Destroy();
            SaveData();
        }
        // auto wipe budgets
        private void OnNewSave() {
            storedData = new StoredData();
            SaveData();
        }



        private void showUsageMessage(BasePlayer player)
        {
            bool isFree = HasPermission(player.UserIDString, permFree);
            string msg = "<color=orange>=============== Buy Day ================</color>";
            msg += "\n- Switch day on and off, whenever you like.";
            if (!isFree)
            {
                msg += "\n- Buy the option to make it day (only) for yourself.";
                msg += "\n- You buy the ability to switch until a point in realtime!";
                msg += "\n- <color=red>The timer is independent of usage!</color>";
            }
            msg += "\n- Effect is only visual and will not change night-effects!";
            msg += "\n\n<color=green>Usage:</color>";
            msg += "\n/day \t\t\t\tShow this info";
            if (!isFree)
            {
                msg += "\n/day buy\t\t\tShow buying details";
                msg += "\n/day buy <stacks>\tBuy ability for stacks*" + Cfg.TimeIncremetMinutes + "minutes";
                msg += "\n/day info \t\t\tShow how long your ability will last";
            }
            msg += "\n/day on \t\t\tTurn on forced daylight";
            msg += "\n/day on 18.5\t\tTurn on forced daylight with time 18.5";
            msg += "\n/day off \t\t\tReturn to normal light";
            msg += "\n/night on \t\t\tTurn on forced nighttime";
            msg += "\n/night off \t\t\tReturn to normal light";

            if (player.IsAdmin){
                msg += "\n\n<color=red>=== Admin Commands</color>";
                msg += "\n/day admin \t\tTells you how many users exist";
            }
            SendReply(player, msg);
        }
        private void showBuyMessage(BasePlayer player)
        {
            string msg = "<color=orange>=============== Buy Day ================</color>";
            msg += "\n- Buy the option to make it day (only) for yourself.";
            msg += "\n- Stacks will extend the current timer.";
            msg += "\n- <color=red>The timer is independent of usage!</color>";
            msg += "\n- One stack adds:\t" + Cfg.TimeIncremetMinutes +" minutes.";
            msg += "\n- Price per stack:\t\t" + Cfg.CostPerTimeIncrement +" RP.";
            msg += "\n\n<color=green>Examples:</color>";
            msg += "\n/day buy 1\t\t\t" + Cfg.TimeIncremetMinutes +" minutes, "+ Cfg.CostPerTimeIncrement +" RP";
            msg += "\n/day buy 2\t\t\t" + 2*Cfg.TimeIncremetMinutes +" minutes, "+ 2*Cfg.CostPerTimeIncrement +" RP";
            msg += "\n/day buy 5\t\t\t" + 5*Cfg.TimeIncremetMinutes +" minutes, "+ 5*Cfg.CostPerTimeIncrement +" RP";
            int incrementPerDay = (int)Math.Ceiling(1440d / Cfg.TimeIncremetMinutes);
            msg += "\n/day buy "+incrementPerDay+"\t\tFull day (24h), "+ incrementPerDay*Cfg.CostPerTimeIncrement +" RP";
            SendReply(player, msg);
        }
        [ChatCommand("night")]
        private void CmdNightMain(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player.UserIDString, permUse))
            {
                SendReply(player, "You don't have permission to use this command (did you unlock /day it in /p?)");
                return;
            }
            if (args.Length == 0)
            {
                showUsageMessage(player);
                return;
            }

            if (args.Length == 1)
            {
                KeyValuePair<int, DateTime> budget;
                switch (args[0])
                {
                    case "on":
                        budget = getBudget(player);
                        if (budget.Key == 0 && !HasPermission(player.UserIDString, permFree))
                        {
                            SendReply(player, "You need to use <color=green>/day buy</color> to buy ability time for RP.");
                        }
                        else
                        {
                            LockPlayerTime(player, Cfg.nightTime);
                            SendReply(player, "Night mode <color=green>activated</color>.");
                        }
                        break;
                    case "off":
                        budget = getBudget(player);
                        if (budget.Key == 0 && !HasPermission(player.UserIDString, permFree))
                        {
                            SendReply(player, "You need to use <color=green>/day buy</color> to buy ability time for RP.");
                        }
                        else
                        {
                            UnlockPlayerTime(player);
                            SendReply(player, "Night mode <color=red>deactivated</color>.");
                        }
                        break;
                    default:
                        showUsageMessage(player);
                        break;
                }
            }
            else
            {
                showUsageMessage(player);
            }
        }

        [ChatCommand("day")]
        private void CmdDayMain(BasePlayer player, string command, string[] args) 
        {
            if(!HasPermission(player.UserIDString, permUse))
            {
                SendReply(player, "You don't have permission to use this command (did you unlock it in /p?)");
                return;
            }
            if (args.Length == 0)
            {
                showUsageMessage(player);
                return;
            }
            
            if (args.Length == 1)
            {
                KeyValuePair<int, DateTime> budget;
                switch(args[0])
                {
                    case "buy":
                        if (HasPermission(player.UserIDString, permFree))
                        {
                            SendReply(player, "You have the FREE /day skill. Just use <color=green>/day on</color>!");
                        }
                        else
                        {
                            showBuyMessage(player);
                        }
                    break;
                    case "info":
                        string msg = "<color=orange>=============== Buy Day ================</color>";
                        budget = getBudget(player);
                        if(budget.Key == 0)
                        {
                            msg += "\nYou currently don't have the ability.";    
                        }
                        else
                        {
                            msg += "\nThe switch ability is active until <color=green>"+budget.Value+" GMT</color>";
                            msg += "\nYou can switch day for <color=green>"+budget.Key+"</color> more minutes";
                            msg += "\n\nYou can extend the time with /day buy";
                        }
                        SendReply(player, msg);
                    break;
                    case "on":
                        budget = getBudget(player);
                        if(budget.Key == 0 && !HasPermission(player.UserIDString, permFree))
                        {
                            SendReply(player, "You need to use <color=green>/day buy</color> to buy ability time for RP.");
                        }
                        else
                        {
                            LockPlayerTime(player, Cfg.dayTime);
                            SendReply(player, "Day mode <color=green>activated</color>.");
                        }
                    break;
                    case "off":
                        budget = getBudget(player);
                        if(budget.Key == 0 && !HasPermission(player.UserIDString, permFree))
                        {
                            SendReply(player, "You need to use <color=green>/day buy</color> to buy ability time for RP.");
                        }
                        else
                        {
                            UnlockPlayerTime(player);
                            SendReply(player, "Day mode <color=red>deactivated</color>.");
                        }
                    break;
                    case "admin":
                        if(player.IsAdmin){
                            SendReply(player, "There are currently <color=green>"+storedData.BuyDayBudget.Count+"</color> users with day time budget.");
                        }
                    break;
                    default:
                        showUsageMessage(player);
                    break;
                }
            }
            if(args.Length == 2){
                // new /day on 15.00 feature
                if (args[0] == "on")
                {
                    KeyValuePair<int, DateTime> budget;
                    budget = getBudget(player);
                    if (budget.Key == 0 && !HasPermission(player.UserIDString, permFree))
                    {
                        SendReply(player, "You need to use <color=green>/day buy</color> to buy ability time for RP.");
                    }
                    else
                    {
                        float time = float.Parse(args[1]);
                        if(time < 0f || time > 24f)
                        {
                            showUsageMessage(player);
                            return;
                        }
                        LockPlayerTime(player, time);
                        SendReply(player, "Day mode <color=green>activated</color> with "+time+" o'clock lighting.");
                    }
                    return;
                }
                // the /day buy option
                if (args[0] != "buy")
                {
                    showUsageMessage(player);
                    return;
                }
                if(HasPermission(player.UserIDString, permFree))
                {
                    SendReply(player, "You have the FREE /day skill. Just use <color=green>/day on</color>!");
                    return;
                }
                int buyStacks = 0;
                try
                {
                    buyStacks = int.Parse(args[1]);
                }
                catch (FormatException)
                {
                    showUsageMessage(player);
                    return;
                }
                int minutesBought = buyStacks*Cfg.TimeIncremetMinutes;
                int buyPrice = buyStacks*Cfg.CostPerTimeIncrement;

                object bal = ServerRewards?.Call("CheckPoints", player.userID);
                if(bal == null) return;
                int playerRP = (int)bal;
                if (playerRP < buyPrice)
                {
                    SendReply(player, "It costs " + buyPrice + " RP to buy " + minutesBought + " minutes, you only have " + playerRP);
                    return;
                }
                else
                {
                    ServerRewards?.Call("TakePoints", player.userID, buyPrice);
                    DateTime budget = addBudget(player, minutesBought);
                    SendReply(player, "You paid " + buyPrice + " RP. Day until: " + budget + " (server time)");
                    LockPlayerTime(player, 12.0f);
                    SendReply(player, "Day mode <color=green>activated</color>.");
                }

            }
        }

        private DateTime addBudget(BasePlayer player, int minutes)
        {
            if(storedData.BuyDayBudget.ContainsKey(player.userID)){
                storedData.BuyDayBudget[player.userID] = storedData.BuyDayBudget[player.userID].AddMinutes(minutes);
            } 
            else 
            {
                storedData.BuyDayBudget.Add(player.userID, DateTime.Now.AddMinutes(minutes));
            }
            SaveData();
            return storedData.BuyDayBudget[player.userID];
        }

        private KeyValuePair<int, DateTime> getBudget(BasePlayer player)
        {
            if(storedData.BuyDayBudget.ContainsKey(player.userID) && storedData.BuyDayBudget[player.userID] > DateTime.Now){
                TimeSpan span = storedData.BuyDayBudget[player.userID].Subtract( DateTime.Now );
                return new KeyValuePair<int, DateTime>((int)Math.Ceiling(span.TotalMinutes), storedData.BuyDayBudget[player.userID]);
            } 
            else 
            {
                return new KeyValuePair<int, DateTime>(0, DateTime.Now);
            }
        }
        // API for ZNExperience
        private void verifyDayVision(BasePlayer player)
        {
            if(!storedData.BuyDayBudget.ContainsKey(player.userID) && !HasPermission(player.UserIDString, permFree))
            {
                UnlockPlayerTime(player);
            }
        }
        // ToDo - replace with coroutine
        private void StartCleanupTimer(){
            cleanupTimer = timer.Every(60f, () =>
            {
                Dictionary<ulong, DateTime> copy = new Dictionary<ulong, DateTime>(storedData.BuyDayBudget);
                foreach(KeyValuePair<ulong, DateTime> budget in copy)
                {
                    VisionTimerLogic(budget);
                }
            });
        }

        private void VisionTimerLogic(KeyValuePair<ulong, DateTime> budget)
        {
            if(budget.Value < DateTime.Now)
            {
                BasePlayer player = BasePlayer.FindByID(budget.Key);
                if (player == null) {
                    player = BasePlayer.FindSleeping(budget.Key);
                }
                if (player == null) {
                    //Puts("Could not find player for " + budget.Key);
                    return;
                }
                storedData.BuyDayBudget.Remove(budget.Key);
                SaveData();
                UnlockPlayerTime(player);
            }
        }

        #region NightVision Plugin API 2.2.01

        public void LockPlayerTime(BasePlayer player, float time, float fog = -1, float rain = -1)
        {
            var args = Core.ArrayPool.Get(4);
            args[0] = player;
            args[1] = time;
            args[2] = fog;
            args[3] = rain;
            NightVision?.CallHook("LockPlayerTime", args);
            Core.ArrayPool.Free(args);
        }

        public void UnlockPlayerTime(BasePlayer player)
        {
            var args = Core.ArrayPool.Get(1);
            args[0] = player;
            NightVision?.CallHook("UnlockPlayerTime", args);
            Core.ArrayPool.Free(args);
        }

        #endregion
       
        
    }
}   
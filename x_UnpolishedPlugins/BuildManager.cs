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
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("BuildManager", "DocValerian", "1.12.5")]
    class BuildManager : RustPlugin
    {
        static BuildManager Plugin;
        
        [PluginReference]
        private Plugin ServerRewards, PatchesForPVE, HeliFromHell, BradleyFromHell, ZombieFromHell, ZNExperience;

        static ConfigFile Cfg = new ConfigFile();
        private const string permAgrade = "buildmanager.agrade.use";
        private const string permAgradeFree = "buildmanager.agrade.free";
        private const string permRepall = "buildmanager.repairall.use";
        private const string permRepallFree = "buildmanager.repairall.free";
        private const string permException = "buildmanager.exception.manage";
        private const string permUnlimited = "buildmanager.unlimited.use";
        private const string permExtended_1 = "buildmanager.extended.t1";
        private const string permExtended_2 = "buildmanager.extended.t2";
        private const string permExtended_3 = "buildmanager.extended.t3";
        private const string permExtended_4 = "buildmanager.extended.t4";
        private const string permExtended_5 = "buildmanager.extended.t5";
        static string[] foundationPrefabNames = {"foundation", "foundation.triangle"};
        static string[] heightPrefabNames = {"wall", "wall.half", "wall.low", "wall.frame", "wall.window", "wall.doorway", "roof", "block.stair.ushape", "block.stair.lshape", "block.stair.spiral", "block.stair.spiral.triangle" };
        static string[] halfHeightPrefabNames = {"wall.half", "wall.low"};
        static string[] buildingPrefabNames = {"foundation", "foundation.triangle","wall", "wall.half", "wall.low", "wall.frame", "wall.window", "wall.doorway", "roof", "block.stair.ushape", "block.stair.lshape", "floor", "floor.frame", "floor.triangle"};
        int tcMasks = LayerMask.GetMask("Deployed");
        class ConfigFile
        {
            public Dictionary<string, Dictionary<string, int>> Limits = new Dictionary<string, Dictionary<string, int>>
            {
                ["town"] = new Dictionary<string, int>
                {
                    ["foundations"] = 10,
                    ["height"] = 2,
                    ["minowntcdistance"] = 200,
                },
                ["exception"] = new Dictionary<string, int>
                {
                    ["foundations"] = 800,
                    ["height"] = 50,
                    ["minowntcdistance"] = 200,
                },
                ["default"] = new Dictionary<string, int>
                {
                    ["foundations"] = 30,
                    ["height"] = 3,
                    ["minowntcdistance"] = 200,
                },
                ["buildmanager.extended.t1"] = new Dictionary<string, int>
                {
                    ["foundations"] = 50,
                    ["height"] = 5,
                    ["minowntcdistance"] = 200,
                },
                ["buildmanager.extended.t2"] = new Dictionary<string, int>
                {
                    ["foundations"] = 80,
                    ["height"] = 7,
                    ["minowntcdistance"] = 200,
                },
                ["buildmanager.extended.t3"] = new Dictionary<string, int>
                {
                    ["foundations"] = 120,
                    ["height"] = 12,
                    ["minowntcdistance"] = 200,
                },
                ["buildmanager.extended.t4"] = new Dictionary<string, int>
                {
                    ["foundations"] = 160,
                    ["height"] = 15,
                    ["minowntcdistance"] = 200,
                },
                ["buildmanager.extended.t5"] = new Dictionary<string, int>
                {
                    ["foundations"] = 800,
                    ["height"] = 50,
                    ["minowntcdistance"] = 200,
                },
                ["buildmanager.unlimited.use"] = new Dictionary<string, int>
                {
                    ["foundations"] = 5000,
                    ["height"] = 30,
                    ["minowntcdistance"] = 10,
                },
            };
            public Dictionary<string, string> BannedItemsAlternatives = new Dictionary<string, string>()
            {
                ["electric.windmill.small"] = "Test Generator (in /trade)",
                ["mining_quarry"] = "/mm",

            };
            public Dictionary<string, List<string>> AllowedDeployables = new Dictionary<string, List<string>>
            {
                ["default"] = new List<string>{ },
                ["town"] = new List<string> { },
                ["zoneID"] = new List<string> { },
                ["buildmanager.extended.t1"] = new List<string> { },
                ["buildmanager.extended.t2"] = new List<string> { },
                ["buildmanager.extended.t3"] = new List<string> { },
                ["buildmanager.extended.t4"] = new List<string> { },
                ["buildmanager.extended.t5"] = new List<string> { },
                ["buildmanager.unlimited.use"] = new List<string> { },
            };
            public Dictionary<string, List<string>> ForbiddenDeployables = new Dictionary<string, List<string>>
            {
                ["default"] = new List<string> {},
                ["town"] = new List<string>
                {
                    "wall.external.high.wood",
                    "wall.external.high.stone",
                    "gates.external.high.wood",
                    "gates.external.high.stone",
                    "watchtower.wood",
                    "teslacoil.deployed",
                    "autoturret_deployed",
                    "laserlight.deployed",
                    "discofloor.deployed",
                    "electric.flasherlight.deployed",
                },
                ["zoneID"] = new List<string>{},
                ["buildmanager.extended.t1"] = new List<string> { },
                ["buildmanager.extended.t2"] = new List<string> { },
                ["buildmanager.extended.t3"] = new List<string> { },
                ["buildmanager.extended.t4"] = new List<string> { },
                ["buildmanager.extended.t5"] = new List<string> { },
                ["buildmanager.unlimited.use"] = new List<string> { },
            };
            public bool Debug = false;
            public bool KillBiggerBases = false;

            public int RepairAllPricePerEntity = 5;
            public float RepairAllResMult = 1.5f;
            public float RepairAllFreeResMult = 1.1f;
            public int AgradePricePerEntity = 5;
            public float AgradeResMult = 1.5f;
            public float AgradeFreeResMult = 1.1f;
        }
        class StoredData
        {
            public List<ulong> ExceptionTcIds = new List<ulong>();
        }
        
        StoredData storedData;

        private Dictionary<ulong, DateTime> globalWoodRefundTimer = new Dictionary<ulong, DateTime>();
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>();
            LoadMessages();
            Plugin = this;
            permission.RegisterPermission(permAgrade, this);
            permission.RegisterPermission(permAgradeFree, this);
            permission.RegisterPermission(permRepall, this);
            permission.RegisterPermission(permRepallFree, this);
            permission.RegisterPermission(permException, this);
            permission.RegisterPermission(permUnlimited, this);
            permission.RegisterPermission(permExtended_1, this);
            permission.RegisterPermission(permExtended_2, this);
            permission.RegisterPermission(permExtended_3, this);
            permission.RegisterPermission(permExtended_4, this);
            permission.RegisterPermission(permExtended_5, this);
            globalWoodRefundTimer = new Dictionary<ulong, DateTime>();

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("BuildManager");
            SaveData();
        }


        private void OnServerInitialized()
        {
            initSkills();
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file...");
            Config.WriteObject(Cfg, true);
        }
        
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("BuildManager", storedData);
        }

        void LoadMessages()
        {
            Dictionary<string, string> msg = new Dictionary<string, string>
            {
				["NoBuildingPrivilege"] = "You are not within a building privlege.",
				["NoAuthorization"] = "You are not authorized in this building privlege.",
				["Line"] = "----------------------------------",
				["BlockCnt"] = "Total Building Blocks: {0}",
				["FoundationCnt"] = "Foundations (all): {0}",
				["HeightCnt"] = "Building height: {0}"
            };
            lang.RegisterMessages(msg, this);
        }


        private void initSkills()
        {
            initSkill(0, 0);
            initSkill(1, 2);
            initSkill(2, 3);
            initSkill(3, 3, true);
            initSkill(4, 4, true);
            initSkill(5, 10, true);

            ZNExperience.Call("SaveSkillSetup");
        }
        private void initSkill(int lvl, int cost, bool prestige = false)
        {
            string id = "buildmanager.extended.t" + lvl;
            string name = "Building";
            string groupId = "abilities";
            int spCost = cost;
            List<string> permissionEffect = new List<string>();
            string description = "";
            string followUpSkillId = "";
            string prerequisiteSkillId = "";
            bool isDefault = false;
            string prestigeUnlockId = "";
            string iconURL = "";

            Dictionary<string, int> limits = Cfg.Limits[(lvl != 0) ? id : "default"];

            description = "Increases your building limits to <color=green>" + limits["foundations"] + "</color> foundations and <color=green>" + limits["height"] + "</color> height for YOUR TCs. (except for /town area)\nCheck your limits with with <color=green>/cb</color>";
            followUpSkillId = "buildmanager.extended.t" +  (lvl + 1);
            if (lvl == 0)
            {
                isDefault = true;
                name += "  Base";
            }
            else
            {
                isDefault = false;
                name += "  Lv." + lvl;
                permissionEffect = new List<string> { id };
                prerequisiteSkillId = "buildmanager.extended.t" + (lvl - 1);
                if (lvl == 5)
                {
                    followUpSkillId = "";
                }
            }
            if (prestige)
            {
                prestigeUnlockId = "pBuild";
            }
            ZNExperience.Call("RegisterSkill", id, name, groupId, spCost, permissionEffect, description, prerequisiteSkillId, followUpSkillId, isDefault, prestigeUnlockId, iconURL);
           
        }
        #region hooks

        //auto upgrade twig - based on BGrade by "Ryan", "1.0.49"
        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            var player = plan?.GetOwnerPlayer();
            var buildingBlock = gameObject.GetComponent<BuildingBlock>();
            if (player == null || buildingBlock == null)
            {
                return;
            }

            if (!player.CanBuild() || buildingBlock.grade != BuildingGrade.Enum.Twigs)
            {
                return;
            }

            BuildingManager.Building b = buildingBlock.GetBuilding();
            if (b == null || HasPermission(player.UserIDString, permUnlimited))
            {
                return;
            }
            if(
                b.buildingBlocks.Count() >= 21 
                && (b.GetDominatingBuildingPrivilege() == null || (!b.GetDominatingBuildingPrivilege().IsAuthed(player))) 
              )
            {
                znBuildMsg(player, "<color=red>(error)</color> You must place a <color=green>Tool Cupboard (TC)</color> after 20 blocks to prevent spam.\n<color=#999999>It can be repositioned later with /remove</color>");
                buildingBlock.Kill(BaseNetworkable.DestroyMode.None);
            }
           // Puts("DEBUG: built " + buildingBlock.PrefabName);
            if (
                b.buildingBlocks.Count() >= 21 && !buildingBlock.PrefabName.Contains("roof")
              )
            {
                Dictionary<int, int> itemsToTake;
                var resourceResponse = TakeResources(player, 1, buildingBlock, out itemsToTake);
                if (!string.IsNullOrEmpty(resourceResponse))
                {
                    SendReply(player, "Not enough resources!");
                    refundBuildingBlock(player, buildingBlock);
                    buildingBlock.Kill();
                    if (globalWoodRefundTimer.ContainsKey(player.userID))
                    {
                        globalWoodRefundTimer[player.userID] = DateTime.Now;
                    }
                    else
                    {
                        globalWoodRefundTimer.Add(player.userID, DateTime.Now);
                    }

                    return;
                }

                foreach (var itemToTake in itemsToTake)
                {
                    if (player.inventory.Take(null, itemToTake.Key, itemToTake.Value) > 0)
                    {
                        player.SendConsoleCommand("note.inv", itemToTake.Key, itemToTake.Value * -1);
                    }
                }

                buildingBlock.SetGrade(BuildingGrade.Enum.Wood);
                buildingBlock.SetHealthToMax();
                buildingBlock.StartBeingRotatable();
                buildingBlock.SendNetworkUpdate();
                buildingBlock.UpdateSkin();
                buildingBlock.ResetUpkeepTime();
                buildingBlock.GetBuilding()?.Dirty();
            }

        }

        // prevent placing TC on oversized bases and connecting partial bases
        private void OnEntitySpawned(BaseEntity entity, UnityEngine.GameObject gameObject)
        {
            /*
            if (entity.ShortPrefabName.Contains("xmas.advanced.lights"))
            {
                
                NextTick(() =>
                {
                   //Puts("DEBUG: adding xmaslight " + entity.OwnerID);
                   entity.Kill(BaseNetworkable.DestroyMode.None);
                });

            }
            */
            if(!entity.ShortPrefabName.Contains("cupboard.tool") && !buildingPrefabNames.Contains(entity.ShortPrefabName)) return;
            if(entity.OwnerID == 0) return;
            BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
            if (player == null) return;
            
            if(player.IsSleeping() == true || player.IsConnected == false || HasPermission(player.UserIDString, permUnlimited)) return;



            BuildingPrivlidge privilege = entity.GetBuildingPrivilege();
            

            // CHECK for TC distance to other TCs (of player) first
            if(entity.ShortPrefabName.Contains("cupboard.tool") && !checkTCdistanceOk(player, privilege)){
                NextTick(() =>
                {
                    entity.Kill(BaseNetworkable.DestroyMode.None);
                });
                Item tcitm = ItemManager.CreateByName("cupboard.tool", 1);
                player.GiveItem(tcitm, BaseEntity.GiveItemReason.Generic);
                return;
            }
            if (!privilege) return;
            BuildingManager.Building building = privilege.GetBuilding();
            
            Dictionary<string, float> dimensions = getBuildingDimensions(building);
            string currentZone = getCurrentZone(player, privilege);
            
            if(dimensions["foundationsCount"] > Cfg.Limits[currentZone]["foundations"])
            {
                //killBuilding(building, entity.OwnerID);
                NextTick(() =>
                {
                    entity.Kill(BaseNetworkable.DestroyMode.None);
                });
                znBuildMsg(player, "You are trying to circumvent the foundation limit! (" 
                            + Cfg.Limits[currentZone]["foundations"] 
                            + ")");
            }

            if(dimensions["height"] > Cfg.Limits[currentZone]["height"])
            {
                if(entity.ShortPrefabName.Contains("foundation"))
                {
                    NextTick(() =>
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.None);
                    });
                    znBuildMsg(player, "You have reached the "+currentZone+" height limit (" + Cfg.Limits[currentZone]["height"] + "), placing height changing foundations will waste resources!");
                    return;
                }
                else
                {
                    NextTick(() =>
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.None);
                    });
                    znBuildMsg(player, "Your base is beyond the " + currentZone + " height limit! ("
                                + Cfg.Limits[currentZone]["height"] 
                                + ")");
                    //killBuilding(building, player.userID);
                    //znBuildMsg(player, "You have somehow (tried to) circumvent the height limit! (" 
                    //            + Cfg.Limits[currentZone]["height"] 
                    //            + ") \n Your entity was destroyed, there will be no refunds!");
				    return;
                }
            }

        }

        private object CanBuild(Planner planner, Construction entity, Construction.Target target)
        {
            if (planner == null || entity == null)
            {
                return null;
            }

            // check if we care about what we are trying to build
            string name = entity?.fullName ?? string.Empty;
            string groupName = string.Empty;
            //Puts("DEBUG: can Build: " + name);

            Match match = Regex.Match(name, @".*assets/prefabs/([^/]+)/.*/([^/]+)\.prefab$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                groupName = match.Groups[1].Value;
                name = match.Groups[2].Value;
            }
            else
            {
                return null;
            }

            bool isFoundation = foundationPrefabNames.Contains(name);
            bool isHeightEntity = heightPrefabNames.Contains(name);
            var player = planner?.GetOwnerPlayer();

            if (globalWoodRefundTimer.ContainsKey(player.userID) && (DateTime.Now - globalWoodRefundTimer[player.userID]).TotalSeconds < 5) return false;

            if (HasPermission(player.UserIDString, permUnlimited)) return null;

            if (!isFoundation && !isHeightEntity)
            {
                if (canBuildInZone(player, name, groupName))
                {
                    return null;
                }
                else
                {
                    return false;
                }
            }
            
			BuildingPrivlidge privilege = player.GetBuildingPrivilege();

            // the first foundation can always be placed outside of privilege
            // after that, a TC must be placed!
            // CHECK for TC distance to other TCs (of player) first
            if (!privilege && target.socket == null)
            {
                if(checkTCdistanceOk(player)){
                    return null;
                } else {
                    return false;
                }
            }
            else if(!privilege && isFoundation)
            {
                return null;
                //TRIAL, see, if people behave...
                //znBuildMsg(player, "You must place a TC (It can be repositioned later).");
                //return false;
            }
            else if (!privilege && target.entity.transform.position.y < 0)
            {
                //Puts("DEBUG: Target " + target.entity.transform.position.y);
                return null;
            }
            else if (!privilege)
            {
                return null;
                //TRIAL, see, if people behave...
                //znBuildMsg(player, "You must place a TC (It can be repositioned later).");
                //return false;
            }
            // If a TC has been placed, don't allow starting new bases in Privilege
            if (privilege && target.socket == null)
            {
                znBuildMsg(player, "You cannot build away from the main building.");
                return false;
            }
            BuildingManager.Building building = privilege.GetBuilding();
            // prevent decoupling foundations
            if(building.buildingBlocks != null && !building.buildingBlocks.Contains(target.entity))
            {
                znBuildMsg(player, "You cannot build away from the main building.");
                return false;
            }

            Dictionary<string, float> dimensions = getBuildingDimensions(building);
            
            string currentZone = getCurrentZone(player, privilege);

            // evil limit hackers must be punished. Destroy everything if they try to build in oversized bases.
            if(dimensions["foundationsCount"] > Cfg.Limits[currentZone]["foundations"])
            {
               
                znBuildMsg(player, "Your base is beyond the " + currentZone + " foundation limit! ("
                            + Cfg.Limits[currentZone]["foundations"] 
                            + ") \n You must remove " 
                            + (dimensions["foundationsCount"] - Cfg.Limits[currentZone]["foundations"]) 
                            + " foundations to be able to build!");
				return false;
            }
            if(isFoundation && dimensions["foundationsCount"] == Cfg.Limits[currentZone]["foundations"])
            {
                znBuildMsg(player, "You have reached the " + currentZone + " foundation limit! (" + Cfg.Limits[currentZone]["foundations"] + ")");
				return false;
            }
            // if we place a wall and we have reached the hight limit (and only then)
            // we need to check if the wall wall type we want to place is making the building taller 
            // (or if it is just an internal wall, or a wall that's on the same level as the top layer)
            if(isHeightEntity && dimensions["height"] == Cfg.Limits[currentZone]["height"]) 
            {
                // the finished build would end at target_start+height(unless foundation!) + height_of_thing_to_build
                float finalHeight = target.entity.transform.position.y + entity.bounds.size.y;
                if(!IsFoundationObject((BuildingBlock)target.entity))
                {
                    finalHeight += target.entity.bounds.size.y;
                }
                //---debug---
                if(Cfg.Debug) Puts("top " + dimensions["topY"]);
                if(Cfg.Debug) Puts("final " + finalHeight);

                // we only need the full numbers to allow for floor-hook 0.000x differences in height.
                if(Math.Ceiling(finalHeight) > Math.Ceiling(dimensions["topY"]))
                { 
                    znBuildMsg(player, "You have reached the " + currentZone + " height limit! (" + Cfg.Limits[currentZone]["height"] + ")");
				    return false;
                }
            }
            return null;
        }

        private bool canBuildInZone(BasePlayer player, string prefabName, string groupName)
        {
            if(HasPermission(player.UserIDString, permUnlimited)) return true;
            //Puts("Debug: groupName: " + groupName);
            //Puts("Debug: prefabName: " + prefabName);

            if (Cfg.BannedItemsAlternatives.ContainsKey(prefabName))
            {
                znBuildMsg(player, "<color=red>You are not allowed to build this on the server!</color>\n<color=green>Alternative:</color> " + Cfg.BannedItemsAlternatives[prefabName]);
                return false;
            }

            string currentZone;
            
            currentZone = getCurrentZone(player, null);
            if(Cfg.AllowedDeployables[currentZone].Count > 0 && !Cfg.AllowedDeployables[currentZone].Contains(prefabName) )
            {
                znBuildMsg(player, "You are not allowed to build that in \"" + currentZone + "\" area!");
                return false;
            }
            else if(Cfg.ForbiddenDeployables[currentZone].Count > 0 && Cfg.ForbiddenDeployables[currentZone].Contains(prefabName) )
            {
                znBuildMsg(player, "You are not allowed to build that in \"" + currentZone + "\" area!");
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion
        #region Commands
        [ChatCommand("agrade")]
        void CommandAgrade(BasePlayer player, string command, string[] args)
        {
            // player group needs permission 
            if (!permission.UserHasPermission(player.UserIDString, permAgrade) || (!ServerRewards && Cfg.AgradePricePerEntity > 0))
            {
                znBuildMsg(player, "You are not allowed to use this command! (did you unlock it in /p?)");
                return;
            }
            if (args.Length == 0)
            {
                agradeUsageInfo(player);
                return;
            }
            if (args.Length == 1)
            {
                int argGrade;
                try
                {
                    argGrade = int.Parse(args[0]);
                }
                catch (FormatException)
                {
                    agradeUsageInfo(player);
                    return;
                }
                if (argGrade < 1 || argGrade > 4)
                {
                    agradeUsageInfo(player);
                    return;
                }
                // you have to be in a building zone
                BuildingPrivlidge privilege = player.GetBuildingPrivilege();
                if (!privilege)
                {
                    player.ChatMessage(LangMsg("NoBuildingPrivilege", player.UserIDString));
                    return;
                }

                // if you are not authorized, you will only get a generic error, before stuff happens
                if (!privilege.IsAuthed(player) && !player.IsAdmin)
                {
                    player.ChatMessage(LangMsg("NoAuthorization", player.UserIDString));
                    return;
                }
                BuildingManager.Building building = privilege.GetBuilding();
                float mult = Cfg.AgradeResMult;
                int rpPerBlock = Cfg.AgradePricePerEntity;
                if (permission.UserHasPermission(player.UserIDString, permAgradeFree))
                {
                    mult = Cfg.AgradeFreeResMult;
                    rpPerBlock = 0;
                };

                if (rpPerBlock > 0)
                {
                    object bal = ServerRewards?.Call("CheckPoints", player.userID);
                    if (bal == null) return;
                    int playerRP = (int)bal;

                    int totalPrice = rpPerBlock * building.buildingBlocks.Count;
                    if (playerRP < totalPrice)
                    {
                        znBuildMsg(player, "This command costs " + totalPrice + " RP to execute, you only have " + playerRP);
                        return;
                    }
                    else
                    {
                        ServerRewards?.Call("TakePoints", player.userID, totalPrice);
                        znBuildMsg(player, "You paid " + totalPrice + " RP, command is executing.");
                    }
                }
                BuildingGrade.Enum newGrade = (BuildingGrade.Enum)int.Parse(args[0]);
                BuildingGrade.Enum oldGrade;
                int agradeCounter = 0;
                foreach (BuildingBlock block in building.buildingBlocks)
                {
                    oldGrade = block.grade;
                    // skip work, if the block is already in the target grade.
                    if (oldGrade == newGrade)
                        continue;

                    block.SetGrade(newGrade);

                    foreach (ItemAmount itemAmount in block.BuildCost())
                    {
                        if ((double)player.inventory.GetAmount(itemAmount.itemid) < ((double)itemAmount.amount * mult))
                        {
                            // can't afford more, roll back
                            block.SetGrade(oldGrade);
                            znBuildMsg(player, "You ran out of materials to upgrade the building!");
                            Puts("AGRADE: used by " + player + " ran out of materials.");
                            return;
                        }
                        else
                        {
                            player.inventory.Take(null, itemAmount.itemid, (int)Math.Ceiling(itemAmount.amount * mult));
                        }
                    }
                    block.SetHealthToMax();
                    block.StartBeingRotatable();
                    block.SendNetworkUpdate();
                    block.UpdateSkin();
                    block.ResetUpkeepTime();
                    block.GetBuilding()?.Dirty();
                    agradeCounter++;
                }
                znBuildMsg(player, "Building was successfully upgraded! (" + agradeCounter + " entities)");
                Puts("AGRADE: used by " + player + " upgrading " + agradeCounter + "entities.");
            }
        }

        [ChatCommand("repairall")]
        void CommandRepairall(BasePlayer player, string command, string[] args)
        {
            // player group needs permission 
            if (!permission.UserHasPermission(player.UserIDString, permRepall) || (!ServerRewards && Cfg.RepairAllPricePerEntity > 0))
            {
                znBuildMsg(player, "You are not allowed to use this command! (did you unlock it in /p?)");
                return;
            }
            bool isHeliTeam = (HeliFromHell != null) ? HeliFromHell.Call<bool>("HasSteaksInHeli", player) : false;
            bool isBradTeam = (BradleyFromHell != null) ? BradleyFromHell.Call<bool>("HasSteaksInBradley", player) : false;
            bool isZombieTeam = (ZombieFromHell != null) ? ZombieFromHell.Call<bool>("HasSteaksInZombie", player) : false;

            if(isHeliTeam || isBradTeam || isZombieTeam)
            {
                znBuildMsg(player, "You can't repairall while fighting (heli/brad/zombie)!");
                return;
            }

            if (args.Length == 0)
            {
                repallUsageInfo(player);
                return;
            }
            if (args.Length == 1)
            {
                string sayYes;
                if (args[0] != "yes")
                {
                    repallUsageInfo(player);
                    return;
                }

                // you have to be in a building zone
                BuildingPrivlidge privilege = player.GetBuildingPrivilege();
                if (!privilege)
                {
                    player.ChatMessage(LangMsg("NoBuildingPrivilege", player.UserIDString));
                    return;
                }

                // if you are not authorized, you will only get a generic error, before stuff happens
                if (!privilege.IsAuthed(player) && !player.IsAdmin)
                {
                    player.ChatMessage(LangMsg("NoAuthorization", player.UserIDString));
                    return;
                }
                BuildingManager.Building building = privilege.GetBuilding();

                bool isHeliBase = (HeliFromHell != null) ? HeliFromHell.Call<bool>("HasSteaksInHeli", privilege) : false;
                bool isBradBase = (BradleyFromHell != null) ? BradleyFromHell.Call<bool>("HasSteaksInBradley", privilege) : false;
                bool isZombieBase = (ZombieFromHell != null) ? ZombieFromHell.Call<bool>("HasSteaksInZombie", privilege) : false;
                if (isHeliBase || isBradBase || isZombieBase)
                {
                    znBuildMsg(player, "You can't repairall a base of someone fighting (heli/brad/zombie)!");
                    return;
                }


                float mult = Cfg.RepairAllResMult;
                int rpPerBlock = Cfg.RepairAllPricePerEntity;
                if (permission.UserHasPermission(player.UserIDString, permRepallFree))
                {
                    mult = Cfg.RepairAllFreeResMult;
                    rpPerBlock = 0;
                };
                if (rpPerBlock > 0)
                {
                    object bal = ServerRewards?.Call("CheckPoints", player.userID);
                    if (bal == null) return;
                    int playerRP = (int)bal;

                    int totalPrice = rpPerBlock * building.buildingBlocks.Count;
                    if (playerRP < totalPrice)
                    {
                        znBuildMsg(player, "This command costs " + totalPrice + " RP to execute, you only have " + playerRP);
                        return;
                    }
                    else
                    {
                        ServerRewards?.Call("TakePoints", player.userID, totalPrice);
                        znBuildMsg(player, "You paid " + totalPrice + " RP, command is executing.");
                    }
                }


                int repairCounter = 0;
                foreach (BuildingBlock block in building.buildingBlocks)
                {
                    // skip work, if the block is already at full health.
                    if (block.MaxHealth() == block.health)
                        continue;
                    double damagePercentage = mult * (1.0 - (block.health / block.MaxHealth()));
                    foreach (ItemAmount itemAmount in block.BuildCost())
                    {
                        if ((double)player.inventory.GetAmount(itemAmount.itemid) < ((double)itemAmount.amount * damagePercentage))
                        {
                            znBuildMsg(player, "You ran out of materials to repair a part the building!");
                            Puts("REPAIRALL: used by " + player + " ran out of materials.");
                            return;
                        }
                        else
                        {
                            player.inventory.Take(null, itemAmount.itemid, (int)Math.Ceiling(itemAmount.amount * damagePercentage));
                        }
                    }
                    block.SetHealthToMax();
                    block.SendNetworkUpdate();
                    block.GetBuilding()?.Dirty();
                    repairCounter++;
                }
                znBuildMsg(player, "Building was successfully repaired! (" + repairCounter + " entities)");
                Puts("REPAIRALL: used by " + player + " repairing " + repairCounter + "entities.");
            }
        }

        [ChatCommand("cbex")]
        void CommandCBEX(BasePlayer player, string command, string[] args)
        {
            // if you are not authorized, you will only get a generic error, before stuff happens
            if (!permCheckVerbose(player, permException)) return;

            // you have to be in a building zone
            BuildingPrivlidge privilege = player.GetBuildingPrivilege();
            if (!privilege)
            {
                player.ChatMessage(LangMsg("NoBuildingPrivilege", player.UserIDString));
                return;
            }
            string zone = "default";
            string msg = "<color=orange>=== Building Limit Exception: ===</color>";
            if (storedData.ExceptionTcIds.Contains(privilege.net.ID))
            {
                storedData.ExceptionTcIds.Remove(privilege.net.ID);
                msg += "\n<color=red>REMOVED!</color>";
            }
            else
            {
                storedData.ExceptionTcIds.Add(privilege.net.ID);
                msg += "\n<color=green>ACTIVATED!</color>";
                zone = "exception";
            }
            msg += "\nLimit is now: " + GetCurrentLimit(zone, "f") + " foundations, " + GetCurrentLimit(zone, "h") + " height";
            SaveData();
            znBuildMsg(player, msg);
        }

        [ChatCommand("cb")]
        void CommandCB(BasePlayer player, string command, string[] args)
        {
            // you have to be in a building zone
            BuildingPrivlidge privilege = player.GetBuildingPrivilege();
            if (!privilege)
            {
                player.ChatMessage(LangMsg("NoBuildingPrivilege", player.UserIDString));
                return;
            }

            // if you are not authorized, you will only get a generic error, before stuff happens
            if (!privilege.IsAuthed(player) && !player.IsAdmin)
            {
                player.ChatMessage(LangMsg("NoAuthorization", player.UserIDString));
                return;
            }

            BuildingManager.Building building = privilege.GetBuilding();
            Dictionary<string, float> dimensions = getBuildingDimensions(building);
            string currentZone = getCurrentZone(player, privilege);
            string msg = "<color=orange>=============== Check Building (/cb) ================</color>";
            if (storedData.ExceptionTcIds.Contains(privilege.net.ID))
            {
                msg += "\n<color=red>Building uses exceptional limits!</color>";
                currentZone = "exception";
            }
            msg += "\n<color=green>Building dimensions:</color>";
            msg += "\nTotal Building Blocks: \t\t" + dimensions["entitiesCount"];
            msg += "\nFoundations: \t\t\t" + dimensions["foundationsCount"] + "/" + GetCurrentLimit(currentZone, "f");
            msg += "\nHeight: \t\t\t\t" + dimensions["height"] + "/" + GetCurrentLimit(currentZone, "h");

            // REPAIRALL Info
            Dictionary<int, float> res = new Dictionary<int, float>();
            Dictionary<BuildingGrade.Enum, int> gradeCounter = new Dictionary<BuildingGrade.Enum, int>() {
                [BuildingGrade.Enum.Twigs] = 0,
                [BuildingGrade.Enum.Wood] = 0,
                [BuildingGrade.Enum.Stone] = 0,
                [BuildingGrade.Enum.Metal] = 0,
                [BuildingGrade.Enum.TopTier] = 0,
            };
            float mult = Cfg.RepairAllResMult;
            float rpPerBlock = Cfg.RepairAllPricePerEntity;
            if (permission.UserHasPermission(player.UserIDString, permRepallFree)) {
                mult = Cfg.RepairAllFreeResMult;
                rpPerBlock = 0f;
            };
           
            foreach (BuildingBlock block in building.buildingBlocks)
            {
                // skip work, if the block is already at full health.
                gradeCounter[block.grade]++;
                if (block.MaxHealth() == block.health)
                    continue;

                double damagePercentage = mult * (1.0 - (block.health / block.MaxHealth()));
                foreach (ItemAmount itemAmount in block.BuildCost())
                {
                    if (res.ContainsKey(itemAmount.itemid))
                    {
                        res[itemAmount.itemid] += (float)Math.Ceiling(itemAmount.amount * damagePercentage);
                    }
                    else
                    {
                        res[itemAmount.itemid] = (float)Math.Ceiling(itemAmount.amount * damagePercentage);
                    }
                }
            }
            msg += "\n\n<color=green>Estimated /repairall cost:</color>";
            if (!permission.UserHasPermission(player.UserIDString, permRepall))
            {
                msg += "\nCommand needs to be unlocked in <color=orange>/p</color> first!";
            }
            else
            {
                int rpPrice = (int) Math.Ceiling(dimensions["entitiesCount"] * rpPerBlock);
                msg += "\n" + rpPrice;
                msg += (rpPrice >= 10000) ? "\t" : "\t\t";
                msg += "RP";
                if (res.Count > 0)
                {
                    foreach (KeyValuePair<int,float> d in res)
                    {

                        msg += "\n" + Math.Ceiling(d.Value);
                        msg += (d.Value >= 10000f) ? "\t" : "\t\t";
                        msg += ItemManager.itemDictionary[d.Key].displayName.translated;
                    }
                }
                else
                {

                    msg += "\nNothing to repair!";
                }
            }
            // AGRADE INFO
            mult = Cfg.AgradeResMult;
            rpPerBlock = Cfg.AgradePricePerEntity;
            if (permission.UserHasPermission(player.UserIDString, permAgradeFree))
            {
                mult = Cfg.AgradeFreeResMult;
                rpPerBlock = 0f;
            };
            msg += "\n\n<color=green>Max /agrade (upgrade all) cost:</color>";
            msg += "\n<color=#aaaaaa>Doesn't consider exact block shape but checks grade!</color>";
            if (!permission.UserHasPermission(player.UserIDString, permAgrade))
            {
                msg += "\nCommand needs to be unlocked in <color=orange>/p</color> first!";
            }
            else
            {
                int rpPrice = (int)Math.Ceiling(dimensions["entitiesCount"] * rpPerBlock);
                msg += "\nCommand base-fee:\t\t<color=#aaaaaa>" + rpPrice + "</color> RP"; 
                int price = 0;
                // grade 1
                price = (int)Math.Ceiling((dimensions["entitiesCount"] - gradeCounter[BuildingGrade.Enum.Wood]) * 200 * mult);
                msg += "\n/agrade 1 (Wood):\t\t<color=#aaaaaa>" + price + "</color> Wood";
                // grade 2
                price = (int)Math.Ceiling((dimensions["entitiesCount"] - gradeCounter[BuildingGrade.Enum.Stone]) * 300 * mult);
                msg += "\n/agrade 2 (Stone):\t\t<color=#aaaaaa>" + price + "</color> Stones";
                // grade 3
                price = (int)Math.Ceiling((dimensions["entitiesCount"] - gradeCounter[BuildingGrade.Enum.Metal]) * 200 * mult);
                msg += "\n/agrade 3 (Metal):\t\t<color=#aaaaaa>" + price + "</color> Metal Fragments";
                // grade 4
                price = (int)Math.Ceiling((dimensions["entitiesCount"] - gradeCounter[BuildingGrade.Enum.TopTier]) * 25 * mult);
                msg += "\n/agrade 4 (HQM):\t\t<color=#aaaaaa>" + price + "</color> High Quality Metal";

            }

            if (Cfg.ForbiddenDeployables.ContainsKey(currentZone) && Cfg.ForbiddenDeployables[currentZone].Count > 0)
            {
                msg += "\n\n<color=green>Forbidden in the area:</color>";
                foreach(string itm in Cfg.ForbiddenDeployables[currentZone])
                {
                    msg += "\n- " + itm;
                }
            }

            SendReply(player, msg);
        }
        #endregion

        #region functions

        private void znBuildMsg(BasePlayer player, string msg)
        {
            SendReply(player, "<color=orange>ZN-Build:</color> " + msg);
        }
        private void refundBuildingBlock(BasePlayer player, BuildingBlock buildingBlock)
        {
            var itemsToGive = new Dictionary<int, int>();
            foreach (var itemAmount in buildingBlock.blockDefinition.grades[0].costToBuild)
            {
                if (!itemsToGive.ContainsKey(itemAmount.itemid))
                {
                    itemsToGive.Add(itemAmount.itemid, 0);
                }

                itemsToGive[itemAmount.itemid] += (int)itemAmount.amount;
            }
            foreach (var itemToGive in itemsToGive)
            {
                player.GiveItem(ItemManager.CreateByItemID(itemToGive.Key, (int)itemToGive.Value), BaseEntity.GiveItemReason.PickedUp);
            }

        }
        private string TakeResources(BasePlayer player, int buildingGrade, BuildingBlock buildingBlock, out Dictionary<int, int> items)
        {
            var itemsToTake = new Dictionary<int, int>();
            if (buildingBlock == null)
            {
                Puts("DEBUG: buildingBlock is null for " + player);
                items = null;
                return "";
            }
            foreach (var itemAmount in buildingBlock.blockDefinition.grades[buildingGrade].costToBuild)
            {
                if (!itemsToTake.ContainsKey(itemAmount.itemid))
                {
                    itemsToTake.Add(itemAmount.itemid, 0);
                }

                itemsToTake[itemAmount.itemid] += (int)itemAmount.amount;
            }

            var canAfford = true;
            foreach (var itemToTake in itemsToTake)
            {
                if (!HasItemAmount(player, itemToTake.Key, itemToTake.Value))
                {
                    canAfford = false;
                }
            }

            items = itemsToTake;
            return canAfford ? null : "Error.Resources";
        }
        private bool HasItemAmount(BasePlayer player, int itemId, int itemAmount)
        {
            var count = 0;
            foreach (var item in player.inventory.AllItems())
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            return count >= itemAmount;
        }
        
        private bool checkTCdistanceOk(BasePlayer player, BuildingPrivlidge bp = null){
            Vector3 position = (bp) ? bp.transform.position : player.transform.position;
            // always allow placing in caves
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(position) - 0.5f;
            if(position.y < terrainHeight) return true;

            string typeString = (bp) ? "TC" : "base";
            float range = Cfg.Limits[getCurrentZone(player, bp)]["minowntcdistance"] + 0f;
            float distance;
            List<BuildingPrivlidge> list = new List<BuildingPrivlidge>();
            Vis.Entities<BuildingPrivlidge>(position, range, list, tcMasks);
            foreach(BuildingPrivlidge entity in list) {
                // ignore TCs in caves and the just placed tc
                terrainHeight = TerrainMeta.HeightMap.GetHeight(entity.transform.position) - 0.5f;
                if(entity.transform.position.y < terrainHeight) continue;
                if(bp && entity == bp) continue;

                distance = Vector3Ex.Distance2D(entity.transform.position, position);
//Puts("Distance: " + distance);
                // player has a TC within 400m already
                if(entity.OwnerID == player.userID){
                    znBuildMsg(player, "You are not allowed to place another "+typeString+" within "+ range + "m to your existing one. ("+distance+"m away)");
                    return false;
                }
                // too close to other base
                //if(distance <= 50f){
                //    znBuildMsg(player, "You are not allowed to place a  "+typeString+" so close to another base.");
                //    return false;
                //}
            }
            // all good
            return true;
        }

        private string getCurrentZone(BasePlayer player, BuildingPrivlidge privlidge){
            string currentZone = "default";
            if(privlidge != null && storedData.ExceptionTcIds.Contains(privlidge.net.ID))
            {
                return "exception";
            }
            if(PatchesForPVE != null)
            {
                bool isInTownRange = (bool)PatchesForPVE?.Call<bool>("IsCloseToTown", player.transform.position);
                if (isInTownRange)
                {
                    return "town";
                }
            }
            // Special Wipe September 2023
            //return permUnlimited;

            if (privlidge != null)
            {
                if (permission.UserHasPermission(privlidge?.OwnerID.ToString(), permExtended_5)) return permExtended_5;
                if (permission.UserHasPermission(privlidge?.OwnerID.ToString(), permExtended_4)) return permExtended_4;
                if (permission.UserHasPermission(privlidge?.OwnerID.ToString(), permExtended_3)) return permExtended_3;
                if (permission.UserHasPermission(privlidge?.OwnerID.ToString(), permExtended_2)) return permExtended_2;
                if (permission.UserHasPermission(privlidge?.OwnerID.ToString(), permExtended_1)) return permExtended_1;
            }
            else
            {
                if (permission.UserHasPermission(player.UserIDString, permExtended_5)) return permExtended_5;
                if (permission.UserHasPermission(player.UserIDString, permExtended_4)) return permExtended_4;
                if (permission.UserHasPermission(player.UserIDString, permExtended_3)) return permExtended_3;
                if (permission.UserHasPermission(player.UserIDString, permExtended_2)) return permExtended_2;
                if (permission.UserHasPermission(player.UserIDString, permExtended_1)) return permExtended_1;
            }
            
            //Puts("DEBUG: currentZone: " + currentZone);
            return currentZone;
        }

        private void agradeUsageInfo(BasePlayer player)
        {
            float mult = Cfg.AgradeResMult;
            int rpPerBlock = Cfg.AgradePricePerEntity;
            if (permission.UserHasPermission(player.UserIDString, permAgradeFree))
            {
                mult = Cfg.AgradeFreeResMult;
                rpPerBlock = 0;
            };
            string msg = "<color=orange>=============== ALL-Upgrade ================</color>";
            msg += "\n- This costs <color=red>" + rpPerBlock + " RP/block + "+ mult + "x the materials</color> for the upgrade";
            msg += "\n- If you run out of materials, the upgrade stops. <color=red>No Refunds!</color>";
            msg += "\n\n<color=orange>Hint:</color> Use <color=green>/cb</color> to see the actual RP cost.";
            msg += "\n\n<color=green>Usage:</color>";
            msg += "\n /agrade \t\t Show usage info";
            msg += "\n /agrade 1 \t\t Upgrade to wood";
            msg += "\n /agrade 2 \t\t Upgrade to stone";
            msg += "\n /agrade 3 \t\t Upgrade to sheet metal";
            msg += "\n /agrade 4 \t\t Upgrade to armored";
            
            SendReply(player, msg);
        }
          private void repallUsageInfo(BasePlayer player)
        {
            float mult = Cfg.RepairAllResMult;
            int rpPerBlock = Cfg.RepairAllPricePerEntity;
            if (permission.UserHasPermission(player.UserIDString, permRepallFree))
            {
                mult = Cfg.RepairAllFreeResMult;
                rpPerBlock = 0;
            };
            string msg = "<color=orange>=============== Repair ALL ================</color>";
            msg += "\n- This costs <color=red>" + rpPerBlock + " RP/block + "+ mult + "x the materials</color> for the repairs";
            msg += "\n- If you run out of materials, the repair stops. <color=red>No Refunds!</color>";
            msg += "\n- This will only repair the building. (no doors etc.)";
            msg += "\n\n<color=orange>Hint:</color> Use <color=green>/cb</color> to see the actual RP cost.";
            msg += "\n\n<color=green>Usage:</color>";
            msg += "\n /repairall \t\tShow usage info";
            msg += "\n /repairall yes \tRepair your entire base";
            SendReply(player, msg);
        }
        private bool permCheckVerbose(BasePlayer player, string perm)
        {
            if(HasPermission(player.UserIDString, perm)) return true;
            znBuildMsg(player, "No permission to use this command!");
            return false;
        } 

        private int GetCurrentLimit(string currentZone, string type){
            string t = (type == "f") ? "foundations" : "height";
            if(Cfg.Limits.ContainsKey(currentZone))
            {
                return Cfg.Limits[currentZone][t];
            }
            else
            {
                return Cfg.Limits["default"][t];
            }
        }

        private bool IsFoundationObject(BuildingBlock entity)
        {            
            // count only foundation-type entities
            return foundationPrefabNames.Contains(entity.ShortPrefabName);
        }

        private Dictionary<string, float> getBuildingDimensions(BuildingManager.Building building)
        {
            Dictionary<string, float> result = new Dictionary<string, float>();

            var entities = building.buildingBlocks;
            result["entitiesCount"] = (float)entities.Count;
            
            result["foundationsCount"] = 0f;
            float lowBlockY = entities[0].transform.position.y;
            float topBlockY = lowBlockY;

            // one loop to go through the building
            for (var i = 0; i < result["entitiesCount"]; i++)
                {
                    // count foundations
                    if (IsFoundationObject(entities[i])) 
                    {
                        result["foundationsCount"]++;
                        // helper to get height of building, foundations have no height
                        CalcHighLow(entities[i].transform.position.y, entities[i].transform.position.y, ref topBlockY, ref lowBlockY);
                    }
                    else
                    {
                        // helper to get height of building add height to top
                        CalcHighLow(entities[i].transform.position.y + entities[i].bounds.size.y, entities[i].transform.position.y, ref topBlockY, ref lowBlockY);
                    }
                }
            float height = (float)Math.Ceiling(topBlockY - lowBlockY);
            result["height"] = rRound(height/3); // one floor is 3y heigh
            result["lowY"] = lowBlockY;
            result["topY"] = topBlockY;

            return result;
        }

        private void CalcHighLow(float eTop, float eLow, ref float top, ref float low)
        {
            if (eTop > top) top = eTop;
            if (eLow < low) low = eLow;
        }

		private string LangMsg(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);

        private float rRound(float d)
        {
            var absoluteValue = Math.Abs(d);
            var integralPart = (long)absoluteValue;
            var decimalPart = absoluteValue - integralPart;
            float roundedNumber;

            if (decimalPart >= 0.5f)
            {
                roundedNumber = integralPart + 0.5f;
            }
            else
            {
                roundedNumber = integralPart;
            }
            return roundedNumber;
        }

        // DEPRECATED
        private void addCodeLock(BasePlayer Player, BaseEntity Entity)
        {
            var S = Entity as StorageContainer;
            if (S || Entity is AnimatedBuildingBlock)
            {
                if (!Entity.IsLocked())
                {
                    var Code = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
                    Code.Spawn();
                    Code.code = ""+RandomCode();
//Puts("DEBUG: code is " + Code.code);
                    Code.SetParent(Entity, Entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
                    Entity.SetSlot(BaseEntity.Slot.Lock, Code);
                    Code.SetFlag(BaseEntity.Flags.Locked, true);
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab", Code.transform.position);
                    Code.whitelistPlayers.Add(Player.userID);
                }
            }
        }
        
        public int RandomCode()  
        {  
            System.Random random = new System.Random();  
            return random.Next(1000, 9999); 
        }

        #endregion
    }
}
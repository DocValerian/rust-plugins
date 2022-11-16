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
using ConVar;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static Oxide.Plugins.ZombieHunt.NpcZhuntBrain;
using Physics = UnityEngine.Physics;
using Random = Oxide.Core.Random;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("ZombieHunt", "DocValerian", "2.5.2")]
    class ZombieHunt : RustPlugin
    {
        static ZombieHunt Plugin;

        [PluginReference]
        private Plugin ServerRewards, Kits, TPapi, LootBoxSpawner, ZNTitleManager;

        const string permUse = "zombiehunt.use";
        const string permAutorec = "zombiehunt.autoloot.autorec";
        private string[] recWhitelist = {
            "lmg.m249",
            "rifle.l96",
            "rifle.m39",
            "rifle.lr300",
            "techparts",
            "cctv.camera",
            "targeting.computer",
            "supply.signal",
            "Jackhammer",
        };
        private string[] buffedWeapons =
        {
            "l96.entity",
            "bolt_rifle.entity",
        };
        private string[] weaponList = {
            "rifle.m39",
            "rifle.lr300",
            "shotgun.spas12",
            "shotgun.pump",
            "rifle.ak",
            "rifle.semiauto",
            "smg.mp5",
            "smg.thompson",
            "pistol.python",
            "pistol.revolver",
            "pistol.semiauto",
            "smg.2",
            "pistol.m92"
        };
        private Dictionary<string, string> weaponMap = new Dictionary<string, string>()
        {
            ["rifle.m39"] = "m39.entity",
            ["rifle.lr300"] = "lr300.entity",
            ["shotgun.spas12"] = "spas12.entity",
            ["shotgun.pump"] = "shotgun_pump.entity",
            ["rifle.ak"] = "ak47u.entity",
            ["rifle.semiauto"] = "semi_auto_rifle.entity",
            ["smg.mp5"] = "mp5.entity",
            ["smg.thompson"] = "thompson.entity",
            ["pistol.python"] = "python.entity",
            ["pistol.revolver"] = "pistol_revolver.entity",
            ["pistol.semiauto"] = "pistol_semiauto.entity",
            ["smg.2"] = "smg.entity",
            ["pistol.m92"] = "m92.entity"
        };
        private string currentWeaknessShortname = "";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);

        #region ConfigDataLoad
        static ConfigFile Cfg = new ConfigFile();
        class ConfigFile
        {
            public int ZombieEventSeconds = 600;
            public int ZombieDamageRange = 130;
            public int BarrelZombiePercent = 30;
            public float ZombieHealth = 200f;
            public int ZombieMinDmg = 5;
            public int ZombieMaxDmg = 17;



        }
        class StoredData
        {
            public Dictionary<string, Vector3> SpawnPoints = new Dictionary<string, Vector3>();
            public int ZombieNumber = 40;
            public int ZombieSpawnRadius = 110;
            public float ZombieArmor = 2.5f;
            public float ZombieMeleeDamage = 0.5f;
            public float ZombieProjectileDamage = 0.35f;
            public float ZombieBuildingDamage = 0f;
            public float ZombieAccuracyPercent = 70f;
            public bool WeaponProtect = true;
            public int RoundWinBonus = 1500;
            public int BaseRPperKill = 15;
            public int MinKillCount = 30;
            public int GunZombiePercent = 6;
            public List<string> ZombieKitNames = new List<string>()
            {
                "spearwolf",
                "evilmummy",
                "woodknight",
                "knifethug",
                "eokabunny"
            };

        }
        StoredData storedData;
        class UserData
        {
            public List<ulong> AutoRecUsers = new List<ulong>();
            public List<ulong> RegisteredHunters = new List<ulong>();
            public List<ulong> BarrelUsers = new List<ulong>();
        }
        UserData userData;

        class ScoreData
        {
            public Dictionary<ulong, int> killCounter = new Dictionary<ulong, int>();
            public Dictionary<ulong, int> winCounter = new Dictionary<ulong, int>();
        }
        ScoreData scoreData;

        public Dictionary<ulong, NpcZhunt> currentZombieList = new Dictionary<ulong, NpcZhunt>();
        private HashSet<ulong> currentCorpseList = new HashSet<ulong>();
        private Timer currentUITimer;
        private Timer zombieTimer;
        private DateTime lastRandomSpawn;
        private Vector3 currentSpawnPoint;
        private int currentKills = 0;
        private string currentSpawnGrid;
        private Dictionary<ulong, int> roundDamageCounter = new Dictionary<ulong, int>();
        private Dictionary<ulong, KeyValuePair<DateTime, Vector3>> lastPlayerPosition = new Dictionary<ulong, KeyValuePair<DateTime, Vector3>>();
        private string LastWinnerName = "";
        private int LastWinnerKills = 0;
        private Coroutine scheduleCoroutine { get; set; }

        private static int groundLayer;
        private static int terainLayer;
        private static int BuildingLayer;
        private static Vector3 Vector3Down;

        private List<Rust.DamageType> allowedDamageTypes = new List<Rust.DamageType>() {
            Rust.DamageType.Arrow,
            Rust.DamageType.Blunt,
            Rust.DamageType.Slash,
            Rust.DamageType.Stab
        };

        private List<Vector3> safeZones = new List<Vector3>();
        #region Classes
        public static class Coroutines
        {
            private static Dictionary<float, YieldInstruction> mem;
            public static void Clear()
            {
                if (mem != null)
                {
                    mem.Clear();
                    mem = null;
                }
            }
            public static YieldInstruction WaitForSeconds(float delay)
            {
                if (mem == null)
                {
                    mem = new Dictionary<float, YieldInstruction>();
                }

                YieldInstruction yield;
                if (!mem.TryGetValue(delay, out yield))
                {
                    yield = new WaitForSeconds(delay);
                    mem.Add(delay, yield);
                }

                return yield;
            }


        }
        #endregion
        void Loaded()
        {
            Cfg = Config.ReadObject<ConfigFile>();
            Plugin = this;
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAutorec, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ZombieHunt");
            userData = Interface.Oxide.DataFileSystem.ReadObject<UserData>("ZombieHunt_users");
            scoreData = Interface.Oxide.DataFileSystem.ReadObject<ScoreData>("ZombieHunt_scores");
            SaveData();
            lastRandomSpawn = DateTime.Now.AddSeconds(-1300);
            currentKills = 0;
            currentZombieList.Clear();


        }
        private void OnServerInitialized()
        {

            if (!TPapi)
            {
                Debug.LogError("[ZombieHunt] Error! TPapi not loaded! Please fix this error before loading ZombieHunt again. Unloading...");
                Interface.Oxide.UnloadPlugin(this.Title);
                return;
            }

            groundLayer = LayerMask.GetMask("Construction", "Terrain", "World");
            terainLayer = LayerMask.GetMask("Terrain", "World", "Water");
            BuildingLayer = LayerMask.GetMask("Construction");
            Vector3Down = new Vector3(0f, -1f, 0f);

            FindMonuments();
            SetupGameTimer();
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (userData.RegisteredHunters.Contains(p.userID) && !LiveUiPlayers.Contains(p))
                {
                    LiveUiPlayers.Add(p);
                }
            }
            initTitles();
        }

        void Unload()
        {
            StopScheduleCoroutine();
            SaveData();

            foreach (var player in UiPlayers.ToList())
            {
                killUI(player);
            }
            foreach (var player in LiveUiPlayers.ToList())
            {
                killLiveUI(player);
            }
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file...");
            Config.WriteObject(Cfg, true);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            killUI(player);
            killLiveUI(player);
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZombieHunt", storedData);
            Interface.Oxide.DataFileSystem.WriteObject("ZombieHunt_users", userData);
            Interface.Oxide.DataFileSystem.WriteObject("ZombieHunt_scores", scoreData);
        }
        void SaveScoreData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZombieHunt_scores", scoreData);
        }
        void SaveAdminData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZombieHunt", storedData);
        }
        void SaveUserData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZombieHunt_users", userData);
        }

        #endregion

        #region CustomNPC
        public bool InitializeStates(ScientistBrain customNPCBrain) => false;

        public bool WantsToPopulateLoot(NpcZhunt customNpc, NPCPlayerCorpse npcplayerCorpse) => false;

        public byte[] GetCustomDesign() => null;
        #endregion

        #region Hooks
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (userData.RegisteredHunters.Contains(player.userID) && !LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Add(player);
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity is NpcZhunt && currentZombieList.ContainsKey(((BasePlayer)entity).userID))
            {
                //Puts("DEBUG: zombie got killed");
                ulong uid = ((BasePlayer)entity).userID;
                currentZombieList.Remove(uid);
                currentKills++;
            }
        }
        object OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
            if (oldValue < newValue || !(player is NpcZhunt) || newValue == 100f || !currentZombieList.ContainsKey(player.userID)) return null;
            float damage = oldValue - newValue;
            if (!(player.lastAttacker is BasePlayer)) return null;
            BasePlayer p = player.lastAttacker as BasePlayer;
            addDamageScore(p.userID, (int)Math.Ceiling(damage));
            //Puts("DEBUG: HC damage " + damage + " by "+ p + " ... " + player.health);
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null) return null;

            // either the target is a zombie || the initiator is
            if ((entity is BasePlayer && currentZombieList.ContainsKey(((BasePlayer)entity).userID)) || (info.Initiator is BasePlayer && currentZombieList.ContainsKey(((BasePlayer)info.Initiator).userID)))
            {

               //Puts("DEBUG: at " +entity+ " with " + info.damageTypes.GetMajorityDamageType() + " w: " +info.WeaponPrefab);


                BasePlayer player = info?.Initiator as BasePlayer;
                NpcZhunt zombie = info?.Initiator as NpcZhunt;
                if (player == null && zombie == null) return null;
                float distance = (Vector3.Distance(info.Initiator.transform.position, entity.transform.position));
                if (distance > 100)
                {
                    info.damageTypes.ScaleAll(0.0f);
                    return true;
                }
                float distanceMultiplyer = 1.0f - (distance / Cfg.ZombieDamageRange);

                // Player attacks
                if (player != null && zombie == null)
                {

                    if (!userData.RegisteredHunters.Contains(player.userID))
                    {
                        SendReply(player, "You did not yet join the hunters. \nUse <color=orange>/zhunt join</color> to start hunting Zombies");
                        info.damageTypes.ScaleAll(0.0f);
                        return 0.0f;
                    }
                    Rust.DamageType thisDamageType = info.damageTypes.GetMajorityDamageType();
                    if (thisDamageType == Rust.DamageType.Explosion || !info.Weapon)
                    {
                        SendReply(player, "Zombies are <color=red>immune to explosions</color>.");
                        info.damageTypes.ScaleAll(0.0f);
                        return 0.0f;
                    }
                    if (allowedDamageTypes.Contains(info.damageTypes.GetMajorityDamageType()))
                    {
                        if (info.Weapon != null && info.Weapon.ShortPrefabName.Contains("jackhammer"))
                        {
                            info.damageTypes.ScaleAll(0.6f);
                        }
                        else
                        {
                            info.damageTypes.ScaleAll(1.7f);
                        }
                    }
                    else
                    {
                        if (info.Weapon != null)
                        {
                            if (buffedWeapons.Contains(info.Weapon.ShortPrefabName))
                            {
                                info.damageTypes.Set(Rust.DamageType.Bullet, 550f);
                            }
                            else if (info.Weapon.ShortPrefabName == weaponMap[currentWeaknessShortname])
                            {
                                if (info.Weapon.ShortPrefabName.Contains("spas12"))
                                {
                                    info.damageTypes.Set(Rust.DamageType.Bullet, 25f);
                                }
                                else
                                {
                                    info.damageTypes.Set(Rust.DamageType.Bullet, 60f);
                                }
                            }
                            else if (info.Weapon.ShortPrefabName.Contains("m249"))
                            {
                                info.damageTypes.Set(Rust.DamageType.Bullet, 5f);
                            }
                            else if (info.Weapon.ShortPrefabName.Contains("spas12"))
                            {
                                info.damageTypes.Set(Rust.DamageType.Bullet, 2f);
                            }
                            else if (info.Weapon.ShortPrefabName.Contains("shotgun_pump"))
                            {
                                info.damageTypes.Set(Rust.DamageType.Bullet, 3f);
                            }
                            else
                            {
                                info.damageTypes.Set(Rust.DamageType.Bullet, 8f);
                            }
                        }


                    }
                    if (player.transform.position.y - entity.transform.position.y > 1)
                    {
                        //Puts("DEBUG: height-diff " + (player.transform.position.y - entity.transform.position.y));
                        info.damageTypes.ScaleAll(0.6f);
                    }
                    entity.lastAttacker = player;
                    info.damageTypes.ScaleAll(handleLastPlayerPosition(player, info.Weapon.ShortPrefabName) );
                    //Puts("DEBUG: damaging zombie for "+ info.damageTypes.Total() + " WITH " + info.damageTypes.GetMajorityDamageType() + " gun " + info.Weapon?.ShortPrefabName);
                }
                // Zombies attack
                if (zombie != null)
                {
                    //no damage to innocents
                    if (entity is BasePlayer && !userData.RegisteredHunters.Contains(((BasePlayer)entity).userID))
                    {
                        info.damageTypes.ScaleAll(0.0f);
                        return true;
                    }

                    float rand = Random.Range(1, 101);
                    if (rand >= storedData.ZombieAccuracyPercent) return true;

                    //Puts("Attacking building"); 
                    if (entity is BuildingBlock || entity is DecayEntity || entity.name.Contains("shopfront.metal") || entity.name.Contains("shutter.metal.embrasure") || entity.name.Contains("door") || entity.name.Contains("rug") || entity.name.Contains("sign"))
                    {
                        // protect innocents

                        info.damageTypes.ScaleAll(0.0f);
                        return true;
                        //Puts("DEBUG: ---- Zombie is attacking BASE with " + info.damageTypes.GetMajorityDamageType() + " w: " +info.WeaponPrefab +" for "+ info.damageTypes.Total());
                    }
                    else
                    {
                        Rust.DamageType damageType = info.damageTypes.GetMajorityDamageType();
                        //Puts("DEBUG: +++++ Zombie is attacking PLAYER with " + info.damageTypes.GetMajorityDamageType() + " w: " +info.WeaponPrefab +" for "+ info.damageTypes.Total()); 
                        if (info.WeaponPrefab != null && info.WeaponPrefab.name == "grenade.f1.deployed")
                        {
                            info.damageTypes.ScaleAll(0.4f);
                        }
                        else if (allowedDamageTypes.Contains(damageType))
                        {
                            info.damageTypes.ScaleAll(storedData.ZombieMeleeDamage);
                        }
                        else
                        {
                            info.damageTypes.ScaleAll(storedData.ZombieProjectileDamage);
                        }
                    }


                }

                //Puts("DEBUG: damage "+ info.damageTypes.Total());
            }
            return null;
        }
        private void OnEntityDeath(BaseCombatEntity victimEntity, HitInfo hitInfo)
        {
            // Ignore - there is no victim for some reason
            if (victimEntity == null)
                return;

            // Try to avoid error when entity was destroyed
            if (victimEntity.gameObject == null)
                return;

            NpcZhunt npc = victimEntity as NpcZhunt;
            if (npc?.userID != null && currentZombieList.ContainsKey(npc.userID))
            {
                //Puts("NPC " + npc + "died --- rare: " + npc.inventory.containerBelt.GetSlot(0).info.rarity + " ranged: " + npc.inventory.containerBelt.GetSlot(0));
                currentCorpseList.Add(npc.userID);
            }
        }
        private object CanBuild(Planner planner, Construction entity, Construction.Target target)
        {
            if (planner == null || entity == null)
            {
                return null;
            }
            var player = planner?.GetOwnerPlayer();
            if (player == null) return null;
            float distance = Vector3Ex.Distance2D(player.transform.position, currentSpawnPoint);
            if (distance <= (storedData.ZombieSpawnRadius + 50))
            {
                var priv = player.GetBuildingPrivilege();
                if (priv != null && priv.IsAuthed(player)) return null;

                SendReply(player, "You can't build in /zhunt area.\nWait for zombies to move on!");
                return false;
            }
            return null;
        }

        void OnEntitySpawned(BaseEntity entity) //handles smoke signals, backpacks, corpses(applying kit)  
        {
            //corpse handling
            if (entity == null) return;
            if (entity is NPCPlayerCorpse)
            {
                NPCPlayerCorpse corpse = entity as NPCPlayerCorpse;
                if (corpse == null) return;
                if (!currentCorpseList.Contains(corpse.playerSteamID)) return;
                NextTick(() =>
                {
                    if (corpse == null) return;
                    foreach(ItemContainer c in corpse.containers)
                    {
                        c.Clear();
                    }
                    corpse.Kill();
                });
            }
            if (entity is DroppedItemContainer)
            {
                DroppedItemContainer backpack = entity as DroppedItemContainer;

                NextTick(() =>
                {
                    if (backpack == null) return;
                    ulong id = backpack.playerSteamID;
                    if (currentCorpseList.Contains(id))
                    {
                        backpack.Kill();
                        currentCorpseList.Remove(id);
                    }
                });

            }
            // no build zone at zhunt
            if (entity.ShortPrefabName.Contains("cupboard.tool"))
            {
                BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
                if (player == null) return;
                float distance = Vector3Ex.Distance2D(entity.transform.position, currentSpawnPoint);
                // CHECK for TC distance to other TCs (of player) first
                if (distance <= (storedData.ZombieSpawnRadius + 50))
                {
                    entity.Kill(BaseNetworkable.DestroyMode.None);
                    Item tcitm = ItemManager.CreateByName("cupboard.tool", 1);
                    player.GiveItem(tcitm, BaseEntity.GiveItemReason.Generic);
                    SendReply(player, "You can't place a TC in /zhunt area.\nWait for zombies to move on!");
                    return;
                }
            }


        }

        #endregion
        #region Commands
        [ConsoleCommand("zhunt.close")]
        private void CmdClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || !HasPermission(arg.Player().UserIDString, permUse))
                return;
            killUI(player);
        }

        private void showAdminMessage(BasePlayer player)
        {
            string msg = "<color=orange>===== Zombie Hunt Admin tools ======</color>";
            msg += "\nCurrent Pop: " + currentZombieList.Count();
            msg += "\n\nUsage:";
            msg += "\n/zadm \t\t\t\tshow this info";
            msg += "\n/zadm forcemove \t\tforce moving";
            msg += "\n/zadm setnum n \t\t\tset total zombie number";
            msg += "\n/zadm setrad n \t\t\tset spawn radius";
            msg += "\n/zadm sett2 n \t\t\tupdate t2 base";
            msg += "\n/zadm sett3 n \t\t\tupdate t3 base";
            msg += "\n/zadm setrpk n \t\t\tupdate RP per kill base";
            msg += "\n/zadm addkit name \t\tadd kit name";
            msg += "\n/zadm removekit name \tadd kit name";
            msg += "\n/zadm addgunkit name \t\tadd kit name";
            msg += "\n/zadm removegunkit name \tadd kit name";
            msg += "\n/zadm setaccuracy x.y \tchange accuracy setting";
            msg += "\n/zadm setarmor x.y \t\tChange armor setting";
            msg += "\n/zadm setmelee x.y \t\tChange melee multiplier";
            msg += "\n/zadm setprojectile x.y \tChange projectile multiplier";
            msg += "\n/zadm setbuilding x.y \t\tChange building damage";
            msg += "\n/zadm setbonus n \t\tset round win RP bonus";

            SendReply(player, msg);
        }

        [ChatCommand("zadm")]
        private void ZombieAdmin(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args.Length == 0)
            {
                SendReply(player, "TEST: " + ItemManager.itemDictionaryByName[currentWeaknessShortname].displayName.english + " " + ItemManager.itemDictionaryByName[currentWeaknessShortname].name);
                showAdminMessage(player);
                return;
            }
            if (args.Length == 2)
            {

                int num = 0;
                float fnum = 0.0f;
                string name = "";
                object isKit;
                switch (args[0])
                {
                    case "forcemove":
                        lastRandomSpawn = DateTime.Now.AddSeconds(-Cfg.ZombieEventSeconds);
                        SendReply(player, "Forced move!");
                        break;
                    case "setbonus":
                        try
                        {
                            num = int.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setbonus X");
                            return;
                        }
                        storedData.RoundWinBonus = num;

                        SaveAdminData();
                        SendReply(player, "Round Bonus set to " + num + " RP");
                        break;
                    case "addkit":
                        name = args[1].ToLower();

                        isKit = Kits?.Call("isKit", name);
                        if (isKit is bool && (bool)isKit && !storedData.ZombieKitNames.Contains(name))
                        {
                            storedData.ZombieKitNames.Add(name);
                            SaveAdminData();
                            SendReply(player, "Kit added to zombie kits: " + name);
                        }
                        else
                        {
                            SendReply(player, "ERROR: not a valid kit: " + name);
                        }
                        break;
                    case "removekit":
                        name = args[1].ToLower();


                        if (storedData.ZombieKitNames.Contains(name))
                        {
                            storedData.ZombieKitNames.Remove(name);
                            SaveAdminData();
                            SendReply(player, "Kit Removed from zombie kits: " + name);
                        }
                        else
                        {
                            SendReply(player, "ERROR: not a valid kit: " + name);
                        }
                        break;
                    case "setrpk":
                        try
                        {
                            num = int.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setrpk X");
                            return;
                        }
                        storedData.BaseRPperKill = num;

                        SaveAdminData();
                        SendReply(player, "Round BaseRPperKill set to " + num);
                        break;
                    case "setrad":
                        try
                        {
                            num = int.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setrad X");
                            return;
                        }
                        if (num < storedData.ZombieSpawnRadius)
                        {
                            RoundEndActions();
                        }
                        storedData.ZombieSpawnRadius = num;

                        SaveAdminData();
                        SendReply(player, "Spawn Radius set to " + num);
                        break;
                    case "setnum":
                        try
                        {
                            num = int.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setnum X");
                            return;
                        }
                        if (num < storedData.ZombieNumber)
                        {
                            RoundEndActions();
                        }
                        storedData.ZombieNumber = num;

                        SaveAdminData();
                        SendReply(player, "Zombie number set to " + num);
                        break;
                    case "setgunpct":
                        try
                        {
                            num = int.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setgunpct X");
                            return;
                        }
                        if (num < storedData.GunZombiePercent)
                        {
                            RoundEndActions();
                        }
                        storedData.GunZombiePercent = num;

                        SaveAdminData();
                        SendReply(player, "Gun Zombie percent set to " + num);
                        break;
                    case "setspawn":
                        name = args[1].ToLower();
                        Vector3 pos = player.transform.position;
                        if (storedData.SpawnPoints.ContainsKey(name))
                        {
                            storedData.SpawnPoints[name] = pos;
                        }
                        else
                        {
                            storedData.SpawnPoints.Add(name, pos);
                        }
                        SendReply(player, "Set spawnpoint: " + name + " at " + pos);
                        SaveAdminData();
                        break;

                    case "setaccuracy":
                        try
                        {
                            fnum = float.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setaccurary X.Y");
                            return;
                        }
                        storedData.ZombieAccuracyPercent = fnum;

                        SaveAdminData();
                        SendReply(player, "Zombie accuracy % set to " + fnum);
                        break;
                    case "setarmor":
                        try
                        {
                            fnum = float.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setarmor X.Y");
                            return;
                        }
                        storedData.ZombieArmor = fnum;

                        SaveAdminData();
                        SendReply(player, "Zombie armor set to " + fnum);
                        break;
                    case "setbuilding":
                        try
                        {
                            fnum = float.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setbuilding X.Y");
                            return;
                        }
                        storedData.ZombieBuildingDamage = fnum;

                        SaveAdminData();
                        SendReply(player, "Zombie building damage set to " + fnum);
                        break;

                    case "setmelee":
                        try
                        {
                            fnum = float.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setmelee X.Y");
                            return;
                        }
                        storedData.ZombieMeleeDamage = fnum;

                        SaveAdminData();
                        SendReply(player, "Zombie melee multiplier set to " + fnum);
                        break;

                    case "setprojectile":
                        try
                        {
                            fnum = float.Parse(args[1]);
                        }
                        catch (FormatException)
                        {
                            SendReply(player, "Use /zadm setprojectile X.Y");
                            return;
                        }
                        storedData.ZombieProjectileDamage = fnum;

                        SaveAdminData();
                        SendReply(player, "Zombie projectile multiplier set to " + fnum);
                        break;
                    default:
                        showAdminMessage(player);
                        break;
                }
            }
        }
        [ChatCommand("zscore")]
        private void ZhuntScoreCommand(BasePlayer player, string command, string[] args)
        {
            reloadUI(player);
        }

        private void showUsageMessage(BasePlayer player)
        {
            string msg = "<color=orange>===== Zombie Hunt (v. " + Plugin.Version + " by " + Plugin.Author + ") ======</color>";
            msg += "\nZombies only attack players that JOINed as hunters!";
            msg += "\n'Kills' are based on damage done to prevent kill-stealing";
            msg += "\n\n<color=green>Usage:</color>";
            msg += "\n/zhunt \t\t\tshow this info";
            msg += "\n/zscore \t\t\tshow Highscores";
            msg += "\n/zhunt join\t\t\tJoin the hunt";
            msg += "\n/zhunt leave\t\tBe safe from Zombies";
            msg += "\n/zhunt where\t\tshow the zombie location and timer";
            msg += "\n/zhunt tp\t\t\tteleport into the zombie area";
            msg += "\n\n<color=green>Loot & RP:</color>";
            msg += "\n/loot \t\t\t\tAfter the round: \n\t\t\t\t - spawn crates with all your loot!\n\t\t\t\t - RP + 1-4 items (based on # kills)";
            SendReply(player, msg);
        }

        [ChatCommand("zhunt")]
        private void CmdZhunt(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                showUsageMessage(player);
                return;
            }
            string msg = "";
            switch (args[0])
            {
                case "join":
                    if (!userData.RegisteredHunters.Contains(player.userID))
                    {
                        userData.RegisteredHunters.Add(player.userID);
                        msg += "You are now a licensed <color=green>Zombie Hunter</color>. Happy hunting!";
                        SendReply(player, msg);
                        if (!LiveUiPlayers.Contains(player))
                        {
                            LiveUiPlayers.Add(player);
                        }
                    }
                    else
                    {
                        msg += "You did already join the hunters. \nUse <color=orange>/zhunt leave</color> to leave and be safe from Zombies";
                        SendReply(player, msg);
                    }
                    SaveUserData();
                    break;
                case "leave":
                    if (getKillsForPlayer(player.userID) == 0)
                    {
                        if (userData.RegisteredHunters.Contains(player.userID))
                        {
                            userData.RegisteredHunters.Remove(player.userID);
                            killLiveUI(player);
                            roundDamageCounter.Remove(player.userID);
                            lastPlayerPosition.Remove(player.userID);
                            msg += "You are now <color=green>safe</color>.\nRound progress deleted!";
                            SendReply(player, msg);
                        }
                        else
                        {
                            msg += "You did not yet join the hunters. \nUse <color=orange>/zhunt join</color> to start hunting Zombies";
                            SendReply(player, msg);
                        }
                        SaveUserData();
                    }
                    else
                    {
                        SendReply(player, "<color=red>WARNING:</color> this will delete your round-progress!\nUse <color=orange>/zhunt leavenow</color> to actually leave!");
                    }
                    break;
                case "leavenow":
                    if (userData.RegisteredHunters.Contains(player.userID))
                    {
                        userData.RegisteredHunters.Remove(player.userID);
                        killLiveUI(player);
                        roundDamageCounter.Remove(player.userID);
                        lastPlayerPosition.Remove(player.userID);
                        msg += "You are now <color=green>safe</color>.\nRound progress deleted!";
                        SendReply(player, msg);
                    }
                    else
                    {
                        msg += "You did not yet join the hunters. \nUse <color=orange>/zhunt join</color> to start hunting Zombies";
                        SendReply(player, msg);
                    }
                    SaveUserData();
                    break;
                case "where":
                    msg += "Zombies are currently in grid <color=green>" + currentSpawnGrid + "</color>";
                    msg += "\nThey will move in <color=orange>" + Math.Ceiling(Cfg.ZombieEventSeconds - (DateTime.Now - lastRandomSpawn).TotalSeconds) + " seconds</color>";
                    SendReply(player, msg);
                    break;
                case "tp":
                    float distance = Vector3Ex.Distance2D(player.transform.position, currentSpawnPoint);
                    if (distance <= (storedData.ZombieSpawnRadius + 50))
                    {
                        SendReply(player, "You are already in the ZHunt area!");
                        break;
                    }
                    else
                    {
                        TPapi?.Call("TeleportPlayerTo", player, GetNewPosition());
                        SendReply(player, "Teleported to Zombie infested area!");
                    }
                    break;
                default:
                    showUsageMessage(player);
                    break;
            }
        }
        #endregion


        #region Spawning
        internal NpcZhunt SpawnNPC(Vector3 pos)
        {
            pos.y = TerrainMeta.HeightMap.GetHeight(pos);
            object position = FindPointOnNavmesh(pos, 10f);
            if (position is Vector3 && (Vector3)position != Vector3.zero)
            {
                NpcZhunt scientistNPC = InstantiateEntity((Vector3)position, Quaternion.Euler(0, 0, 0));

                if (scientistNPC == null) return null;

                scientistNPC.enableSaving = false;
                scientistNPC.Spawn();
                //  _allNPC.Add(scientistNPC);
                scientistNPC.gameObject.SetActive(true);
                NextTick(() =>
                {
                    scientistNPC.InitNewSettings((Vector3)position, Cfg.ZombieHealth);
                    if (storedData.ZombieKitNames.Count > 0)
                        scientistNPC.setUpGear(storedData.ZombieKitNames.GetRandom());
                    else
                    scientistNPC.setUpGear("");
                });
                scientistNPC.displayName = "Zhunt Zombie";

                if (scientistNPC != null)
                    return scientistNPC;
            }

            return null;
        }

        private static NpcZhunt InstantiateEntity(Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = Instantiate.GameObject(GameManager.server.FindPrefab("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab"), position, Quaternion.identity);
            gameObject.name = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_full_any.prefab";
            gameObject.SetActive(false);
            ScientistNPC scientistNPC = gameObject.GetComponent<ScientistNPC>();
            ScientistBrain defaultBrain = gameObject.GetComponent<ScientistBrain>();

            defaultBrain._baseEntity = scientistNPC;

            NpcZhunt component = gameObject.AddComponent<NpcZhunt>();
            NpcZhuntBrain brains = gameObject.AddComponent<NpcZhuntBrain>();
            brains.Pet = false;
            brains.AllowedToSleep = false;
            CopyFields<NPCPlayer>(scientistNPC, component);

            brains._baseEntity = component;

            UnityEngine.Object.DestroyImmediate(defaultBrain, true);
            UnityEngine.Object.DestroyImmediate(scientistNPC, true);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
            UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());

            return component;
        }

        private static void CopyFields<T>(T src, T dst)
        {
            var fields = typeof(T).GetFields();

            foreach (var field in fields)
            {
                if (field.IsStatic) continue;
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }


        private static List<Vector3> GetCircumferencePositions(Vector3 center, float radius, float next, float y) // Thanks to ArenaWallGenerator
        {
            var positions = new List<Vector3>();
            float degree = 0f;

            while (degree < 360)
            {
                float angle = (float)(2 * Math.PI / 360) * degree;
                float x = center.x + radius * (float)Math.Cos(angle);
                float z = center.z + radius * (float)Math.Sin(angle);
                var position = new Vector3(x, center.y, z);
                object success = FindPointOnNavmesh(position, 10f);
                if (success != null)
                    positions.Add((Vector3)success);

                degree += next;
            }

            return positions;
        }

        private static NavMeshHit navmeshHit;

        private static RaycastHit raycastHit;

        private static Collider[] _buffer = new Collider[256];

        private const int WORLD_LAYER = 65536;

        internal static object FindPointOnNavmesh(Vector3 targetPosition, float maxDistance = 4f)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 position = i == 0 ? targetPosition : targetPosition + (UnityEngine.Random.onUnitSphere * maxDistance);
                if (NavMesh.SamplePosition(position, out navmeshHit, maxDistance, 1))
                {
                    if (IsInRockPrefab2(navmeshHit.position))
                        continue;

                    if (IsInRockPrefab(navmeshHit.position))
                        continue;

                    if (IsNearWorldCollider(navmeshHit.position))
                        continue;

                    return navmeshHit.position;
                }
            }
            return null;
        }

        internal static bool IsInRockPrefab(Vector3 position)
        {
            Physics.queriesHitBackfaces = true;

            bool isInRock = Physics.Raycast(position, Vector3.up, out raycastHit, 20f, WORLD_LAYER, QueryTriggerInteraction.Ignore) &&
                            blockedColliders.Any(s => raycastHit.collider?.gameObject?.name.Contains(s) ?? false);

            Physics.queriesHitBackfaces = false;

            return isInRock;
        }

        internal static bool IsInRockPrefab2(Vector3 position)
        {
            Vector3 p1 = position + new Vector3(0, 20f, 0);
            Vector3 p2 = position + new Vector3(0, -2f, 0);
            Vector3 diff = p2 - p1;
            RaycastHit[] hits;
            hits = Physics.RaycastAll(p1, diff, diff.magnitude);
            if (hits.Length > 0)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    if (hit.collider != null)
                    {
                        bool isRock = blockedColliders.Any(s => hit.collider?.gameObject?.name.ToLower().Contains(s) ?? false);
                        if (isRock) return true;
                    }
                }
            }
            return false;
        }

        internal static bool IsNearWorldCollider(Vector3 position)
        {
            Physics.queriesHitBackfaces = true;

            int count = Physics.OverlapSphereNonAlloc(position, 2f, _buffer, WORLD_LAYER, QueryTriggerInteraction.Ignore);
            Physics.queriesHitBackfaces = false;

            int removed = 0;
            for (int i = 0; i < count; i++)
            {
                if (acceptedColliders.Any(s => _buffer[i].gameObject.name.Contains(s)))
                    removed++;
            }


            return count - removed > 0;
        }

        private static readonly string[] acceptedColliders = new string[] { "road", "carpark", "rocket_factory", "range", "train_track", "runway", "_grounds", "concrete_slabs", "lighthouse", "cave", "office", "walkways", "sphere", "tunnel", "industrial", "junkyard" };

        private static readonly string[] blockedColliders = new string[] { "rock", "junk", "range", "invisible", "cliff", "prevent_movement", "formation_" };
        #endregion

        #region NpcZhunt
        public class NpcZhunt {
            // This code is removed, since I didn't write it myself. To make it work, find an AI class or refactor to use chaos NPCs
        }
        #endregion


        #region Brain
        public class NpcZhuntBrain
        {
            // This code is removed, since I didn't write it myself. To make it work, find an AI Brain class or refactor to use chaos NPCs

        }
        #endregion


        #region Functions 

        private void initTitles()
        {
            ZNTitleManager?.Call("UpsertTitle", getTitleID(), "Z-Hunt", "Zombies", "Legendary Z-Hunter", "Legendary Z-Huntress", "ZombieHunter", "ZombieHuntress");
        }

        private string getTitleID()
        {
            return Plugin.Name + "_zhunt";

        }

        private void TMP_SimulateAction()
        {
            roundDamageCounter.Add(24235235, 2900);
            roundDamageCounter.Add(95643, 100);
            roundDamageCounter.Add(95234643, 440);
            roundDamageCounter.Add(9563443, 3000);
            roundDamageCounter.Add(95673443, 4800);
            currentKills = 112;
        }

        private float handleLastPlayerPosition(BasePlayer player, string gun = "")
        {
            if (lastPlayerPosition.ContainsKey(player.userID))
            {
                if(lastPlayerPosition[player.userID].Key < DateTime.Now.AddSeconds(-30))
                {
                    //Puts("DEBUG: Camping for " + (DateTime.Now - lastPlayerPosition[player.userID].Key) + "s");
                    if (Vector3Ex.Distance2D(lastPlayerPosition[player.userID].Value, player.transform.position) <= 15)
                    {
                        //Puts("DEBUG: Didn't move! only" + Vector3Ex.Distance2D(lastPlayerPosition[player.userID].Value, player.transform.position) + "m");
                        SendReply(player, "<color=red>WARNING!</color> Attacking from your current location will reduce damage to 5% (anti camping)." );
                        
                        return 0.05f;
                    }
                    else
                    {
                        //update location data
                        lastPlayerPosition[player.userID] = new KeyValuePair<DateTime, Vector3>(DateTime.Now, player.transform.position);
                        return 1.0f;
                    }
                }
            }
            else
            {
                lastPlayerPosition.Add(player.userID, new KeyValuePair<DateTime, Vector3>(DateTime.Now, player.transform.position));
                return 1.0f;
            }
            return 1.0f;
        }

        private int getKillsFromDamage(int damage)
        {
            return (int)Math.Floor(damage / Cfg.ZombieHealth);
        }
        private int getKillsForPlayer(ulong playerID)
        {
            return roundDamageCounter.ContainsKey(playerID) ? getKillsFromDamage(roundDamageCounter[playerID]) : 0;
        }
        private float getKillPercent(ulong playerID)
        {
            if (roundDamageCounter.Count() == 0) return 0f;
            int playerDamage = roundDamageCounter.ContainsKey(playerID) ? roundDamageCounter[playerID] : 0;
            float totalDamage = (float)roundDamageCounter.Sum(x => x.Value);
            float percent = (float)(playerDamage / totalDamage) * 100f;
            return (float)Math.Round(percent, 2);
        }

        private int getRoundBonus()
        {
            if (roundDamageCounter.Count() == 0) return 0;
            int roundAboveMinPlayers = -1;
            int minDamage = storedData.MinKillCount * 100;
            foreach (int dmg in roundDamageCounter.Values)
            {
                if (dmg >= minDamage) roundAboveMinPlayers++;
            }
            int realBonus = (roundAboveMinPlayers > 0) ? storedData.RoundWinBonus * roundAboveMinPlayers : 0;

            return realBonus;
        }

        private int getRP(ulong playerID)
        {
            int kills = getKillsForPlayer(playerID);
            if (kills == 0) return 0;
            float killPercent = getKillPercent(playerID) / 100f;

            int killRP = kills * storedData.BaseRPperKill;
            int roundRP = 0;
            if (kills >= storedData.MinKillCount)
            {
                roundRP = (int)Math.Ceiling(getRoundBonus() * killPercent);
            }

            //Puts("DEBUG: dd k:" + killRP + " r:" + roundRP + " a:" + killPercent);
            return killRP + roundRP;

        }
        private Dictionary<string, float> getStatDict(ulong playerID)
        {
            if (currentKills == 0)
            {
                return new Dictionary<string, float>()
                {
                    ["kills"] = 0,
                    ["killPercent"] = 0,
                    ["killRP"] = 0,
                    ["bonusRP"] = 0,
                    ["totalRP"] = 0,
                };
            }
            int kills = getKillsForPlayer(playerID);
            float killPercent = getKillPercent(playerID) / 100f;

            int killRP = kills * storedData.BaseRPperKill;
            int roundRP = 0;

            if (kills >= storedData.MinKillCount)
            {
                roundRP = (int)Math.Ceiling(getRoundBonus() * killPercent);
            }
            Dictionary<string, float> ret = new Dictionary<string, float>()
            {
                ["kills"] = kills,
                ["killPercent"] = killPercent * 100,
                ["killRP"] = killRP,
                ["bonusRP"] = roundRP,
                ["totalRP"] = killRP + roundRP,
            };

            //Puts("DEBUG: k:" + killRP + " r:" + roundRP + " a:" + killPercent);
            return ret;

        }

        private void addDamageScore(ulong playerID, int amount)
        {
            if (roundDamageCounter.ContainsKey(playerID))
            {
                roundDamageCounter[playerID] += amount;
            }
            else
            {
                roundDamageCounter.Add(playerID, amount);
            }
        }
        private Vector3 getRandomSpawnArea()
        {
            for (int i = 0; i < 100; i++)
            {
                Vector3 randomPos = new Vector3(
                    UnityEngine.Random.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2),
                    150,
                    UnityEngine.Random.Range(-TerrainMeta.Size.z / 2, TerrainMeta.Size.z / 2)
                );
                if (TerrainMeta.HeightMap.GetHeight(randomPos) < 0f) continue;
                if (isNearSafeZone(randomPos)) continue;

                return randomPos;
            }
            return currentSpawnPoint;
        }
        private bool isNearSafeZone(Vector3 spot)
        {
            foreach (Vector3 zone in safeZones)
            {
                if (Vector3Ex.Distance2D(spot, zone) <= 300)
                {
                    //Puts("DEBUG: prevented to spawn near Monument at " + zone);
                    return true;
                }
            }
            return false;
        }

        public static string getGrid(Vector3 position)  // Credit: Jake_Rich. Fix by trixxxi (y,x -> x,y switch). Fix by yetzt (150 -> 146.3)
        {
            // Code removed, since i didn't write it
            return "removed since I didn't write it";
        }

        public static string NumberToLetter(int num) // Credit: Jake_Rich
        {
            // Code removed, since i didn't write it
            return "removed since I didn't write it";
        }

        /*
        private void giveRecycledItemToPlayer(BasePlayer player, Item itm)
        {
            // items that can't be crafted get directly into the inventory
            if(itm.info?.Blueprint == null || recWhitelist.Contains(itm.info.shortname)) 
            {
                player.GiveItem(ItemManager.Create(itm.info, itm.amount, 0UL), BaseEntity.GiveItemReason.PickedUp);
                return;
            }
            float num1 = 1.0f; // recycle efficiency
            int num2 = 1;
            if (itm.amount > 1)
                num2 = Mathf.CeilToInt(Mathf.Min((float) itm.amount, (float) itm.info.stackable * 0.1f));
            using (List<ItemAmount>.Enumerator enumerator = itm.info.Blueprint.ingredients.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                ItemAmount current = enumerator.Current;
                if (!(current.itemDef.shortname == "scrap"))
                {
                    float num3 = current.amount / (float) itm.info.Blueprint.amountToCreate;
                    int num4 = 0;
                    if ((double) num3 <= 1.0)
                    {
                    for (int index = 0; index < num2; ++index)
                    {
                        if ((double) UnityEngine.Random.Range(0.0f, 1f) <= (double) num3 * (double) num1)
                        ++num4;
                    }
                    }
                    else
                    num4 = Mathf.CeilToInt(Mathf.Clamp(num3 * num1 * UnityEngine.Random.Range(1f, 1f), 0.0f, current.amount) * (float) num2);
                    if (num4 > 0)
                    {
                    int num5 = Mathf.CeilToInt((float) num4 / (float) current.itemDef.stackable);
                    for (int index = 0; index < num5; ++index)
                    {
                        int iAmount = num4 > current.itemDef.stackable ? current.itemDef.stackable : num4;
                        player.GiveItem(ItemManager.Create(current.itemDef, iAmount, 0UL), BaseEntity.GiveItemReason.PickedUp);
                        num4 -= iAmount;
                        if (num4 <= 0)
                        break;
                    }
                    }
                }
                }
            }
        }
        */

        private void CheckResettleZombies()
        {
            // only work every ZombieEventSeconds
            if ((DateTime.Now - lastRandomSpawn).TotalSeconds <= Cfg.ZombieEventSeconds && currentSpawnPoint != null)
            {
                return;
            }
            //time to resettle
            currentSpawnPoint = getRandomSpawnArea();
            lastRandomSpawn = DateTime.Now;
            currentSpawnGrid = getGrid(currentSpawnPoint);
            RoundEndActions();
            Puts("Round ended, resettling zombies to  " + currentSpawnGrid);
            //TMP_SimulateAction();
            string msg = "";
            msg += "<color=red>WARNING:</color> Zombies are now moving to <color=green>" + currentSpawnGrid + "</color>";
            msg += "\nTheir weaknes is: <color=green>" + ItemManager.itemDictionaryByName[currentWeaknessShortname].displayName.english + "</color>\n";
            //Puts("DEBUG "+ LastWinnerName + " " + LastWinnerKills);
            if (LastWinnerName != "")
            {
                msg += "\n<color=orange>" + LastWinnerName + "</color> has won this round with <color=orange>" + LastWinnerKills + "</color> killed Zombies";
            }
            msg += "\nUse /zhunt and /zscore for details!";

            Server.Broadcast(msg);
        }
        private void SetupGameTimer()
        {
            StartScheduleCoroutine();
        }


        private void StopScheduleCoroutine()
        {
            RoundEndActions();
            if (scheduleCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(scheduleCoroutine);
                scheduleCoroutine = null;
            }
        }

        private void StartScheduleCoroutine()
        {
            StopScheduleCoroutine();
            timer.Once(0.2f, () =>
            {
                scheduleCoroutine = ServerMgr.Instance.StartCoroutine(ScheduleCoroutine());
            });
        }

        private IEnumerator ScheduleCoroutine()
        {
            while (true)
            {
                CheckResettleZombies();

                if (LiveUiPlayers.Count > 0)
                {
                    foreach (BasePlayer p in LiveUiPlayers)
                    {
                        reloadLiveUI(p);
                    }
                }
                if (currentZombieList.Count < storedData.ZombieNumber)
                {
                    SpawnZombie(storedData.ZombieNumber - currentZombieList.Count, Vector3.zero);
                }

                yield return Coroutines.WaitForSeconds(2f);
            }
        }



        private Vector3 GetNewPosition()
        {
            return TPapi.Call<Vector3>("GetPositionAround", currentSpawnPoint, (float)storedData.ZombieSpawnRadius, 10f);
        }

        private static NpcZhunt InstantiateEntity(Vector3 position)
        {
            var prefabName = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
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
            return go.GetComponent<NpcZhunt>();
        }

        private void SpawnZombie(int amount, Vector3 spawnPoint)
        {
            // make sure there are no more than x zombies online at a time
            int current_entities = (currentZombieList.Count >= 50) ? amount - 1 : 0;
            while (current_entities < amount)
            {
                current_entities++;
                Vector3 thisSpawnPoint = (spawnPoint == Vector3.zero) ? GetNewPosition() : spawnPoint;

                NpcZhunt entity = SpawnNPC(thisSpawnPoint);
                if (entity == null) { return; }
                currentZombieList.Add(entity.userID, entity);
                
            }
        }
        void ClearZombies()
        {
            foreach (var bot in currentZombieList.ToDictionary(pair => pair.Key, pair => pair.Value).Where(bot => bot.Value != null))
                currentZombieList[bot.Key].Kill();
        }

        void RoundEndActions()
        {
            ClearZombies();
            LastWinnerName = "";
            LastWinnerKills = 0;
            if (roundDamageCounter.Count > 0)
            {
                allParticipantActions();
                ulong winner = roundDamageCounter.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
                ZNTitleManager?.Call("CheckScore", getTitleID(), (float)getKillsFromDamage(roundDamageCounter[winner]), winner);
                BasePlayer player = FindPlayer(winner);
                if (player != null)
                {
                    if (scoreData.winCounter.ContainsKey(winner))
                    {
                        scoreData.winCounter[winner]++;
                    }
                    else
                    {
                        scoreData.winCounter.Add(winner, 1);
                    }
                    SaveScoreData();
                    LastWinnerName = player.displayName;
                    LastWinnerKills = getKillsFromDamage(roundDamageCounter[winner]);
                }
            }
            currentWeaknessShortname = weaponList.GetRandom((uint)DateTimeOffset.Now.ToUnixTimeMilliseconds());
            roundDamageCounter.Clear();
            lastPlayerPosition.Clear();
            currentZombieList.Clear();
            currentKills = 0;
        }
        private void allParticipantActions()
        {
            foreach (ulong playerID in roundDamageCounter.Keys)
            {
                Dictionary<string, float> stats = getStatDict(playerID);
                //add score stats
                if (scoreData.killCounter.ContainsKey(playerID))
                {
                    scoreData.killCounter[playerID] += getKillsFromDamage(roundDamageCounter[playerID]);
                }
                else
                {
                    scoreData.killCounter.Add(playerID, getKillsFromDamage(roundDamageCounter[playerID]));
                }
                // give loot
                LootBoxSpawner?.Call("storeZhuntClaim", playerID, 0, ((int)Math.Floor(stats["kills"])), ((int)Math.Floor(stats["totalRP"])));
                BasePlayer player = BasePlayer.FindAwakeOrSleeping("" + playerID);
                if (!player) continue;

                string msg = "<color=orange>Round statistics:</color>";
                msg += "\nYour Kills:\t\t\t" + stats["kills"] + " (" + stats["killPercent"] + "%)";
                msg += "\nTotal RP earend:\t\t" + stats["totalRP"];
                msg += "\n-----";
                msg += "\nRP from kills:\t\t" + stats["killRP"];
                msg += "\nRP from bonus:\t\t" + stats["bonusRP"];

                SendReply(player, msg);
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

        private List<ulong> sortKillers(Dictionary<ulong, int> playerSet)
        {
            List<ulong> retVal = new List<ulong>();
            if (playerSet.Count == 0) return retVal;

            foreach (KeyValuePair<ulong, int> d in playerSet.OrderByDescending(x => x.Value))
            {
                retVal.Add(d.Key);
            }
            return retVal;
        }
        private void FindMonuments()
        {
            List<MonumentInfo> monuments;
            if (TerrainMeta.Path == null)
            {
                monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().ToList();
            }
            else monuments = TerrainMeta.Path.Monuments;

            foreach (var monument in monuments)
            {
                //Puts("DEBUG: Monument " + monument.name);

                if (monument.name.Contains("/compound") ||
                    monument.name.Contains("bandit_town") ||
                    monument.name.Contains("launch_site") ||
                    monument.name.Contains("harbor") ||
                    monument.name.Contains("fishing") ||
                    monument.name.Contains("sphere") ||
                    monument.name.Contains("water") ||
                    monument.name.Contains("airfield")
                    )
                {
                    safeZones.Add(monument.transform.position);
                    //Puts("DEBUG: adding monument " + monument.name + " at " + monument.transform.position);
                }
            }
        }
        #endregion

        #region GUI
        private HashSet<BasePlayer> LiveUiPlayers = new HashSet<BasePlayer>();
        private void reloadLiveUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            if (!LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Add(player);
            }
            CuiHelper.DestroyUi(player, mainName + "_live");
            displayLiveUI(player, errorMsg);
        }
        private void killLiveUI(BasePlayer player)
        {
            if (LiveUiPlayers.Contains(player))
            {
                LiveUiPlayers.Remove(player);
                CuiHelper.DestroyUi(player, mainName + "_live");
            }
        }

        private void displayLiveUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            GUILiveElement(player, mainName + "_live", errorMsg);
        }

        private void GUILiveElement(BasePlayer player, string name, string errorMsg = "none")
        {
            var mainName = name;
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
                    AnchorMin =  "0.5 0",
                    AnchorMax = "0.5 0",
                    OffsetMin = "-220 85",
                    OffsetMax = "200 105"
                },
                CursorEnabled = false
            }, "Hud", mainName);

            var secondsTilReward = Math.Floor((Cfg.ZombieEventSeconds) - (DateTime.Now - lastRandomSpawn).TotalSeconds);
            int totalKills = currentKills;
            float colWidth = 0.25f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "/zhunt at: " +currentSpawnGrid + " ("+secondsTilReward+"s)",
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = "0.01 0",
                    AnchorMax = colWidth + " 1"
                }
            }, mainName);

            int kills = getKillsForPlayer(player.userID);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Kills: " + kills + "/"+totalKills+ " ("+getKillPercent(player.userID) +"%)",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = colWidth +" 0",
                    AnchorMax = (colWidth+0.2f) +" 1"
                }
            }, mainName);
            colWidth += 0.2f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "RP: " + getRP(player.userID),
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = colWidth +" 0",
                    AnchorMax = (colWidth+0.2f) +" 1"
                }
            }, mainName);
            colWidth += 0.2f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Weakness: " + ItemManager.itemDictionaryByName[currentWeaknessShortname].displayName.english,
                    FontSize = 10,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.3 1 0.3 1"
                },
                RectTransform =
                {
                    AnchorMin = colWidth +" 0",
                    AnchorMax = (colWidth+0.35f) +" 1"
                }
            }, mainName);
            colWidth += 0.35f;


            CuiHelper.AddUi(player, elements);
        }


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
                foreach (string ui in uiElements)
                {
                    CuiHelper.DestroyUi(player, ui);
                }
            }
        }

        private void displayUI(BasePlayer player, string errorMsg = globalNoErrorString)
        {
            GUIHeaderElement(player, mainName + "_head", errorMsg);
            GUIStatsElement(player, mainName + "_current", "current", roundDamageCounter);
            GUIStatsElement(player, mainName + "_alltime", "alltime", scoreData.killCounter);
            GUIFooterElement(player, mainName + "_foot");
        }

        private const string globalNoErrorString = "none";
        private const string mainName = "ZhuntUI";
        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();
        private string[] uiElements =
        {
            mainName+"_head",
            mainName+"_current",
            mainName+"_foot",
            mainName+"_alltime"
        };
        private float globalLeftBoundary = 0.1f;
        private float globalRighttBoundary = 0.9f;
        private float globalTopBoundary = 0.95f;
        private float globalBottomBoundary = 0.1f;
        private float globalSpace = 0.01f;
        private float eContentWidth = 0.395f;
        private float eHeadHeight = 0.05f;
        private float eFootHeight = 0.05f;
        private float eSlotHeight = 0.123f;

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
                    Text = Plugin.Name + " Highscore",
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

        private void GUIStatsElement(BasePlayer player, string elUiId, string list, Dictionary<ulong, int> players)
        {

            var elements = new CuiElementContainer();
            float leftBoundary = (list == "current") ? globalLeftBoundary : globalLeftBoundary + eContentWidth + globalSpace;
            float rightBoundary = (list == "current") ? globalLeftBoundary + eContentWidth : globalLeftBoundary + globalSpace + 2 * eContentWidth;

            float topBoundary = globalTopBoundary - eHeadHeight - globalSpace;
            float botBoundary = globalBottomBoundary + eFootHeight + globalSpace;
            float positionNumber = 0;
            float rowHeight = 0.025f;
            BasePlayer thisPlayer;

            elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 0.5",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary + " " + (botBoundary),
                    AnchorMax = rightBoundary + " " + (topBoundary)
                },
                CursorEnabled = true
            }, "Hud", elUiId);

            topBoundary = 1f - globalSpace;
            botBoundary = topBoundary - (positionNumber + 1) * 2f * rowHeight;
            leftBoundary = 0f + globalSpace;
            rightBoundary = 1f - globalSpace;


            string headerText;
            if (list == "current")
            {
                headerText = "Current Round (";
                headerText += "" + Math.Ceiling(Cfg.ZombieEventSeconds - (DateTime.Now - lastRandomSpawn).TotalSeconds) + "s left)";

                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = headerText,
                        FontSize = 18,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary +" " + (botBoundary),
                        AnchorMax = rightBoundary +" " + (topBoundary)
                    }
                }, elUiId);

                topBoundary = botBoundary - 0.5f * globalSpace;
                botBoundary = topBoundary - (positionNumber + 1) * 4f * rowHeight;

                headerText = "<color=#333333>Total Kills:</color>              " + getKillsFromDamage(roundDamageCounter.Sum(x => x.Value));
                headerText += "\n<color=#333333>Min Bonus Kills:</color>     " + storedData.MinKillCount;
                headerText += "\n<color=#333333>Round Bonus:</color>          " + getRoundBonus() + " (split between players based on %)";
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = headerText,
                        FontSize = 12,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary +" " + (botBoundary),
                        AnchorMax = rightBoundary +" " + (topBoundary)
                    }
                }, elUiId);
            }
            else
            {
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "All Wipe",
                        FontSize = 18,
                        Align = TextAnchor.UpperLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary +" " + (botBoundary),
                        AnchorMax = rightBoundary +" " + (topBoundary)
                    }
                }, elUiId);
            }





            positionNumber++;
            leftBoundary = 0f + globalSpace;
            rightBoundary = 1f - globalSpace;
            topBoundary = botBoundary - 0.5f * globalSpace;
            botBoundary = topBoundary - rowHeight;
            float nameLength = 0.54f;

            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Player",
                    FontSize = 11,
                    Align = TextAnchor.MiddleLeft,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary + " " + (botBoundary),
                    AnchorMax = rightBoundary + " " + (topBoundary)
                }
            }, elUiId);

            leftBoundary = leftBoundary + nameLength;
            rightBoundary = leftBoundary + 0.2f;
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Zombie Kills",
                    FontSize = 11,
                    Align = TextAnchor.MiddleRight,
                    Color = "1 1 1 1"
                },
                RectTransform =
                {
                    AnchorMin = leftBoundary + " " + (botBoundary),
                    AnchorMax = rightBoundary + " " + (topBoundary)
                }
            }, elUiId);

            if (list != "current")
            {

                leftBoundary = rightBoundary + 0.02f;
                rightBoundary = leftBoundary + 0.2f;
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "Round Wins",
                        FontSize = 11,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " " + (botBoundary),
                        AnchorMax = rightBoundary + " " + (topBoundary)
                    }
                }, elUiId);

            }
            else
            {

                leftBoundary = rightBoundary + 0.02f;
                rightBoundary = leftBoundary + 0.2f;
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = "%",
                        FontSize = 11,
                        Align = TextAnchor.MiddleRight,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " " + (botBoundary),
                        AnchorMax = rightBoundary + " " + (topBoundary)
                    }
                }, elUiId);

            }


            foreach (ulong playerID in sortKillers(players))
            {
                thisPlayer = FindPlayer(playerID);
                //if (thisPlayer == null) continue;
                //if (!thisPlayer.IsConnected || thisPlayer.IsSleeping()) continue;
                positionNumber++;
                if (positionNumber > 30) continue;


                Dictionary<string, float> stats = getStatDict(playerID);

                leftBoundary = 0f + globalSpace;
                rightBoundary = 1f - globalSpace;
                topBoundary = botBoundary - 0.5f * globalSpace;
                botBoundary = topBoundary - rowHeight;

                elements.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.19 0.19 0.19 0.6"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " " + (botBoundary),
                        AnchorMax = rightBoundary + " " + (topBoundary)
                    },
                    CursorEnabled = true
                }, elUiId);
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = (thisPlayer != null) ? new Regex(@"[^A-Za-z0-9\/:*?<>|!@#$%^&()\[\] ]+").Replace(thisPlayer.displayName, " ") : "(in death limbo)",
                        FontSize = 11,
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " " + (botBoundary),
                        AnchorMax = rightBoundary + " " + (topBoundary)
                    }
                }, elUiId);

                leftBoundary = leftBoundary + nameLength;
                rightBoundary = leftBoundary + 0.2f;
                string killCounter = "";
                string color = "1 1 1 1";
                if (list == "current")
                {
                    killCounter += stats["kills"];
                    color = (stats["kills"] >= storedData.MinKillCount) ? "0 0.9 0 1" : "0.8 0 0 1";
                }
                else
                {
                    killCounter += scoreData.killCounter[playerID] + "";
                }
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = killCounter,
                        FontSize = 11,
                        Align = TextAnchor.MiddleRight,
                        Color = color,
                    },
                    RectTransform =
                    {
                        AnchorMin = leftBoundary + " " + (botBoundary),
                        AnchorMax = rightBoundary + " " + (topBoundary)
                    }
                }, elUiId);


                if (list != "current")
                {

                    leftBoundary = rightBoundary + 0.02f;
                    rightBoundary = leftBoundary + 0.2f;
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = "" + ((scoreData.winCounter.ContainsKey(playerID)) ? scoreData.winCounter[playerID] : 0),
                            FontSize = 11,
                            Align = TextAnchor.MiddleRight,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = leftBoundary + " " + (botBoundary),
                            AnchorMax = rightBoundary + " " + (topBoundary)
                        }
                    }, elUiId);

                }
                else
                {
                    leftBoundary = rightBoundary + 0.02f;
                    rightBoundary = leftBoundary + 0.2f;
                    elements.Add(new CuiLabel
                    {
                        Text =
                        {
                            Text = stats["killPercent"] + "%",
                            FontSize = 11,
                            Align = TextAnchor.MiddleRight,
                            Color = "1 1 1 1"
                        },
                        RectTransform =
                        {
                            AnchorMin = leftBoundary + " " + (botBoundary),
                            AnchorMax = rightBoundary + " " + (topBoundary)
                        }
                    }, elUiId);

                }


            }


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
            }, "Overlay", elUiId);

            var closeButton = new CuiButton
            {
                Button =
                {
                    Command = "zhunt.close",
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

    }
}
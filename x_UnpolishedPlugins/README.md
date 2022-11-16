# Unpolished Plugins
The plugins published here are NOT yet polished to an extend that they should work drag-and-drop. There is no extensive documentation for each.
They probably won't work without refinement and they could contain old and dirty code.

These plugins were used in a very specific configuration together with "glue" plugins to create the unique ZN Setup and are at times optimized for a specific balance or playstyle in mind with less config options etc.

However, they contain some extensive featureset and could be valuable if modified properly.

**Use at you own risk**

If you modify them to make them work standalone, please send a PR or fork to allow others to benefit as well. (GNU GPLv3 applies to them, too!)

I might at some point pick individual plugins to clean them up and document them myself, but there's no ETA/guarantee on that.

**Plugins**
- [AdmTools](AdmTools.cs) - A lot of small helpers and gimmics, including "ChaosMode"
- [BradleyFromHell](BradleyFromHell.cs) - 4 Tier Bradley at your base, including queue and high-scores (with controversial movement ;))
- [BuildManager](BuildManager.cs) - A tool that enforces building limits (foundations, height) in a relatively efficient way, also contains /repairall and /agrade (upgrade all)
- [HomeCommander](HomeCommander.cs) - Command that allows you to lock all doors, remote auth clan/team/players etc.
- [HeliFromHell](HeliFromHell.cs) - 4 Tier Heli plugin, includes queue, several protection mechanisms, high-scores etc.
- [InfinityVendor](InfinityVendor.cs) - Allows you to set specific player vending machines to be in finite, also makes trade instant (no wait)
- [InfoAPI](InfoAPI.cs) - Glue Code used by some plugins to show pop-up infos
- [LootBoxSpawner](LootBoxSpawner.cs) - Plugin used by many others to create a Player-Locked Loot on command, abstracts loot table and loot distribution from other plugins (Heli, Brad...). Very Hard-Coded!
- [OilrigFromHell](OilrigFromHell.cs) - A so-so plugin to spawn a horde of Scientist and a Crate on large Oilrig (a bit wonky)
- [ScavengerHunt](ScavengerHunt.cs) - A Plugin that contains a Timed Challenge game mode (/qc) (also contains a lot of unused legacy Quest game code)
- [TurretCommander](TurretCommander.cs) - Plugin allows to remote control and fill/empty autoturrets and traps, as well as auth team/clan/players (pvp-friendly)
- [UsrTools](UsrTools.cs) - Plugin contains lots of opinionated PVE code, also GetMyStuff command, GetOut command and instant research
- [VirtualQuarries](VirtualQuarries.cs) - also known as /mm (Mining Manager) a virtualizes auto quarry plugin allows players to buy quarries for RP without lag of ingame quarries
- [ZNExperience](ZNExperience.cs) - Custom MegaPlugin that focuses the entire server around XP gain to unlock Skills (based on permissions) and Prestige. (Rank display in Chat requires BetterChat modification!)
- [ZNFarming](ZNFarming.cs) - Plugin works in conjunction with ZNExperience to handle farming rate increasing skills. (Example of how to register skills from an external plugin with ZNExperience)
- [ZNQuests](ZNQuests.cs) - Quest system that allows players to complete 10 daily quests and get rewards, contains a server-challenge reward the more people complete quests
- [ZNTitleManager](ZNTitleManager.cs) - A plugin in need of rebuilding that handles custom titles and high-scores for players, used by many of the others. (Don't use All-Time highscores!!!)
- [ZombieHunt](ZombieHunt.cs) - (BROKEN!) The ZN signature /zhunt game mode, Zombies spawn in random map locations and can be hunted by players for rewards, incl. highscores, random weakness etc. **This Plugin is missing AI Brains, since I didn't write them.** To make it work, find some AI and Brain code or rewrite to use Chaos NPCs or something

---

**Disclaimer**

Again, these plugins are not polished ;) 

I have created them over the last 4 years with very specific purposes and growing levels of knowledge and experience with Rust (and C# in General). 
They are probably a pain to look at for many pro-modders, but they served their purpose.
I removed (hopefully all) parts of the code that I didn't write myself, because there were existing solutions.

I publish this work after the End of ZN (a non-profit, 100% free Rust Gaming Server), so maybe some folks can utilize them to create fun experiences for their players.
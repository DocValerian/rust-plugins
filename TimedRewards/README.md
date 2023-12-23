# TimedRewards
This plugin allows you to distribute playtime-based daily rewards to your players every day.
The rewards can ge configured for 6 different playtime tiers from 0 minutes to 480 (8h).

It highly rewards longer activity on the server and increases player numbers. 
You can use PlaytimeTracker features to decide if AFK farming should be allowed. (Generally it helps keep player numbers higher)

##### Table of Contents  
* [Dependencies](#Dependencies)  
* [Permissions](#Permissions)  
* [Commands](#Commands)  
* [Configuration](#Configuration)
* [Planned_Features](#Planned_Features) 
* [Known Issues](#Known_Issues) 

![Reward Screen](https://github.com/DocValerian/rust-plugins/blob/main/assets/TimedRewards.png?raw=true)

---

## Dependencies
- [PlaytimeTracker](https://umod.org/plugins/playtime-tracker)

## Permissions
```
This plugin uses the umod permission system. 
To assign a permission, use oxide.grant <user or group> <name or steam id> <permission>. 
To remove a permission, use oxide.revoke <user or group> <name or steam id> <permission>.
```

``timedrewards.use`` - grant access to all commands.

## Commands
``/daily`` OR ``/dr`` - (can be freely configured) open the UI

## Configuration
```
{
  "Reward Item:amount per minute playtime (max 6)": {
    "0": {
      "blueberries": 5,
      "lowgradefuel": 10,
      "pookie.bear": 1,
      "wood": 5000
    },
    "30": {
      "metal.refined": 50,
      "blueberries": 10,
      "generator.wind.scrap": 1,
      "blood": 2000
    },
    ...
    "480": {
      "sticks": 500,
      "blueberries": 40,
      "blood": 3000,
      "metal.refined": 150,
      "supply.signal": 1,
      "explosive.timed": 5
    }
  },
  "Chat command aliases": [
    "dr",
    "daily"
  ]
}
```
**Reward Item:amount per minute playtime (max 6)**
Here you can configure the individual loot that players get for each of the predefined 6 time tiers. 

The timing split between:
- 0m
- 30m
- 60m (1h)
- 120m (2h)
- 240m (4h)
- 480m (8h)

It is currently not indended to be significantly changed. The counter depends on your configuration of 
PlaytimeTracker (especially the AFK time).

To configure a reward, just add an item_shortname and the desired amount. The amount will be given to players in one stack! 
It is possible, but not recommended, to configure more than 6 items per timeslot (UI doesn't auto-scale!)

**Chat command aliases**
You can freely define the chat command to be used for the UI to open, to stay compatible with your system.

---

## Planned_Features
* customizable time slots?

Feel free to open feature requests or PRs

---

## Known_Issues
* none
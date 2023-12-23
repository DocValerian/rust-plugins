# VirtualSmelter
This plugin allows you to open a virtual version of large furnaces with various options to upgrade and increase efficiency.

It has been originally built to reduce the lag caused by massive farms of large furnaces on PVE servers. 
Due to the ability to upgrade the efficiency of resource smelting it did lead to a completely new market, where players with
high upgrade levels would offer ore<output trade options.

The system is designed to automatically wipe CONTENTS of smelters afer every wipe. If you want to wipe the player's upgrades,
you have to manually (or automatically) delete the data/VirtualSmelter folder contents. This allows you to only wipe upgrades monthly (or never) 
whilst still doing shorter normal wipes.

**Features**
- You can add up to 6 Smelting Slots (that allow you to smelt 1 resource TYPE each).
- The overall smelter system has a LEVEL that can be upgraded to affect every slot increasing the max resource amount they can take etc.
- You can upgrade the fuel consupmtion to save wood
- You can upgrade the smelting speed to reduce smelt time (at the cost of higher fuel consumption)
- You can upgrade the efficiency to gain more smelted output per each 1 ore you put in. (this should be expensive!)
- Prices and efficiencies can be configured
- dynamic ui
- etc.




##### Table of Contents  
* [Dependencies](#Dependencies)  
* [Permissions](#Permissions)  
* [Commands](#Commands)  
* [Configuration](#Configuration)
* [Planned_Features](#Planned_Features) 
* [Known Issues](#Known_Issues) 

![Reward Screen](https://github.com/DocValerian/rust-plugins/blob/main/assets/VirtualSmelter.png?raw=true)

---

## Dependencies
- [ServerRewards](https://umod.org/plugins/server-rewards)

## Permissions
```
This plugin uses the umod permission system. 
To assign a permission, use oxide.grant <user or group> <name or steam id> <permission>. 
To remove a permission, use oxide.revoke <user or group> <name or steam id> <permission>.
```

``virtualsmelter.use`` - grant access to all commands.

## Commands
``/smelt`` - open the UI

## Configuration
The configuration of this plugin is a bit more advanced, since the upgrades function in a very complex way and it takes a bit of
figuring out to understand how this system actually works.

I suggest reading the [Documentation Table](https://docs.google.com/spreadsheets/d/e/2PACX-1vQVlNLpDDJaWPy5kvrhStpRM4wH21sAvogjTRqkybLXrySE5pkoRb_xDtYiI6f-GhHUHgHrrxxwGCIp/pubhtml)
for the upgrades that are in the default config.

```
{
  "Fuel consumption per slot level per minute": 70,
  "Close UI after seconds": 60.0,
  "Base resource capacity per slot": 100000,
  "Base price per slot level": 1,
  "Max Slot Level": 50,
  "Fuel consumption multiplier per Speed Upgrade": 3.0,
  "Base res per ore multiplier (int!)": {
    "wood": 1,
    "hqm": 1,
    "metal": 1,
    "sulfur": 1
  },
  "Base output per minute for each resource (int)": {
    "wood": 70,
    "hqm": 100,
    "metal": 200,
    "sulfur": 350
  },
  "Available upgrade types": [
    "slots",
    "speed",
    "fuel",
    "efficiency"
  ],
  "Available resources to use": [
    "hqm",
    "sulfur",
    "metal",
    "wood"
  ],
  "Currency to use for upgrading (RP|tcomp|mill|furnace)": {
    "slots": "RP",
    "speed": "tcomp",
    "fuel": "mill",
    "efficiency": "RP",
    "level": "furnace"
  },
  "Upgrade effect and price configuration": {
    "slots": {
      "1": {
        "effect": 2.0,
        "price": 4000.0
      },
      ...
      "5": {
        "effect": 6.0,
        "price": 8000.0
      }
    },
    "speed": {
      "1": {
        "effect": 2.0,
        "price": 10.0
      },
      ...
      "10": {
        "effect": 11.0,
        "price": 100.0
      }
    },
    "fuel": {
      "1": {
        "effect": 0.95,
        "price": 5.0
      },
     ...
      },
      "10": {
        "effect": 0.25,
        "price": 50.0
      }
    },
    "efficiency": {
      "1": {
        "effect": 1.08,
        "price": 5000.0
      },
      ...
      },
      "10": {
        "effect": 1.8,
        "price": 9500.0
      }
    }
  }
}
```
**Fuel consumption per slot level per minute**
How much wood per minute is consumed on the baseline.

**Close UI after seconds** 
The UI does automatically refresh causing a better UX but also maintaining any number of timers on the server. 
This configures the timeout to auto-close the UI and save resources.

**Base resource capacity per slot**
How much ore can every Slot hold at base-level (every level increases this by 100%)

**Base price per slot level**
Slot levelup-pricing follows the formula: 

``2*(ROUNDDOWN(LEVEL/10,0) * ROUNDDOWN(LEVEL/10,0)) + BASEPRICE``

**Max Slot Level**
How many upgrade levels should be available for the smelter? These levels add 100% capacity to EVERY slot.

**Fuel consumption multiplier per Speed Upgrade**
Speed upgrades reduce the time to smelt resources, this configuration adds a fuel consumption malus to this.

**Base res per ore multiplier (int!)**
This is the basic output multiplier, that should usually be 1 res for 1 ore put in. 
*Not sure why this is an int value (it shouldn't be), I might fix this at some point...*

**Base output per minute for each resource (int)**
How much of each resource will be smelted per minute on default level? (this is modified by speed upgrades)

**Available upgrade types**
Configure which upgrade types should be available.

**Available resources to use**
Configure which resources should be usable with the smelter system.

**Currency to use for upgrading (RP|tcomp|mill|furnace)**
This is a (not fully free) config option to modify the type of currency that is used for each upgrade. 
The prices are configured separately.
Available types:
- RP (ServerRewards)
- tcomp (target computers)
- mill (wind turbines)
- furnace (large furnaces)

**Upgrade effect and price configuration**
For each upgrade type this allows you to configure all available levels of upgrades, their price in currency and the 
effect multipliers to the respective base values.

---

## Planned_Features
* fully customizable currencies for upgrades
* customizable chat command

Feel free to open feature requests or PRs

---

## Known_Issues
* none

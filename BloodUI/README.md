# BloodUI
This plugin allows you to use the command /blood to bring up a UI in game that allows players to exchange ServerReward RP with in-game Blood items.

##### Table of Contents  
* [Dependencies](#Dependencies)  
* [Permissions](#Permissions)  
* [Commands](#Commands)  
* [Configuration](#Configuration)
* [Planned_Features](#Planned_Features) 

![screenshot](https://github.com/DocValerian/rust-plugins/blob/main/assets/BloodUI.png?raw=true)

---

## Dependencies
- ServerRewards
- ImageLibrary

## Permissions
The plugin does not currently use permissions

## Commands
``/blood`` - open the UI

## Configuration
```
{
  "options": [
    10,
    100,
    1000,
    10000
  ],
  "onlyAtHome": false,
  "infoText": "Some Info Here",
  "rpImgUrl": "http://m2v.eu/i/zn/rp1.png"
}
```
**Options** define the stack sizes for the exchang

**onlyAtHome** prevents players from opening the UI outside of authed TC range


---
## Planned_Features
* use permissions
* allow dynamic configuration of in-game item
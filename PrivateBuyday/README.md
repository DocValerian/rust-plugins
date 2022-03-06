# PrivateBuyday
This plugin is basically a player-level wrapper around NightVision, which allows players to customize their own ingame time for RP.

**Features**
- The change in time only affects the player that bought and activated the command. 
- Time can also be customized for RPG purposes or to get /night for fireworks. 
- It also removes all weather effects (fog, rain etc.) for perfect sunshine.

This is NOT recommended in PVP settings because it is providing users a very unfair advantage during nighttime.


##### Table of Contents  
* [Dependencies](#Dependencies)  
* [Permissions](#Permissions)  
* [Commands](#Commands)  
* [Configuration](#Configuration)
* [Planned_Features](#Planned_Features) 
* [Known Issues](#Known_Issues) 

![Command Screenshot](https://github.com/DocValerian/rust-plugins/blob/main/assets/PrivateBuyday.png?raw=true)

---

## Dependencies
- [NightVision](https://umod.org/plugins/night-vision) **[MODIFICATION NEEDED!]**
- [ServerRewards](https://umod.org/plugins/server-rewards)

Recent Versions of NightVision check permissions in a later internal call, which is not really required and prevents 
this plugin from working for normal users. 
If you want normal players to be able to use this plugin, modify NightVision.cs:

Find:
```C#
private NVPlayerData GetNVPlayerData(BasePlayer pl)
{
    _playerData[pl.userID] = _playerData.ContainsKey(pl.userID) ? _playerData[pl.userID] : new NVPlayerData();
    _playerData[pl.userID].timeLocked = !(!_playerData[pl.userID].timeLocked || !permission.UserHasPermission(pl.UserIDString, PERM_ALLOWED));
    return _playerData[pl.userID];
}
```
Replace:
```C#
private NVPlayerData GetNVPlayerData(BasePlayer pl)
{
    _playerData[pl.userID] = _playerData.ContainsKey(pl.userID) ? _playerData[pl.userID] : new NVPlayerData();
    return _playerData[pl.userID];
}
```
This does NOT allow players to use /nightvision directly. That part is secured by the plugin separately.


## Permissions
```
This plugin uses the umod permission system. 
To assign a permission, use oxide.grant <user or group> <name or steam id> <permission>. 
To remove a permission, use oxide.revoke <user or group> <name or steam id> <permission>.
```

``privatebuyday.use`` - grant access to the /day and /night commands.
``privatebuyday.free`` - grant access to the commands without the payment mechanic (VIP feature)

## Commands
- ``/day`` - Display command info
- ``/day buy`` - show buying details
- ``/day buy <stacks>`` - actually purchase \<stacks> of (real)time to use daylight
- ``/day info`` - display information on your time budget
- ``/day on`` - activate daylight (auto-triggered on purchase)
- ``/day on 18.5`` - activate daylight with a specific ingame time between 0 - 24
- ``/day off`` - deactivate forced daylight (does NOT preserve bought realtime budget!)
- ``/night on | off`` - turn on/off forced nighttime (e.g. for fireworks)


## Configuration
```
{
  "Server Rewards Price per time_stack": 666,
  "Time in Minutes per time_stack": 10,
  "default /day set time to (0.0 -> 24.0)": 12.0,
  "default /night set time to (0.0 -> 24.0)": 0.0
}
```
**Server Rewards Price per time_stack**
Configure how much RP a time stack costs

**Time in Minutes per time_stack**
Configure how many realtime minutes a stack will offer players. (Ideally as many as a night takes on your server)

**default /day set time to (0.0 -> 24.0)**
Define default /day ingame time. 12.0f = high noon

**default /night set time to (0.0 -> 24.0)**
Define default /night ingame time. 0.0f = midnight

---

## Planned_Features
* none?

---

## Known_Issues
* Not really an issue, but Players often confuse the time stacks they get for a budget that they only use up when using (/day on).
But it is just a time in real-time that they are free to switch.
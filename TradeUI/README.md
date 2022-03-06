# TradeUI
This plugin allows you to use the command /trade to access all vending machines and NPC vendors on the map from anywhere.
It has additional favorite and "edit offer" features to get around trade size limitations in the default Rust UI.

This allows players to have full market price transparency and instantly buy from everywhere.

##### Table of Contents  
* [Dependencies](#Dependencies)  
* [Permissions](#Permissions)  
* [Commands](#Commands)  
* [Configuration](#Configuration)
* [Planned_Features](#Planned_Features) 
* [Known Issues](#Known_Issues) 

![Home Screen](https://github.com/DocValerian/rust-plugins/blob/main/assets/TradeUI_home.png?raw=true)
![Vendor Secreen](https://github.com/DocValerian/rust-plugins/blob/main/assets/TradeUI_vendors.png?raw=true)
![Sell Screen](https://github.com/DocValerian/rust-plugins/blob/main/assets/TradeUI_sell.png?raw=true)

---

## Dependencies
- ImageLibrary

## Permissions
```
This plugin uses the umod permission system. 
To assign a permission, use oxide.grant <user or group> <name or steam id> <permission>. 
To remove a permission, use oxide.revoke <user or group> <name or steam id> <permission>.
```

``tradeui.use`` - grant access to all commands.

## Commands
``/trade`` - open the UI

``/trade f1`` - open the UI at favorite 1 view

``/sell`` - open the Sell Item filter

F1 ``tradeui.close`` - close the UI (sometimes helps when stuck, as long as the client is not desynced)

## Configuration
```
{
  "Prevent selling damaged items": false,
  "Default home category": "Resources",
  "Log vendor update info": true,
  "Allow owner to edit prices in ui?": true,
  "Allow access to NPC vendors?": true,
  "Show stock amount in UI?": true
}
```
**Prevent selling damaged items (false)**
Will block players from adding damaged items to vendors globally.

**Default home category (Resources)**
Set the default Item Category to open on /trade (from Rust item categories F1), case sensitive

**Log vendor update info (true)**
Will add a log entry like ``[TradeUI] INFO: Updated vendors after 7200.1571355s of inactivity - found: 26`` into 
the server log to keep track of the amount of vending machines on the server.

**Allow owner to edit prices in ui? (true)**
Provides an option in the UI to modify the amounts of items on OWNED! vending machines (beyond the default rust stack sizes).
This might be useful for servers with higher stack sizes for res and items.

**Allow access to NPC vendors? (true)**
By default /trade will also allow players to access outpost, bandit camp and fishing vendors remotely. If you want only player
machines to show up, deactivate this option.

**Show stock amount in UI? (true)**
Disable this, if you don't want to instantly show how many items are in stock in a particular machine.

---

## Planned_Features
* none?

---

## Known_Issues
* When players have poor network connectivity and click the ui too fast it sometimes happens that the UI will detatch from the server 
 and can't be closed without re-login.
It is not very common, but does happen every now and then. (I'll have to investigate and am open to suggestions)
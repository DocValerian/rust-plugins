# Simple Info Note
This plugin allows you to define a chat command that does nothing more than give the caller a simple Note with a configurable text.
One use case might be to give players a way to copy/paste your discord URL in an easy way.

There is a configurable global Cooldown to prevent binding spam that could lag your server.

##### Table of Contents  
* [Dependencies](#Dependencies)  
* [Permissions](#Permissions)  
* [Commands](#Commands)  
* [Configuration](#Configuration)
* [Planned_Features](#Planned_Features) 

![screenshot](https://github.com/DocValerian/rust-plugins/blob/main/assets/SimpleInfoNote.png?raw=true)

---

## Dependencies
- none

## Permissions
The plugin does not currently use permissions

## Commands
``/discord`` (configurable) - give a note with configured content

## Configuration
```
{
  "Chat command aliases": [
    "discord"
  ],
  "Text to add to Notes": "Discord URL: https://example.gg/abcdefg",
  "Global Note Cooldown (s) - Spam protection": 5.0
}
```
Should be self-explanatory :)


---
## Planned_Features
* maybe a different icon/skin for the note?
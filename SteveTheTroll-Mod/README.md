# SteveTheTroll

Fluffy Slippers: soft plush slippers for the utility slot, with armor and frost resistance. Wear them and see what happens.

## Features
- **Fluffy Slippers** (workbench 1): 5 Troll hide, 4 Deer hide, 6 Blueberries. Utility slot: armor 5 (+1 per quality, up to quality 4),
  resistant to frost. Every upgrade costs 2 Troll hide + 2 Blueberries.
- The recipes show up as soon as you pick up one of their materials.
- Press **F6** for the SteveTheTroll menu: the slippers in 3D with their stats, and more tabs once you find out what the slippers do.

## How to use
- Craft the slippers at the workbench and put them on.
- **F6** (config `[General] MenuKey`) opens / closes the menu, Esc closes it.

## Configuration
`BepInEx/config/com.kydldev.SteveTheTroll.cfg` (or the in-game config manager):

| Section | Setting | Default | What it does |
|---------|---------|---------|--------------|
| General | EnableAll | true | Switches the whole mod on / off while playing |
| General | MenuKey | F6 | Opens / closes the menu |
| DebugLog | Enable | false | Detailed lines in the BepInEx log for bug reports |

Edits to the file are picked up while the game runs.

## Multiplayer
Every player needs the mod (same minor version). Each player's trolls belong to that player and work around that player's bed.

## Spoiler: what the slippers do
Stop reading here if you want the surprise.

- **Taming:** wear the slippers near a wild troll for a minute and it becomes yours, named Steve 1, Steve 2, ... Tamed trolls are tougher,
  hit harder and can be renamed. One tamed troll with the slippers alone.
- **More trolls near home:** with the slippers on, trolls spawn 200 - 500 m from your bed in any land biome (never closer to the house),
  up to 2 at a time; with the whole costume, up to 6.
- **Farming:** a tamed troll fells trees and breaks rocks and ore veins on its own, and picks up wood, ores and scrap within 50 m into a
  backpack of 4 chests. Choose what it should go for in the menu (Troll work tab), or let it follow its own priority list.
- **Depots:** a troll with a full backpack walks home and empties it into troll depots it builds 15 - 50 m from your bed (stacks of chests,
  up to 5 high; uneven ground is levelled first).
- **Protection:** trolls defend you within 50 m; when you are hit with a troll within 10 m it goes into a rage and swings at everything
  around you until you are left alone. A troll that is hit answers with ground slams, then goes back to work.
- **Home:** take the slippers off (or log out) and your trolls go home and wait 15 m from the bed. Within 15 m of your bed trolls never
  swing, so the house stays whole. Near the bed they heal quickly.
- **Map:** every tamed troll has a pin on the map at its last known place. The Trolls tab shows each troll's health, stars, attacks,
  resistances, position and how full its backpack is.
- **The Troll Mascot costume:** head, suit and trousers plus the **Troll Club** (a whole tree trunk). Slippers + the 3 pieces = the set
  **Steve The Troll**: +50 % movement speed, +75 carry weight, -50 % stamina usage, up to 5 tamed trolls. The Costume tab appears once you
  have crafted all four costume items.

## Bug reports
Turn on `[DebugLog] Enable`, reproduce the problem, and attach `BepInEx/LogOutput.log`.

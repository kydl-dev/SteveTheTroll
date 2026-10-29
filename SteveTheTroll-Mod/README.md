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
  hit harder, move and swing 25 % faster, are immune to fire, frost, lightning, poison and spirit damage
  and can be renamed. One tamed troll near you with the slippers alone, up to 5 near you with the whole costume, and never more than
  15 living tamed trolls in total per player. In water from 1.5 m deep, tamed trolls wade three times as fast as they walk; in very
  deep water they swim up to three times their running speed and catch up when they fall behind. In water a following troll keeps
  15 m away from you, so you have room to steer your boat, and tamed trolls never damage a ship.
- **More trolls near home:** with the slippers on, trolls spawn 200 - 500 m from your bed in any land biome (never closer to the house),
  up to 2 at a time; with the whole costume, up to 6. Ashlands and the Deep North count too when your bed is there. Without the costume's
  set power every troll spawned this way carries a club; with the set power half of them come unarmed.
- **Farming:** a tamed troll fells trees and breaks rocks and ore veins on its own, and picks up wood, ores and scrap within 50 m into a
  backpack of 4 chests. Choose what it should go for in the menu (Troll work tab), or let it follow its own priority list. Tamed trolls
  break what a normal pickaxe or axe cannot: Yggdrasil shoots, ancient trees, scorched trees, Timberwood trees, giant remains and
  petrified Gammeltrolls. While a troll is in the Ashlands, Mistlands or Deep North, the Troll work list also offers Blackwood,
  Black marble, Timberwood and Gold ore.
- **Orders:** press **Mouse 4** (config `[Controls] AttackKey`) while looking at a creature and your nearest troll attacks it; at a tree,
  rock or ore vein and it breaks it; at nothing and it starts farming. The My trolls tab lists your trolls with their distance and
  buttons to call them (a far troll is brought next to you) or to kill one (asks first; its backpack drops).
- **Depots:** a troll with a full backpack walks home and empties it into troll depots it builds 15 - 50 m from your bed (stacks of chests,
  up to 5 high; uneven ground is levelled first).
- **Protection:** trolls defend you within 50 m; when you are hit with a troll within 10 m it goes into a rage and swings at everything
  around you until you are left alone. A troll that is hit answers with ground slams, then goes back to work.
- **Fortress walls:** trolls with 2 stars break the walls of the Ashlands fortresses - when a wall stands between them and an enemy,
  when you send one at a wall with Mouse 4, or when you hit a wall yourself and they are near (they join in).
- **Home:** take the slippers off (or log out) and your trolls go home and wait 15 m from the bed. Within 15 m of your bed trolls never
  swing, so the house stays whole. Near the bed they heal quickly.
- **Map:** every tamed troll has a pin on the map at its last known place. The Trolls tab shows each troll's health, stars, attacks,
  resistances, position and how full its backpack is.
- **The Troll Mascot costume:** head, suit and trousers plus the **Troll Club** (a whole tree trunk). Slippers + the 3 pieces = the set
  **Steve The Troll**: +50 % movement speed, +25 % carry weight, -50 % stamina usage, 75 % less stagger, up to 5 tamed trolls. The
  Costume tab appears once you have crafted all four costume items.
- **Troll Club:** 150 blunt, 50 chop, 50 pickaxe (+35 / +20 / +20 per upgrade, quality 4: 255 / 110 / 110), strong knockback,
  durability 500. Secondary attack: a wide horizontal swing for double damage. At any quality it fells Yggdrasil shoots, scorched trees
  and Timberwood trees and breaks giant remains and petrified Gammeltrolls; fortress walls are too much even for the club.

## Bug reports
Turn on `[DebugLog] Enable`, reproduce the problem, and attach `BepInEx/LogOutput.log`.

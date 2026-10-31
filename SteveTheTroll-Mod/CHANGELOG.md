# Changelog

## 2.0.0
- Breaking: SteveTheTroll now needs the KydlCore mod (hard dependency). Install KydlCore as well.
- The menu is now tabs (Slippers, Costume, My trolls, Troll work, Trolls, Settings) in the KY hub window of KydlCore (F6); the own menu window and
  the `[General] MenuKey` setting are gone. The troll tabs show a note until you have a living tamed troll.
- New config switches: `[General] EnableAll`, `[Slippers] Enable`, `[Costume] Enable`, `[Trolls] Enable` (taming, farming, backpack, depots, map pins).
  A switched-off part is not set up at all: restart the game after changing it.
- The attack key moved from `[Controls] AttackKey` to `[Trolls] AttackKey` (set it again if you changed it).
- `[DebugLog] Enable` writes detailed lines to the BepInEx log.
- **Troll Hub**: a big building you make with the hammer (KY-Items; 50 wood, 20 copper ore, 20 tin ore, 20 resin, 50 stone) once the Steve The Troll set bonus has been active.
  Up to 10 per player, at least 1000 m apart, each serves a 500 m circle. Trolls go there when you log out, unload there and heal there like at the bed;
  a troll uses the hub you assigned in the new **Troll Hub** tab (F6) or the nearest one (`[TrollHub] AutoAssign`). Each hub has a sign (HUB 1, 2, 3 ...)
  you can rename and style with the pen in the tab or with E on the sign. With hubs on and the set bonus active, trolls with no hub within 750 m work at half speed
  and trolls inside a hub circle are 50 % faster than vanilla (instead of 25 %). Removing a hub with the hammer gives back what it cost. `[TrollHub] Enable` switches it off.
- Hub window 15 % wider; the Slippers / Costume / My trolls / Troll work / Trolls tabs use the Settings tab's text size.
- Far-troll farming costs much less: the zones around far trolls are searched once a second instead of on every game object update (fewer stutters with many trolls).
- Safer little trolls on the slippers: when the skin reading goes wrong they follow the foot instead of ending up under the sole.

## 1.0.0
- First release.
- **Fluffy Slippers** (utility slot, workbench 1): armor 5 (+1 per quality, up to 4), frost resistance, a little plush troll on each foot.
- **Troll taming** with the slippers: a wild troll near you for a minute becomes yours (Steve 1, Steve 2, ... with a club, Spencer
  without). One tamed troll near you with the slippers, 5 with the whole costume, at most 15 living tamed trolls per player in total.
- **Tamed trolls:** 3x health and damage, move and swing 25 % faster, immune to fire, frost, lightning, poison and spirit, resistant to
  pierce and slash. They wade 3x as fast from 1.5 m deep water and swim in very deep water up to three times their running speed
  (always a little faster than you when following, and catching up when far behind; faster acceleration and turning in water).
  In water a following troll keeps 15 m from you (room to steer the boat); tamed trolls never damage ships.
- **More trolls near home:** with the slippers, trolls spawn 200 - 500 m from your bed in every land biome (Ashlands and Deep North
  included), 2 at a time, 6 with the costume. Without the set power all of them carry a club; with it half come unarmed.
- **Farming:** trees, logs, rocks, ore veins and other breakables, Yggdrasil shoots, scorched and Timberwood trees, giant remains and
  petrified Gammeltrolls included. Focus per troll in the Troll work tab; Blackwood, Black marble, Timberwood and Gold ore are offered
  while the troll is in the Ashlands, Mistlands or Deep North. Wood, ores and scrap within 50 m go into a 4-chest backpack.
- **Depots:** full trolls walk home and unload into troll depots 15 - 50 m from your bed (chest stacks up to 5 high, ground levelled).
- **Protection:** trolls defend you within 50 m, rage when you are hit with a troll within 10 m, answer hits with ground slams.
  Trolls with 2 stars break Ashlands fortress walls in their way, on your order, or when you hit a wall near them.
- **Orders:** Mouse 4 (`[Controls] AttackKey`) sends your nearest troll at a creature, a breakable or to farm.
- **Home:** slippers off or logout - trolls wait 15 m from the bed; no troll swings within 15 m of it; quick healing near the bed.
- **Troll Mascot costume** (head, suit, trousers) and the **Troll Club** (150 blunt / 50 chop / 50 pickaxe, +35 / +20 / +20 per
  upgrade, wide swing for double damage as the secondary attack, breaks Yggdrasil shoots, scorched and Timberwood trees, giant remains
  and petrified Gammeltrolls at any quality). Set bonus **Steve The Troll**: +50 % movement speed, +25 % carry weight, -50 % stamina
  use, 75 % less stagger, up to 5 tamed trolls.
- **F6 menu:** Slippers, Costume, My trolls (distance, Follow, Kill), Troll work and Trolls tabs; map pins for your trolls.

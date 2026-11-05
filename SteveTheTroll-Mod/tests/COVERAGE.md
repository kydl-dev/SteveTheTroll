# SteveTheTroll - test coverage (spec 12)

**Automated** = an automated test that ran green; **Manual** = needs a person (`SteveTheTroll-Manual` or the release checklist); **Gap** = not covered.
Status "Automated" here is only true after the TestLab run named in `_agent\TESTLOG.md`.

| Feature / requirement | Unit | Integration | TestLab | Manual | Status |
|---|---|---|---|---|---|
| Loads with its exact KydlCore, V2 + spec build, no errors | - | ManifestTests, ArchitectureRulesTests | architecture-v2, startup-clean | - | Automated |
| Features run, tabs declared, KY-Items listed, HUB icon (T-210, T-200) | KydlCore.Core FeatureRegistry | ArchitectureRulesTests | kydl-spec-port | hub-look (looks, icon) | Automated + Manual |
| Feature switches live, no stale tab / KY-Items entry | - | - | feature-toggle | - | Automated |
| Costume recipes only after the first slipper equip, survives relog (T-201) | CostumeUnlockRulesTests | - | costume-after-slippers | hub-look (message) | Automated + Manual |
| Player config layout (EnableAll, Enables, DebugLog, ranges) | - | SettingsTests, SuiteRulesTests | (configOverrides) | - | Automated |
| Harmony patch targets still exist | - | PatchTargetsTests | - | - | Automated |
| Taming, health / elemental immunity | TamingRules*, HealthRules | - | troll-elemental-immune, health-clean | - | Automated |
| Farming, far search, spot return, recovery | IdleRules, FarmGate, StuckRules, Recovery* | - | farm-chops-tree, far-search, spot-return, recovery-walled-tree, unarmed-farm, noweapon-farm | - | Automated |
| Backpack, depots, pickup, death crates | Backpack*, DepotRules, DepotPlanner | - | backpack-*, depot-*, full-* | - | Automated |
| Hubs: choice, manual, walk back | HubRules, Hub pipelines | - | hub-* | - | Automated |
| Combat: defend, rage, counter, guard, walls, no-hit | Combat rules | - | defend-rage, counter-slam, guard-mob, guard-mob-kills (forest), guard-mob-kills-open (cleared ground), guard-mob-nohit, walls-two-star, nohit-zone | - | Automated |
| Orders (attack key) | OrderRules | - | attack-key-* | real Mouse 4 | Automated + Manual |
| Ownership / multiplayer | - | - | ownership-handover | real 2-player test | Automated (virtual) + Gap (real) |
| Slipper figures on the feet | FitGeometry (Unity EditMode) | - | slipper-skin | look | Automated + Manual |
| Bed heal, hold when looked at, order latency | - | - | bed-heal, hold-look, instruction-latency | - | Automated |
| Set bonus carry +25 % (D-112), constant upgrade cost (D-035) | Carry*, UpgradeCostTests | - | - | - | Unit only |
| Spawn boost / census cap near the bed (D-042, D-085) | Spawn / census rules | - | spawn-cap | - | Automated (pending first run) |
| Water keep: 15 m ring around a swimming player, no ship damage (D-131) | water / swim rules | - | water-keep | - | Automated (pending first run) |
| Hub speed: half speed with no hub within 1250 m, x1.5 in a hub circle (D-135) | speed factor rules | - | hub-speed | - | Automated (pending first run) |
| Two trolls never share a tree, friend detour (D-151) | reservation rules | - | farm-one-troll-per-tree, farm-friend-detour | - | Automated (pending first run) |
| Newly tamed troll walks to its tamer and waits (D-043) | - | - | troll-arrival | - | Automated (pending first run) |
| Troll club breaks special trees (D-120) | - | - | club-breaks | - | Automated (pending first run) |
| An ordered source survives a fight: after a guard / counter fight the troll goes back to the tree it was ordered to break (2026-11-05, gate run 1) | - | - | ordered-source-after-fight | - | Automated (pending first run) |
| Test infrastructure: frozen creatures really hold still (farm-friend-detour), trolls spawn into free space and onto a loaded terrain collider (instruction-latency) | - | - | farm-friend-detour, instruction-latency (+ every stt.spawnTroll) | - | Automated |
| Map pins, hover name of a tamed troll (D-081, D-060) | - | - | - | look | **Gap** (manual only) |
| Hub sign editor (D-135) | - | - | - | hub-look | Manual |

## How the TestLab suite is built (D-180, DrakesTestLab docs\TESTLAB-AUTHORING.md)

Reusable steps live in `tests/TestLab/suites/SteveTheTroll/_functions/` (loaded automatically for the suite; a failure inside names the function):

| Function / block | What it does | Used by |
|---|---|---|
| `start` (block: `clearTrolls`, `slippers`, `hub`) | arena spot, stt.reset, no test trolls, slippers on, HUB 1 25 m ahead | every test |
| `openGround` (block: `metres`, `radius`) | the player further out + world trees / rocks cleared | attack-key-*, guard-mob-kills-open, chase-push-through, farm-* |
| `guardFight` (function: `near`, with asserts) | AI target -> forced target -> chase -> within `near` m -> first hit -> dead | guard-mob-kills, guard-mob-kills-open, chase-push-through |
| `pressAttackKeyAt` (block: `target`, `expectName`) | aim + check the ray + no window open + one press | attack-key-creature, attack-key-farming-creature |
| `unloadAndConserve` (function: `timeout`, with assert) | full backpack -> unloads everything -> nothing lost or made | depot-full-builds-new |

Negative tests (`expectFailure`) prove that checks can fail: `meta-unmet-min-fails`, `meta-wrong-target-fails`.
Quick checks of one test: `Run-TestLabSuites.ps1 -Suites "SteveTheTroll/<test>"`; full runs before a release gate.

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
| Combat: defend, rage, counter, guard, walls, no-hit | Combat rules | - | defend-rage, counter-slam, guard-mob, walls-two-star, nohit-zone | - | Automated |
| Orders (attack key) | OrderRules | - | attack-key-* | real Mouse 4 | Automated + Manual |
| Ownership / multiplayer | - | - | ownership-handover | real 2-player test | Automated (virtual) + Gap (real) |
| Slipper figures on the feet | FitGeometry (Unity EditMode) | - | slipper-skin | look | Automated + Manual |

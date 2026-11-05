# Code audit SteveTheTroll

## unused

== SteveTheTroll: 5 never referenced
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Backpack/Domain/BackpackRules.cs:29 member Collects
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Combat/Domain/BreakRules.cs:132 member StaggerTaken
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Combat/Domain/CombatDecision.cs:45 member WhyNot
  SteveTheTroll/SteveTheTroll-Mod/src/Features/Trolls/Taming/Domain/TamingRules.cs:126 member Defends
  SteveTheTroll/SteveTheTroll-Mod/src/Testing/TestHooks/SlipperTestHooks.cs:7 class SlipperTestHooks

## dupes
== same name in several files
  AddHub                       HubTabs.cs:161, TestHooks.Part3.cs:283
  Apply                        SignFonts.cs:86, DepotGround.cs:235, TrollDepot.Part2.cs:28
  Begin                        TrollCounter.cs:237, TrollSlam.cs:37, TrollArrival.cs:60
  Build                        HubPrefab.cs:35, SlipperDropModel.cs:26, DepotPrefab.cs:131, TrollDepot.cs:313, HubTab.cs:30, TrollsKyPage.cs:24
  BusyReason                   IdleRules.cs:37, TrollIdle.cs:234
  Check                        HubRules.cs:29, TrollReturn.cs:84
  Decide                       Approach.cs:33, FarmGate.cs:75, OrderRules.cs:50
  Describe                     TrollGuard.cs:261, TrollHold.cs:25, TrollMoveDiag.cs:15
  FloorLift                    DepotData.cs:46, DepotPrefab.cs:36
  Follow                       TrollCommands.cs:43, TestHooks.Part3.cs:364
  Forget                       CostumeUnlock.cs:50, TrollGuard.cs:236
  HeldByOther                  TargetRules.cs:94, FarmReservations.cs:21
  Holds                        HomeRules.cs:37, TrollHold.cs:43
  IconOf                       CostumeItems.cs:162, HubPrefab.cs:150
  InRange                      BackpackRules.cs:35, TrollBedHeal.cs:26
  Inside                       NoHitRules.cs:10, TrollNoHit.cs:36, BuildDumpCommand.cs:170
  Install                      CostumeItems.cs:100, HubPrefab.cs:30, SlipperItem.cs:50, DepotGround.cs:65, DepotPrefab.cs:30, TrollTaming.cs:104
  IsHub                        HubUnlockRules.cs:12, HubPlacement.cs:79
  MakeModel                    CostumeMenuData.cs:154, SlipperMenuData.cs:71
  MeshSpan                     DepotPrefab.cs:76, SlipperTestHooks.cs:46
  Nearest                      HubGeometry.cs:13, HubRegistry.cs:130, HubSpeed.cs:44, GuardRules.cs:31, TrollOrders.cs:261
  Order                        TrollGuard.cs:149, TrollWalls.cs:46, BuildDumpCommand.cs:81
  Place                        TrollDepot.cs:341, ArenaTestHooks.cs:290
  Reachable                    DepotSpots.cs:194, IdleRules.cs:74
  Refresh                      HubRegistry.cs:57, SignFonts.cs:66, SlipperUpgradeCostPatch.cs:40, TrollRegistry.cs:190, TamedTrollStats.cs:46
  Release                      TrollDepot.cs:205, FarmReservations.cs:48
  Remove                       TrollDepot.Part2.cs:291, HubTabs.cs:176
  ReservedByOther              DepotRules.cs:29, TrollDepot.cs:182
  SelfCheck                    BackpackStore.cs:59, TrollDefend.cs:42, TrollFarming.cs:71, ValheimTrollMovement.cs:59, TamedTrollSpeed.cs:47, TamedTrollStats.cs:33
  SetActive                    HubPlacement.cs:29, GapTestHooks.cs:273
  Shown                        TrollIdle.cs:182, TrollOrders.cs:411, TrollCommands.cs:36
  Slippers                     ModelKyPages.cs:58, CombatTestHooks.Part2.cs:186
  Target                       TrollReturn.cs:47, CombatTestHooks.cs:147
  TargetMethods                ClubBreaks.cs:30, SetTooltipPatch.cs:94, TrollFarmHits.cs:27, CostumePatches.cs:14, CostumePatches.cs:59, TrollsCombatPatches.cs:34
  Track                        SlipperUpgradeCostPatch.cs:30, TrollZones.Part2.cs:88
  Watch                        TrollIdle.cs:192, TrollOrders.cs:92
  Waypoint                     TargetRules.cs:56, FarmHomeTrip.cs:44
  Within                       TrollZones.Part2.cs:50, ClearCommand.cs:138

== identical bodies (>=5 lines)


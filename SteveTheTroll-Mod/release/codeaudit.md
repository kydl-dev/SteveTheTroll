# Code audit SteveTheTroll

## unused

== SteveTheTroll: 3 never referenced
  SteveTheTroll/SteveTheTroll-Mod/src/Item/SlipperStatsPatch.cs:62 class Tooltip
  SteveTheTroll/SteveTheTroll-Mod/src/Taming/BreakRules.cs:77 member StaggerTaken
  SteveTheTroll/SteveTheTroll-Mod/src/Taming/TamingRules.cs:121 member Slam

## dupes
== same name in several files
  Apply                        TrollDepot.cs:178, SignFonts.cs:55
  Begin                        TrollArrival.cs:19, TrollCounter.cs:96, TrollFarming.cs:30, TrollSlam.cs:24
  Build                        DepotPrefab.cs:74, TrollDepot.cs:142, TrollFigureFactory.cs:23, HubPrefab.cs:19, SlipperDropModel.cs:20
  FloorLift                    DepotData.cs:40, DepotPrefab.cs:27
  IconOf                       HubPrefab.cs:93, CostumeItems.cs:78
  Inside                       BuildDumpCommand.cs:99, TrollNoHit.cs:28
  Install                      DepotGround.cs:31, DepotPrefab.cs:24, HubPrefab.cs:17, CostumeItems.cs:54, SlipperItem.cs:40, TrollTaming.cs:54
  MakeModel                    CostumeMenuData.cs:84, SlipperMenuData.cs:48
  Nearest                      HubRegistry.cs:79, HubRules.cs:39, HubSpeed.cs:29, TrollOrders.cs:64
  Order                        BuildDumpCommand.cs:53, TrollGuard.cs:76, TrollWalls.cs:35
  Refresh                      HubRegistry.cs:40, SignFonts.cs:20, SlipperUpgradeCostPatch.cs:30, TamedTrollStats.cs:30, TrollRegistry.cs:119
  SelfCheck                    BackpackStore.cs:32, TamedTrollSpeed.cs:29, TamedTrollStats.cs:20, TrollDefend.cs:19, TrollFarming.cs:27, TrollTamingPatch.cs:48
  Shown                        TrollIdle.cs:90, TrollOrders.cs:119, TrollCommands.cs:27
  Strip                        TrollFigureFactory.cs:78, SlipperItem.cs:110
  Subscribe                    SlipperDriver.cs:58, TrollFigure.cs:106
  TargetMethods                ClubBreaks.cs:23, SetTooltipPatch.cs:65, TrollFarmHits.cs:19
  Track                        SlipperUpgradeCostPatch.cs:21, TrollZones.cs:261
  Within                       TrollZones.cs:234, ClearCommand.cs:83

== identical bodies (>=5 lines)


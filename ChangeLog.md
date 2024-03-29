# Configurable Containers Change Log

## v2.6.2.1 / 2022-04-01

* Compiled for KSP 1.12.3
* Internal project changes for CI/CD

## v2.6.2

* Compatible with KSP-1.11.1
* UI: using G9 formatter instead of R for better performance
* API: removed `ITankManager.AvailableVolumePercent`

## v2.6.1

* **Parts**
    * **Hangar**: allow surface attachment of all the containers
* Fixed CC behaviour in symmetry groups
* Fixed resource amount update when tank volume is changed
(by APR or TweakScale)
* Fixed part cloning when the part is resized by TweakScale
* Fixed UI not showing when the `VolumeConfigs.user` file is absent
* Using bundle shipped with AT_Utils

## v2.6.0 - **New UI**

* **Reimplemented tank manager UI from scratch with uGUI framework**
* **Fixed counterparts rescaling with TweakScale**
* Improved tank types/configs tooltips by emphasizing with bold font
* Minor performance improvements

## v2.5.0.1

* Compiled against KSP-1.10

## v2.5.0

* **Moved CC configs to AT_Utils GameData and removed CC-Core**
* Added resource **max amount and max mass display**
* **Added new patches** 
    * Dodo Labs - Stockalike Electron
    * Mk2 Hypersonic Systems
    * Mk-X
    * Hyper Propulsion
* **Updated** patches
    * BDB
    * Mk2 Expansion
    * ReStock+
* Only filter parts with **B9 Part Switch** if it has SUBTYPE with 
a tankType defined. This allowed to **add CC to several parts** that
only had mesh/node variants, not the resource switching. **Affects**
    * Bluedog Design Bureau
    * Mk2 Expansion
    * Mk3 Expansion
* Moved hangar resource tanks to FuelTank category
* Removed the OPT patch since OPT Reconfig provides its own now
* Minor fixes and improvements
* Updated the list of supported mods in netkan

## v2.4.8.3

* Compiled against AT_Utils 1.9.3

## v2.4.8.2

* **Compatible with KSP-1.9**
* Compiled against AT_Utils 1.9.2

## v2.4.8.1

* Compiled against AT_Utils 1.9.1

## v2.4.8

* Hangar patch:
    * Added a procedural fuel tank made from Procedural Adapter
* In APR ResourceUpdater no longer handles dynamic resources
* Corrected LH2O ratio in CryoEngines tank config
* Added CryoCooling variant of LH2O tank config
* Fixed InvalidOperationException on tank add/remove

## v2.4.7.1

* Updated AT_Utils

## v2.4.7

* Supports KSP-1.8.1
* IFS is fully compatible with CC patches
* Rebalanced "Snacks and Soil" tank config to keep 1u food to 1u soil 
    as suggested in #30 by @LouisCyfer
* Small performance improvements.

## v2.4.6

* **Added patches**
    * Mining Expansion
    * Kerbal Planetary Base System
    * ReStock+
    * Streamline - Engines and Tanks
* **Updated patches**
    * Bluedog Design Bureau
    * Mk2 Expansion
    * Mk3 Expansion
    * Near Future Propulsion
    * Mk2.5 Spaceplane Parts
    * Squad
* Corrected a typo in squad xenon tanks' names

## v2.4.5

* Added ability to change UI color scheme at runtime
    * Added "C" button to the tank manager window titlebar which summons the Color Scheme dialog

## v2.4.4

* Compatible with KSP-1.7
* Fixed MM Warnings (multiple NEEDS)
* Fixed in-editor part cloning/symmetry bug (issue #31)

## v2.4.3.2 -4

* Version bumps due to updates of AT_Utils.

## v2.4.3.1

* SpecializedParts are also used by GC now

## v2.4.3

* Added patches for Bluedog Design Bureau and Making History Expansion.

## v2.4.2

* Renamed Resource to CryoResource for clarity and to prevent name clashes.
* Twealscaled tanks retain volume on load. Fixed #22.
    * Corrected calculation of ModuleSaveFromPrefab flag.
* Removed support for ProceduralParts =(
* Added SpareParts to Components TankType for DangIt.
* Removed FOR[ConfigurableContainers] stanza, changed ref to KSP 1.4.1
* Updater Squad patch
* Supporting KWRocketryRebalanced. Can't support multiple KWR flavors.
    * Well, it's possible with multiple .ckans, but I don't have the time =\
* Updated patches for Mk2/Mk3Exp, FTPlus, Mk2Plane

## v2.4.1.1

* Fixed Cryogenic/CryoCooling NEEDS, fixed KarbonitePlus requirement for Metal.
* Update TankTypes.cfg
    * added Snacks support (Snacks --> Food, Soil --> Soil)
    * modified KolonyTools support --> ColonySupplies are visible/usable/transferable even if USI-LS is not installed
* Added Chemicals to LiquidChemicals for KolonyTools.
* Attempt to fix #10 using the patch suggested by @Starwaster.
* Fixed issues:
    * 16 - Attempting to change configuration when none exists results in NullReferenceException
    * 18 - Tweakscaled tank saved prior to installation of CC gets capacity reset to un-scaled value
    * 20 - Lag/freeze when placing tanks in VAB

## v2.4.1

* All tanks except high-pressure now use TankManager. Wings use IncludeTankType to restrict contents to liquid chemicals.
* Updated patches:
    * Stock
    * FuleTanks+
    * ModularRockeSystems
    * NearFuture
    * KWRocketry
    * Mk3 Expansion
* Added patches:
    * Mk2.5 spaceplane parts
    * Fuel Tank Expansion
    * B9 Procedural Wings
* Added patch for B9 mods **made by ShadyAct** to *IntrusivePatches* optional folder. See the archive structure and the included readme file for details.
* Part info now respects Include/ExcludeTankTypes options.
* CC modules are now properly initialized when they're added to existing parts (in flight) by MM. **This should fix most of incompatibility with other fuel switches.**

## v2.4.0.6

* Compatible with KSP-1.3
* Fixed Metal tank type as pointed out by TheKurgan.
* Removed Plutonium-238 as it is internal resource for USI

## v2.4.0.5

* Corrected CKAN metadata.
* Small bugfixes.

## v2.4.0.4

* Added patch for GPOSpeedFuelPump for time being.

## v2.4.0.3

* Added FindTankType by resource_name method to TankType library.
* Added ForceSwitchResource method to SwitchableTank.
* GroundConstruction will be using MaterialKits, so added it to Components TankType users.
* Use round-trip format for the volume field.

## v2.4.0.2

* Fixed TankManager initialization with disabled AddRemove capability.
* Fixed TankManager initialization using empty config.
* Fixed in-flight tank creation.

## v2.4.0.1

* Added patch for **OPT Spaceplane Parts** made by **octarine-noise**
* Small bugfixes.

## v2.4.0

* Compiled against KSP-1.2.2.
* Added boiloff and active cooling for cryogenic resources based on simple thermodynamics.
* Added CryoCooling tank type.
* Added KSPIE resources to TankTypes.cfg.
* Added tooltips with Info to TankType choosers.
* Replaced Tank Type dropdown list with the LeftRightChooser.

## v2.3.1

* Corrected Cryogenic tank type parameters.
* Fixed Food tank type.
* In Editor automatically remove current resource when trying to switch it or the tank type.
* Fixed Soil TANKTYPE definition.
* Fixed installation directive in CC-Core.netkan
* Fixed ProceduralParts bug and return to VAB bug. Closed #3 and #4.

## v2.3.0

* Added per-tank volume editing and volume definition in % along with m3.
* Added support for:
    * **Tweak Scale**
    * **Procedural Parts**
    * Parts ++with stock resources++ converted:
        * Stock
        * KW Rocketry
        * Mk2 Expansion
        * Mk3 Expansion
        * SpaceY-Lifters
        * SpaceY-Expanded
        * Fuel Tanks Plus
        * Modular Rocket Systems
        * Standard Propulsion Systems
        * Near Future Propulsion
        * Spherical and Toroidal Tank Pack
    * Supported resources:
        * Stock
        * TAC Life Support
        * Extrapalentary Launchapads
        * Near Future Propulsion
        * All USI
        * *Some* of KSPIE
* Different TankTypes can now have different additional mass
* Added Tank Types:
    * Battery
    * Cryogenic
* Added Tank Setups:
    * TAC Life Support -- with food, water and oxigen. Made by **Bit Fiddler**.
    * LH2O -- with Liquid Hydrogen and Oxidizer for CryoEngines.
* Corrected unit/volume ratios for:
    * Monopropellant
    * Argon Gas
    * Liquid Hydrogen
    * Liquid Methane (which mod uses it?)
    * Karbonite

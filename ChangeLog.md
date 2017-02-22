#Configurable Containers ChangeLog

* **v2.4.0.5**
	* Corrected CKAN metadata.
	* Small bugfixes.

* v2.4.0.4
	* Added patch for GPOSpeedFuelPump for time being.

* v2.4.0.3
    * Added FindTankType by resource_name method to TankType library.
    * Added ForceSwitchResource method to SwitchableTank.
    * GroundConstruction will be using MaterialKits, so added it to Components TankType users.
    * Use round-trip format for the volume field.

* v2.4.0.2
	* Fixed TankManager initialization with disabled AddRemove capability.
	* Fixed TankManager initialization using empty config.
	* Fixed in-flight tank creation.

* v2.4.0.1
    * Added patch for **OPT Spaceplane Parts** made by **octarine-noise**
    * Small bugfixes.

* v2.4.0
    * Compiled against KSP-1.2.2.
    * Added boiloff and active cooling for cryogenic resources based on simple thermodynamics.
    * Added CryoCooling tank type.
    * Added KSPIE resources to TankTypes.cfg.
    * Added tooltips with Info to TankType choosers.
    * Replaced Tank Type dropdown list with the LeftRightChooser.

* v2.3.1
    * Corrected Cryogenic tank type parameters.
    * Fixed Food tank type.
    * In Editor automatically remove current resource when trying to switch it or the tank type.
    * Fixed Soil TANKTYPE definition.
    * Fixed installation directive in CC-Core.netkan
    * Fixed ProceduralParts bug and return to VAB bug. Closed #3 and #4.

* v2.3.0
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
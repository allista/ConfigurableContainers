# Configurable Containers

##Requirements

* [ModuleManager](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-12)
* [AT_Utils](https://github.com/allista/AT_Utils) (already includeds)

##For Players

This mod converts fuel tanks and resource containers so that you can change the resource(s) they hold in Editor and in Flight.

##Supported Mods

Configurable Containers support manu part packs and mods:

* **TweakScale**
* **ProceduralParts**
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
* Supported resources:
    * Stock
    * TAC Life Support
    * Extrapalentary Launchapads
    * All USI
    * *Some* of KSPIE
    * *Some* of Near Future Propulsion

###Types of the Containers

_Tank Type_ is a set of resources that, gamewise, have something in common. For example gases, or liquid chemicals, or metals. There are also two kinds of configurable containers.

* **Simple** containers belong to a single Tank Type (which can be changed in Editor) and can hold only a single resource. In flight this resource may be changed only if the container is empty, and only within its Tank Type.
* **Compound** containers are in fact collections of simple containers inside of a single part. In Editor you can partition the inside space of such part, creating as many simple containers as you need. The only restriction imposed by KSP is that a part cannot have two identical resources stored. So if you have two containers for liquid chemicals in a part, only one of them can hold Liquid Fuel.

Compound containers have a dedicated user interface so as not to clutter part menu:

![TankManager GUI](http://i.imgur.com/6Tbr5JG.gif)

##For Modders

### CC is a part of the [AT_Utils](https://github.com/allista/AT_Utils) framework.

It provides the **SwitchableTank** module that allows for creation of container parts for predefined sets of resources **switchable in-flight**. Sets are configured in a separate .cfg file and are intended to contain similar things like gases (one set), liquid chemicals (another) and so on.

Another module Configurable Containers provide is the **TankManager** which enables _in-editor_ partitioning of a container, effectively converting it into a set of independent SwitchableTanks.

The third, utility module named **SimpleTextureSwitcher** allows you to cycle through a predefined set of textures for the model or a part of the model, so a container may be easily identified.
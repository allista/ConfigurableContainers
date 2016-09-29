
import os, re
from KSPUtils import ConfigNode, NamedObject, Part, Module, Resource

tank_types_file = 'GameData/ConfigurableContainers/TankTypes.cfg'
game_data = '/home/storage/Games/KSP_linux/PluginsArchives/Development/AT_KSP_Plugins/KSP-test/KSP_test_1.1.3/GameData'


class TankType(NamedObject):
    def __init__(self):
        NamedObject.__init__(self)
        self.UnitsPerLiter = dict()

    def load(self, node):
        NamedObject.load(self, node)
        val = self.values.get('PossibleResources')
        if val:
            self.UnitsPerLiter = dict((r[0], float(r[1])) for r in
                                      (res.strip().split() for res in val.value.split(';'))
                                      if len(r) > 1)

    @property
    def PossibleResources(self): return self.UnitsPerLiter.keys()
TankType.register('TANKTYPE')
TankType.mirror_value('UsefulVolumeRatio', float)
TankType.mirror_value('TankCostPerSurface', float)


class TanksLib(NamedObject): pass
TanksLib.setup_children_dict('types', 'TANKTYPE')


class ModuleTankManager(Module):
    def __init__(self):
        NamedObject.__init__(self)
        self.name = 'ModuleTankManager'
ModuleTankManager.mirror_value('Volume', float)
ModuleTankManager.mirror_value('DoCostPatch', bool)


class ModuleTank(Module):
    def __init__(self):
        NamedObject.__init__(self)
        self.name = 'ModuleSwitchableTank'
ModuleTank.mirror_value('Volume', float)
ModuleTank.mirror_value('InitialAmount', float)
ModuleTank.mirror_value('TankType')
ModuleTank.mirror_value('CurrentResource')
ModuleTank.mirror_value('ChooseTankType', bool)
ModuleTank.mirror_value('DoCostPatch', bool)


class Tank(NamedObject):
    type = 'TANK'
    def __init__(self, name=None):
        NamedObject.__init__(self)
        if name: self.name = name
Tank.mirror_value('Volume', float)
Tank.mirror_value('InitialAmount', float)
Tank.mirror_value('TankType')
Tank.mirror_value('CurrentResource')


if __name__ == '__main__':
    tank_types = TanksLib.from_node(ConfigNode.Load(tank_types_file))
    types = tank_types.types

    def volume(tanktype, name, units):
        t = types[tanktype]
        upl = t.UnitsPerLiter.get(name)
        return units/upl/t.UsefulVolumeRatio/1e3 if upl else 0

    def get_parts(path):
        parts = []
        for dirpath, _dirnames, filenames in os.walk(path):
            for filename in filenames:
                if not filename.endswith('.cfg'): continue
                node = ConfigNode.Load(os.path.join(dirpath, filename))
                if node.name != 'PART': continue
                part = Part.from_node(node)
                resources = part.resources
                parts.append((part, resources))
        return parts

    def print_patches(patches, header):
        print('\n//%s\n//Automatically generated using PyKSPutils library\n' % header)
        print('\n\n'.join(str(p) for p in patches))

    def patch_1RES(parts, tank_type, res_name, monotype=None, add_values={}):
        patches = []
        rate = volume(tank_type, res_name, 1)
        polytype = lambda part: True
        if monotype:
            if isinstance(monotype, (list, tuple)):
                polytype = lambda part: part.name not in monotype
            elif monotype == 'all':
                polytype = lambda part: False
            elif isinstance(monotype, str):
                mono_re = re.compile(monotype)
                polytype = lambda part: mono_re.match(part.name) is None
        for part, resources in parts:
            if len(resources) == 1 and res_name in resources:
                res = resources[res_name]
                patch = Part.Patch('@', part.name, ':FOR[ConfigurableContainers]')
                V = res.maxAmount * rate
                ini = res.amount/res.maxAmount
                comment = '%f units of %s: conversion rate is %f m3/u' % (res.maxAmount, res_name, rate)
                patch.AddChild(Resource.Patch('!', res_name))
                if V < 8:
                    tank = ModuleTank()
                    tank.Volume = V
                    tank.SetComment('Volume', comment)
                    tank.InitialAmount = ini
                    tank.DoCostPatch = True
                    tank.ChooseTankType = polytype(part)
                    tank.TankType = tank_type
                    tank.CurrentResource = res_name
                    patch.AddChild(tank)
                else:
                    mgr = ModuleTankManager()
                    mgr.Volume = V
                    mgr.SetComment('Volume', comment)
                    mgr.DoCostPatch = True
                    tank = Tank()
                    tank.TankType = tank_type
                    tank.CurrentResource = res_name
                    tank.InitialAmount = ini
                    tank.Volume = 100
                    mgr.AddChild(tank)
                    patch.AddChild(mgr)
                add = add_values.get(part.name)
                if add: [patch.AddValue(*v) for v in add]
                patches.append(patch)
        if patches: print_patches(patches, '%s Tanks' % res_name)

    def patch_LFO(parts, add_values={}):
        patches = []
        rate = volume('LiquidChemicals', 'LiquidFuel', 1) / 0.45
        for part, resources in parts:
            if len(resources) == 2 and 'LiquidFuel' in resources and 'Oxidizer' in resources:
                lf = resources['LiquidFuel']
                patch = Part.Patch('@', part.name, ':FOR[ConfigurableContainers]:HAS[!MODULE[InterstellarFuelSwitch]]')
                patch.AddChild(Resource.Patch('!', 'LiquidFuel'))
                patch.AddChild(Resource.Patch('!', 'Oxidizer'))
                mgr = ModuleTankManager()
                mgr.Volume = lf.maxAmount * rate
                mgr.SetComment('Volume', '%f units of LF: conversion rate is %f m3/u' % (lf.maxAmount, rate))
                mgr.DoCostPatch = True
                tank = Tank('LFO')
                tank.Volume = 100
                mgr.AddChild(tank)
                patch.AddChild(mgr)
                add = add_values.get(part.name)
                if add: [patch.AddValue(*v) for v in add]
                patches.append(patch)
        if patches: print_patches(patches, 'Rocket Fuel Tanks')

    xenon_titles = {
        'xenonTank': [('@title', 'PB-X150 Pressurized Gass Container')],
        'xenonTankLarge': [('@title', 'PB-X750 Pressurized Gass Container')],
        'xenonTankRadial': [('@title', 'PB-X50R Pressurized Gass Container')],
    }

    parts = get_parts(os.path.join(game_data, 'Squad', 'Parts'))
    patch_LFO(parts)
    patch_1RES(parts, 'LiquidChemicals', 'LiquidFuel', '.*[Ww]ing.*')
    patch_1RES(parts, 'LiquidChemicals', 'MonoPropellant')
    patch_1RES(parts, 'Gases', 'XenonGas', 'all', xenon_titles)
    patch_1RES(parts, 'Soil', 'Ore')
    print('//:mode=c#:')




import os
import re

from KSPUtils.config_node_utils import ConfigNode, NamedObject, Part, Module, Resource
from KSPUtils.config_node_utils.named_object import ChildrenDict, ValueProperty
from KSPUtils.config_node_utils.search import SearchQuery, SearchTerm


class TankType(NamedObject):
    type = 'TANKTYPE'

    UsefulVolumeRatio = ValueProperty(float)
    TankCostPerSurface = ValueProperty(float)

    def __init__(self):
        NamedObject.__init__(self)
        self.UnitsPerLiter = dict()

    def load(self, node):
        NamedObject.load(self, node)
        val = self.values.get('PossibleResources')
        if val:
            self.UnitsPerLiter = {r[0]: float(r[1]) for r in
                                  (res.strip().split() for res in val.value.split(';'))
                                  if len(r) > 1}

    @property
    def PossibleResources(self):
        return list(self.UnitsPerLiter.keys())


class TanksLib(NamedObject):
    type = 'TANKSLIB'
    types = ChildrenDict(TankType)


FLOAT_DECIMALS = 6


def round_float(val):
    return round(float(val), FLOAT_DECIMALS)


class ModuleTankManager(Module):
    Volume = ValueProperty(round_float)
    DoCostPatch = ValueProperty(bool)
    DoMassPatch = ValueProperty(bool)
    IncludeTankTypes = ValueProperty(str)
    ExcludeTankTypes = ValueProperty(str)

    def __init__(self):
        Module.__init__(self)
        self.name = 'ModuleTankManager'


class ModuleTank(Module):
    Volume = ValueProperty(round_float)
    InitialAmount = ValueProperty(round_float)
    TankType = ValueProperty(str)
    CurrentResource = ValueProperty(str)
    ChooseTankType = ValueProperty(bool)
    DoCostPatch = ValueProperty(bool)
    DoMassPatch = ValueProperty(bool)
    IncludeTankTypes = ValueProperty(str)
    ExcludeTankTypes = ValueProperty(str)

    def __init__(self):
        Module.__init__(self)
        self.name = 'ModuleSwitchableTank'


class Tank(NamedObject):
    type = 'TANK'

    Volume = ValueProperty(round_float)
    InitialAmount = ValueProperty(round_float)
    TankType = ValueProperty(str)
    CurrentResource = ValueProperty(str)

    def __init__(self, name=None):
        NamedObject.__init__(self)
        if name: self.name = name


common_patch_spec = ':HAS[' \
                    '!MODULE[InterstellarFuelSwitch],' \
                    '!MODULE[FSfuelSwitch],' \
                    '!MODULE[ModuleB9PartSwitch]:HAS[@SUBTYPE:HAS[#tankType]]]' \
                    ':NEEDS[!modularFuelTanks&!RealFuels]'


class Patcher(object):
    def __init__(self, typelib, gamedata):
        self.game_data = gamedata
        self.tank_types_file = typelib
        self.tank_types = TanksLib.from_node(ConfigNode.Load(self.tank_types_file))
        self.types = self.tank_types.types
        self.part_filter = None

    def volume(self, tanktype, name, units):
        t = self.types[tanktype]
        upl = t.UnitsPerLiter.get(name)
        if not upl:
            print('WARNING: no UnitsPerLiter value for %s in %s:\n%s' % (name, tanktype, str(t)))
        return units / upl / t.UsefulVolumeRatio / 1e3 if upl else 0

    @staticmethod
    def get_parts(path):
        parts = []
        for dirpath, _dirnames, filenames in os.walk(path):
            for filename in filenames:
                if filename.endswith('.cfg'):
                    node = ConfigNode.Load(os.path.join(dirpath, filename))
                    if node.name == 'PART':
                        part = Part.from_node(node)
                        resources = part.resources
                        parts.append((part, resources))
        return parts

    @staticmethod
    def print_patches(stream, patches, header):
        stream.write(f'\n//{header}\n//Automatically generated using PyKSPutils library\n')
        stream.write('\n\n'.join(str(p) for p in patches))

    @staticmethod
    def add_patches(part, patch, addons):
        for term, addon in addons:
            if term.match(part):
                if isinstance(addon, NamedObject.Value):
                    patch.AddValueItem(addon)
                elif isinstance(addon, NamedObject):
                    patch.AddChild(addon)

    def patch_1RES(self, stream, parts, tank_type, res_name,
                   monotype=None, addons=None, add_spec=''):
        patches = []
        rate = self.volume(tank_type, res_name, 1)
        polytype = lambda part: True
        if monotype:
            if isinstance(monotype, (list, tuple)):
                polytype = lambda part: part.name not in monotype
            elif monotype in ('all', 'strict'):
                polytype = lambda part: False
            elif isinstance(monotype, str):
                mono_re = re.compile(monotype)
                polytype = lambda part: mono_re.match(part.name) is None
        for part, resources in parts:
            if len(resources) == 1 and res_name in resources:
                if self.part_filter is not None and self.part_filter.match(part):
                    continue
                print('Patching %s' % part.name)
                res = resources[res_name]
                patch = Part.Patch('@', part.name, common_patch_spec + add_spec)
                V = res.maxAmount * rate
                ini = res.amount / res.maxAmount
                comment = f'{res.maxAmount} units of {res_name}: conversion rate is {rate:.6f} m3/u'
                patch.AddChild(Resource.Patch('!', res_name))
                can_change_type = polytype(part)
                if can_change_type or monotype != 'strict':
                    mgr = ModuleTankManager()
                    mgr.Volume = V
                    mgr.SetComment('Volume', comment)
                    mgr.DoCostPatch = True
                    mgr.DoMassPatch = True
                    if not can_change_type:
                        mgr.IncludeTankTypes = tank_type
                    tank = Tank()
                    tank.TankType = tank_type
                    tank.CurrentResource = res_name
                    tank.InitialAmount = ini
                    tank.Volume = 100
                    mgr.AddChild(tank)
                    patch.AddChild(mgr)
                else:
                    tank = ModuleTank()
                    tank.Volume = V
                    tank.SetComment('Volume', comment)
                    tank.InitialAmount = ini
                    tank.DoCostPatch = True
                    tank.DoMassPatch = True
                    tank.ChooseTankType = False
                    tank.TankType = tank_type
                    tank.CurrentResource = res_name
                    patch.AddChild(tank)
                if addons:
                    self.add_patches(part, patch, addons)
                patches.append(patch)
        if patches: self.print_patches(stream, patches, '%s Tanks' % res_name)

    def patch_LFO(self, stream, parts, addons=None, add_spec=''):
        patches = []
        rate = self.volume('LiquidChemicals', 'LiquidFuel', 1) / 0.45
        for part, resources in parts:
            if len(resources) == 2 and 'LiquidFuel' in resources and 'Oxidizer' in resources:
                if self.part_filter is not None and self.part_filter.match(part):
                    continue
                print('Patching %s' % part.name)
                lf = resources['LiquidFuel']
                patch = Part.Patch('@', part.name, common_patch_spec + add_spec)
                patch.AddChild(Resource.Patch('!', 'LiquidFuel'))
                patch.AddChild(Resource.Patch('!', 'Oxidizer'))
                mgr = ModuleTankManager()
                mgr.Volume = lf.maxAmount * rate
                mgr.SetComment('Volume',
                               f'{lf.maxAmount} units of LF: conversion rate is {rate:.6f} m3/u')
                mgr.DoCostPatch = True
                mgr.DoMassPatch = True
                tank = Tank('LFO')
                tank.Volume = 100
                mgr.AddChild(tank)
                patch.AddChild(mgr)
                if addons:
                    self.add_patches(part, patch, addons)
                patches.append(patch)
        if patches: self.print_patches(stream, patches, 'Rocket Fuel Tanks')

    def patch_parts(self, output, paths, addons=None, add_spec=''):
        for path in paths:
            parts = self.get_parts(os.path.join(self.game_data, *path))
            if not parts:
                print('No parts in: {}'.format('/'.join(path)))
                continue
            with open(os.path.join(self.game_data, *output), 'w') as out:
                out.write('//Configurable Containers patch for %s\n' % os.path.join(*path))
                self.patch_LFO(out, parts, addons=addons, add_spec=add_spec)
                self.patch_1RES(out, parts,
                                'LiquidChemicals', 'LiquidFuel', '.*[Ww]ing.*',
                                addons=addons, add_spec=add_spec)
                self.patch_1RES(out, parts,
                                'LiquidChemicals', 'MonoPropellant',
                                addons=addons, add_spec=add_spec)
                self.patch_1RES(out, parts,
                                'Gases', 'XenonGas', 'strict',
                                addons=addons, add_spec=add_spec)
                self.patch_1RES(out, parts,
                                'Gases', 'ArgonGas', 'strict',
                                addons=addons, add_spec=add_spec)
                self.patch_1RES(out, parts,
                                'Soil', 'Ore',
                                addons=addons, add_spec=add_spec)
                out.write('\n//:mode=c#:\n')
            print('%s done.\n' % os.path.join(*path))

    def patch_mod(self, mod, addons=None, add_spec=''):
        output = ('ConfigurableContainers', 'Parts', f'{mod}_Patch.cfg')
        path = [[mod]]
        add_spec += ':AFTER[%(mod)s]' % {'mod': mod}
        self.patch_parts(output, path, addons, add_spec)

    def patch_mods(self, *mods):
        for m in mods: self.patch_mod(m)


if __name__ == '__main__':
    patcher = Patcher('../GameData/000_AT_Utils/TankTypes.cfg',
                      '/home/storage/Games/KSP_linux/PluginsArchives/'
                      'Development/AT_KSP_Plugins/KSP-test/'
                      'KSP_test_1.9.1/GameData')

    patcher.part_filter = SearchQuery('PART/MODULE:.*Engines.*/')
    patcher.part_filter.Or('PART/MODULE:.*Converter.*/')
    patcher.part_filter.Or('PART/MODULE:.*Harvester.*/')
    patcher.part_filter.Or('PART/MODULE:.*Drill.*/')
    patcher.part_filter.Or('PART/MODULE:.*[Ff]uelSwitch/')
    patcher.part_filter.Or('PART/MODULE:.*[Rr]esourceSwitch/')
    # filter out only resource switching B9PS
    patcher.part_filter.Or('PART/MODULE:ModuleB9PartSwitch/SUBTYPE/tankType:.*')

    xenon_titles = [
        (SearchTerm('name:xenonTank$'),
         Part.PatchValue('@', 'title', 'PB-X150 Pressurized Gas Container')),
        (SearchTerm('name:xenonTankLarge$'),
         Part.PatchValue('@', 'title', 'PB-X750 Pressurized Gas Container')),
        (SearchTerm('name:xenonTankRadial$'),
         Part.PatchValue('@', 'title', 'PB-X50R Pressurized Gas Container')),
    ]

    patcher.patch_parts(('ConfigurableContainers', 'Parts', 'Squad_Patch.cfg'),
                        [('Squad', 'Parts')], xenon_titles)

    patcher.patch_parts(('ConfigurableContainers', 'Parts', 'MakingHistory_Patch.cfg'),
                        [('SquadExpansion', 'MakingHistory', 'Parts')])

    # nothing to patch there
    # patcher.patch_parts(('ConfigurableContainers', 'Parts', 'Squad_Serenity_Patch.cfg'),
    #                     [('SquadExpansion', 'Serenity', 'Parts')])

    patcher.patch_mods('KWRocketry',
                       'Mk2Expansion',
                       'Mk3Expansion',
                       'SpaceY-Lifters',
                       'SpaceY-Expanded',
                       'FuelTanksPlus',
                       'ModRocketSys',
                       'NearFuturePropulsion',
                       'SPS',  # Standard Propulsion Systems
                       'RaginCaucasian',  # Mk2.5 spaceplane parts
                       'MunarIndustries',  # Fuel Tank Expansion
                       'Bluedog_DB',  # Bluedog Design Bureau
                       'StreamlineEnginesTanks',
                       # 'UniversalStorage2',  # uses USFuelSwitch
                       'MiningExpansion',
                       # 'StationPartsExpansionRedux',  # uses B9PartSwitch
                       # 'ReStock', # no parts
                       'ReStockPlus',
                       'PlanetaryBaseInc',  # Kerbal Planetary Base Systems
                       # 'FelineUtilityRover',  # uses ModuleKerbetrotterResourceSwitch
                       'Benjee10_X-37B',  # Mk-X Spaceplane Parts
                       'Mk3HypersonicSystems',
                       'DodoLabs',  # Stockalike Electron
                       # 'Mkerb',  # no parts
                       # 'JSI'  # no parts
                       )

    patcher.patch_parts(('ConfigurableContainers', 'Parts', 'Tal-Tanks_Patch.cfg'),
                        [('ModsByTal', 'Parts')],
                        [(SearchTerm(''), Module.Patch('!', 'ModuleFuelTanks'))],
                        add_spec=':AFTER[ModsByTal]')

    # USI uses FSfuelSwitch, so no patching for it
    # patcher.patch_parts(('ConfigurableContainers', 'Parts', 'USI-MKS_Patch.cfg'),
    #                     [('UmbraSpaceIndustries', 'Akita'),
    #                      ('UmbraSpaceIndustries', 'Konstruction'),
    #                      ('UmbraSpaceIndustries', 'Kontainers'),
    #                      ('UmbraSpaceIndustries', 'MKS'),
    #                      ('UmbraSpaceIndustries', 'ReactorPack'),
    #                     ],
    #                     add_spec=':NEEDS[MKS]:AFTER[MKS]')
    #
    # patcher.patch_parts(('ConfigurableContainers', 'Parts', 'USI-LS_Patch.cfg'),
    #                     [('UmbraSpaceIndustries', 'LifeSupport'),
    #                      ],
    #                     add_spec=':NEEDS[USILifeSupport]:AFTER[USILifeSupport]')
    #
    # patcher.patch_parts(('ConfigurableContainers', 'Parts', 'USI-FTT_Patch.cfg'),
    #                     [('UmbraSpaceIndustries', 'FTT'),
    #                      ],
    #                     add_spec=':NEEDS[FTT]:AFTER[FTT]')
    #
    # patcher.patch_parts(('ConfigurableContainers', 'Parts', 'USI-ExpPack_Patch.cfg'),
    #                     [('UmbraSpaceIndustries', 'ExpPack'),
    #                      ],
    #                     add_spec=':NEEDS[ExpPack]:AFTER[ExpPack]')

    print('Done')

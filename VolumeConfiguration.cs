//   VolumeConfiguration.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System.Collections.Generic;
using System.Linq;

namespace AT_Utils
{
    public class BaseVolume : ConfigNodeObject
    {
        public new const string NODE_NAME = "VOLUME";
        [Persistent] public float Volume = 1;

        public virtual string Info(float volume_conversion = 1)
        {
            return $"{Utils.formatVolume(Volume * volume_conversion)}\n";
        }

        public virtual float AddMass(float volume_conversion = 1)
        {
            return 0f;
        }

        public virtual float Cost(float volume_conversion = 1)
        {
            return 0f;
        }

        public virtual float ResourceCost(bool maxAmount = true, float volume_conversion = 1)
        {
            return 0f;
        }

        public virtual float ResourceMass(bool maxAmount = true, float volume_conversion = 1)
        {
            return 0f;
        }

        public virtual float ResourceAmount(bool maxAmount = true, float volume_conversion = 1)
        {
            return 0f;
        }

        public virtual bool ContainsType(string tank_type)
        {
            return false;
        }
    }


    public class TankVolume : BaseVolume
    {
        public new const string NODE_NAME = "TANK";
        [Persistent] public string CurrentResource;
        [Persistent] public float InitialAmount;

        [Persistent] public string TankType;

        public SwitchableTankType Type => SwitchableTankType.GetTankType(TankType);

        public override float AddMass(float volume_conversion = 1)
        {
            var t = Type;
            return t?.AddMass(Volume * volume_conversion) ?? 0;
        }

        public override float Cost(float volume_conversion = 1)
        {
            var t = Type;
            return t?.Cost(Volume * volume_conversion) ?? 0;
        }

        public override float ResourceCost(bool maxAmount = true, float volume_conversion = 1)
        {
            try
            {
                var t = Type;
                var res = t.Resources[CurrentResource];
                var cost = res.def.unitCost * res.UnitsPerLiter * t.UsefulVolume(Volume) * volume_conversion * 1000;
                return maxAmount ? cost : cost * InitialAmount;
            }
            catch
            {
                return 0;
            }
        }

        public override float ResourceMass(bool maxAmount = true, float volume_conversion = 1)
        {
            try
            {
                var t = Type;
                var res = t.Resources[CurrentResource];
                var mass = res.def.density * res.UnitsPerLiter * t.UsefulVolume(Volume) * volume_conversion * 1000;
                return maxAmount ? mass : mass * InitialAmount;
            }
            catch
            {
                return 0;
            }
        }

        public override float ResourceAmount(bool maxAmount = true, float volume_conversion = 1)
        {
            try
            {
                var t = Type;
                var res = t.Resources[CurrentResource];
                var amount = res.UnitsPerLiter * t.UsefulVolume(Volume) * volume_conversion * 1000;
                return maxAmount ? amount : amount * InitialAmount;
            }
            catch
            {
                return 0;
            }
        }

        public override string Info(float volume_conversion = 1)
        {
            var info = StringBuilderCache.Acquire();
            info.Append($"- {TankType}");
            if(!string.IsNullOrEmpty(CurrentResource))
                info.Append($"/{CurrentResource}");
            info.Append($"\n   {Utils.formatVolume(Volume * volume_conversion)}");
            info.Append($"\n   {Utils.formatBigValue(ResourceAmount(true, volume_conversion), "u")}");
            info.Append($"\n   {Utils.formatBigValue(ResourceMass(true, volume_conversion), "t")}");
            info.Append($"\n   {Cost(volume_conversion):F1}");
            if(InitialAmount > 0)
                info.Append($"+{ResourceCost(false, volume_conversion):F1}");
            info.Append(" Cr\n");
            return info.ToStringAndRelease();
        }

        public override bool ContainsType(string tank_type)
        {
            return TankType == tank_type;
        }

        public static TankVolume FromResource(PartResource res)
        {
            var tank = new TankVolume();
            var tank_type = SwitchableTankType.FindTankType(res.resourceName);
            if(tank_type == null)
                return null;
            tank.TankType = tank_type.name;
            tank.CurrentResource = res.resourceName;
            tank.Volume = (float)(res.maxAmount
                                  / tank_type.Resources[res.resourceName].UnitsPerLiter
                                  / 1000
                                  / tank_type.UsefulVolumeRatio);
            tank.InitialAmount = (float)(res.amount / res.maxAmount);
            return tank;
        }
    }


    public class VolumeConfiguration : BaseVolume
    {
        public new const string NODE_NAME = "TANKCONF";

        /// <summary>
        ///     The name of a configuration.
        /// </summary>
        [Persistent]
        public string name = "";

        public readonly List<BaseVolume> Volumes = new List<BaseVolume>();
        public float TotalVolume { get { return Volumes.Aggregate(0f, (v, t) => v + t.Volume); } }
        public bool Valid => Volumes.Count > 0 && TotalVolume > 0;

        public override void Load(ConfigNode node)
        {
            Volumes.Clear();
            base.Load(node);
            var volumes = node.GetNodes();
            for(var i = 0; i < volumes.Length; i++)
            {
                var v = volumes[i];
                switch(v.name)
                {
                    case TankVolume.NODE_NAME:
                    {
                        var preset = VolumeConfigsLibrary.GetConfig(v.GetValue("name"));
                        if(preset != null)
                        {
                            if(!float.TryParse(v.GetValue("Volume"), out var volume))
                                volume = 100f;
                            var cfg = preset.Clone<VolumeConfiguration>();
                            cfg.Volume = volume;
                            Volumes.Add(cfg);
                        }
                        else
                        {
                            Volumes.Add(FromConfig<TankVolume>(v));
                        }
                        break;
                    }
                    case NODE_NAME:
                        Volumes.Add(FromConfig<VolumeConfiguration>(v));
                        break;
                }
            }
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            Volumes.ForEach(t => t.SaveInto(node));
        }

        public override string Info(float volume_conversion = 1)
        {
            volume_conversion = Volume * volume_conversion / TotalVolume;
            return Volumes.Aggregate("", (s, v) => s + v.Info(volume_conversion));
        }

        public override float AddMass(float volume_conversion = 1)
        {
            volume_conversion = Volume * volume_conversion / TotalVolume;
            return Volumes.Aggregate(0f, (s, v) => s + v.AddMass(volume_conversion));
        }

        public override float Cost(float volume_conversion = 1)
        {
            volume_conversion = Volume * volume_conversion / TotalVolume;
            return Volumes.Aggregate(0f, (s, v) => s + v.Cost(volume_conversion));
        }

        public override float ResourceCost(bool maxAmount = true, float volume_conversion = 1)
        {
            volume_conversion = Volume * volume_conversion / TotalVolume;
            return Volumes.Aggregate(0f, (s, v) => s + v.ResourceCost(maxAmount, volume_conversion));
        }

        public override float ResourceMass(bool maxAmount = true, float volume_conversion = 1)
        {
            volume_conversion = Volume * volume_conversion / TotalVolume;
            return Volumes.Aggregate(0f, (s, v) => s + v.ResourceMass(maxAmount, volume_conversion));
        }

        public override bool ContainsType(string tank_type)
        {
            return Volumes.Any(v => v.ContainsType(tank_type));
        }

        public bool ContainsTypes(IEnumerable<string> tank_types)
        {
            return tank_types.Any(ContainsType);
        }

        public static VolumeConfiguration FromResources(IEnumerable<PartResource> resources)
        {
            var volume = new VolumeConfiguration();
            foreach(var res in resources)
            {
                var tank = TankVolume.FromResource(res);
                if(tank == null)
                    return null;
                volume.Volumes.Add(tank);
            }
            volume.Volume = volume.TotalVolume;
            return volume;
        }
    }
}

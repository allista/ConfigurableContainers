//   ResourceBoiloff.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AT_Utils
{
	public class CryogenicsParams : ConfigNodeObject
	{
		public new const string NODE_NAME = "CRYOGENICS";

		public class Resource : ConfigNodeObject
		{
			public new const string NODE_NAME = "RESOURCE";

			[Persistent] public string name = "";
			[Persistent] public float  BoiloffTemperature = 120;
			[Persistent] public float  VaporizationHeat   = -1;
			[Persistent] public float  CoolingEfficiency  = 0.3f;

			public double GetVaporizationHeat(PartResource r)
			{ 
				return VaporizationHeat > 0? VaporizationHeat : 
					r.info.specificHeatCapacity * Instance.SpecificHeat2VaporisationHeat;
			}
		}
		public Dictionary<string,Resource> Resources = new Dictionary<string, Resource>();

		[Persistent] public float SpecificHeat2VaporisationHeat = 1000;
		[Persistent] public float InsulationFactor = 1e-3f;

		[Persistent] public float ElectricCharge2kJ = 10;
		[Persistent] public float MaxAbsoluteCoolerPower = 10;
		[Persistent] public float MaxSpecificCoolerPower = 10;

		const string config_path = "ConfigurableContainers/Cryogenics/";
		static CryogenicsParams instance;
		public static CryogenicsParams Instance
		{
			get
			{
				if(instance == null)
				{
					instance = new CryogenicsParams();
					var node = GameDatabase.Instance.GetConfigNode(config_path+NODE_NAME);
					if(node != null) instance.Load(node);
					else Utils.Log("CryogenicsParams NODE not found: {}", config_path+NODE_NAME);
				}
				return instance;
			}
		}

		public Resource GetResource(PartResource r)
		{
			Resource res;
			return Resources.TryGetValue(r.resourceName, out res)? res : null;
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			Resources.Clear();
			foreach(var n in node.GetNodes(Resource.NODE_NAME))
			{
				var res = ConfigNodeObject.FromConfig<Resource>(n);
				Resources.Add(res.name, res);
			}
		}
	}

	public class ResourceBoiloff : ConfigNodeObject
	{
		public new const string NODE_NAME = "RES_BOILOFF";

		protected Part part;
		protected PartResource resource;
		protected CryogenicsParams.Resource cryo_info;

		[Persistent] public double CoreTemperature = -1;
		[Persistent] public double LastUpdateTime = -1;

		public bool Valid { get { return cryo_info != null; } }

		protected double boiloffTemperature; //K
		protected double vaporizationHeat; //per unit
		protected double specificHeatCapacity; //per unit

		public virtual void SetResource(PartResource res)
		{
			if(res == null)
			{
				part = null;
				resource = null;
				cryo_info = null;
				return;
			}
			resource = res;
			part = res.part;
			cryo_info = CryogenicsParams.Instance.GetResource(res);
			if(cryo_info == null) return;
			boiloffTemperature = cryo_info.BoiloffTemperature;
			specificHeatCapacity = res.info.specificHeatCapacity * res.info.density;
			vaporizationHeat = cryo_info.GetVaporizationHeat(res) * res.info.density;
		}

		protected virtual void UpdateCoreTemperature(double deltaTime)
		{
			var resThermalMass = resource.amount*specificHeatCapacity;
			var partThermalMass = Math.Max(part.thermalMass-resThermalMass, 1e-3);
			var equilibriumT = (part.temperature*partThermalMass+CoreTemperature*resThermalMass)/part.thermalMass;
			var CoreDeltaT = (equilibriumT-CoreTemperature)*(1-Math.Exp(-deltaTime*CryogenicsParams.Instance.InsulationFactor));
			var PartDeltaT = (equilibriumT-part.temperature)*(1-Math.Exp(-deltaTime*CryogenicsParams.Instance.InsulationFactor));
			part.AddThermalFlux(PartDeltaT*part.thermalMass/TimeWarp.fixedDeltaTime);
			Utils.Log("P.T {}, C.T {}, PdT {}, CdT {}", 
			          part.temperature, CoreTemperature, PartDeltaT, CoreDeltaT);//debug
			CoreTemperature += CoreDeltaT;
		}

		public virtual void FixedUpdate()
		{
			if(!HighLogic.LoadedSceneIsFlight ||
			   resource == null || part == null || cryo_info == null ||
			   resource.amount <= 0) return;
			if(LastUpdateTime < 0)
				CoreTemperature = resource.amount > 0? Math.Max(boiloffTemperature-10, PhysicsGlobals.SpaceTemperature) : part.temperature;
			var deltaTime = GetDeltaTime();
			Utils.Log("deltaTime: {}", deltaTime);//debug
			if(deltaTime < 0) return;
			UpdateCoreTemperature(deltaTime);
			var dTemp = CoreTemperature-boiloffTemperature;
//			Utils.Log("dTemp: {}", dTemp);//debug
			if(dTemp > 0)
			{
				var boiled_off = resource.amount*(1-Math.Exp(-dTemp*specificHeatCapacity/vaporizationHeat));
				if(boiled_off > resource.amount) boiled_off = resource.amount;
				Utils.Log("last amount {}, amount {}, boiled off {}",
				          resource.amount, resource.amount-boiled_off, boiled_off);//debug
				resource.amount -= boiled_off;
				if(resource.amount < 1e-9) resource.amount = 0;
				CoreTemperature = boiloffTemperature;
			}
		}

		double GetDeltaTime()
		{
			if(Time.timeSinceLevelLoad < 1 || !FlightGlobals.ready) return -1;
			if(LastUpdateTime < 0)
			{
				LastUpdateTime = Planetarium.GetUniversalTime();
				return TimeWarp.fixedDeltaTime;
			}
			var time = Planetarium.GetUniversalTime();
			var dT = time - LastUpdateTime;
			LastUpdateTime = time;
			return dT;
		}
	}

	public class ActiveCooling : ResourceBoiloff
	{
		public new const string NODE_NAME = "RES_COOLING";

		double MaxPower = 10;
		double Efficiency = 0.17f;

		[Persistent] public double Power = -1;
		[Persistent] public double CoolingEfficiency = 0;

		[Persistent] public double LastPartTemp = -1;
		[Persistent] public double PartTempRate = -1;

		[Persistent] public bool   Enabled = true;
		[Persistent] public bool   IsCooling;

		double Q2W { get { return (part.skinTemperature-CoreTemperature)/CoreTemperature/Efficiency; } }

		public override void SetResource(PartResource res)
		{
			base.SetResource(res);
			if(cryo_info == null) return;
			MaxPower = Math.Min(CryogenicsParams.Instance.MaxSpecificCoolerPower * res.maxAmount * specificHeatCapacity,
			                    CryogenicsParams.Instance.MaxAbsoluteCoolerPower);
			Efficiency = cryo_info.CoolingEfficiency;
		}

		protected override void UpdateCoreTemperature(double deltaTime)
		{
			var last_core_temp = CoreTemperature;
			base.UpdateCoreTemperature(deltaTime);
			if(!Enabled) return;
			IsCooling = false;
			if(part.skinTemperature/part.skinMaxTemp > PhysicsGlobals.TemperatureGaugeThreshold)
				goto disable;
			var temp_excess = CoreTemperature-boiloffTemperature;
			Utils.Log("Skin.T {}, dTemp {}", part.skinTemperature, temp_excess);//debug
			if(temp_excess < 0)
			{
				var work_needed = Math.Abs(CoreTemperature-last_core_temp) *
					resource.amount*specificHeatCapacity *
					Q2W/CryogenicsParams.Instance.ElectricCharge2kJ;
				var work = work_needed/deltaTime > MaxPower? MaxPower*deltaTime : work_needed;
				Power = work/deltaTime;
				CoolingEfficiency = work/work_needed;
				Utils.Log("Power {}/{}, Efficiency {}", Power, MaxPower, CoolingEfficiency);//debug
				return;
			}
			if(deltaTime < 1)
			{
				var q2w = Q2W;
				var resThermalMass = resource.amount*specificHeatCapacity;
				var work = temp_excess*resThermalMass*q2w/CryogenicsParams.Instance.ElectricCharge2kJ;
				if(work/deltaTime > MaxPower) work = MaxPower*deltaTime;
				work = part.vessel.RequestResource(part, Utils.ElectricChargeID, work, false);
				if(work <= 0) goto disable;
				IsCooling = true;
				Power = work/deltaTime;
				var energy_extracted = work/q2w*CryogenicsParams.Instance.ElectricCharge2kJ;
				var cooled = energy_extracted/resThermalMass;
				CoolingEfficiency = cooled/temp_excess;
				CoreTemperature -= cooled;
				part.AddSkinThermalFlux(energy_extracted/TimeWarp.fixedDeltaTime);
				if(LastPartTemp >= 0) PartTempRate = (part.skinTemperature-LastPartTemp)/deltaTime;
				LastPartTemp = part.skinTemperature;
				Utils.Log("Power {}/{}, Cooled {}/{}", Power, MaxPower, cooled, temp_excess);//debug
				return;
			}
			else if(Power > 0)
			{
				var coolingTime = deltaTime;
				var part_temp_delta = 0.0;
				if(PartTempRate > 0)
				{
					part_temp_delta = PartTempRate*deltaTime;
					if(part_temp_delta+LastPartTemp > part.skinMaxTemp*PhysicsGlobals.TemperatureGaugeThreshold)
						part_temp_delta = Math.Max(part.skinMaxTemp*PhysicsGlobals.TemperatureGaugeThreshold-LastPartTemp, 0);
					if(part_temp_delta <= 0) goto disable;
					coolingTime = part_temp_delta/PartTempRate;
				}
				var needed_work = Power*coolingTime;
				var energy_spent = part.vessel.RequestResource(part, Utils.ElectricChargeID, needed_work, false);
				if(energy_spent <= 0) goto disable;
				IsCooling = true;
				CoreTemperature -= temp_excess*CoolingEfficiency*energy_spent/needed_work*coolingTime/deltaTime;
				if(part_temp_delta > 0)
					part.AddSkinThermalFlux(part_temp_delta*part.skinThermalMass/TimeWarp.fixedDeltaTime);
				Utils.Log("Efficiency {}, cooling time {}/{}, energy spent {}/{}, Cooled {}/{}", 
				          CoolingEfficiency, 
				          coolingTime, deltaTime, 
				          energy_spent, needed_work, 
				          temp_excess*CoolingEfficiency*energy_spent/needed_work*coolingTime/deltaTime, temp_excess);//debug
				return;
			}
			disable:
			{
				Power = 0;
				CoolingEfficiency = 0;
				IsCooling = false;
				Enabled = false;
			}
		}
	}
}


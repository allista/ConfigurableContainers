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

		public const double AbsZero = -273.15;

		[Persistent] public float SpecificHeat2VaporisationHeat = 1000;
		[Persistent] public float InsulationFactor = 1e-3f;

		[Persistent] public float ElectricCharge2kJ = 10;
		[Persistent] public float MaxAbsoluteCoolerPower = 500;
		[Persistent] public float MaxSpecificCoolerPower = 1;
		[Persistent] public float ShutdownThreshold = 0.99f;

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
			var temperature_transfer = 1-Math.Exp(-deltaTime*CryogenicsParams.Instance.InsulationFactor);
			var CoreDeltaT = (equilibriumT-CoreTemperature)*temperature_transfer;
			var PartDeltaT = (equilibriumT-partThermalMass)*temperature_transfer;
			part.AddThermalFlux(PartDeltaT*part.thermalMass/TimeWarp.fixedDeltaTime);
			Utils.Log("P.T {} > Eq.T {} < C.T {}, P.dT {}, C.dT {}", 
			          part.temperature, equilibriumT, CoreTemperature, PartDeltaT, CoreDeltaT);//debug
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
			Utils.Log("deltaTime: {}, fixedDeltaTime {}", deltaTime, TimeWarp.fixedDeltaTime);//debug
			if(deltaTime < 0) return;
			UpdateCoreTemperature(deltaTime);
			var dTemp = CoreTemperature-boiloffTemperature;
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

		[Persistent] public double LastSkinTemp = -1;
		[Persistent] public double SkinTempRate = -1;
		[Persistent] public double LastEc = -1;
		[Persistent] public double EcRate = double.NaN;

		[Persistent] public bool   Enabled = true;
		[Persistent] public bool   IsCooling;

		public double PowerConsumptionAt300K 
		{ 
			get 
			{ 
				var resThermalMass = resource.amount*specificHeatCapacity;
				var partThermalMass = part.mass * PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier;
				var equilibriumT = (300*partThermalMass+boiloffTemperature*resThermalMass)/(partThermalMass+resThermalMass);
				Utils.Log("res.tM {}, part.tM {}, eq.T {}", resThermalMass, partThermalMass, equilibriumT);//debug
				var temperature_transfer = 1-Math.Exp(-TimeWarp.fixedDeltaTime*CryogenicsParams.Instance.InsulationFactor);
				var CoreDeltaT = (equilibriumT-boiloffTemperature)*temperature_transfer;
				return Math.Min(CoreDeltaT*resThermalMass *
				                (300-boiloffTemperature)/boiloffTemperature/Efficiency /
				                CryogenicsParams.Instance.ElectricCharge2kJ / 
				                TimeWarp.fixedDeltaTime, 
				                MaxPower);
			} 
		}

		double Q2W { get { return (part.skinTemperature-CoreTemperature)/CoreTemperature/Efficiency; } }

		public override void SetResource(PartResource res)
		{
			base.SetResource(res);
			if(cryo_info == null) return;
			MaxPower = Math.Min(CryogenicsParams.Instance.MaxSpecificCoolerPower * res.maxAmount * specificHeatCapacity,
			                    CryogenicsParams.Instance.MaxAbsoluteCoolerPower);
			Efficiency = cryo_info.CoolingEfficiency;
		}

		void update_rates(double deltaTime)
		{
			if(LastSkinTemp >= 0) SkinTempRate = (part.skinTemperature-LastSkinTemp)/deltaTime;
			LastSkinTemp = part.skinTemperature;
			double ec_amount, ec_max;
			part.GetConnectedResourceTotals(Utils.ElectricChargeID, out ec_amount, out ec_max);
			if(LastEc >= 0) EcRate = (LastEc-ec_amount)/deltaTime;
			LastEc = ec_amount;
			Utils.Log("SkinTempRate {}, EcRate {}", SkinTempRate, EcRate);//debug
		}

		protected override void UpdateCoreTemperature(double deltaTime)
		{
			var last_core_temp = CoreTemperature;
			base.UpdateCoreTemperature(deltaTime);
			IsCooling = false;
			if(!Enabled) return;
			if(part.skinTemperature/part.skinMaxTemp > PhysicsGlobals.TemperatureGaugeThreshold)
				goto disable;
			var temperature_excess = CoreTemperature-boiloffTemperature;
			Utils.Log("Skin.T {}, dTemp {}", part.skinTemperature, temperature_excess);//debug
			if(temperature_excess < 0)
			{
				var electric_charge_needed = Math.Abs(CoreTemperature-last_core_temp) *
					resource.amount*specificHeatCapacity *
					Q2W/CryogenicsParams.Instance.ElectricCharge2kJ;
				var electric_charge = electric_charge_needed/deltaTime > MaxPower? MaxPower*deltaTime : electric_charge_needed;
				Power = electric_charge/deltaTime;
				CoolingEfficiency = electric_charge/electric_charge_needed;
				update_rates(deltaTime);
				Utils.Log("Would Consume {}/{}, Power {}/{}, Efficiency {}", 
				          electric_charge, electric_charge_needed, Power, MaxPower, CoolingEfficiency);//debug
				return;
			}
			if(Math.Abs(deltaTime-TimeWarp.fixedDeltaTime) < 1e-5)
			{
				var q2w = Q2W;
				var resThermalMass = resource.amount*specificHeatCapacity;
				var electric_charge_needed = temperature_excess*resThermalMass*q2w/CryogenicsParams.Instance.ElectricCharge2kJ;
				if(electric_charge_needed/deltaTime > MaxPower) electric_charge_needed = MaxPower*deltaTime;
				var electric_charge = part.vessel.RequestResource(part, Utils.ElectricChargeID, electric_charge_needed, false);
				if(electric_charge/electric_charge_needed < CryogenicsParams.Instance.ShutdownThreshold)
				{
					Utils.Message("Not enough energy, CryoCooler is disabled.");
					goto disable;
				}
				IsCooling = true;
				Power = electric_charge/deltaTime;
				var energy_extracted = electric_charge/q2w*CryogenicsParams.Instance.ElectricCharge2kJ;
				var cooled = energy_extracted/resThermalMass;
				CoolingEfficiency = cooled/temperature_excess;
				CoreTemperature -= cooled;
				part.AddSkinThermalFlux(energy_extracted/TimeWarp.fixedDeltaTime);
				update_rates(deltaTime);
				Utils.Log("Consumed {}, Power {}/{}, Cooled {}/{}, SkinTempRate {}", 
				          electric_charge, Power, MaxPower, cooled, temperature_excess, SkinTempRate);//debug
				return;
			}
			else if(Power > 0) //catch up after bein unloaded
			{
				var coolingTime = deltaTime;
				var skin_temp_delta = 0.0;
				if(SkinTempRate > 0)
				{
					skin_temp_delta = SkinTempRate*deltaTime;
					if(skin_temp_delta+LastSkinTemp > part.skinMaxTemp*PhysicsGlobals.TemperatureGaugeThreshold)
						skin_temp_delta = Math.Max(part.skinMaxTemp*PhysicsGlobals.TemperatureGaugeThreshold-LastSkinTemp, 0);
					if(skin_temp_delta <= 0) goto disable;
					coolingTime = skin_temp_delta/SkinTempRate;
				}
				var electric_charge_needed = double.IsNaN(EcRate)? Power*coolingTime : EcRate*coolingTime;
				var electric_charge = part.vessel.RequestResource(part, Utils.ElectricChargeID, electric_charge_needed, false);
				if(electric_charge_needed > 0 &&
				   electric_charge/electric_charge_needed < CryogenicsParams.Instance.ShutdownThreshold)
				{
					Utils.Message("Not enough energy, CryoCooler is disabled.");
					goto disable;
				}
				IsCooling = true;
				var ec_efficiency = electric_charge_needed > 0? electric_charge/electric_charge_needed : 1;
				CoreTemperature -= temperature_excess*CoolingEfficiency*ec_efficiency*coolingTime/deltaTime;
				if(skin_temp_delta > 0)
					part.AddSkinThermalFlux(skin_temp_delta*part.skinThermalMass/TimeWarp.fixedDeltaTime);
				Utils.Log("Efficiency {}, cooling time {}/{}, Consumed {}/{}, Cooled {}/{}, Skin.dT {}/{}", 
				          CoolingEfficiency, 
				          coolingTime, deltaTime, 
				          electric_charge, electric_charge_needed, 
				          temperature_excess*CoolingEfficiency*electric_charge/electric_charge_needed*coolingTime/deltaTime, temperature_excess,
				          skin_temp_delta, SkinTempRate*deltaTime);//debug
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


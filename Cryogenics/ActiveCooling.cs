﻿//   ResourceBoiloff.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using JetBrains.Annotations;

namespace AT_Utils
{
    public class ActiveCooling : ResourceBoiloff
    {
        [UsedImplicitly] public new const string NODE_NAME = "RES_COOLING";

        [Persistent] public double CoolingEfficiency;
        private double Efficiency = 0.17f;

        [Persistent] public bool Enabled = true;
        [Persistent] public bool IsCooling;

        private double MaxPower = 10;

        public ActiveCooling(ModuleSwitchableTank tank) : base(tank) { }

        public double PowerConsumptionAt300K
        {
            get
            {
                var CoreDeltaT = CoreDeltaTAt300K(out _, out var resThermalMass);
                return Math.Min(CoreDeltaT
                                * resThermalMass
                                * (300 - boiloffTemperature)
                                / boiloffTemperature
                                / Efficiency
                                / CryogenicsParams.Instance.ElectricCharge2kJ,
                    MaxPower);
            }
        }

        /// <summary>
        ///     Converts the amount of heat that needs to be retrieved from the core to the amount of work that is
        ///     required to transfer that heat to the part's skin.
        /// </summary>
        protected double Q2W => (part.skinTemperature - CoreTemperature) / CoreTemperature / Efficiency;

        public override void SetResource(PartResource res)
        {
            base.SetResource(res);
            if(cryo_info == null)
                return;
            MaxPower = Math.Min(CryogenicsParams.Instance.MaxSpecificCoolerPower * res.maxAmount * specificHeatCapacity,
                CryogenicsParams.Instance.MaxAbsoluteCoolerPower);
            Efficiency = cryo_info.CoolingEfficiency;
        }

        protected override void UpdateCoreTemperature(double deltaTime)
        {
            var last_core_temp = CoreTemperature;
            base.UpdateCoreTemperature(deltaTime);
            IsCooling = false;
            if(!Enabled)
                return;
            if(part.skinTemperature / part.skinMaxTemp > PhysicsGlobals.TemperatureGaugeThreshold)
                goto disable;
            var temperature_excess = CoreTemperature - boiloffTemperature;
//            Utils.Log("Skin.T {}, dTemp {}", part.skinTemperature, temperature_excess);//debug
            if(temperature_excess < 0)
            {
                var electric_charge_needed = Math.Abs(CoreTemperature - last_core_temp)
                                             * resource.amount
                                             * specificHeatCapacity
                                             * Q2W
                                             / CryogenicsParams.Instance.ElectricCharge2kJ;
                var electric_charge = electric_charge_needed / deltaTime > MaxPower
                    ? MaxPower * deltaTime
                    : electric_charge_needed;
                CoolingEfficiency = electric_charge / electric_charge_needed;
//                Utils.Log("Would Consume {}/{}, Power {}/{}, Efficiency {}", 
//                          electric_charge, electric_charge_needed, electric_charge/deltaTime, MaxPower, CoolingEfficiency);//debug
                return;
            }
            if(Math.Abs(deltaTime - TimeWarp.fixedDeltaTime) < 1e-5)
            {
                var q2w = Q2W;
                var resThermalMass = resource.amount * specificHeatCapacity;
                var electric_charge_needed =
                    temperature_excess * resThermalMass * q2w / CryogenicsParams.Instance.ElectricCharge2kJ;
                if(electric_charge_needed / deltaTime > MaxPower)
                    electric_charge_needed = MaxPower * deltaTime;
                var electric_charge =
                    part.vessel.RequestResource(part, Utils.ElectricCharge.id, electric_charge_needed, false);
                if(electric_charge / electric_charge_needed < CryogenicsParams.Instance.ShutdownThreshold)
                {
                    Utils.Message("Not enough energy, CryoCooler is disabled.");
                    goto disable;
                }
                IsCooling = true;
                var energy_extracted = electric_charge / q2w * CryogenicsParams.Instance.ElectricCharge2kJ;
                var cooled = energy_extracted / resThermalMass;
                CoolingEfficiency = cooled / temperature_excess;
                CoreTemperature -= cooled;
                part.AddSkinThermalFlux(energy_extracted / TimeWarp.fixedDeltaTime);
//                Utils.Log("Consumed {}, Power {}/{}, Cooled {}/{}, SkinTempRate {}", 
//                          electric_charge, electric_charge/deltaTime, MaxPower, cooled, temperature_excess);//debug
                return;
            }
            if(CoolingEfficiency > 0) //catch up after being unloaded
            {
                IsCooling = true;
                CoreTemperature -= temperature_excess * CoolingEfficiency;
//                Utils.Log("Efficiency {}, Cooled {}/{}", 
//                          CoolingEfficiency, temperature_excess*CoolingEfficiency, temperature_excess);//debug
                return;
            }
            disable:
            {
                CoolingEfficiency = 0;
                IsCooling = false;
                Enabled = false;
            }
        }
    }
}

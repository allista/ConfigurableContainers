//   Boiloff.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using UnityEngine;

namespace AT_Utils
{
    public class ResourceBoiloff : ConfigNodeObject
    {
        public new const string NODE_NAME = "RES_BOILOFF";

        protected Part part;
        protected PartResource resource;
        protected CryogenicsParams.CryoResource cryo_info;
        protected ModuleSwitchableTank tank;

        /// <summary>
        /// Current temperature of the resource mass.
        /// </summary>
        [Persistent] public double CoreTemperature = -1;
        /// <summary>
        /// The last UT the core temperature was updated.
        /// </summary>
        [Persistent] public double LastUpdateTime = -1;

        /// <summary>
        /// The temperature (K) at which the boiloff occures.
        /// </summary>
        protected double boiloffTemperature;
        /// <summary>
        /// The vaporization heat of the resource per unit.
        /// </summary>
        protected double vaporizationHeat;
        /// <summary>
        /// The heat capacity of the resource per unit.
        /// </summary>
        protected double specificHeatCapacity;
        /// <summary>
        /// The insulator conductivity in kW/s.
        /// </summary>
        protected double insulatorConductivity;

        /// <summary>
        /// This is <c>true</c> if the SetResource is successfull, <c>false</c> otherwise.
        /// </summary>
        public bool Valid { get { return cryo_info != null; } }

        protected double temperatureTransfer(double deltaTime, double partThermalMass, double resThermalMass, double partT, double resT, out double equilibriumT)
        {
            var totalThermalMass = partThermalMass+resThermalMass;
            equilibriumT = (partT*partThermalMass+resT*resThermalMass)/totalThermalMass;
            return 1-Math.Exp(deltaTime*insulatorConductivity*totalThermalMass/(partThermalMass+resThermalMass));
        }

        protected double CoreDeltaTAt300K(out double partThermalMass, out double resThermalMass)
        {
            resThermalMass = resource.amount*specificHeatCapacity;
            partThermalMass = part.mass * PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier;
            double equilibriumT;
            var transfer = temperatureTransfer(1, partThermalMass, resThermalMass, 300, boiloffTemperature, out equilibriumT);
//            Utils.Log("Eq.T {}, C.dT {}, conductivity {}, (P.tM {} + C.tM {})/(P.tM*C.tM) = {}, transfer {}", 
//                      equilibriumT, (equilibriumT-boiloffTemperature)*transfer, 
//                      insulatorConductivity,
//                      partThermalMass, resThermalMass, (partThermalMass+resThermalMass)/(resThermalMass*partThermalMass),
//                      transfer);//debug
            return (equilibriumT-boiloffTemperature)*transfer;
        }

        public double BoiloffAt300K 
        { 
            get 
            { 
                double resThermalMass, partThemralMass;
                var CoreDeltaT = CoreDeltaTAt300K(out partThemralMass, out resThermalMass);
                return resource.amount*(1-Math.Exp(-CoreDeltaT*specificHeatCapacity/vaporizationHeat));
            } 
        }

        public ResourceBoiloff(ModuleSwitchableTank tank)
        {
            this.tank = tank;
            part = tank.part;
        }

        public virtual void SetResource(PartResource res)
        {
            if(res == null)
            {
                resource = null;
                cryo_info = null;
                return;
            }
            resource = res;
            cryo_info = CryogenicsParams.Instance.GetResource(res);
            if(cryo_info == null) return;
            boiloffTemperature = cryo_info.BoiloffTemperature;
            specificHeatCapacity = res.info.specificHeatCapacity * res.info.density;
            vaporizationHeat = cryo_info.GetVaporizationHeat(res) * res.info.density;
            UpdateInsulation();
        }

        public void UpdateInsulation()
        { insulatorConductivity = CryogenicsParams.Instance.GetInsulatorConductivity(tank.Volume); }

        protected virtual void UpdateCoreTemperature(double deltaTime)
        {
            var resThermalMass = resource.amount*specificHeatCapacity;
            var partThermalMass = Math.Max(part.thermalMass-resThermalMass, 1e-3);
            double equilibriumT;
            var transfer = temperatureTransfer(deltaTime, partThermalMass, resThermalMass, part.temperature, CoreTemperature, out equilibriumT);
            var CoreDeltaT = (equilibriumT-CoreTemperature)*transfer;
            var PartDeltaT = (equilibriumT-part.temperature)*transfer;
            part.AddThermalFlux(PartDeltaT*part.thermalMass/TimeWarp.fixedDeltaTime);
//            Utils.Log("P.T {} > Eq.T {} < C.T {}, P.dT {}, C.dT {}, conductivity {}, (P.tM {} + C.tM {})/(P.tM*C.tM) = {}, transfer {}", 
//                      part.temperature, equilibriumT, CoreTemperature, PartDeltaT, CoreDeltaT, 
//                      insulatorConductivity,
//                      partThermalMass, resThermalMass, (partThermalMass+resThermalMass)/(resThermalMass*partThermalMass),
//                      transfer);//debug
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
//            Utils.Log("deltaTime: {}, fixedDeltaTime {}", deltaTime, TimeWarp.fixedDeltaTime);//debug
            if(deltaTime < 0) return;
            UpdateCoreTemperature(deltaTime);
            var dTemp = CoreTemperature-boiloffTemperature;
            if(dTemp > 0)
            {
                var boiled_off = resource.amount*(1-Math.Exp(-dTemp*specificHeatCapacity/vaporizationHeat));
                if(boiled_off > resource.amount) boiled_off = resource.amount;
//                Utils.Log("last amount {}, amount {}, boiled off {}",
//                          resource.amount, resource.amount-boiled_off, boiled_off);//debug
                resource.amount -= boiled_off;
                if(resource.amount < 1e-9) resource.amount = 0;
                CoreTemperature = boiloffTemperature;
            }
        }

        private double GetDeltaTime()
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
}


//   ResourceBoiloff.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2016 Allis Tauri

using System;
using AT_Utils;

namespace AT_Utils
{
	//Adapted from CryoTanks by Chris Adderley:
	//https://github.com/ChrisAdderley/CryoTanks/blob/master/Source/ModuleSimpleBoiloff.cs
	public class ResourceBoiloff : ConfigNodeObject
	{
		public new const string NODE_NAME = "ResourceBoiloff";

		readonly Part part;
		readonly Vessel vessel;
		readonly PartResource resource;

		// Rate of boiling off in %/hr
		[Persistent] public float Rate = 0.025f;
		double fraction_per_second = 0.0;

		// Last timestamp that boiloff occurred
		[Persistent] public double LastUpdateTime;

		[Persistent] public bool BoiloffOccuring;

		public ResourceBoiloff(PartResource res)
		{
			resource = res;
			part = res.part;
			vessel = part.vessel;
		}

		public override void Load(ConfigNode node)
		{
			base.Load(node);
			fraction_per_second = Rate/100.0/3600.0;
		}

		public override string GetInfo()
		{
			return string.Format("Loss Rate: {0:F2}% {1} per hour", Rate, resource.resourceName);
		}

		public void Catchup()
		{
			if(vessel.missionTime > 0)
				Boiloff(vessel.missionTime - LastUpdateTime);
		}

		void Boiloff(double time, double scale = 1)
		{ resource.amount *= 1-Math.Pow(1-fraction_per_second, time)*scale; }

		void FixedUpdate()
		{
			if(!HighLogic.LoadedSceneIsFlight || resource.amount.Equals(0)) return;
			Boiloff(TimeWarp.fixedDeltaTime);
			if(vessel.missionTime > 0)
				LastUpdateTime = vessel.missionTime;
		}
	}
}


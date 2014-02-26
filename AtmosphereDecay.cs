using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NBody
{
	[KSPAddon(KSPAddon.Startup.Flight, true)]
	public class AtmosphereDecay : MonoBehaviour
	{
		public static AtmosphereDecay s_singleton = null;
		public static AtmosphereDecay GetInstance() { return s_singleton; }

		public static double sAirDensityThreshold = 0.000000001;
		public static double sAverageCd = 0.15;
		
		public void Awake()
		{
			Debug.Log("NBody Awake()");
			if (s_singleton == null)
				s_singleton = this;
			DontDestroyOnLoad(s_singleton);
		}

		bool activated = true;

		public void Update()
		{
			if (HighLogic.LoadedSceneIsFlight == false) return;
			if (FlightGlobals.fetch != null)
			{
				if (Input.GetKey(KeyCode.RightAlt) && Input.GetKeyDown(KeyCode.D))
				{
					activated = !activated;
					if (activated)
					{
						Debug.Log("Atmosphere Decoy Activated.");
					}
					else
					{
						Debug.Log("Atmosphere Decoy Deactivated.");
					}
				}
			}
		}

		public void FixedUpdate()
		{
			if (HighLogic.LoadedSceneIsFlight == false) return;
			if (FlightGlobals.fetch != null)
			{
				foreach (Vessel v in FlightGlobals.fetch.vessels)
				{
					if (FlightGlobals.fetch.activeVessel == v) continue;
					if (v.packed == true && v.LandedOrSplashed == false)
					{ 
						double airDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(v.GetWorldPos3D(), v.mainBody));
						if (airDensity >= sAirDensityThreshold && v.mainBody.atmosphere == true && v.altitude <= v.mainBody.maxAtmosphereAltitude)
						{
							Vector3d dragVector = -v.orbit.vel.normalized * (float)(0.5 * sAverageCd * airDensity * v.orbit.vel.sqrMagnitude / 1000.0);
							Debug.Log(v.orbit.vel.ToString() + " 0.5*" + sAverageCd.ToString() + "*" + (airDensity * v.orbit.vel.sqrMagnitude).ToString() + "/1000.0 = " + dragVector.ToString());
							
							Vector3d position = v.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
							Orbit orbit2 = new Orbit(v.orbit.inclination, v.orbit.eccentricity, v.orbit.semiMajorAxis, v.orbit.LAN, v.orbit.argumentOfPeriapsis, v.orbit.meanAnomalyAtEpoch, v.orbit.epoch, v.orbit.referenceBody);
							orbit2.UpdateFromStateVectors(position, v.orbit.vel + dragVector * TimeWarp.fixedDeltaTime, v.orbit.referenceBody, Planetarium.GetUniversalTime());

							if (!double.IsNaN(orbit2.inclination) && !double.IsNaN(orbit2.eccentricity) && !double.IsNaN(orbit2.semiMajorAxis)) // && orbit2.timeToAp > TimeWarp.fixedDeltaTime)
							{
								v.orbit.inclination = orbit2.inclination;
								v.orbit.eccentricity = orbit2.eccentricity;
								v.orbit.semiMajorAxis = orbit2.semiMajorAxis;
								v.orbit.LAN = orbit2.LAN;
								v.orbit.argumentOfPeriapsis = orbit2.argumentOfPeriapsis;
								v.orbit.meanAnomalyAtEpoch = orbit2.meanAnomalyAtEpoch;
								v.orbit.epoch = orbit2.epoch;
								v.orbit.referenceBody = orbit2.referenceBody;
								v.orbit.Init();

								//prevVessel.orbit.UpdateFromOrbitAtUT(orbit2, Planetarium.GetUniversalTime(), orbit2.referenceBody);
								v.orbit.UpdateFromUT(Planetarium.GetUniversalTime());
							}
						}
					}
				}

			}
		}
	}
}

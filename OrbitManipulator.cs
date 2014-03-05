using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NBody
{
	[KSPAddon(KSPAddon.Startup.Flight, true)]
	public class OrbitManipulator : MonoBehaviour
	{
		public static OrbitManipulator s_singleton = null;
		public static OrbitManipulator GetInstance() { return s_singleton; }

		public Dictionary<Vessel, Vector3d> manipulations = null;

		public void Awake()
		{
			Debug.Log("NBody Awake()");
			if (s_singleton == null)
				s_singleton = this;
			DontDestroyOnLoad(s_singleton);

			manipulations = new Dictionary<Vessel, Vector3d>();
		}

		public void AddManipulation(Vessel vessel, Vector3d acceleration)
		{
			if (manipulations.ContainsKey(vessel))
			{
				manipulations[vessel] = (manipulations[vessel] + acceleration);
			}
			else
			{
				manipulations[vessel] = acceleration;
			}
		}

		public void FixedUpdate()
		{
			foreach (KeyValuePair<Vessel, Vector3d> kv in manipulations)
			{
				Vessel vessel = kv.Key;
				Vector3d totalAccOnVessel = kv.Value;

				Vector3d position = vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
				Orbit orbit2 = new Orbit(vessel.orbit.inclination, vessel.orbit.eccentricity, vessel.orbit.semiMajorAxis, vessel.orbit.LAN, vessel.orbit.argumentOfPeriapsis, vessel.orbit.meanAnomalyAtEpoch, vessel.orbit.epoch, vessel.orbit.referenceBody);
				orbit2.UpdateFromStateVectors(position, vessel.orbit.vel + new Vector3d(totalAccOnVessel.x, totalAccOnVessel.z, totalAccOnVessel.y) * TimeWarp.fixedDeltaTime, vessel.orbit.referenceBody, Planetarium.GetUniversalTime());

				if (!double.IsNaN(orbit2.inclination) && !double.IsNaN(orbit2.eccentricity) && !double.IsNaN(orbit2.semiMajorAxis)) // && orbit2.timeToAp > TimeWarp.fixedDeltaTime)
				{
					vessel.orbit.inclination = orbit2.inclination;
					vessel.orbit.eccentricity = orbit2.eccentricity;
					vessel.orbit.semiMajorAxis = orbit2.semiMajorAxis;
					vessel.orbit.LAN = orbit2.LAN;
					vessel.orbit.argumentOfPeriapsis = orbit2.argumentOfPeriapsis;
					vessel.orbit.meanAnomalyAtEpoch = orbit2.meanAnomalyAtEpoch;
					vessel.orbit.epoch = orbit2.epoch;
					vessel.orbit.referenceBody = orbit2.referenceBody;
					vessel.orbit.Init();

					//prevVessel.orbit.UpdateFromOrbitAtUT(orbit2, Planetarium.GetUniversalTime(), orbit2.referenceBody);
					vessel.orbit.UpdateFromUT(Planetarium.GetUniversalTime());
				}
			}
			manipulations.Clear();
		}
	}
}

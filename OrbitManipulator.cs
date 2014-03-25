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

		public void Start()
		{
			ConfigNode settingNode = null;
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("ORBITMANIPULATORSETTINGS"))
			{
				settingNode = node;
				break;
			}
			if (settingNode != null)
			{
				if (settingNode.HasValue("AtmosphereDecay"))
				{
					bool atmosphereDecayActivated;
					if (bool.TryParse(settingNode.GetValue("AtmosphereDecay"), out atmosphereDecayActivated))
					{
						if (AtmosphereDecay.s_singleton != null)
						{
							AtmosphereDecay.s_singleton.activated = atmosphereDecayActivated;
							AtmosphereDecay.s_singleton.btnAtmosphereDecay.TexturePath = atmosphereDecayActivated ? "NBody/Textures/AtmosDecayOn" : "NBody/Textures/AtmosDecayOff";
						}
					}
				}
				if (settingNode.HasValue("NBodyForce"))
				{
					bool nBodyForceActivated;
					if (bool.TryParse(settingNode.GetValue("NBodyForce"), out nBodyForceActivated))
					{
						if (NBody.s_singleton != null)
						{
							NBody.s_singleton.forceApplying = nBodyForceActivated;
							NBody.s_singleton.btnNBodyForce.TexturePath = nBodyForceActivated ? "NBody/Textures/NBodyOn" : "NBody/Textures/NBodyOff";
						}
					}
				}
				if (settingNode.HasValue("WarpableEngineList"))
				{
					bool warpableEngineListActivated;
					if (bool.TryParse(settingNode.GetValue("WarpableEngineList"), out warpableEngineListActivated))
					{
						if (WarpableEngineThrottleGUI.s_singleton != null)
						{
							WarpableEngineThrottleGUI.s_singleton.activated = warpableEngineListActivated;
							WarpableEngineThrottleGUI.s_singleton.btnWarpableEngineList.TexturePath = warpableEngineListActivated ? "NBody/Textures/WarpableEngineListOn" : "NBody/Textures/WarpableEngineListOff";
						}
					}
				}
				if (settingNode.HasValue("KillThrottleWhenExitingTimeWarp"))
				{
					bool killThrottleWhenExitingTimeWarp;
					if (bool.TryParse(settingNode.GetValue("KillThrottleWhenExitingTimeWarp"), out killThrottleWhenExitingTimeWarp))
					{
						if (WarpableEngineThrottleGUI.s_singleton != null)
						{
							WarpableEngineThrottleGUI.s_singleton.killThrottleWhenExitingTimeWarp = killThrottleWhenExitingTimeWarp;
						}
					}
				}
			}
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

		public void SaveConfigs() 
		{
			ConfigNode settingNode = null;
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("ORBITMANIPULATORSETTINGS"))
			{
				settingNode = node;
				break;
			}
			if (settingNode != null)
			{
				settingNode.SetValue("AtmosphereDecay", AtmosphereDecay.s_singleton.activated.ToString());
				settingNode.SetValue("NBodyForce", NBody.s_singleton.forceApplying.ToString());
				settingNode.SetValue("WarpableEngineList", WarpableEngineThrottleGUI.s_singleton.activated.ToString());
				settingNode.SetValue("KillThrottleWhenExitingTimeWarp", WarpableEngineThrottleGUI.s_singleton.killThrottleWhenExitingTimeWarp.ToString()); 
				ConfigNode saveNode = new ConfigNode();
				saveNode.AddNode(settingNode);
				saveNode.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/NBody/Settings.cfg");
			}
		}

		public void FixedUpdate()
		{
			foreach (KeyValuePair<Vessel, Vector3d> kv in manipulations)
			{
				Vessel vessel = kv.Key;
				Vector3d totalAccOnVessel = kv.Value;
				if ((totalAccOnVessel * TimeWarp.fixedDeltaTime).magnitude >= float.Epsilon)
				{
					Vector3d position = vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
					Orbit orbit2 = new Orbit(vessel.orbit.inclination, vessel.orbit.eccentricity, vessel.orbit.semiMajorAxis, vessel.orbit.LAN, vessel.orbit.argumentOfPeriapsis, vessel.orbit.meanAnomalyAtEpoch, vessel.orbit.epoch, vessel.orbit.referenceBody);
					orbit2.UpdateFromStateVectors(position, vessel.orbit.vel + totalAccOnVessel * TimeWarp.fixedDeltaTime, vessel.orbit.referenceBody, Planetarium.GetUniversalTime());

					if (!double.IsNaN(orbit2.inclination) && !double.IsNaN(orbit2.eccentricity) && !double.IsNaN(orbit2.semiMajorAxis))
					{
						if (double.IsNaN(orbit2.timeToAp) || (orbit2.timeToAp > TimeWarp.fixedDeltaTime * 10.0f && orbit2.timeToAp < orbit2.period - TimeWarp.fixedDeltaTime * 10.0f))
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
			}
			manipulations.Clear();
		}
	}
}

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

		public IButton btnAtmosphereDecay = null;

		public void Awake()
		{
			Debug.Log("NBody Awake()");
			if (s_singleton == null)
				s_singleton = this;
			DontDestroyOnLoad(s_singleton);

			if (ToolbarManager.ToolbarAvailable)
			{
				btnAtmosphereDecay = ToolbarManager.Instance.add("NBody", "atmospheredecay");
				btnAtmosphereDecay.TexturePath = "NBody/Textures/AtmosDecayOn";
				btnAtmosphereDecay.ToolTip = "Atmosphere Decay";
				btnAtmosphereDecay.OnClick += (e) =>
				{
					activated = !activated;
					btnAtmosphereDecay.TexturePath = activated ? "NBody/Textures/AtmosDecayOn" : "NBody/Textures/AtmosDecayOff";
					OrbitManipulator.s_singleton.SaveConfigs();
				};
			}
		}

		public void OnDestroy()
		{
			if(btnAtmosphereDecay != null)
				btnAtmosphereDecay.Destroy();
		}

		public bool activated = true;

		public void Update()
		{
			if (HighLogic.LoadedSceneIsFlight == false) return;
			if (FlightGlobals.fetch != null)
			{
				if (Input.GetKey(KeyCode.RightAlt) && Input.GetKeyDown(KeyCode.D))
				{
					activated = !activated;
					btnAtmosphereDecay.TexturePath = activated ? "NBody/Textures/AtmosDecayOn" : "NBody/Textures/AtmosDecayOff";
					OrbitManipulator.s_singleton.SaveConfigs(); 
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
							//Debug.Log(v.orbit.vel.ToString() + " 0.5*" + sAverageCd.ToString() + "*" + (airDensity * v.orbit.vel.sqrMagnitude).ToString() + "/1000.0 = " + dragVector.ToString());

							if (OrbitManipulator.s_singleton != null)
								OrbitManipulator.s_singleton.AddManipulation(v, dragVector);

						}
					}
				}

			}
		}
	}
}

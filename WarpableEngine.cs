using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace NBody
{
	public class WarpableEngine : PartModule
	{
		// This is a class used to allow low-thrust engines to work during time-warp.
		[KSPField(isPersistant = true, guiName = "Throttle", guiActive = true, guiActiveEditor = false),
		UI_FloatRange(controlEnabled = true, maxValue = 100, minValue = 0, scene = UI_Scene.Flight, stepIncrement = 5)]
		public float throttle = 100f;

		[KSPField(isPersistant = false)]
		public int engineIndex = 0;

		private ModuleEngines engine = null;
		private ModuleEnginesFX engineFX = null;
		private bool isEngineFX = false;

		bool lastUpdateIsWarping = false;

		public override void OnStart(StartState state)
		{
			BindEngine();
		}

		private void BindEngine()
		{ 
			engine = null;
			engineFX = null;
			int index = 0;
			foreach(PartModule pm in part.Modules)
			{
				if (pm is ModuleEngines)
				{
					if (index == engineIndex)
					{
						engine = pm as ModuleEngines;
						isEngineFX = false;
						return;
					}
					else
						index++;
				}
				if (pm is ModuleEnginesFX)
				{
					if (index == engineIndex)
					{
						engineFX = pm as ModuleEnginesFX;
						isEngineFX = true;
						return;
					}
					else
						index++;
				}
			}
		}

		void FixedUpdate()
		{
			if (HighLogic.LoadedSceneIsFlight == false) return;
			if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)
			{
				// Warping.
				// Need to calculate resource consumption, calculate thrust and finally apply the acceleration.

				Vector3d finalAcc = new Vector3d();
				if (isEngineFX == false && engine != null)
				{
					engine.requestedThrottle = engine.currentThrottle = throttle / 100.0f;
					engine.GetType().GetMethod("UpdatePropellantStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(engine, null);
					engine.finalThrust = engine.CalculateThrust();

					try
					{
						if (engine.flameout)
						{
							engine.DeactivatePowerFX();
							engine.DeactivateRunningFX();
							engineFX.finalThrust = 0.0f;
						}
						else
						{
							engine.ActivatePowerFX();
							engine.ActivateRunningFX();
							engine.part.findFxGroup("power").SetPower(engineFX.finalThrust / engineFX.maxThrust);
							engine.part.findFxGroup("running").SetPower(engineFX.finalThrust / engineFX.maxThrust);
							if (engine.part.Modules.Contains("FXModuleAnimateThrottle"))
							{
								FXModuleAnimateThrottle m = engine.part.Modules["FXModuleAnimateThrottle"] as FXModuleAnimateThrottle;
								engine.EngineIgnited = true;
								float origThrottle = vessel.ctrlState.mainThrottle;
								vessel.ctrlState.mainThrottle = throttle / 100.0f;
								m.OnUpdate();
								vessel.ctrlState.mainThrottle = origThrottle;
							}
						}
					}
					catch (Exception e)
					{ }

					Vector3d averageThrustVector = new Vector3d();
					foreach (Transform tf in engine.thrustTransforms)
						averageThrustVector += tf.forward * -1;
					averageThrustVector /= engine.thrustTransforms.Count;
					finalAcc = averageThrustVector.normalized * engine.finalThrust / vessel.GetTotalMass();
				}
				else if (isEngineFX == true && engineFX != null)
				{
					engineFX.requestedThrottle = engineFX.currentThrottle = throttle / 100.0f;
					engineFX.GetType().GetMethod("UpdatePropellantStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(engineFX, null);
					engineFX.finalThrust = engineFX.CalculateThrust();
					try
					{
						if (engineFX.flameout)
						{
							engineFX.part.Effect(engineFX.powerEffectName, 0.0f);
							engineFX.part.Effect(engineFX.runningEffectName, 0.0f);
							engineFX.finalThrust = 0.0f;
						}
						else
						{
							engineFX.part.Effect(engineFX.powerEffectName, engineFX.finalThrust / engineFX.maxThrust);
							engineFX.part.Effect(engineFX.runningEffectName, engineFX.finalThrust / engineFX.maxThrust);
							if (engine.part.Modules.Contains("FXModuleAnimateThrottle"))
							{
								FXModuleAnimateThrottle m = engine.part.Modules["FXModuleAnimateThrottle"] as FXModuleAnimateThrottle;
								engine.EngineIgnited = true; 
								float origThrottle = vessel.ctrlState.mainThrottle;
								vessel.ctrlState.mainThrottle = throttle / 100.0f;
								m.OnUpdate();
								vessel.ctrlState.mainThrottle = origThrottle;
							}
						}
					}
					catch (Exception e)
					{ }

					Vector3d averageThrustVector = new Vector3d();
					foreach (Transform tf in engine.thrustTransforms)
						averageThrustVector += tf.forward * -1;
					averageThrustVector /= engine.thrustTransforms.Count;
					finalAcc = averageThrustVector.normalized * engineFX.finalThrust / vessel.GetTotalMass();
				}

				if (finalAcc.magnitude > 0.001)
				{
					Vector3d position = vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
					Orbit orbit2 = new Orbit(vessel.orbit.inclination, vessel.orbit.eccentricity, vessel.orbit.semiMajorAxis, vessel.orbit.LAN, vessel.orbit.argumentOfPeriapsis, vessel.orbit.meanAnomalyAtEpoch, vessel.orbit.epoch, vessel.orbit.referenceBody);
					orbit2.UpdateFromStateVectors(position, vessel.orbit.vel + new Vector3d(finalAcc.x, finalAcc.z, finalAcc.y) * TimeWarp.fixedDeltaTime, vessel.orbit.referenceBody, Planetarium.GetUniversalTime());

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

				lastUpdateIsWarping = true;
			}
			else
			{
				// Reset the throttle after exiting the warp.
				if (lastUpdateIsWarping == true)
					throttle = 0f;
				lastUpdateIsWarping = false;
				engine.DeactivatePowerFX();
			}
		}
	}
}

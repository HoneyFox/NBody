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
		UI_FloatRange(controlEnabled = true, maxValue = 100, minValue = 0, scene = UI_Scene.Flight, stepIncrement = 1)]
		public float throttle = 0f;

		[KSPField(isPersistant = false)]
		public int engineIndex = 0;

		private ModuleEngines engine = null;
		private ModuleEnginesFX engineFX = null;
		private bool isEngineFX = false;

		bool lastUpdateIsWarping = false;
		float lastThrottle = 0f;

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

		static Vector3d FixedUpdateFor(WarpableEngine warpableEngine, float throttle)
		{
			// Need to calculate resource consumption, calculate thrust and finally apply the acceleration.
			Vector3d finalAcc = new Vector3d();
			if (warpableEngine.isEngineFX == false && warpableEngine.engine != null)
			{
				warpableEngine.engine.requestedThrottle = warpableEngine.engine.currentThrottle = throttle / 100.0f;
				warpableEngine.engine.GetType().GetMethod("UpdatePropellantStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(warpableEngine.engine, null);
				warpableEngine.engine.finalThrust = warpableEngine.engine.CalculateThrust();

				try
				{
					if (warpableEngine.engine.flameout || warpableEngine.engine.engineShutdown)
					{
						warpableEngine.engine.DeactivatePowerFX();
						warpableEngine.engine.DeactivateRunningFX();
						warpableEngine.engine.finalThrust = 0.0f;
					}
					else
					{
						warpableEngine.engine.ActivatePowerFX();
						warpableEngine.engine.ActivateRunningFX();
						warpableEngine.engine.part.findFxGroup("power").SetPower(warpableEngine.engine.finalThrust / warpableEngine.engine.maxThrust);
						warpableEngine.engine.part.findFxGroup("running").SetPower(warpableEngine.engine.finalThrust / warpableEngine.engine.maxThrust);
						if (warpableEngine.engine.part.Modules.Contains("FXModuleAnimateThrottle"))
						{
							FXModuleAnimateThrottle m = warpableEngine.engine.part.Modules["FXModuleAnimateThrottle"] as FXModuleAnimateThrottle;
							//m.isEnabled = false;
							if (m.animation.IsPlaying(m.animationName) == false)
								m.animation.Play(m.animationName);
							m.animation[m.animationName].normalizedTime = throttle;
							m.gameObject.SampleAnimation(m.animation[m.animationName].clip, throttle / m.animation[m.animationName].clip.length);
						}
					}
				}
				catch (Exception e)
				{ }

				Vector3d averageThrustVector = new Vector3d();
				foreach (Transform tf in warpableEngine.engine.thrustTransforms)
					averageThrustVector += tf.forward * -1;
				averageThrustVector /= warpableEngine.engine.thrustTransforms.Count;
				finalAcc = averageThrustVector.normalized * warpableEngine.engine.finalThrust / warpableEngine.vessel.GetTotalMass();
				return finalAcc;
			}
			else if (warpableEngine.isEngineFX == true && warpableEngine.engineFX != null)
			{
				warpableEngine.engineFX.requestedThrottle = warpableEngine.engineFX.currentThrottle = throttle / 100.0f;
				warpableEngine.engineFX.GetType().GetMethod("UpdatePropellantStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(warpableEngine.engineFX, null);
				warpableEngine.engineFX.finalThrust = warpableEngine.engineFX.CalculateThrust();
				try
				{
					if (warpableEngine.engineFX.flameout || warpableEngine.engineFX.engineShutdown)
					{
						warpableEngine.engineFX.part.Effect(warpableEngine.engineFX.powerEffectName, 0.0f);
						warpableEngine.engineFX.part.Effect(warpableEngine.engineFX.runningEffectName, 0.0f);
						warpableEngine.engineFX.finalThrust = 0.0f;
					}
					else
					{
						warpableEngine.engineFX.part.Effect(warpableEngine.engineFX.powerEffectName, warpableEngine.engineFX.finalThrust / warpableEngine.engineFX.maxThrust);
						warpableEngine.engineFX.part.Effect(warpableEngine.engineFX.runningEffectName, warpableEngine.engineFX.finalThrust / warpableEngine.engineFX.maxThrust);
						if (warpableEngine.engineFX.part.Modules.Contains("FXModuleAnimateThrottle"))
						{
							FXModuleAnimateThrottle m = warpableEngine.engineFX.part.Modules["FXModuleAnimateThrottle"] as FXModuleAnimateThrottle;
							//m.isEnabled = false;
							if (m.animation.IsPlaying(m.animationName) == false)
								m.animation.Play(m.animationName);
							m.animation[m.animationName].normalizedTime = throttle;
							m.gameObject.SampleAnimation(m.animation[m.animationName].clip, throttle / m.animation[m.animationName].clip.length);
						}
					}
				}
				catch (Exception e)
				{ }

				Vector3d averageThrustVector = new Vector3d();
				foreach (Transform tf in warpableEngine.engineFX.thrustTransforms)
					averageThrustVector += tf.forward * -1;
				averageThrustVector /= warpableEngine.engineFX.thrustTransforms.Count;
				finalAcc = averageThrustVector.normalized * warpableEngine.engineFX.finalThrust / warpableEngine.vessel.GetTotalMass();
				return finalAcc;
			}
			else
			{
				return Vector3d.zero;
			}
		}

		void FixedUpdate()
		{
			if (HighLogic.LoadedSceneIsFlight == false) return;
			if (throttle != lastThrottle)
			{
				foreach(Part p in this.part.symmetryCounterparts)
				{
					if (p == this.part) continue;
					if (p.Modules.Contains("WarpableEngine"))
					{
						WarpableEngine we = p.Modules["WarpableEngine"] as WarpableEngine;
						we.lastThrottle = we.throttle = throttle;
					}
				}
				lastThrottle = throttle; 
			}

			if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)
			{
				// Warping.
				Vector3d totalAccOnVessel = WarpableEngine.FixedUpdateFor(this, this.throttle);
				if (totalAccOnVessel.magnitude > 0.0001)
				{
					if (OrbitManipulator.s_singleton != null)
						OrbitManipulator.s_singleton.AddManipulation(vessel, new Vector3d(totalAccOnVessel.x, totalAccOnVessel.z, totalAccOnVessel.y));
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

				//if(isEngineFX == false)
				//{
				//    if (engine.part.Modules.Contains("FXModuleAnimateThrottle"))
				//    {
				//        FXModuleAnimateThrottle m = engine.part.Modules["FXModuleAnimateThrottle"] as FXModuleAnimateThrottle;
				//        m.isEnabled = true;
				//    }
				//}
				//else
				//{
				//    if (engineFX.part.Modules.Contains("FXModuleAnimateThrottle"))
				//    {
				//        FXModuleAnimateThrottle m = engineFX.part.Modules["FXModuleAnimateThrottle"] as FXModuleAnimateThrottle;
				//        m.isEnabled = true;
				//    }
				//}
			}
		}
	}
}

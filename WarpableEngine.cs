using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace NBody
{
	[KSPAddon(KSPAddon.Startup.Flight, true)]
	public class WarpableEngineThrottleGUI : MonoBehaviour
	{
		public static WarpableEngineThrottleGUI s_singleton = null;
		public static WarpableEngineThrottleGUI GetInstance() { return s_singleton; }
		
		public IButton btnWarpableEngineList = null;
		public bool activated = true;

		public bool killThrottleWhenExitingTimeWarp = true;
		
		private Rect windowRect = new Rect(60, 120, 200, 50);
		private List<WarpableEngine> engines = new List<WarpableEngine>();

		public void Awake()
		{
			Debug.Log("NBody Awake()");
			if (s_singleton == null)
				s_singleton = this;
			DontDestroyOnLoad(s_singleton);

			if (ToolbarManager.ToolbarAvailable)
			{
				btnWarpableEngineList = ToolbarManager.Instance.add("NBody", "warpableenginelist");
				btnWarpableEngineList.TexturePath = "NBody/Textures/WarpableEngineListOn";
				btnWarpableEngineList.ToolTip = "WarpableEngine List";
				btnWarpableEngineList.OnClick += (e) =>
				{
					activated = !activated;
					btnWarpableEngineList.TexturePath = activated ? "NBody/Textures/WarpableEngineListOn" : "NBody/Textures/WarpableEngineListOff";
					OrbitManipulator.s_singleton.SaveConfigs();
				};
			}

			engines.Clear();
		}

		public void OnDestroy()
		{
			if (btnWarpableEngineList != null)
				btnWarpableEngineList.Destroy();
			engines.Clear();
		}
		
		void OnGUI()
		{
			if (HighLogic.LoadedSceneIsFlight && activated && engines.Count > 0)
			{
				GUI.skin = HighLogic.Skin;
				windowRect = GUILayout.Window(22113141, windowRect, DrawGUI, "Warpable Engines", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			}
		}

		void Update()
		{
			if (InputLockManager.IsLocked(ControlTypes.THROTTLE))
			{
				List<string> locksToBeRemoved = new List<string>();
				foreach (KeyValuePair<string, ulong> kv in InputLockManager.lockStack)
				{
					//Debug.Log(kv.Key + ": " + Convert.ToString((long)kv.Value, 2));
					//if ((kv.Value & (uint)(ControlTypes.THROTTLE)) != 0)
					//	locksToBeRemoved.Add(kv.Key);
				}
				locksToBeRemoved.Add("TimeWarpLock");
				
				foreach(string lockName in locksToBeRemoved)
					InputLockManager.RemoveControlLock(lockName);
			}
			
			if(FlightGlobals.fetch != null && FlightGlobals.fetch.activeVessel != null)
			{
				foreach (Part p in FlightGlobals.fetch.activeVessel.Parts)
				{
					if (p.Modules.Contains("WarpableEngine"))
					{
						WarpableEngine we = p.Modules["WarpableEngine"] as WarpableEngine;
						if (!engines.Contains(we))
						{
							engines.Add(we);
						}
					}
				}
				for (int i = 0; i < engines.Count; ++i)
				{
					if (engines[i] == null || engines[i].vessel != FlightGlobals.fetch.activeVessel)
					{
						engines.RemoveAt(i);
						i--;
					}
				}
			}
		}

		private void DrawGUI(int windowId)
		{
			GUILayout.BeginVertical();
			bool orgKillThrottleWhenExitingTimeWarp = killThrottleWhenExitingTimeWarp;
			killThrottleWhenExitingTimeWarp = GUILayout.Toggle(killThrottleWhenExitingTimeWarp, "Auto Cut-Off", GUI.skin.button);
			if (killThrottleWhenExitingTimeWarp != orgKillThrottleWhenExitingTimeWarp)
				OrbitManipulator.s_singleton.SaveConfigs();
			GUILayout.BeginHorizontal();
			bool killThrottle = GUILayout.Button(" Cut-Off ");
			bool fullThrottle = GUILayout.Button("MaxThrust");
			GUILayout.EndHorizontal();
			foreach (WarpableEngine we in engines)
			{
				string title = we.part.partInfo.title;
				if (title.Length >= 33)
					title = title.Substring(0, 29) + "...";
				GUILayout.Label(title, GUILayout.MaxWidth(200));
				we.throttle = Mathf.RoundToInt(GUILayout.HorizontalSlider(we.throttle, 0.0f, 100.0f, GUILayout.Width(200)));

				if (killThrottle) we.throttle = 0.0f;
				if (fullThrottle) we.throttle = 100.0f;
			}
			
			GUILayout.EndVertical();
			GUI.DragWindow(new Rect(0, 0, 200, 20));
		}
	}

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
				if (lastUpdateIsWarping == true && WarpableEngineThrottleGUI.s_singleton.killThrottleWhenExitingTimeWarp)
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NBody
{
	[KSPAddon(KSPAddon.Startup.Flight, true)]
	public class NBody : MonoBehaviour
	{
		public static NBody s_singleton = null;
		public static GameObject GameObjectInstance; 
		public static NBody GetInstance() { return CreateInstance(); }
		private static NBody CreateInstance()
		{
			if (GameObjectInstance == null)
			{
				GameObjectInstance = new GameObject("NBodyInstance", typeof(NBody));
				UnityEngine.Object.DontDestroyOnLoad(GameObjectInstance);
				s_singleton = GameObjectInstance.GetComponent<NBody>();
			}
			return s_singleton;
		}

		public IButton btnNBodyForce = null;

		public void Awake()
		{
			Debug.Log("NBody Awake()");
			if (s_singleton == null)
				s_singleton = this;
			DontDestroyOnLoad(s_singleton);

			if (ToolbarManager.ToolbarAvailable)
			{
				btnNBodyForce = ToolbarManager.Instance.add("NBody", "force");
				btnNBodyForce.TexturePath = "NBody/Textures/NBodyOn";
				btnNBodyForce.ToolTip = "NBody";
				btnNBodyForce.OnClick += (e) =>
				{
					forceApplying = !forceApplying;
					btnNBodyForce.TexturePath = forceApplying ? "NBody/Textures/NBodyOn" : "NBody/Textures/NBodyOff";
					OrbitManipulator.s_singleton.SaveConfigs();
				};
			}
		}

		public void OnDestroy()
		{
			if(btnNBodyForce != null)
				btnNBodyForce.Destroy();
		}

		Vessel prevVessel = null;
		public bool activated = false;
		public bool forceApplying = true;
		float timeAccumulated = 0.0f;

		public void Update()
		{
			if (HighLogic.LoadedSceneIsFlight == false) return;
			if (FlightGlobals.fetch != null)
			{
				if (Input.GetKey(KeyCode.RightAlt) && Input.GetKey(KeyCode.RightControl) && Input.GetKeyDown(KeyCode.N))
				{
					forceApplying = !forceApplying;
					btnNBodyForce.TexturePath = forceApplying ? "NBody/Textures/NBodyOn" : "NBody/Textures/NBodyOff";
					OrbitManipulator.s_singleton.SaveConfigs(); 
					if (forceApplying)
					{
						Debug.Log("NBody Simulation Force Activated.");
					}
					else
					{
						Debug.Log("NBody Simulation Force Deactivated.");
					}
				}

				if (Input.GetKey(KeyCode.RightAlt) && Input.GetKeyDown(KeyCode.N))
				{
					activated = !activated;
					if (activated)
					{
						Debug.Log("NBody Simulation Prediction Activated.");
					}
					else
					{
						ResetIteration(prevVessel);
						Debug.Log("NBody Simulation Prediction Deactivated.");
					}
				}

				if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.N))
				{
					ResetIteration(prevVessel);
				}
			}
		}

		public void FixedUpdate()
		{
			if (FlightGlobals.fetch != null && FlightGlobals.fetch.activeVessel != null)
			{
				if (FlightGlobals.fetch.activeVessel != prevVessel)
				{
					prevVessel = FlightGlobals.fetch.activeVessel;
					ResetIteration(prevVessel);
				}
				
				if (FlightGlobals.fetch.activeVessel != null)
				{
					List<CelestialBody> refBodies = new List<CelestialBody>();
					refBodies.Add(FlightGlobals.fetch.activeVessel.mainBody);
					do
					{
						refBodies.Add(refBodies[refBodies.Count - 1].referenceBody);
					}
					while (refBodies[refBodies.Count - 1].referenceBody.bodyName != "Sun");
	
					foreach (Part part in FlightGlobals.fetch.activeVessel.parts)
					{
						if (part.physicalSignificance == Part.PhysicalSignificance.FULL && part.rigidbody != null)
						{
							Vector3d force = new Vector3d(0.0, 0.0, 0.0);
							foreach (CelestialBody cb in FlightGlobals.fetch.bodies)
							{
								if (!refBodies.Contains(cb)) //(cb != FlightGlobals.fetch.activeVessel.mainBody)
								{
									List<CelestialBody> parentBodies = new List<CelestialBody>();
									parentBodies.Add(cb);
									do
									{
										parentBodies.Add(parentBodies[parentBodies.Count - 1].referenceBody);
									}
									while (parentBodies[parentBodies.Count - 1].referenceBody.bodyName != "Sun");
									if (parentBodies.Contains(FlightGlobals.fetch.activeVessel.mainBody))
									{
										Vector3d locationVector = cb.position - part.transform.position;
										force += locationVector.normalized * cb.gravParameter / locationVector.sqrMagnitude;
									}
								}
								else
								{
									if(cb == prevVessel.mainBody && cb.referenceBody != null && cb.referenceBody != cb)
									{
										Vector3d locationVector;

										locationVector = cb.referenceBody.position - part.transform.position;
										Vector3d forceTopBodyToVessel = locationVector.normalized * cb.referenceBody.gravParameter / locationVector.sqrMagnitude;
										locationVector = cb.referenceBody.position - cb.position;
										Vector3d forceTopBodyToMainBody = locationVector.normalized * cb.referenceBody.gravParameter / locationVector.sqrMagnitude;
										
										//if((forceTopBodyToVessel - forceTopBodyToMainBody).magnitude > 0.0000001f)
										//	Debug.Log("AccDiff: " + (forceTopBodyToVessel - forceTopBodyToMainBody).ToString()); 
										force += (forceTopBodyToVessel - forceTopBodyToMainBody);
									}
								}
							}
							//if (FlightGlobals.fetch.vessels != null)
							//{
							//    foreach (Vessel vs in FlightGlobals.fetch.vessels)
							//    {
							//        if (vs != FlightGlobals.fetch.activeVessel)
							//        {
							//            Vector3d locationVector = GetVesselAbsolutePosition(vs, Planetarium.GetUniversalTime()) - part.transform.position;
							//            force += locationVector.normalized * vs.GetTotalMass() * gravConst / locationVector.sqrMagnitude;
							//        }
							//    }
							//}
							if (forceApplying)
							{
								if (!(TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate))
								{
									if (Input.GetKey(KeyCode.Slash))
										part.rigidbody.AddForceAtPosition(force * part.mass * 10000.0f, part.transform.TransformPoint(part.CoMOffset), ForceMode.Force);
									else if (Input.GetKey(KeyCode.Backslash))
										part.rigidbody.AddForceAtPosition(force * part.mass * 100.0f, part.transform.TransformPoint(part.CoMOffset), ForceMode.Force);
									else
										part.rigidbody.AddForceAtPosition(force * part.mass, part.transform.TransformPoint(part.CoMOffset), ForceMode.Force);
								}
								else
								{
									if (part == prevVessel.rootPart)
									{
										Vector3d actualForce = new Vector3d(force.x, force.z, force.y);
										float times = 1.0f;
										if (Input.GetKey(KeyCode.Slash))
											times = 10000.0f;
										else if (Input.GetKey(KeyCode.Backslash))
											times = 100.0f;

										if ((timeAccumulated * actualForce * times).magnitude >= 0.001f)
										{
											//Debug.Log("NBody: acc/pos/vel: " + actualForce.ToString() + " " + prevVessel.orbit.pos.ToString() + " " + prevVessel.orbit.vel.ToString());

											if (OrbitManipulator.s_singleton != null)
												OrbitManipulator.s_singleton.AddManipulation(prevVessel, actualForce * timeAccumulated * times / TimeWarp.fixedDeltaTime);
											timeAccumulated = 0.0f;
										}
										else
										{
											timeAccumulated += TimeWarp.fixedDeltaTime;
										}
									}
								}
							}
						}
					}

					if (activated == false)
					{
						predictionLineRenderer.enabled = false;
						coordXRenderer.enabled = false;
						coordYRenderer.enabled = false;
						coordZRenderer.enabled = false; 
						return;
					}

					UpdatePredictedLine();
				}
			}
			else 
			{
				prevVessel = null;
			}
		}

		LineRenderer predictionLineRenderer = null;
		GameObject predictionLineObj = null;
		LineRenderer coordXRenderer = null;
		GameObject coordXObj = null;
		LineRenderer coordYRenderer = null;
		GameObject coordYObj = null;
		LineRenderer coordZRenderer = null;
		GameObject coordZObj = null;

		double gravConst;

		public void InitializePredictedLine()
		{
			predictionLineObj = new GameObject("PredictionLineObj");
			coordXObj = new GameObject("coordXObj");
			coordYObj = new GameObject("coordYObj");
			coordZObj = new GameObject("coordZObj");

			predictionLineObj.layer = 9;
			predictionLineRenderer = predictionLineObj.AddComponent<LineRenderer>();
			predictionLineRenderer.transform.parent = null;
			predictionLineRenderer.useWorldSpace = true;
			predictionLineRenderer.material = new Material(Shader.Find("Particles/Additive"));
			predictionLineRenderer.SetColors(Color.cyan, Color.white);

			
			coordXObj.layer = 9;
			coordXRenderer = coordXObj.AddComponent<LineRenderer>();
			coordXRenderer.transform.parent = null;
			coordXRenderer.useWorldSpace = true;
			coordXRenderer.material = new Material(Shader.Find("Particles/Additive"));
			coordXRenderer.SetColors(Color.white, Color.red);
			
			coordYObj.layer = 9;
			coordYRenderer = coordYObj.AddComponent<LineRenderer>();
			coordYRenderer.transform.parent = null;
			coordYRenderer.useWorldSpace = true;
			coordYRenderer.material = new Material(Shader.Find("Particles/Additive"));
			coordYRenderer.SetColors(Color.white, Color.green);
			
			coordZObj.layer = 9;
			coordZRenderer = coordZObj.AddComponent<LineRenderer>();
			coordZRenderer.transform.parent = null;
			coordZRenderer.useWorldSpace = true;
			coordZRenderer.material = new Material(Shader.Find("Particles/Additive"));
			coordZRenderer.SetColors(Color.white, Color.blue);
			
		}

		public void UpdatePredictedLine()
		{
			for(int i = 0; i < timesPerUpdate; i++)
				Iterate(timeStep);

			if (predictionLineRenderer == null)
				InitializePredictedLine();

			if (MapView.MapIsEnabled)
			{
				MapView mapCamera = (MapView)GameObject.FindObjectOfType(typeof(MapView));
				try
				{
					predictionLineRenderer.SetVertexCount(prevRefPositions.Count);
					predictionLineRenderer.enabled = true;
					predictionLineRenderer.useWorldSpace = true;
					predictionLineRenderer.SetWidth(0.0015f * MapView.MapCamera.Distance, 0.0015f * MapView.MapCamera.Distance);
					for (int i = 0; i < prevRefPositions.Count; i++)
					{
						predictionLineRenderer.SetPosition(i, ScaledSpace.LocalToScaledSpace(prevRefPositions[i] + prevVessel.mainBody.getPositionAtUT(Planetarium.GetUniversalTime())));
					}

					coordXRenderer.enabled = true;
					coordXRenderer.SetVertexCount(2);
					coordXRenderer.SetWidth(0.002f * MapView.MapCamera.Distance, 0.002f * MapView.MapCamera.Distance);
					coordXRenderer.SetPosition(0, ScaledSpace.LocalToScaledSpace(Vector3.zero));
					coordXRenderer.SetPosition(1, ScaledSpace.LocalToScaledSpace(new Vector3(1000000.0f, 0.0f, 0.0f)));

					coordYRenderer.enabled = true;
					coordYRenderer.SetVertexCount(2);
					coordYRenderer.SetWidth(0.002f * MapView.MapCamera.Distance, 0.002f * MapView.MapCamera.Distance);
					coordYRenderer.SetPosition(0, ScaledSpace.LocalToScaledSpace(Vector3.zero));
					coordYRenderer.SetPosition(1, ScaledSpace.LocalToScaledSpace(new Vector3(0.0f, 1000000.0f, 0.0f)));

					coordZRenderer.enabled = true;
					coordZRenderer.SetVertexCount(2);
					coordZRenderer.SetWidth(0.002f * MapView.MapCamera.Distance, 0.002f * MapView.MapCamera.Distance);
					coordZRenderer.SetPosition(0, ScaledSpace.LocalToScaledSpace(Vector3.zero));
					coordZRenderer.SetPosition(1, ScaledSpace.LocalToScaledSpace(new Vector3(0.0f, 0.0f, 1000000.0f)));
				}
				catch
				{
				}
			}
			else
			{
				predictionLineRenderer.enabled = false;

				coordXRenderer.enabled = false;
				coordYRenderer.enabled = false;
				coordZRenderer.enabled = false;
			}
		}

		protected Vector3d EvaluateAcceleration(Vessel vessel, Vector3d position, double universeTime)
		{
			Vector3d totalForce = new Vector3d(0.0, 0.0, 0.0);
			if (FlightGlobals.fetch.bodies != null)
			{
				foreach (CelestialBody cb in FlightGlobals.fetch.bodies)
				{
					if (cb == vessel.mainBody)
					{
						Vector3d locationVector = GetCelestialBodyAbsolutePosition(cb, universeTime) - position;
						totalForce += locationVector.normalized * cb.gravParameter / locationVector.sqrMagnitude;
					}
					else
					{
						Vector3d locationVector = GetCelestialBodyAbsolutePosition(cb, universeTime) - position;
						totalForce += locationVector.normalized * cb.gravParameter / locationVector.sqrMagnitude;
					}
				}
			}
			//if (FlightGlobals.fetch.vessels != null)
			//{
			//    foreach (Vessel vs in FlightGlobals.fetch.vessels)
			//    {
			//        if (vs != vessel)
			//        {
			//            Vector3d locationVector = GetVesselAbsolutePosition(vs, universeTime) - position;
			//            totalForce += locationVector.normalized * vs.GetTotalMass() * gravConst / locationVector.sqrMagnitude;
			//        }
			//    }
			//}
			return totalForce;
		}

		protected Vector3d GetAbsolutePosition()
		{
			return -FlightGlobals.fetch.bodies[0].getPositionAtUT(Planetarium.GetUniversalTime());
		}

		protected Vector3d GetCelestialBodyAbsolutePosition(CelestialBody cb)
		{
			Vector3d ownAbsolutePosition = GetAbsolutePosition();
			Vector3d relativePosition = cb.getTruePositionAtUT(Planetarium.GetUniversalTime());
			return ownAbsolutePosition + relativePosition;
		}

		protected Vector3d GetCelestialBodyAbsolutePosition(CelestialBody cb, double ut)
		{
			Vector3d ownAbsolutePosition = GetAbsolutePosition();
			Vector3d relativePosition = cb.getTruePositionAtUT(ut);
			return ownAbsolutePosition + relativePosition;
		}

		protected Vector3d GetVesselAbsolutePosition(Vessel vs, double ut)
		{
			Vector3d ownAbsolutePosition = GetAbsolutePosition();
			Vector3d relativePosition = vs.orbit.getTruePositionAtUT(ut);
			return ownAbsolutePosition + relativePosition;
		}

		protected Vector3d GetAbsoluteVelocity(Vessel vessel)
		{
			return new Vector3d(vessel.orbit.GetFrameVel().x, vessel.orbit.GetFrameVel().z, vessel.orbit.GetFrameVel().y);
		}

		protected Vector3 IterateVelocity(Vessel vessel, Vector3d prevPosition, Vector3d prevVelocity, double universeTime, float deltaTime)
		{
			Vector3d result = prevVelocity;
			result += EvaluateAcceleration(vessel, prevPosition, universeTime) * deltaTime;

			return result;
		}

		protected Vector3d IteratePosition(Vessel vessel, Vector3d prevPosition, Vector3d prevVelocity, double universeTime, float deltaTime)
		{
			Vector3d result = prevPosition;
			Vector3d newVelocity = IterateVelocity(vessel, prevPosition, prevVelocity, universeTime, deltaTime);
			result += newVelocity * deltaTime;
			this.prevVelocity = newVelocity;
			return result;
		}

		protected double IterateTime(double universeTime, float deltaTime)
		{
			double result = universeTime;
			result += deltaTime;
			return result;
		}

		Vector3d prevPosition;
		List<Vector3d> prevRefPositions = new List<Vector3d>();
		Vector3d prevVelocity;
		double prevUniverseTime;
		int totalIndex = 0;
		float timeStep = 0.25f;
		int timesPerUpdate = 500;

		Vector3d basePosition;

		public void Iterate(float deltaTime)
		{
			if (prevRefPositions.Count >= 30000) return;

			totalIndex++;

			prevUniverseTime = IterateTime(prevUniverseTime, deltaTime);
			Vector3d newPosition = IteratePosition(FlightGlobals.fetch.activeVessel, prevPosition, prevVelocity, prevUniverseTime, deltaTime);
			prevPosition = newPosition;
			Vector3d newRefPosition = newPosition - GetCelestialBodyAbsolutePosition(FlightGlobals.fetch.activeVessel.mainBody, prevUniverseTime);
			if(totalIndex % 50 == 0)
				prevRefPositions.Add(newRefPosition);
		}

		public void ResetIteration(Vessel newVessel)
		{
			Debug.Log("Resetting to vessel.");
			if (predictionLineRenderer != null)
			{
				predictionLineRenderer.SetVertexCount(0);
				predictionLineRenderer.enabled = false;

				coordXRenderer.SetVertexCount(0);
				coordXRenderer.enabled = false;
				coordYRenderer.SetVertexCount(0);
				coordYRenderer.enabled = false;
				coordZRenderer.SetVertexCount(0);
				coordZRenderer.enabled = false;
			}

			InitializePredictedLine();

			gravConst = newVessel.mainBody.gravParameter / newVessel.mainBody.Mass;

			prevUniverseTime = Planetarium.GetUniversalTime();
			Debug.Log("Init: " + Planetarium.GetUniversalTime().ToString());

			basePosition = GetAbsolutePosition();
			Debug.Log("basePosition: " + basePosition.ToString());

			prevVelocity = GetAbsoluteVelocity(newVessel);
			Debug.Log("baseVelocity: " + prevVelocity.ToString());

			prevPosition = basePosition;
			prevRefPositions.Clear();

			prevRefPositions.Add(basePosition - GetCelestialBodyAbsolutePosition(newVessel.mainBody));

			totalIndex = 0;
			
			for (int i = 0; i < timesPerUpdate; i++)
				Iterate(timeStep);
		}
	}
}

using UnityEngine;

namespace VehicleDynamics.TestsRuntime
{
    public class RuntimeScenarioDriver : MonoBehaviour
    {
        [Header("References")]
        public VehicleDynamics.VehicleModel vehicleModel;
        public Rigidbody vehicleBody;

        private Vector3 defaultPosition;
        private Quaternion defaultRotation;
        private bool defaultsCaptured;

        public void CaptureDefaultsIfNeeded()
        {
            if (defaultsCaptured || vehicleModel == null)
            {
                return;
            }

            defaultPosition = vehicleModel.transform.position;
            defaultRotation = vehicleModel.transform.rotation;
            defaultsCaptured = true;
        }

        public void PrepareCase(RuntimeTestCaseDefinition definition)
        {
            if (vehicleModel == null)
            {
                Debug.LogError("RuntimeScenarioDriver: vehicleModel is not assigned.");
                return;
            }

            CaptureDefaultsIfNeeded();
            ApplyFeatureFlags(definition);
            ApplySurfaceOverride(definition.surfaceOverride);
            ResetVehicleState(definition);
        }

        public void ApplyInputs(float time, RuntimeTestCaseDefinition definition)
        {
            RuntimeInputSegment segment = definition.ResolveSegment(time);

            if (segment == null)
            {
                vehicleModel.steeringInput = 0f;
                vehicleModel.throttleInput = 0f;
                vehicleModel.brakeInput = 0f;
                vehicleModel.clutchInput = 0f;
                vehicleModel.handbrakeInput = 0f;
                return;
            }

            vehicleModel.steeringInput = segment.steering;
            vehicleModel.throttleInput = segment.throttle;
            vehicleModel.brakeInput = segment.brake;
            vehicleModel.clutchInput = segment.clutch;
            vehicleModel.handbrakeInput = segment.handbrake;
        }

        public Vector3 CurrentPosition => vehicleModel != null ? vehicleModel.transform.position : Vector3.zero;

        private void ResetVehicleState(RuntimeTestCaseDefinition definition)
        {
            if (vehicleBody == null)
            {
                vehicleBody = vehicleModel.vehicleRigidbody;
            }

            Vector3 startPosition = definition.overrideInitialPose ? definition.initialPosition : defaultPosition;
            Quaternion startRotation = definition.overrideInitialPose
                ? Quaternion.Euler(definition.initialEulerAngles)
                : defaultRotation;

            vehicleModel.transform.SetPositionAndRotation(startPosition, startRotation);

            Rigidbody[] allBodies = vehicleModel.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < allBodies.Length; i++)
            {
                Rigidbody rb = allBodies[i];
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (vehicleBody != null)
            {
                vehicleBody.linearVelocity = definition.initialLinearVelocity;
                vehicleBody.angularVelocity = definition.initialAngularVelocity;
            }

            if (vehicleModel.drivetrain != null)
            {
                if (vehicleModel.drivetrain.transmission != null)
                {
                    int maxGearIndex = vehicleModel.drivetrain.transmission.gearRatios != null
                        ? vehicleModel.drivetrain.transmission.gearRatios.Length - 1
                        : 1;
                    vehicleModel.drivetrain.transmission.currentGear = Mathf.Clamp(definition.startingGear, 0, Mathf.Max(0, maxGearIndex));
                }

                if (vehicleModel.drivetrain.engine != null)
                {
                    float idle = Mathf.Max(0f, vehicleModel.drivetrain.engine.rpmIdle);
                    vehicleModel.drivetrain.engine.engineRpm = idle;
                    vehicleModel.drivetrain.engine.engineAngularVelocity = idle * Mathf.PI / 30f;
                }
            }

            vehicleModel.steeringInput = 0f;
            vehicleModel.throttleInput = 0f;
            vehicleModel.brakeInput = 0f;
            vehicleModel.clutchInput = 0f;
            vehicleModel.handbrakeInput = 0f;
            vehicleModel.starterHeld = false;
        }

        private void ApplyFeatureFlags(RuntimeTestCaseDefinition definition)
        {
            vehicleModel.hasABS = definition.absEnabled;
            vehicleModel.hasTCS = definition.tcsEnabled;

            if (vehicleModel.carSuspension == null)
            {
                return;
            }

            for (int i = 0; i < vehicleModel.carSuspension.Length; i++)
            {
                VehicleDynamics.Suspension suspension = vehicleModel.carSuspension[i];
                if (suspension == null)
                {
                    continue;
                }

                float localZ = vehicleModel.transform.InverseTransformPoint(suspension.transform.position).z;
                bool isFront = localZ >= 0f;
                suspension.hasAntirollBar = isFront ? definition.frontAntiRollEnabled : definition.rearAntiRollEnabled;
            }
        }

        private void ApplySurfaceOverride(SurfaceOverride surfaceOverride)
        {
            if (surfaceOverride == null || !surfaceOverride.apply)
            {
                return;
            }

            VehicleDynamics.MeshRoadSurface[] meshRoads = FindObjectsByType<VehicleDynamics.MeshRoadSurface>(FindObjectsSortMode.None);
            for (int i = 0; i < meshRoads.Length; i++)
            {
                meshRoads[i].frictionCoefficients = new Vector2(surfaceOverride.longitudinalMu, surfaceOverride.lateralMu);
                meshRoads[i].rollingResistanceCoefficient = surfaceOverride.rollingResistance;
                meshRoads[i].decayingFrictionCoefficient = surfaceOverride.decayingFriction;
            }

            VehicleDynamics.TerrainRoadSurface[] terrainRoads = FindObjectsByType<VehicleDynamics.TerrainRoadSurface>(FindObjectsSortMode.None);
            for (int i = 0; i < terrainRoads.Length; i++)
            {
                VehicleDynamics.TerrainRoadSurface terrainRoad = terrainRoads[i];
                for (int layer = 0; layer < terrainRoad.terrainRoadLayersList.Count; layer++)
                {
                    VehicleDynamics.TerrainRoadLayer roadLayer = terrainRoad.terrainRoadLayersList[layer];
                    roadLayer.frictionCoefficients = new Vector2(surfaceOverride.longitudinalMu, surfaceOverride.lateralMu);
                    roadLayer.rollingResistanceCoefficient = surfaceOverride.rollingResistance;
                    roadLayer.decayingFrictionCoefficient = surfaceOverride.decayingFriction;
                }
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleModel : MonoBehaviour
    {
        [Header("Subsystem References")]
        public Drivetrain drivetrain;
        public Suspension[] carSuspension;

        [Header("Unified Vehicle Inputs")]
        [Range(-1f, 1f)] public float steeringInput = 0f;
        [Range(0f, 1f)] public float throttleInput = 0f;
        [Range(0f, 1f)] public float brakeInput = 0f;
        [Range(0f, 1f)] public float clutchInput = 0f;
        public bool handbrake = false;
        public bool shiftUp = false;
        public bool shiftDown = false;
        [Header("FFB")]
        public float alignmentTorque = 0f;
        [Header("Vehicle Parameters")]
        public float steeringWheelMaxAngle = 1080f;
        public float userWheelMaxAngle = 900f;
        public GameObject visualSteeringWheel;
        private Rigidbody vehicleRigidbody;
        [SerializeField] private Vector3 centerOfMass = Vector3.zero;
        [Header("Text displays")]
        public TMPro.TextMeshProUGUI rpmDisplay;
        public TMPro.TextMeshProUGUI speedDisplay;
        public TMPro.TextMeshProUGUI gearDisplay;
        [Header("Simulation Settings")]
        private SimulationSettings simSettings;

        void Start()
        {
            if (vehicleRigidbody == null)
            {
                vehicleRigidbody = GetComponent<Rigidbody>();
                Debug.Assert(vehicleRigidbody != null, "Rigidbody component not found on VehicleModel GameObject.");
            }
            vehicleRigidbody.centerOfMass = centerOfMass;
            vehicleRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            vehicleRigidbody.interpolation = RigidbodyInterpolation.None;

            // Get suspension
            if (carSuspension == null || carSuspension.Length == 0)
            {
                carSuspension = GetComponentsInChildren<Suspension>();
                Debug.Assert(carSuspension != null && carSuspension.Length > 0, "No KinematicSuspension components found in children of VehicleModel GameObject.");
            }

            // Get drivetrain
            if (drivetrain == null)
            {
                drivetrain = GetComponentInChildren<Drivetrain>();
                Debug.Assert(drivetrain != null, "No Drivetrain component found in children of VehicleModel GameObject.");
            }

            simSettings = Resources.Load<SimulationSettings>("Settings/SimulationSettings");
            Debug.Assert(simSettings != null, "SimulationSettings asset not found in Resources folder.");
            Time.fixedDeltaTime = 1f / simSettings.simulationRate;
            Physics.gravity = new Vector3(0f, -simSettings.gravity, 0f);
            Application.targetFrameRate = simSettings.maxFrameRate;
            Time.timeScale = simSettings.timeMultiplier;
            Physics.defaultSolverIterations = simSettings.solverIterations;

            // Setup visual steering wheel
            if (visualSteeringWheel != null)
            {
                GameObject wheel = Instantiate(visualSteeringWheel, visualSteeringWheel.transform.position, visualSteeringWheel.transform.rotation, transform);
                visualSteeringWheel.transform.SetParent(wheel.transform);
                visualSteeringWheel.transform.localRotation = Quaternion.identity;

            }
        }
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (drivetrain != null)
            {
                drivetrain.throttleInput = throttleInput;
                drivetrain.clutchInput = clutchInput;
                drivetrain.Step(dt);
            }

            float steeringWheelInput = Mathf.Clamp(steeringInput * (userWheelMaxAngle / steeringWheelMaxAngle), -1f, 1f);
            alignmentTorque = 0f;
            foreach (var suspension in carSuspension)
            {
                if (suspension != null)
                {
                    // Set steering input
                    suspension.steeringInput = steeringWheelInput;
                    // Apply braking torque
                    suspension.SetBrakeInput(brakeInput);
                    alignmentTorque = suspension.GetAlignmentTorque();

                    suspension.Step(dt);
                }
            }

            // Visual steering wheel rotation
            if (visualSteeringWheel != null)
            {
                // Rotate steering wheel around its forward axis
                float steeringAngle = steeringWheelInput * steeringWheelMaxAngle * 0.5f;
                // Vector3 rotationAxis = visualSteeringWheel.transform.forward;
                // visualSteeringWheel.transform.RotateAround(visualSteeringWheel.transform.position, rotationAxis, steeringAngle - visualSteeringWheel.transform.localEulerAngles.z);
                visualSteeringWheel.transform.localEulerAngles = new Vector3(0f, 0f, steeringAngle);
            }

            // Update displays
            if (rpmDisplay != null)
                rpmDisplay.text = $"RPM: {drivetrain.engine.engineRpm:0}";
            if (speedDisplay != null)
                speedDisplay.text = $"Air Speed [km/h]: {drivetrain.vehicleBody.linearVelocity.magnitude * 3.6f:0}";
            if (gearDisplay != null)
                gearDisplay.text = $"Gear: {drivetrain.transmission.currentGear} / {drivetrain.transmission.gearRatios.Length - 1}";
        }


        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 comWorld = transform.TransformPoint(centerOfMass);
            Gizmos.DrawWireSphere(comWorld, 0.1f);
        }


        public Vector3 GetContactForce()
        {
            Vector3 totalForce = Vector3.zero;
            foreach (var suspension in carSuspension)
            {
                totalForce += suspension.GetContactForce();
            }
            return totalForce;
        }

        public Vector3 GetLeverArm()
        {
            Vector3 totalLeverArm = Vector3.zero;
            foreach (var suspension in carSuspension)
            {
                totalLeverArm += suspension.GetLeverArm();
            }
            return totalLeverArm;
        }
    }
}

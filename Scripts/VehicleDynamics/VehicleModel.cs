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
        [Range(0f, 1f)] public float handbrakeInput = 0f;
        public bool shiftUp = false;
        public bool shiftDown = false;
        [Header("FFB")]
        public float tireAlignTorque = 0f;
        public float steeringArmTorque = 0f;
        [Header("Vehicle Parameters")]
        public float steeringWheelMaxAngle = 1080f;
        public float userWheelMaxAngle = 900f;
        public GameObject visualSteeringWheel;
        public Rigidbody vehicleRigidbody;
        [Header("ABS Parameters")]
        public bool hasABS = false;
        public float absSlipOpt = -0.15f;    // optimal slip
        public float absSlipTol = 0.05f;     // tolerance before ABS kicks in
        public float absMinVelocity = 5f;   // minimum velocity for ABS to be active
        [Header("TCS Parameters")]
        public bool hasTCS = false;
        public float tcsSlipOpt = 0.15f;     // optimal slip
        public float tcsSlipTol = 0.05f;     // tolerance before TCS kicks in
        public float tcsMinVelocity = 5f;   // minimum velocity for TCS to be active
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
            vehicleRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            vehicleRigidbody.interpolation = RigidbodyInterpolation.None;

            // Get suspension
            if (carSuspension == null || carSuspension.Length == 0)
            {
                carSuspension = GetComponentsInChildren<Suspension>();
                Debug.Assert(carSuspension != null && carSuspension.Length > 0, "No Suspension components found in children of VehicleModel GameObject.");
            }

            // Initialize suspensions
            foreach (var suspension in carSuspension)
            {
                suspension.Init();
            }

            // Get drivetrain
            if (drivetrain == null)
            {
                drivetrain = GetComponentInChildren<Drivetrain>();
                Debug.Assert(drivetrain != null, "No Drivetrain component found in children of VehicleModel GameObject.");
            }

            // Initialize drivetrain
            drivetrain.Init(this);

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
                GameObject wheel = new("SteeringWheelTransform");
                wheel.transform.SetPositionAndRotation(visualSteeringWheel.transform.position, visualSteeringWheel.transform.rotation);
                wheel.transform.SetParent(transform);
                visualSteeringWheel.transform.SetParent(wheel.transform);
                visualSteeringWheel.transform.localRotation = Quaternion.identity;

            }
        }
        /*
        Execution order:
        - VehicleModel.FixedUpdate
            - Suspension.Step
                - Strut.Step
                - Hub.Step
                    - Wheel.Step
            - Drivetrain.Step
                - Engine.Step
                - Differential.GetUpVelocity
                - Transmission.GetUpVelocity
                - Clutch.Step
            - Suspension.PostDrivetrainStep
                - Hub.PostDrivetrainStep
                    - Wheel.PostDrivetrainStep
        */
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            float steeringWheelInput = Mathf.Clamp(steeringInput * (userWheelMaxAngle / steeringWheelMaxAngle), -1f, 1f);
            tireAlignTorque = 0f;
            steeringArmTorque = 0f;
            foreach (var suspension in carSuspension)
            {
                if (suspension != null)
                {
                    // Set steering input
                    if(suspension.steerable){
                        suspension.steeringInput = steeringWheelInput;
                        tireAlignTorque += suspension.GetTireAlignmentTorque();
                        steeringArmTorque += suspension.GetSteeringArmTorque();
                    }
                    // Apply braking torque
                    suspension.SetBrakeInput(brakeInput);

                    suspension.Step(dt);
                }
            }

            if (drivetrain != null)
            {
                drivetrain.throttleInput = throttleInput;
                drivetrain.clutchInput = clutchInput;
                drivetrain.Step(dt);
            }

            foreach (var suspension in carSuspension)
            {
                if (suspension != null)
                {
                    suspension.PostDrivetrainStep(dt);
                }
            }

            // Visual steering wheel rotation
            if (visualSteeringWheel != null)
            {
                // Rotate steering wheel around its forward axis
                float steeringAngle = steeringWheelInput * steeringWheelMaxAngle * 0.5f;
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
    }
}

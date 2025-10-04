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
        public KinematicSuspension[] carSuspension;

        [Header("Unified Vehicle Inputs")]
        [Range(-1f, 1f)] public float steeringInput = 0f;
        [Range(0f, 1f)] public float throttleInput = 0f;
        [Range(0f, 1f)] public float brakeInput = 0f;
        [Range(0f, 1f)] public float clutchInput = 0f;
        public bool handbrake = false;
        public bool shiftUp = false;
        public bool shiftDown = false;
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
                carSuspension = GetComponentsInChildren<KinematicSuspension>();
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
        }
        void FixedUpdate()
        {
            if (drivetrain != null)
            {
                drivetrain.throttleInput = throttleInput;
                drivetrain.clutchInput = clutchInput;
            }

            float steeringWheelInput = Mathf.Clamp(steeringInput * (userWheelMaxAngle / steeringWheelMaxAngle), -1f, 1f);

            foreach (var suspension in carSuspension)
            {
                if (suspension != null)
                {
                    // Set steering input
                    suspension.steeringInput = steeringWheelInput;
                    // Apply braking torque
                    suspension.SetBrakeInput(brakeInput);
                }
            }

            // Visual steering wheel rotation
            if (visualSteeringWheel != null)
            {
                // Rotate steering wheel around its forward axis
                float steeringAngle = steeringWheelInput * steeringWheelMaxAngle * 0.5f;
                Vector3 rotationAxis = visualSteeringWheel.transform.forward;
                visualSteeringWheel.transform.RotateAround(visualSteeringWheel.transform.position, rotationAxis, steeringAngle - visualSteeringWheel.transform.localEulerAngles.z);
            }

            // Update displays
            if (rpmDisplay != null)
                rpmDisplay.text = $"RPM: {drivetrain.engineRpm:0}";
            if (speedDisplay != null)
                speedDisplay.text = $"Air Speed [km/h]: {drivetrain.vehicleBody.linearVelocity.magnitude * 3.6f:0}";
            if (gearDisplay != null)
                gearDisplay.text = $"Gear: {drivetrain.currentGear} / {drivetrain.gearRatios.Length - 1}";
        }


        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 comWorld = transform.TransformPoint(centerOfMass);
            Gizmos.DrawWireSphere(comWorld, 0.1f);
        }


        // https://gist.github.com/Maesla/7b3cffbda7d0a5b02aa7166d3eed5def
        // public static void DiagonalizeInertiaTensor(Matrix4x4 m, out Vector3 inertiaTensor, out Quaternion inertiaTensorRotation)
        // {
        //     float m11 = m[0, 0];
        //     float m12 = m[0, 1];
        //     float m13 = m[0, 2];
        //     float m22 = m[1, 1];
        //     float m23 = m[1, 2];
        //     float m33 = m[2, 2];

        //     const int maxSweeps = 32;
        //     const float epsilon = 1e-10F;
        //     float fabs(float x) { return Mathf.Abs(x); }
        //     float sqrt(float x) { return Mathf.Sqrt(x); }

        //     Matrix4x4 r = Matrix4x4.identity;
        //     for (int a = 0; a < maxSweeps; a++)
        //     {
        //         // Exit if off.diagonal entries small enough
        //         if ((fabs(m12) < epsilon) && (fabs(m13) < epsilon) && (fabs(m23) < epsilon))
        //             break;

        //         // Annihilate (1,2) entry.
        //         if (m12 != 0.0F)
        //         {
        //             float u = (m22 - m11) * 0.5F / m12;
        //             float u2 = u * u;
        //             float u2p1 = u2 + 1.0F;
        //             float t = (u2p1 != u2) ?
        //             ((u < 0.0F) ? -1.0F : 1.0F) * (sqrt(u2p1) - fabs(u))
        //             : 0.5F / u;
        //             float c = 1.0F / sqrt(t * t + 1.0F);
        //             float s = c * t;
        //             m11 -= t * m12;
        //             m22 += t * m12;
        //             m12 = 0.0F;
        //             float temp = c * m13 - s * m23;
        //             m23 = s * m13 + c * m23;
        //             m13 = temp;
        //             for (int i = 0; i < 3; i++)
        //             {
        //                 float tempInner = c * r[i, 0] - s * r[i, 1];
        //                 r[i, 1] = s * r[i, 0] + c * r[i, 1];
        //                 r[i, 0] = tempInner;
        //             }
        //         }

        //         // Annihilate (1,3) entry.
        //         if (m13 != 0.0F)
        //         {
        //             float u = (m33 - m11) * 0.5F / m13;
        //             float u2 = u * u;
        //             float u2p1 = u2 + 1.0F;
        //             float t = (u2p1 != u2) ?
        //             ((u < 0.0F) ? -1.0F : 1.0F) * (sqrt(u2p1) - fabs(u))
        //             : 0.5F / u;
        //             float c = 1.0F / sqrt(t * t + 1.0F);
        //             float s = c * t;
        //             m11 -= t * m13;
        //             m33 += t * m13;
        //             m13 = 0.0F;
        //             float temp = c * m12 - s * m23;
        //             m23 = s * m12 + c * m23;
        //             m12 = temp;
        //             for (int i = 0; i < 3; i++)
        //             {
        //                 float tempInner = c * r[i, 0] - s * r[i, 2];
        //                 r[i, 2] = s * r[i, 0] + c * r[i, 2];
        //                 r[i, 0] = tempInner;
        //             }
        //         }

        //         // Annihilate (2,3) entry.
        //         if (m23 != 0.0F)
        //         {
        //             float u = (m33 - m22) * 0.5F / m23;
        //             float u2 = u * u;
        //             float u2p1 = u2 + 1.0F;
        //             float t = (u2p1 != u2) ?
        //             ((u < 0.0F) ? -1.0F : 1.0F) * (sqrt(u2p1) - fabs(u))
        //             : 0.5F / u;
        //             float c = 1.0F / sqrt(t * t + 1.0F);
        //             float s = c * t;
        //             m22 -= t * m23;
        //             m33 += t * m23;
        //             m23 = 0.0F;
        //             float temp = c * m12 - s * m13;
        //             m13 = s * m12 + c * m13;
        //             m12 = temp;
        //             for (int i = 0; i < 3; i++)
        //             {
        //                 float tempInner = c * r[i, 1] - s * r[i, 2];
        //                 r[i, 2] = s * r[i, 1] + c * r[i, 2];
        //                 r[i, 1] = tempInner;
        //             }
        //         }
        //     }

        //     inertiaTensor.x = m11;
        //     inertiaTensor.y = m22;
        //     inertiaTensor.z = m33;

        //     inertiaTensorRotation = r.rotation;
        // }
    }
}

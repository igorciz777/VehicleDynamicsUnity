using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace VehicleDynamics
{
    [RequireComponent(typeof(Rigidbody))]
    public class Hub : MonoBehaviour
    {
        [Header("Wheel Parameters")]
        public float wheelUnloadedRadius = 0.3f;
        public float wheelWidth = 0.2f;
        public float wheelMass = 10f;
        public float wheelRollingResistance = 0.25f;
        public float tirePressure = 220000f; // Pa / N/m^2
        public float tirePressurePSI = 32f; // PSI
        public float tirePressureBar = 2.2f; // Bar

        public float tireStiffness = 200f; // N/m
        [Header("Brake Parameters")]
        public float maxBrakeTorque = 1500f; // Nm

        [Header("Suspension Runtime")]
        [HideInInspector] public bool rightSided = false;
        [HideInInspector] public float wheelRate = 0f;
        [HideInInspector] public float wheelDamping = 0f;

        [Header("Wheel Alignment")]
        public float steeringAngle = 0f;
        public float camberAngle = 0f;
        public float toeAngle = 0f;

        [Header("References")]
        public GameObject visualHub;
        public GameObject visualWheel;
        [HideInInspector] public Rigidbody vehicleBody;
        [HideInInspector] public Rigidbody hubBody;
        public Suspension parentSuspension;
        public Wheel wheel;

        [Header("Mounts & Dummies")]
        private GameObject dummyHub;
        private GameObject dummyWheel;
        private Transform springChassisMount;
        private Transform springHubMount;

        [Header("Wheel Center & Inertia")]
        public Vector3 wheelCenter = Vector3.zero;
        private float wheelInertia = 0.0f;
        
        [Header("Tire Squeal Audio")]
        public AudioClip tireSquealClip;
        [HideInInspector] public AudioSource slipRatioSquealSource, slipAngleSquealSource;
        public float slipRatioThreshold = 0.3f;
        public float slipAngleThreshold = 5f;
        public float maxSquealVolume = 0.5f;
        public float minSquealPitch = 0.6f;
        public float maxSquealPitch = 1.3f;

        void Awake()
        {
            parentSuspension = GetComponentInParent<Suspension>();
            vehicleBody = parentSuspension.GetComponentInParent<Rigidbody>();
            hubBody = GetComponent<Rigidbody>();

            // Set hub rigidbody mass from wheelMass
            hubBody.mass = wheelMass;

            // Check if right sided from vehicle body transform
            if (vehicleBody.transform.InverseTransformPoint(transform.position).x > 0f)
            {
                rightSided = true;
            }

            dummyHub = new GameObject("dummyHub");
            dummyHub.transform.SetParent(parentSuspension.transform);
            dummyWheel = new GameObject("dummyWheel");
            dummyWheel.transform.SetParent(parentSuspension.transform);
            wheel = dummyWheel.AddComponent<Wheel>();
            wheel.hub = this;

            wheelCenter = transform.position + transform.right * (rightSided ? parentSuspension.hubSpacing : -parentSuspension.hubSpacing);
            wheelInertia = 0.5f * wheelMass * wheelUnloadedRadius * wheelUnloadedRadius;

            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.1f;
            col.height = 0.1f;
            gameObject.layer = LayerMask.NameToLayer("WheelCollider");
        }
        void Start()
        {
            if (rightSided)
            {
                springChassisMount = parentSuspension.rightSpringChassisMount;
                springHubMount = parentSuspension.rightSpringHubMount;
            }
            else
            {
                springChassisMount = parentSuspension.leftSpringChassisMount;
                springHubMount = parentSuspension.leftSpringHubMount;
            }

            // Set visual wheel parent to dummy wheel
            if (visualWheel != null)
            {
                visualWheel.transform.SetParent(dummyWheel.transform);
                visualWheel.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            // Set visual hub parent to dummy hub
            if (visualHub != null)
            {
                visualHub.transform.SetParent(dummyHub.transform);
                visualHub.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            // Setup tire squeal audio sources
            if (tireSquealClip != null)
            {
                slipRatioSquealSource = gameObject.AddComponent<AudioSource>();
                slipRatioSquealSource.clip = tireSquealClip;
                slipRatioSquealSource.loop = true;
                slipRatioSquealSource.playOnAwake = false;
                slipRatioSquealSource.spatialBlend = 1.0f; // 3D sound
                slipRatioSquealSource.maxDistance = 50f;
                slipRatioSquealSource.volume = 0f;
                slipRatioSquealSource.pitch = minSquealPitch;
                slipRatioSquealSource.Play();

                slipAngleSquealSource = gameObject.AddComponent<AudioSource>();
                slipAngleSquealSource.clip = tireSquealClip;
                slipAngleSquealSource.loop = true;
                slipAngleSquealSource.playOnAwake = false;
                slipAngleSquealSource.spatialBlend = 1.0f; // 3D sound
                slipAngleSquealSource.maxDistance = 50f;
                slipAngleSquealSource.volume = 0f;
                slipAngleSquealSource.pitch = minSquealPitch;
                slipAngleSquealSource.Play();
            }
        }
        public void Step(float dt)
        {
            wheelCenter = transform.position + transform.right * (rightSided ? parentSuspension.hubSpacing : -parentSuspension.hubSpacing);

            tirePressurePSI = tirePressure / 6894.76f;
            tirePressureBar = tirePressure / 100000f;

            if (dummyHub != null)
            {
                dummyHub.transform.position = transform.position;
                dummyHub.transform.localEulerAngles = new Vector3(0f, transform.localEulerAngles.y, 0f);
            }
            if (visualWheel != null)
            {
                visualWheel.transform.position = wheelCenter;
                visualWheel.transform.Rotate(Vector3.right, wheel.wheelAngularVelocity * dt * Mathf.Rad2Deg, Space.Self);
            }

            // Calculate camber relative to dummyHub
            Vector3 dummyHubRight = dummyHub.transform.right;
            Vector3 wheelAxis = transform.right;
            camberAngle = Vector3.SignedAngle(dummyHubRight, wheelAxis, transform.forward);
            if(rightSided) camberAngle = -camberAngle;

            // Camber adjustment
            camberAngle += parentSuspension.camberAdjustment;
            // Toe adjustment
            toeAngle = rightSided ? parentSuspension.toeAdjustment : -parentSuspension.toeAdjustment;

            // Rotate dummy wheel to match steering, toe and camber angles
            dummyWheel.transform.position = wheelCenter;
            dummyWheel.transform.localEulerAngles = new Vector3(0f, transform.localEulerAngles.y, rightSided ? -camberAngle : camberAngle);

            wheel.Step(dt);
        }
        public void UpdateSteering(float steerInput)
        {
            float ackermannOffset = parentSuspension.maxSteeringAngle * Mathf.Abs(steerInput) * parentSuspension.ackermanPercentage / 2f * Mathf.Sign(transform.localPosition.x);
            steeringAngle = parentSuspension.maxSteeringAngle * steerInput + ackermannOffset + toeAngle;
        }
        public void ApplyDriveTorque(float driveTorque)
        {
            wheel.ApplyTorque(driveTorque);
        }
        public void ApplyBrakeTorque(float brakeTorque)
        {
            wheel.ApplyBrake(brakeTorque);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, wheelUnloadedRadius);
        }
    }
}

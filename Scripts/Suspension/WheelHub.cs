using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace VehicleDynamics
{
    [RequireComponent(typeof(Rigidbody))]
    public class WheelHub : MonoBehaviour
    {
        [Header("Wheel Parameters")]
        public float wheelUnloadedRadius = 0.3f;
        public float wheelWidth = 0.2f;
        public float wheelMass = 10f;
        public float wheelRollingResistance = 0.25f;
        public float tireMass = 8f; // kg
        public float radialTireStiffness = 200f; // N/m
        public float radialDampingRatio = 1f; // N·s/m
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
        public KinematicSuspension parentSuspension;
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
        public float slipRatioThreshold = 0.1f;
        public float slipAngleThreshold = 5f;
        public float maxSquealVolume = 1.0f;
        public float minSquealPitch = 0.6f;
        public float maxSquealPitch = 1.3f;

        void Awake()
        {
            parentSuspension = GetComponentInParent<KinematicSuspension>();
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

            // Set visual hub and wheel parent to dummy hub
            if (visualWheel != null && dummyHub != null)
            {
                visualWheel.transform.SetParent(dummyHub.transform);
                visualWheel.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            if (visualHub != null && dummyHub != null)
            {
                visualHub.transform.SetParent(dummyHub.transform);
                visualHub.transform.position = Vector3.zero;
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
        public void Step()
        {
            wheelCenter = transform.position + transform.right * (rightSided ? parentSuspension.hubSpacing : -parentSuspension.hubSpacing);

            if (rightSided)
            {
                wheelRate = parentSuspension.rightWheelRate;
                wheelDamping = parentSuspension.rightWheelDampingRate;
            }
            else
            {
                wheelRate = parentSuspension.leftWheelRate;
                wheelDamping = parentSuspension.leftWheelDampingRate;
            }

            // Calculate camber relative to vehicle body
            Vector3 wheelUp = transform.up;
            Vector3 bodyUp = vehicleBody.transform.up;
            Vector3 bodyRight = vehicleBody.transform.right;
            camberAngle = Vector3.SignedAngle(wheelUp, bodyUp, bodyRight);

            // Calculate toe relative to vehicle body
            Vector3 wheelForward = transform.forward;
            Vector3 bodyForward = vehicleBody.transform.forward;
            Vector3 bodyUpAxis = vehicleBody.transform.up;
            toeAngle = Vector3.SignedAngle(wheelForward, bodyForward, bodyUpAxis);

            if (dummyHub != null)
            {
                dummyHub.transform.position = transform.position;
                // dummyHub.transform.localEulerAngles = new Vector3(0f, steeringAngle + (rightSided ? -parentSuspension.toeAdjustment : parentSuspension.toeAdjustment), rightSided ? -parentSuspension.camberAdjustment : parentSuspension.camberAdjustment);
                dummyHub.transform.rotation = transform.rotation;
                dummyHub.transform.localEulerAngles += new Vector3(0f, steeringAngle + (rightSided ? -parentSuspension.toeAdjustment : parentSuspension.toeAdjustment), rightSided ? -parentSuspension.camberAdjustment : parentSuspension.camberAdjustment);
            }
            if (visualWheel != null)
            {
                visualWheel.transform.position = wheelCenter;
                // Rotate wheel based on angular velocity
                visualWheel.transform.Rotate(Vector3.right, wheel.wheelAngularVelocity * Time.fixedDeltaTime * Mathf.Rad2Deg, Space.Self);
            }

            // Camber adjustment
            camberAngle += parentSuspension.camberAdjustment;
            // Toe adjustment
            // toeAngle += rightSided ? -parentSuspension.toeAdjustment : parentSuspension.toeAdjustment;
            // Hard set toe to avoid car pulling to one side
            toeAngle = rightSided ? -parentSuspension.toeAdjustment : parentSuspension.toeAdjustment;

            // Rotate dummy wheel to match steering angle and toe
            // Camber angle is handled by tire model
            dummyWheel.transform.localEulerAngles = new Vector3(0f, steeringAngle + toeAngle, rightSided ? -camberAngle : camberAngle);
            dummyWheel.transform.position = wheelCenter;

            wheel.Step();
        }
        public void UpdateSteering(float steerInput)
        {
            float ackermannOffset = parentSuspension.maxSteeringAngle * Mathf.Abs(steerInput) * parentSuspension.ackermanPercentage / 2f * Mathf.Sign(transform.localPosition.x);
            steeringAngle = parentSuspension.maxSteeringAngle * steerInput + ackermannOffset;
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
            Gizmos.DrawWireSphere(transform.TransformPoint(wheelCenter), wheelUnloadedRadius);
        }
    }
}

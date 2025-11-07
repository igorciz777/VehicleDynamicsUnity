using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    [RequireComponent(typeof(Rigidbody))]
    public class Hub : MonoBehaviour
    {
        [Header("Wheel Parameters")]
        public float wheelUnloadedRadius = 0.3f; // m
        public float wheelWidth = 0.2f; // m
        public float wheelMass = 15f; // kg
        public float wheelRollingResistance = 0.25f; // Coefficient of rolling resistance
        public TireObject tireDataObject;
        public float tireMass = 10f; // kg
        public float tirePressure = 220000f; // Pa [N/m^2]
        [Range(0.1f, 2.0f)]
        public float tireDampingRatio = 1f;
        public float tireNominalLoad = 4000f; // N
        public float tireRelaxationLength = 0.3f; // m

        [Header("Suspension Runtime")]
        [HideInInspector] public bool rightSided = false;

        [Header("Wheel Alignment")]
        public float steeringAngle = 0f;
        public float camberAngle = 0f;
        public float toeAngle = 0f;

        [Header("References")]
        public GameObject visualHub;
        public GameObject visualWheel;
        [HideInInspector] public Rigidbody vehicleBody;
        [HideInInspector] public Rigidbody hubBody;
        private Suspension pS;
        private Wheel wheel;

        [Header("Mounts & Dummies")]
        private GameObject dummyHub;
        private GameObject dummyWheel;

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
            pS = GetComponentInParent<Suspension>();
            vehicleBody = pS.GetComponentInParent<Rigidbody>();
            hubBody = GetComponent<Rigidbody>();

            // Set hub rigidbody mass from wheelMass
            hubBody.mass = wheelMass;

            hubBody.linearDamping = 0f;
            hubBody.angularDamping = 0f;

            // Check if right sided from vehicle body transform
            if (vehicleBody.transform.InverseTransformPoint(transform.position).x > 0f)
            {
                rightSided = true;
            }

            dummyHub = new GameObject("dummyHub");
            dummyHub.transform.SetParent(pS.transform);
            dummyWheel = new GameObject("dummyWheel");
            dummyWheel.transform.SetParent(pS.transform);
            wheel = dummyWheel.AddComponent<Wheel>();
            wheel.hub = this;

            wheelCenter = transform.position + transform.right * (rightSided ? pS.hubSpacing : -pS.hubSpacing);
            wheelInertia = 0.5f * wheelMass * wheelUnloadedRadius * wheelUnloadedRadius;

            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.1f;
            col.height = 0.1f;
            gameObject.layer = LayerMask.NameToLayer("WheelCollider");
        }
        void Start()
        {
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
            wheelCenter = transform.position + transform.right * (rightSided ? pS.hubSpacing : -pS.hubSpacing);

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
            if (rightSided) camberAngle = -camberAngle;

            // Camber adjustment
            camberAngle += pS.camberAdjustment;
            // Toe adjustment
            toeAngle = rightSided ? pS.toeAdjustment : -pS.toeAdjustment;

            // Rotate dummy wheel to match steering, toe and camber angles
            dummyWheel.transform.position = wheelCenter;
            dummyWheel.transform.localEulerAngles = new Vector3(0f, transform.localEulerAngles.y, rightSided ? -camberAngle : camberAngle);

            wheel.Step(dt);
        }
        public void PostDrivetrainStep(float dt)
        {
            wheel.PostDrivetrainStep(dt);
        }
        public void ApplyDriveTorque(float driveTorque)
        {
            wheel.ApplyDriveTorque(driveTorque);
        }
        public void ApplyBrakeTorque(float brakeTorque)
        {
            wheel.ApplyBrakeTorque(brakeTorque);
        }
        public void ApplyBrakePressure(float brakePressure)
        {
            bool ABS = pS.vehicleModel.hasABS;
            if (ABS)
            {
                float wheelSlip = wheel.slipRatio;
                float slipOpt = pS.vehicleModel.absSlipOpt;
                float slipTol = pS.vehicleModel.absSlipTol;
                float pressureDropRate = pS.vehicleModel.absPressureDropRate;
                float pressureRiseRate = pS.vehicleModel.absPressureRiseRate;

                if (Mathf.Abs(wheelSlip - slipOpt) > slipTol && brakePressure > 0f)
                {
                    // Reduce brake pressure
                    brakePressure -= pressureDropRate;
                    brakePressure = Mathf.Max(brakePressure, 0f);
                }
                else
                {
                    // Increase brake pressure
                    brakePressure += pressureRiseRate * Time.fixedDeltaTime;
                    brakePressure = Mathf.Min(brakePressure, pS.brakePressure);
                }
            }
            float brakeTorque = pS.brakePadCount * pS.brakeFrictionCoefficient * brakePressure * (pS.brakePistonArea * 0.1f) * pS.brakeRotorRadius;
            wheel.ApplyBrakeTorque(brakeTorque);
        }

        private void OnDrawGizmosSelected() {
            // Draw physical wheel radius and width
            if (visualWheel != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 wheelCenter = visualWheel.transform.position;
                CustomGizmos.DrawCircle(wheelCenter + 0.5f * wheelWidth * visualWheel.transform.right, visualWheel.transform.right, wheelUnloadedRadius, Color.yellow);
                CustomGizmos.DrawCircle(wheelCenter - 0.5f * wheelWidth * visualWheel.transform.right, visualWheel.transform.right, wheelUnloadedRadius, Color.yellow);
            }
        }

        // Getters
        public Wheel GetWheel()
        {
            return wheel;
        }

        public Suspension GetSuspension()
        {
            return pS;
        }

        // Setters
        public void SetTireParameters(float newTireMass, float newTirePressure, float newTireDampingRatio, float newTireNominalLoad)
        {
            tireMass = newTireMass;
            tirePressure = newTirePressure;
            tireDampingRatio = newTireDampingRatio;
            tireNominalLoad = newTireNominalLoad;
        }
    }
}

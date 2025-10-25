using UnityEngine;

namespace VehicleDynamics
{
    public class Wheel : MonoBehaviour
    {
        public enum TireFrictionModel
        {
            MF_Simplified,
            MF,
        }

        [Header("Wheel Parameters")]
        public bool isPowered = false; // Is this wheel driven by the drivetrain
        private float nominalLoad = 4000f; // Fz0
        private float tireDampingStiffness = 0f;

        [Header("Tire Model")]
        public TireFrictionModel tireFrictionModel = TireFrictionModel.MF;
        public LayerMask layerMask = ~0; // Layers to consider for ground contact
        public float decayingFrictionCoefficient = 0f; // lowers friction with slip speed
        public Vector2 frictionCoefficient = new(1.0f, 1.0f); // (longitudinal mu_x, lateral mu_y)

        [Header("Wheel State (Runtime)")]
        public bool isGrounded = false; // Is the wheel in contact with the ground
        public float wheelAngularVelocity = 0f; // rad/s
        public float wheelTorque = 0f; // Torque applied to wheel from drivetrain
        public float brakeTorque = 0f;
        public Vector3 wheelVelocity = Vector3.zero;
        public Vector3 contactPatchVelocity = Vector3.zero;
        private Vector3 hitPoint = Vector3.zero;
        public float slipRatio = 0f; // κ
        public float slipAngle = 0f; // α
        [SerializeField] private float normalLoad = 0f; // Fz
        [SerializeField] private float wheelLoadedRadius = 0f;
        [SerializeField] private float wheelEffectiveRadius = 0f;

        [Header("References")]
        public Hub hub;
        private Rigidbody hubBody;
        private Rigidbody vehicleBody;

        [Header("Internal")]
        private float wheelUnloadedRadius;
        private float wheelInertia = 0.0f;
        private float wheelAngularAcceleration = 0.0f;
        private float alignmentTorque = 0f;
        private float referenceVelocity = 0f;

        // Force cap
        private const float maxVerticalForce = 600000f;
        private const float maxTireForce = 100000f;
        private const float maxTorque = 20000f;

        public Tire tireModel;
        private TireInput tireInput;

        void Start()
        {
            hubBody = hub.hubBody;
            vehicleBody = hub.vehicleBody;
            wheelUnloadedRadius = hub.wheelUnloadedRadius;
            wheelInertia = 0.5f * hub.wheelMass * wheelUnloadedRadius * wheelUnloadedRadius;
            nominalLoad = hub.tireNominalLoad;
            layerMask = LayerMask.GetMask("Default", "Ground");

            // Initialize tire model
            tireModel = tireFrictionModel switch
            {
                TireFrictionModel.MF_Simplified => new MagicFormulaSimplified(),
                TireFrictionModel.MF => new MagicFormula(),
                _ => new MagicFormulaSimplified(),
            };

            // Initialize tire input
            tireInput = new TireInput
            {
                kap = 0f,
                alph = 0f,
                Fz = 0f,
                gam = 0f,
                Fz0 = 0f,
                r0 = wheelUnloadedRadius,
                mu = frictionCoefficient
            };

            referenceVelocity = Mathf.Sqrt(Mathf.Abs(Physics.gravity.y) * wheelUnloadedRadius);

            tireDampingStiffness = 2f * hub.tireDampingRatio * Mathf.Sqrt(hub.tirePressure * hub.tireMass);
        }
        public void Step(float dt)
        {
            Vector3 wheelCenter = hub.wheelCenter;

            // Calculate wheel velocity
            wheelVelocity = hubBody.GetPointVelocity(wheelCenter);

            // Raycast setup
            float rayRadius = hub.wheelWidth * 0.5f;
            if (hub.wheelWidth > wheelUnloadedRadius)
                rayRadius = wheelUnloadedRadius * 0.5f;
            float rayLength = wheelUnloadedRadius - rayRadius;

            Vector3 centerRayOrigin = wheelCenter;

            Ray[] rays = {
                new (centerRayOrigin, -transform.up),
                // new (centerRayOrigin, transform.up),
                new (centerRayOrigin, transform.forward),
                new (centerRayOrigin, -transform.forward),
                // new (centerRayOrigin, (transform.up + transform.forward).normalized),
                // new (centerRayOrigin, (transform.up - transform.forward).normalized),
                new (centerRayOrigin, (-transform.up + transform.forward).normalized),
                new (centerRayOrigin, (-transform.up - transform.forward).normalized),
            };

            isGrounded = false;
            float minRayHitDistance = float.MaxValue;
            RaycastHit selectedHit = default;

            foreach (Ray ray in rays)
            {
                if (Physics.SphereCast(ray, rayRadius, out RaycastHit hit, rayLength, layerMask))
                {
                    isGrounded = true;
                    hitPoint = hit.point;
                    float rayHitDistance = hit.distance;
                    if (rayHitDistance < minRayHitDistance)
                    {
                        minRayHitDistance = rayHitDistance;
                        selectedHit = hit;
                    }
                    DrawTireRay(ray);
                }
            }

            if (!isGrounded)
            {
                normalLoad = 0f;
                slipAngle = 0f;
                slipRatio = 0f;
                wheelEffectiveRadius = wheelUnloadedRadius;
                wheelLoadedRadius = wheelUnloadedRadius;
                alignmentTorque = 0f;
                return;
            }

            Vector3 contactPoint = selectedHit.point;
            Vector3 contactNormal = selectedHit.normal;
            Vector3 contactVelWorld = hubBody.GetPointVelocity(contactPoint);

            // Penetration
            float distance = selectedHit.distance;
            float currPen = rayLength - distance;
            float penVel = Vector3.Dot(contactVelWorld, contactNormal);

            float stiffnessForce = hub.tirePressure * currPen;
            float dampingForce = -penVel * tireDampingStiffness;

            float verticalForce = stiffnessForce + dampingForce;
            verticalForce = Mathf.Clamp(verticalForce, -maxVerticalForce, maxVerticalForce);
            normalLoad = verticalForce;

            // Compute loaded/effective radius
            float loadedRadius = wheelUnloadedRadius * (1 - currPen / wheelUnloadedRadius);
            float theta = Mathf.Acos(Mathf.Clamp(loadedRadius / wheelUnloadedRadius, -1f, 1f));
            float omega = wheelUnloadedRadius * (theta * theta) / 2f;
            float effectiveRadius = loadedRadius + 2 * omega / 3f;

            // Set class-wheel state
            wheelEffectiveRadius = effectiveRadius;
            wheelLoadedRadius = loadedRadius;

            // Compute slip values
            slipAngle = CalculateSlipAngle();
            slipRatio = CalculateSlipRatio(slipAngle);

            Vector2 compositeFriction = frictionCoefficient / (1f + decayingFrictionCoefficient * wheelVelocity.magnitude / referenceVelocity);

            // Tire input
            tireInput.kap = slipRatio;
            tireInput.alph = slipAngle;
            tireInput.Fz = Mathf.Max(0f, verticalForce);
            tireInput.gam = hub.camberAngle * Mathf.Deg2Rad;
            tireInput.Fz0 = nominalLoad;
            tireInput.r0 = wheelUnloadedRadius;
            tireInput.mu = compositeFriction;

            // Get tire forces
            Vector4 tireForces = tireModel.GetForcesAndTorque(ref tireInput);
            // roadForce in world space projected on plane of contact normal
            Vector3 roadForce =
                tireForces.x * Vector3.ProjectOnPlane(transform.forward, contactNormal).normalized +
                tireForces.z * Vector3.ProjectOnPlane(transform.right, contactNormal).normalized;

            // Apply vertical and road forces
            if (Mathf.Abs(verticalForce) > Mathf.Epsilon)
                hubBody.AddForceAtPosition(verticalForce * contactNormal, contactPoint, ForceMode.Force);

            hubBody.AddForceAtPosition(roadForce, contactPoint, ForceMode.Force);

            // Apply aligning torque
            alignmentTorque = Mathf.Clamp(tireForces.w, -maxTorque, maxTorque);
            if (Mathf.Abs(alignmentTorque) > Mathf.Epsilon)
                hubBody.AddTorque(alignmentTorque * transform.up, ForceMode.Force);

            // Rolling resistance moment
            float rrMoment = tireModel.GetRollingResistanceMoment();

            // Reaction torque from longitudinal tire force -> wheel spin
            float Fy = tireForces.x;
            float tireTorque = -Fy * wheelEffectiveRadius;

            // Apply rolling resistance to wheel angular velocity
            float rollingResistanceTorque = rrMoment * Mathf.Sign(wheelAngularVelocity);
            float rollingResistanceDeceleration = rollingResistanceTorque / wheelInertia;
            if (Mathf.Abs(wheelAngularVelocity) >= Mathf.Abs(rollingResistanceDeceleration * dt))
            {
                wheelAngularVelocity -= rollingResistanceDeceleration * dt;
            }
            else
            {
                wheelAngularVelocity = 0f;
            }

            // Update wheel angular velocity
            wheelAngularAcceleration = tireTorque / wheelInertia;
            wheelAngularVelocity += wheelAngularAcceleration * dt;

            // Draw tire forces
            DrawTireForces(contactPoint, roadForce);

            if (hub.tireSquealClip != null)
            {
                UpdateTireSqueal();
            }
        }
        public void PostDrivetrainStep(float dt)
        {

            if (!isPowered && isGrounded)
            {
                // Update wheel angular velocity based on ground contact
                float wheelLinearVelocity = Vector3.Dot(wheelVelocity, transform.forward);
                wheelAngularVelocity = wheelLinearVelocity / wheelEffectiveRadius;
            }
            else if (isPowered)
            {
                // Update wheel angular velocity based wheel torque

                wheelAngularAcceleration = wheelTorque / wheelInertia;
                wheelAngularVelocity += wheelAngularAcceleration * dt;
                wheelTorque = 0f; // Reset torque after applying
            }
            else // In air not powered
            {
                wheelAngularAcceleration = hub.wheelRollingResistance * -wheelAngularVelocity / wheelInertia;
                wheelAngularVelocity += wheelAngularAcceleration * dt;
            }
            // Apply brake torque
            float brakeDeceleration = Mathf.Sign(wheelAngularVelocity) * brakeTorque / wheelInertia;
            if (Mathf.Abs(wheelAngularVelocity) >= Mathf.Abs(brakeDeceleration * dt))
            {
                wheelAngularVelocity -= brakeDeceleration * dt;
            }
            else
            {
                wheelAngularVelocity = 0f;
            }

        }
        // Calculate longitudinal slip / slip ratio κ
        private float CalculateSlipRatio(float slipAngle = 0f)
        {
            float slipEPS = 0.01f;
            Vector3 localWheelVelocity = vehicleBody.transform.InverseTransformDirection(wheelVelocity);
            float wheelLongitudalVelocity = localWheelVelocity.z;

            if (Mathf.Abs(wheelLongitudalVelocity) < slipEPS)
            {
                return 0f;
            }

            float vel = Mathf.Abs(wheelLongitudalVelocity);

            // Reduce sensitivity at low speeds
            if (vel < 10f) vel = 10f;

            float slipRatio = (wheelAngularVelocity * wheelEffectiveRadius - wheelLongitudalVelocity) / vel;
            slipRatio = Mathf.Clamp(slipRatio, -1f, 1f);
            return slipRatio;
        }

        // Calculate slip angle α
        private float CalculateSlipAngle()
        {

            float slipEPS = 0.01f;
            Vector3 localWheelVelocity = transform.InverseTransformDirection(wheelVelocity);

            if (localWheelVelocity.sqrMagnitude < slipEPS * slipEPS)
            {
                return 0f;
            }

            float forwardVel = Mathf.Abs(localWheelVelocity.z);
            float lateralVel = localWheelVelocity.x;
            float yawRate = vehicleBody.angularVelocity.y;
            // Wong method
            float denominator = forwardVel + hub.GetSuspension().GetTrackWidth() * 0.5f * Mathf.Abs(yawRate) + Mathf.Epsilon;
            if (Mathf.Abs(denominator) < slipEPS)
            {
                return 0f;
            }

            // Calculate slip angle
            float slipAngle = (hub.GetSuspension().GetWheelBase() * yawRate + lateralVel) / denominator;
            if (tireFrictionModel == TireFrictionModel.MF_Simplified)
                slipAngle = -slipAngle;
            return Mathf.Asin(Mathf.Clamp(slipAngle, -1f, 1f)); // Clamp to valid range for Asin
        }
        public float GetAlignmentTorque()
        {
            return alignmentTorque;
        }
        public void ApplyTorque(float torque)
        {
            wheelTorque += torque;
        }
        public void ApplyBrakeTorque(float torque)
        {
            brakeTorque = torque;
        }
        void UpdateTireSqueal()
        {
            // Longitudinal slip squeal
            bool longSlipActive = Mathf.Abs(slipRatio) > hub.slipRatioThreshold && isGrounded;
            float longSlipNorm = Mathf.InverseLerp(hub.slipRatioThreshold, 1f, Mathf.Abs(slipRatio));
            float longVolume = Mathf.Clamp(longSlipNorm, 0, hub.maxSquealVolume);
            hub.slipRatioSquealSource.volume = longVolume;
            hub.slipRatioSquealSource.pitch = Mathf.Lerp(hub.minSquealPitch, hub.maxSquealPitch, longSlipNorm);
            if (longSlipActive && !hub.slipRatioSquealSource.isPlaying)
                hub.slipRatioSquealSource.Play();
            else if (!longSlipActive && hub.slipRatioSquealSource.isPlaying)
                hub.slipRatioSquealSource.Stop();

            // Lateral slip squeal
            bool latSlipActive = Mathf.Abs(slipAngle * Mathf.Rad2Deg) > hub.slipAngleThreshold && isGrounded;
            float latSlipNorm = Mathf.InverseLerp(hub.slipAngleThreshold, 15f, Mathf.Abs(slipAngle * Mathf.Rad2Deg));
            float latVolume = Mathf.Clamp(latSlipNorm, 0, hub.maxSquealVolume);
            hub.slipAngleSquealSource.volume = latVolume;
            hub.slipAngleSquealSource.pitch = Mathf.Lerp(hub.minSquealPitch, hub.maxSquealPitch, latSlipNorm);
            if (latSlipActive && !hub.slipAngleSquealSource.isPlaying)
                hub.slipAngleSquealSource.Play();
            else if (!latSlipActive && hub.slipAngleSquealSource.isPlaying)
                hub.slipAngleSquealSource.Stop();
        }
        public Vector3 GetContactForce()
        {
            if (isGrounded)
            {
                return normalLoad * -transform.up; // Approximation of vertical force
            }
            return Vector3.zero;
        }
        private void DrawTireRay(Ray ray)
        {
            Color rayColor = isGrounded ? Color.green : Color.red;
            Debug.DrawRay(ray.origin, ray.direction * wheelUnloadedRadius, rayColor);
        }
        private void DrawTireForces(Vector3 hitPointLocal, Vector3 roadForce)
        {
            float forceScaleZ = 0.001f; // Scale down for visualization
            float forceScaleY = 0.005f; // Scale down for visualization
            // Draw Fx (longitudinal)
            Debug.DrawRay(hitPointLocal, forceScaleZ * roadForce.z * transform.forward, Color.red);
            // Draw Fz (lateral response as before)
            Debug.DrawRay(hitPointLocal, forceScaleY * roadForce.y * transform.right, Color.green);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            if (isGrounded) Gizmos.DrawSphere(hitPoint, 0.02f);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            // Draw SphereCasts
            float rayRadius = hub.wheelWidth * 0.5f;
            float rayLength = wheelUnloadedRadius - rayRadius;
            Vector3 centerRayOrigin = transform.position;
            Ray[] rays = {
                new (centerRayOrigin, -transform.up),
                new (centerRayOrigin, transform.up),
                new (centerRayOrigin, transform.forward),
                new (centerRayOrigin, -transform.forward),
                new (centerRayOrigin, (transform.up + transform.forward).normalized),
                new (centerRayOrigin, (transform.up - transform.forward).normalized),
                new (centerRayOrigin, (-transform.up + transform.forward).normalized),
                new (centerRayOrigin, (-transform.up - transform.forward).normalized),
            };
            foreach (Ray ray in rays)
            {
                Gizmos.DrawSphere(ray.origin + ray.direction * rayLength, rayRadius);
            }
        }
    }
}

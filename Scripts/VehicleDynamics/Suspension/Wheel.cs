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
        private float tireTorque = 0f;
        private float rollingResistanceTorque = 0f;
        public Vector3 wheelVelocity = Vector3.zero;
        private float wheelLongitudalVelocity = 0f;
        private float wheelLateralVelocity = 0f;
        private float wheelRotationalVelocity = 0f;
        private Vector3 hitPoint = Vector3.zero;
        public float slipRatio = 0f; // κ
        public float slipAngle = 0f; // α
        public float tireContactCamberAngle = 0f; // γ
        [SerializeField] private float normalLoad = 0f; // Fz
        [SerializeField] private float wheelLoadedRadius = 0f;
        [SerializeField] private float wheelEffectiveRadius = 0f;

        [Header("References")]
        private Hub hub;
        private Rigidbody hubBody;
        private Rigidbody vehicleBody;

        [Header("Internal")]
        private float wheelUnloadedRadius;
        private float wheelInertia = 0.0f;
        private float alignmentTorque = 0f;
        private float referenceVelocity = 0f;
        private float lastSlipAngleLagged = 0f; // for slip angle relaxation length
        private float currRollingResistanceCoefficient = 0f;

        // Force cap
        private const float maxForce = 600000f;
        // Camber angle limit
        private const float radLimit = Mathf.PI / 6f;

        public Tire tireModel;
        private TireInput tireInput;

        public void Init(Hub hub)
        {
            this.hub = hub;
            hubBody = hub.hubBody;
            vehicleBody = hub.vehicleBody;
            wheelUnloadedRadius = hub.wheelUnloadedRadius;
            wheelInertia = 0.5f * hub.wheelMass * wheelUnloadedRadius * wheelUnloadedRadius;
            nominalLoad = hub.tireNominalLoad;
            layerMask = LayerMask.GetMask("Default", "Ground");

            // Initialize tire model
            TireObject tireDataObject = hub.tireDataObject;
            tireModel = tireFrictionModel switch
            {
                TireFrictionModel.MF_Simplified => new MagicFormulaSimplified((MFSimpleTireObject)tireDataObject),
                TireFrictionModel.MF => new MagicFormula((MFTireObject)tireDataObject),
                _ => new MagicFormulaSimplified((MFSimpleTireObject)tireDataObject),
            };

            // Initialize tire input
            tireInput = new();

            referenceVelocity = Mathf.Sqrt(Mathf.Abs(Physics.gravity.y) * wheelUnloadedRadius);

            tireDampingStiffness = 2f * hub.tireDampingRatio * Mathf.Sqrt(hub.tirePressure * hub.tireMass);
        }
        public void Step(float dt)
        {
            Vector3 wheelCenter = hub.wheelCenter;

            // Calculate wheel velocity
            wheelVelocity = hubBody.GetPointVelocity(wheelCenter);
            Vector3 localWheelVel = hubBody.transform.InverseTransformDirection(wheelVelocity);
            wheelLongitudalVelocity = localWheelVel.z;
            wheelLateralVelocity = localWheelVel.x;

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

            if (hub.tireSquealClip != null)
            {
                UpdateTireSqueal();
            }

            if (!isGrounded)
            {
                normalLoad = 0f;
                slipAngle = 0f;
                slipRatio = 0f;
                wheelEffectiveRadius = wheelUnloadedRadius;
                wheelLoadedRadius = wheelUnloadedRadius;
                alignmentTorque = 0f;
                tireTorque = 0f;
                rollingResistanceTorque = 0f;
                tireContactCamberAngle = 0f;
                return;
            }

            // Get ground surface friction properties
            if (selectedHit.collider.TryGetComponent<TerrainRoadSurface>(out var terrainSurface))
            {
                var roadLayer = terrainSurface.GetRoadLayerAtPosition(selectedHit.point);
                if (roadLayer != null)
                {
                    frictionCoefficient = roadLayer.frictionCoefficients;
                    decayingFrictionCoefficient = roadLayer.decayingFrictionCoefficient;
                    currRollingResistanceCoefficient = roadLayer.rollingResistanceCoefficient;
                    hub.slipRatioSquealSource.clip = roadLayer.surfaceSkidSound;
                    hub.slipAngleSquealSource.clip = roadLayer.surfaceSkidSound;
                    hub.maxSquealPitch = roadLayer.surfaceSkidPitch;
                }
            }
            else if (selectedHit.collider.TryGetComponent<MeshRoadSurface>(out var meshSurface))
            {
                frictionCoefficient = meshSurface.frictionCoefficients;
                decayingFrictionCoefficient = meshSurface.decayingFrictionCoefficient;
                currRollingResistanceCoefficient = meshSurface.rollingResistanceCoefficient;
                hub.slipRatioSquealSource.clip = meshSurface.surfaceSkidSound;
                hub.slipAngleSquealSource.clip = meshSurface.surfaceSkidSound;
                hub.maxSquealPitch = meshSurface.surfaceSkidPitch;
            }
            else
            {
                // Default friction values
                frictionCoefficient = new Vector2(1.0f, 1.0f);
                decayingFrictionCoefficient = 0f;
                currRollingResistanceCoefficient = 0.015f;
                hub.slipRatioSquealSource.clip = hub.tireSquealClip;
                hub.slipAngleSquealSource.clip = hub.tireSquealClip;
                hub.maxSquealPitch = 1.3f;
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
            verticalForce = Mathf.Clamp(verticalForce, -maxForce, maxForce);
            normalLoad = verticalForce;

            // Compute loaded/effective radius
            float loadedRadius = wheelUnloadedRadius * (1 - currPen / wheelUnloadedRadius);
            float effectiveRadius = loadedRadius + (2 * currPen / 3f);

            // Tire contact patch camber angle
            Vector3 wheelSpinAxis = hub.rightSided ? -transform.right : transform.right;
            float theta = Mathf.Acos(Vector3.Dot(contactNormal, wheelSpinAxis));
            tireContactCamberAngle = (Mathf.PI / 2f) - theta;
            // Limit camber angle (above 30 degrees goes unstable)
            tireContactCamberAngle = Mathf.Clamp(tireContactCamberAngle, -radLimit, radLimit);
            // Set class-wheel state
            wheelEffectiveRadius = effectiveRadius;
            wheelLoadedRadius = loadedRadius;

            // Compute slip values
            slipAngle = CalculateSlipAngle(dt);
            slipRatio = CalculateSlipRatio(slipAngle);

            float longitudinalSlipVelocity = wheelLongitudalVelocity - wheelRotationalVelocity * wheelEffectiveRadius;
            float lateralSlipVelocity = wheelLateralVelocity;

            Vector2 compositeFriction;
            compositeFriction.x = frictionCoefficient.x / (1f + decayingFrictionCoefficient * Mathf.Abs(longitudinalSlipVelocity) / referenceVelocity);
            compositeFriction.y = frictionCoefficient.y / (1f + decayingFrictionCoefficient * Mathf.Abs(lateralSlipVelocity) / referenceVelocity);

            Vector2 degressiveFriction;
            float A_mu = 10f;
            degressiveFriction.x = A_mu * compositeFriction.x / (1f + (A_mu - 1f) * compositeFriction.x);
            degressiveFriction.y = A_mu * compositeFriction.y / (1f + (A_mu - 1f) * compositeFriction.y);

            // Tire input
            tireInput.UpdateTireInput(
                slipRatio,
                slipAngle,
                normalLoad,
                tireContactCamberAngle,
                nominalLoad,
                wheelUnloadedRadius,
                wheelEffectiveRadius,
                currRollingResistanceCoefficient,
                frictionCoefficient,
                compositeFriction,
                degressiveFriction
            );
            // Get tire forces
            Vector4 tireForces = tireModel.GetForcesAndTorque(ref tireInput);
            // roadForce in world space projected on plane of contact normal
            Vector3 roadForce =
                tireForces.x * Vector3.ProjectOnPlane(transform.forward, contactNormal).normalized +
                tireForces.z * Vector3.ProjectOnPlane(transform.right, contactNormal).normalized;

            // Apply vertical and road forces
            if (Mathf.Abs(verticalForce) > Mathf.Epsilon)
                hubBody.AddForceAtPosition(verticalForce * contactNormal, contactPoint, ForceMode.Force);

            if(roadForce.sqrMagnitude > Mathf.Epsilon)
                hubBody.AddForceAtPosition(roadForce, contactPoint, ForceMode.Force);

            // Apply aligning torque
            alignmentTorque = tireForces.w;
            if (Mathf.Abs(alignmentTorque) > Mathf.Epsilon)
                hubBody.AddTorque(alignmentTorque * transform.up, ForceMode.Force);

            // Rolling resistance moment
            float rrMoment = tireModel.GetRollingResistanceMoment();

            // Reaction torque from longitudinal tire force -> wheel spin
            float Fx = tireForces.x;
            tireTorque = -Fx * wheelEffectiveRadius;

            // Apply rolling resistance to wheel angular velocity
            rollingResistanceTorque = rrMoment * Mathf.Sign(wheelAngularVelocity);

            // Draw tire forces
            DrawTireForces(contactPoint, tireForces);
        }
        public void PostDrivetrainStep(float dt)
        {
            float currentRadius = isGrounded ? wheelEffectiveRadius : wheelUnloadedRadius;
            float netTorque = wheelTorque + tireTorque + rollingResistanceTorque;

            // Apply brakes
            if (brakeTorque > 0f)
            {
                const float eps = 1e-4f;
                float brakeSign;
                if (Mathf.Abs(wheelAngularVelocity) > eps)
                {
                    // If wheel is spinning, brakes oppose spin
                    brakeSign = Mathf.Sign(wheelAngularVelocity);
                }
                else
                {
                    // If wheel is stationary, brakes oppose net torque
                    brakeSign = Mathf.Abs(netTorque) > Mathf.Epsilon ? Mathf.Sign(netTorque) : 1f;
                }
                netTorque -= brakeTorque * brakeSign;
            }

            // Integrate angular acceleration from net torque
            float angularAcceleration = netTorque / wheelInertia;
            float newAngularVelocity = wheelAngularVelocity + angularAcceleration * dt;

            // Prevent overshoot
            if (wheelAngularVelocity > 0f && newAngularVelocity < 0f || wheelAngularVelocity < 0f && newAngularVelocity > 0f)
            {
                newAngularVelocity = 0f;
            }

            wheelAngularVelocity = newAngularVelocity;
            wheelRotationalVelocity = wheelAngularVelocity * currentRadius;

            // Reset wheelTorque
            wheelTorque = 0f;
        }
        // Calculate longitudinal slip / slip ratio κ
        // TODO: fix low speed oscillations
        private float CalculateSlipRatio(float slipAngle = 0f)
        {
            float slipEPS = 0.01f;
            float slipRatio;

            if (Mathf.Abs(wheelLongitudalVelocity) < slipEPS)
            {
                return 0f;
            }

            float vel = Mathf.Abs(wheelLongitudalVelocity);

            // Reduce sensitivity at low speeds
            if (vel < 10f) vel = 10f;

            if(vehicleBody.linearVelocity.magnitude < 0.5f)
            {
                slipRatio = (wheelAngularVelocity * wheelEffectiveRadius - wheelLongitudalVelocity) / vel;
            }
            else
            {
                slipRatio = (wheelAngularVelocity * wheelEffectiveRadius - wheelLongitudalVelocity * Mathf.Cos(slipAngle)) / vel;
            }
            slipRatio = Mathf.Clamp(slipRatio, -5f, 5f);
            return slipRatio;
        }

        // Calculate slip angle α
        // TODO: fix low speed oscillations
        private float CalculateSlipAngle(float dt)
        {
            float slipEPS = 0.01f;

            float yawRate = vehicleBody.angularVelocity.y;

            // Wong method
            float denominator = Mathf.Abs(wheelLongitudalVelocity) +
                                hub.GetSuspension().GetTrackWidth() * 0.5f * Mathf.Abs(yawRate) +
                                Mathf.Epsilon;

            // if (Mathf.Abs(denominator) < slipEPS)
            // {
            //     denominator = slipEPS;
            // }

            float slipAngle = (hub.GetSuspension().GetDistanceToCOM() * yawRate + wheelLateralVelocity) / denominator;

            if (tireFrictionModel == TireFrictionModel.MF_Simplified)
                slipAngle = -slipAngle;

            float alpha_c = Mathf.Atan(slipAngle);

            // if(vehicleBody.linearVelocity.magnitude < 8.3f)
            // {
            //     return alpha_c;
            // }

            // Relaxation length model (slip angle lag)
            float Vx = Mathf.Max(Mathf.Abs(wheelLongitudalVelocity), slipEPS);
            float tau = hub.tireRelaxationLength / Vx;

            // Discrete integration (first-order lag)
            float alpha_l = lastSlipAngleLagged + dt / tau * (alpha_c - lastSlipAngleLagged);

            lastSlipAngleLagged = alpha_l;

            // Lerp with speed to reduce oscillations at low speed
            float speedLerp = Mathf.InverseLerp(0f, 8.3f, vehicleBody.linearVelocity.magnitude);
            alpha_l = Mathf.Lerp(alpha_c, alpha_l, speedLerp);

            return alpha_l;
        }
        public void ApplyDriveTorque(float torque)
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
            float longVelocityNorm = Mathf.InverseLerp(0f, 10f, Mathf.Abs(wheelAngularVelocity)); // Normalize angular velocity
            float longVolume = Mathf.Clamp(longSlipNorm * longVelocityNorm, 0, hub.maxSquealVolume);
            hub.slipRatioSquealSource.volume = longVolume;
            hub.slipRatioSquealSource.pitch = Mathf.Lerp(hub.minSquealPitch, hub.maxSquealPitch, longSlipNorm * longVelocityNorm);
            if (longSlipActive && !hub.slipRatioSquealSource.isPlaying)
            hub.slipRatioSquealSource.Play();
            else if (!longSlipActive && hub.slipRatioSquealSource.isPlaying)
            hub.slipRatioSquealSource.Stop();

            // Lateral slip squeal
            bool latSlipActive = Mathf.Abs(slipAngle * Mathf.Rad2Deg) > hub.slipAngleThreshold && isGrounded;
            float latSlipNorm = Mathf.InverseLerp(hub.slipAngleThreshold, 15f, Mathf.Abs(slipAngle * Mathf.Rad2Deg));
            float latVelocityNorm = Mathf.InverseLerp(0f, 10f, Mathf.Abs(vehicleBody.linearVelocity.magnitude)); // Normalize angular velocity
            float latVolume = Mathf.Clamp(latSlipNorm * latVelocityNorm, 0, hub.maxSquealVolume);
            hub.slipAngleSquealSource.volume = latVolume;
            hub.slipAngleSquealSource.pitch = Mathf.Lerp(hub.minSquealPitch, hub.maxSquealPitch, latSlipNorm * latVelocityNorm);
            if (latSlipActive && !hub.slipAngleSquealSource.isPlaying)
            hub.slipAngleSquealSource.Play();
            else if (!latSlipActive && hub.slipAngleSquealSource.isPlaying)
            hub.slipAngleSquealSource.Stop();
        }

        private void DrawTireRay(Ray ray)
        {
            Color rayColor = isGrounded ? Color.green : Color.red;
            Debug.DrawRay(ray.origin, ray.direction * wheelUnloadedRadius, rayColor);
        }
        private void DrawTireForces(Vector3 hitPointLocal, Vector4 tireForces)
        {
            float forceScaleLong = 0.0005f; // Scale down for visualization
            float forceScaleLat = 0.0005f; // Scale down for visualization
            // Draw Fx (longitudinal)
            Debug.DrawRay(hitPointLocal, forceScaleLong * tireForces.x * transform.forward, Color.red);
            // Draw Fy (lateral)
            Debug.DrawRay(hitPointLocal, forceScaleLat * tireForces.z * transform.right, Color.green);
            // Draw Mz (aligning torque)
            Debug.DrawRay(hitPointLocal, forceScaleLat * tireForces.w * transform.up, Color.blue);
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

        public Vector3 GetContactForce()
        {
            if (isGrounded)
            {
                return normalLoad * -transform.up; // Approximation of vertical force
            }
            return Vector3.zero;
        }
        public float GetAlignmentTorque()
        {
            return alignmentTorque;
        }
    }
}

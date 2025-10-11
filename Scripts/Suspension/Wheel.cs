using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
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
        [SerializeField] private float nominalLoad = 3500f; // Fz0

        [Header("Tire Model")]
        public TireFrictionModel tireFrictionModel = TireFrictionModel.MF;
        public LayerMask layerMask = ~0; // Layers to consider for ground contact
        public Vector2 frictionCoefficient = new(1.0f, 1.0f); // (longitudinal mu_x, lateral mu_y)

        [Header("Wheel State (Runtime)")]
        public bool isGrounded = false; // Is the wheel in contact with the ground
        public float wheelAngularVelocity = 0f; // rad/s
        public float wheelTorque = 0f; // Torque applied to wheel from drivetrain
        public Vector3 wheelVelocity = Vector3.zero;
        public Vector3 contactPatchVelocity = Vector3.zero;
        private Vector3 hitPoint = Vector3.zero;
        [SerializeField] private float slipRatio = 0f; // κ
        [SerializeField] private float slipAngle = 0f; // α
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
        private float currTirePenDistance = 0f;
        private float tirePenetrationVelocity = 0f;
        private float wheelStiffnessForce = 0f;
        private float wheelDampingForce = 0f;
        private float alignmentTorque = 0f;

        // Add a cap for the maximum vertical force
        private const float maxVerticalForce = 200000f;

        public Tire tireModel;
        private TireInput tireInput;

        void Start()
        {
            hubBody = hub.hubBody;
            vehicleBody = hub.vehicleBody;
            wheelUnloadedRadius = hub.wheelUnloadedRadius;
            wheelInertia = 0.5f * hub.wheelMass * wheelUnloadedRadius * wheelUnloadedRadius;
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
        }
        public void Step()
        {
            Vector3 wheelCenter = hub.wheelCenter;

            // Calculate wheel velocity
            wheelVelocity = hubBody.GetPointVelocity(wheelCenter);

            // Raycast setup
            float rayLength = wheelUnloadedRadius;

            Vector3 leftRayOrigin = wheelCenter - 0.5f * hub.wheelWidth * transform.right;
            Vector3 centerRayOrigin = wheelCenter;
            Vector3 rightRayOrigin = wheelCenter + 0.5f * hub.wheelWidth * transform.right;

            Ray[] rays = {
                new (leftRayOrigin, -transform.up),
                new (centerRayOrigin, -transform.up),
                new (rightRayOrigin, -transform.up),
            };

            Vector3 averageHitPoint = Vector3.zero;
            Vector3 weightedHitPointSum = Vector3.zero;
            // TODO: find better weighting to prevent harsh jumps between rays
            float weightSum = 0f;
            float sharpness = 100f;
            Vector3 averageHitNormal = Vector3.zero;
            float averageDistance = 0f;
            int hitAmount = 0;
            isGrounded = false;

            foreach (Ray ray in rays)
            {
                if (Physics.Raycast(ray, out RaycastHit hit, rayLength, layerMask))
                {
                    float weight = Mathf.Exp(-hit.distance * sharpness);
                    weightedHitPointSum += hit.point * weight;
                    weightSum += weight;
                    averageDistance += hit.distance;
                    averageHitNormal += hit.normal;
                    isGrounded = true;
                    hitAmount++;
                    DrawTireRay(ray);
                }
            }

            if (hitAmount > 0)
            {
                averageHitPoint = weightedHitPointSum / weightSum;
                averageDistance /= hitAmount;
                averageHitNormal.Normalize();
            }

            if (isGrounded)
            {
                hitPoint = averageHitPoint;
                // Contact patch velocity relative to the road surface
                contactPatchVelocity = wheelVelocity - wheelAngularVelocity * wheelLoadedRadius * transform.forward;

                // Hookes law
                // This keeps wheels above ground, the suspension is handled by spring joints
                currTirePenDistance = rayLength - averageDistance;

                tirePenetrationVelocity = Vector3.Dot(hubBody.GetPointVelocity(averageHitPoint), averageHitNormal);

                // TODO: make the constant a parameter that makes sense, pressure?
                wheelStiffnessForce = maxVerticalForce * currTirePenDistance;
                wheelDampingForce = -tirePenetrationVelocity * hub.radialTireStiffness;

                // Sum vertical force and clamp to max
                float verticalForce = wheelStiffnessForce + wheelDampingForce;
                verticalForce = Mathf.Clamp(verticalForce, -maxVerticalForce, maxVerticalForce);

                // Calculate loaded and effective radius
                wheelLoadedRadius = wheelUnloadedRadius * (1 - currTirePenDistance / wheelUnloadedRadius);
                float theta = Mathf.Acos(wheelLoadedRadius / wheelUnloadedRadius);
                float omega = wheelUnloadedRadius * (theta * theta) / 2f;
                wheelEffectiveRadius = wheelLoadedRadius + 2 * omega / 3f;

                // Vertical load on tire
                normalLoad = verticalForce;
                if (normalLoad < float.Epsilon)
                {
                    normalLoad = 0f;
                }


                // Calculate slip ratio and slip angle
                slipRatio = CalculateLongitudinalSlip();
                slipAngle = CalculateSlipAngle();

                // Tire model input
                tireInput.kap = slipRatio;
                tireInput.alph = slipAngle;
                tireInput.Fz = normalLoad;
                tireInput.gam = hub.camberAngle * Mathf.Deg2Rad;
                tireInput.Fz0 = nominalLoad;
                tireInput.r0 = wheelUnloadedRadius;
                tireInput.mu = frictionCoefficient;

                // Calculate road forces (Fx, 0, Fy, Mz)
                Vector4 tireForces = tireModel.GetForcesAndTorque(ref tireInput);
                Vector3 roadForce = tireForces.x * Vector3.ProjectOnPlane(transform.forward, averageHitNormal).normalized
                                  + tireForces.z * Vector3.ProjectOnPlane(transform.right, averageHitNormal).normalized;


                if(Mathf.Abs(verticalForce) > Mathf.Epsilon)
                    hubBody.AddForceAtPosition(verticalForce * averageHitNormal, averageHitPoint, ForceMode.Force);

                // Apply road forces to vehicle body at contact patch
                vehicleBody.AddForceAtPosition(roadForce, averageHitPoint, ForceMode.Force);

                // Add aligning torque
                alignmentTorque = tireForces.w;
                if (Mathf.Abs(alignmentTorque) > Mathf.Epsilon)
                    hubBody.AddTorque(alignmentTorque * transform.up, ForceMode.Force);

                // Calculate rolling resistance moment
                float rollingResistanceMoment = tireModel.GetRollingResistanceMoment();

                // Apply rolling resistance to wheel angular velocity
                float rollingResistanceTorque = rollingResistanceMoment * Mathf.Sign(wheelAngularVelocity);
                float rollingResistanceDeceleration = rollingResistanceTorque / wheelInertia;

                if (Mathf.Abs(wheelAngularVelocity) >= Mathf.Abs(rollingResistanceDeceleration * Time.fixedDeltaTime))
                {
                    wheelAngularVelocity -= rollingResistanceDeceleration * Time.fixedDeltaTime;
                }
                else
                {
                    wheelAngularVelocity = 0f;
                }

                // Longitudinal force (Fz in unity coordinates)
                float Fy = tireForces.x;

                // Apply reaction torque from tire force to wheel angular velocity
                float tireTorque = -Fy * wheelEffectiveRadius; // Negative: tire force resists wheel spin
                wheelAngularAcceleration = tireTorque / wheelInertia;
                wheelAngularVelocity += wheelAngularAcceleration * Time.fixedDeltaTime;


                // Draw tire forces
                DrawTireForces(ref averageHitPoint, ref roadForce);
            }
            else
            {
                isGrounded = false;
                normalLoad = 0f;
                currTirePenDistance = 0f;
                tirePenetrationVelocity = 0f;
                wheelStiffnessForce = 0f;
                wheelDampingForce = 0f;
                wheelLoadedRadius = wheelUnloadedRadius;
                wheelEffectiveRadius = wheelUnloadedRadius;
            }

            if (hub.tireSquealClip != null)
            {
                UpdateTireSqueal();
            }

            if (!isPowered && isGrounded)
            {
                // Update wheel angular velocity based on ground contact
                float wheelLinearVelocity = Vector3.Dot(wheelVelocity, transform.forward);
                wheelAngularVelocity = wheelLinearVelocity / wheelEffectiveRadius;

                // Prevent NaN and negative angular velocity
                if (float.IsNaN(wheelAngularVelocity) || float.IsInfinity(wheelAngularVelocity))
                {
                    wheelAngularVelocity = 0f;
                }
            }
            else if (isPowered)
            {
                // Update wheel angular velocity based wheel torque

                wheelAngularAcceleration = wheelTorque / wheelInertia;
                wheelAngularVelocity += wheelAngularAcceleration * Time.fixedDeltaTime;
                wheelTorque = 0f; // Reset torque after applying
                                  // Prevent NaN
                if (float.IsNaN(wheelAngularVelocity) || float.IsInfinity(wheelAngularVelocity))
                {
                    wheelAngularVelocity = 0f;
                }
            }
            else
            {
                // TODO: wheel in air, apply drag
            }

        }
        // Calculate longitudinal slip / slip ratio κ
        private float CalculateLongitudinalSlip()
        {
            float slipEPS = 0.01f;
            Vector3 localWheelVelocity = transform.InverseTransformDirection(wheelVelocity);
            float wheelLongitudalVelocity = localWheelVelocity.z;

            if (Mathf.Abs(wheelLongitudalVelocity) < slipEPS)
            {
                return 0f;
            }

            float vel = Mathf.Abs(wheelLongitudalVelocity);

            // Reduce sensitivity at low speeds
            if (vel < 10f) vel = 10f;

            float slipRatio = (wheelAngularVelocity * wheelEffectiveRadius - wheelLongitudalVelocity) / vel;

            slipRatio = Mathf.Clamp(slipRatio, -5f, 5f);
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
            float denominator = forwardVel + hub.parentSuspension.GetTrackWidth() * Mathf.Abs(yawRate) + Mathf.Epsilon;
            if (Mathf.Abs(denominator) < slipEPS)
            {
                return 0f;
            }

            // Calculate slip angle
            float slipAngle = (hub.parentSuspension.GetWheelBase() * yawRate + lateralVel) / denominator;
            return Mathf.Asin(Mathf.Clamp(slipAngle, -1f, 1f)); // Clamp to valid range for Asin
        }
        // Calculate turn slip
        private float CalculateTurnSlip() // gamma = -(derivative of steer angle / V)
        {
            // Steer angle - this transform rotation
            float wheelLongitudinalVelocity = Vector3.Dot(wheelVelocity, transform.forward);
            if (Mathf.Abs(wheelLongitudinalVelocity) < float.Epsilon) return 0f;
            float steerAngle = transform.localEulerAngles.y;
            float steerRate = steerAngle / Time.fixedDeltaTime;
            return -steerRate / wheelLongitudinalVelocity;
        }
        public float GetAlignmentTorque()
        {
            return alignmentTorque;
        }
        public void ApplyTorque(float torque)
        {
            wheelTorque += torque;
        }
        public void ApplyBrake(float brakeTorque)
        {
            // float wheelFrictionTorque = Mathf.Sign(wheelAngularVelocity) * hub.maxBrakeTorque;
            float appliedBrakeTorque = brakeTorque;

            float brakeDeceleration = Mathf.Sign(wheelAngularVelocity) * appliedBrakeTorque / wheelInertia;
            if (Mathf.Abs(wheelAngularVelocity) >= Mathf.Abs(brakeDeceleration * Time.fixedDeltaTime))
            {
                wheelAngularVelocity -= brakeDeceleration * Time.fixedDeltaTime;
            }
            else
            {
                wheelAngularVelocity = 0f;
            }
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
        public float GetMaxTractionTorque()
        {
            return normalLoad * frictionCoefficient.x * wheelEffectiveRadius;
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
            float normalizedForce = Mathf.Clamp01(wheelStiffnessForce / hub.parentSuspension.springStiffness * 2);
            Color rayColor = Color.Lerp(Color.green, Color.red, normalizedForce);
            rayColor.a = 0.5f;
            Debug.DrawRay(ray.origin, ray.direction * wheelUnloadedRadius, rayColor);
        }
        private void DrawTireForces(ref Vector3 hitPoint, ref Vector3 roadForce)
        {
            float forceScaleZ = 0.001f; // Scale down for visualization
            float forceScaleY = 0.005f; // Scale down for visualization
            // Draw Fx
            Debug.DrawRay(hitPoint, forceScaleZ * roadForce.z * transform.forward, Color.red);
            // Draw Fz
            Debug.DrawRay(hitPoint, forceScaleY * roadForce.y * transform.right, Color.green);
        }
        void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            if (isGrounded) Gizmos.DrawSphere(hitPoint, 0.02f);
        }
    }
}

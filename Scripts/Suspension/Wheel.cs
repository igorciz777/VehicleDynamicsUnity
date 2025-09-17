using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class Wheel : MonoBehaviour
    {
        public enum TireFrictionModel
        {
            Pacejka,
            Brush
        }
        public TireFrictionModel tireFrictionModel = TireFrictionModel.Pacejka;
        public bool isPowered = false; // Is this wheel driven by the drivetrain
        public bool isGrounded = false; // Is the wheel in contact with the ground
        private Vector3 wheelPreviousPosition = Vector3.zero;
        private Vector3 contactPreviousPosition = Vector3.zero;
        public WheelHub hub;
        private Rigidbody hubBody;
        private Rigidbody vehicleBody;
        private float wheelRadius;

        private float previousSuspensionDistance = 0f;
        private float currentSuspensionDistance = 0f;
        private float springVelocity = 0f;
        private float springForce = 0f;
        private float damperForce = 0f;
        private float normalLoad = 0f; // Fz
        public float wheelTorque = 0f; // Torque applied to wheel from drivetrain
        private float slipRatio = 0f; // κ
        private float slipAngle = 0f; // α
        private float nominalLoad = 3500f; // Fz0
        public Vector3 wheelVelocity = Vector3.zero;
        public float wheelAngularVelocity = 0f; // rad/s
        private float wheelInertia = 0.0f;
        private float wheelAngularAcceleration = 0.0f;

        [Header("Tire Squeal Audio")]
        public AudioSource tireSquealSource;
        public float slipRatioThreshold = 0.1f;
        public float slipAngleThreshold = 5f;
        public float maxSquealVolume = 1.0f;
        public float minSquealPitch = 0.9f;
        public float maxSquealPitch = 1.3f;

        void Start()
        {
            hubBody = hub.hubBody;
            vehicleBody = hub.vehicleBody;
            wheelRadius = hub.parentSuspension.wheelRadius;
            wheelPreviousPosition = hub.wheelCenter;
        }
        public void Step()
        {
            Vector3 wheelCenter = hub.wheelCenter;

            // Calculate wheel velocity
            wheelVelocity = (wheelCenter - wheelPreviousPosition) / Time.fixedDeltaTime;
            wheelPreviousPosition = wheelCenter;

            Vector3 rayOrigin = wheelCenter;
            rayOrigin.y += wheelRadius;
            Ray ray = new(rayOrigin, -hub.parentSuspension.transform.up);

            float rayLength = wheelRadius;
            LayerMask layerMask = LayerMask.GetMask("Default");
            if (Physics.SphereCast(ray, wheelRadius, out RaycastHit hit, rayLength, layerMask))
            {
                // Hookes law
                // This keeps wheels above ground, the suspension is handled by spring joints
                previousSuspensionDistance = currentSuspensionDistance;
                currentSuspensionDistance = rayLength - hit.distance;

                springVelocity = (currentSuspensionDistance - previousSuspensionDistance) / Time.fixedDeltaTime;

                springForce = hub.wheelRate * currentSuspensionDistance;
                damperForce = hub.parentSuspension.damperConstant * springVelocity;

                Vector3 force = hub.parentSuspension.transform.up * (springForce + damperForce);
                hubBody.AddForceAtPosition(force, hit.point, ForceMode.Force);
                // Fz
                normalLoad = springForce + damperForce;
                if (normalLoad < float.Epsilon)
                {
                    normalLoad = 0f;
                }

                isGrounded = true;

                if (!isPowered)
                {
                    // Update wheel angular velocity based on ground contact
                    float wheelLinearVelocity = Vector3.Dot(wheelVelocity, transform.forward);
                    wheelAngularVelocity = wheelLinearVelocity / wheelRadius;

                    // Prevent NaN and negative angular velocity
                    if (float.IsNaN(wheelAngularVelocity) || float.IsInfinity(wheelAngularVelocity))
                    {
                        wheelAngularVelocity = 0f;
                    }
                }
                else
                {
                    // Update wheel angular velocity based wheel torque
                    wheelInertia = 0.5f * hub.parentSuspension.wheelMass * wheelRadius * wheelRadius;
                    wheelAngularAcceleration = wheelTorque / wheelInertia;
                    wheelAngularVelocity += wheelAngularAcceleration * Time.fixedDeltaTime;
                    wheelTorque = 0f; // Reset torque after applying
                                      // Prevent NaN
                    if (float.IsNaN(wheelAngularVelocity) || float.IsInfinity(wheelAngularVelocity))
                    {
                        wheelAngularVelocity = 0f;
                    }
                }

                // Calculate slip ratio and slip angle
                slipRatio = CalculateLongitudinalSlip();
                slipAngle = CalculateSideSlip();
                // Calculate road forces
                Vector3 roadForce = CalculateRoadForce();
                vehicleBody.AddForceAtPosition(roadForce, hit.point, ForceMode.Force);

                float Fx = Vector3.Dot(roadForce, transform.forward); // Longitudinal force at contact patch
                wheelInertia = 0.5f * hub.parentSuspension.wheelMass * wheelRadius * wheelRadius;

                // Apply reaction torque from tire force to wheel angular velocity
                float tireTorque = -Fx * wheelRadius; // Negative: tire force resists wheel spin
                wheelAngularAcceleration = tireTorque / wheelInertia;
                wheelAngularVelocity += wheelAngularAcceleration * Time.fixedDeltaTime;

                DrawRays(ray, hit);
                DrawTireForces(hit, ref roadForce);
            }
            else
            {
                isGrounded = false;
                normalLoad = 0f;
                currentSuspensionDistance = 0f;
                springVelocity = 0f;
                springForce = 0f;
                damperForce = 0f;
            }

            if (tireSquealSource != null)
            {
                UpdateTireSqueal();
            }

        }
        public float GetSpringCompression()
        {
            float compression = currentSuspensionDistance / hub.parentSuspension.springLength;
            return compression;
        }
        // Calculate slip ratio
        private float CalculateLongitudinalSlip()
        {
            Vector3 localWheelVelocity = transform.InverseTransformDirection(wheelVelocity);
            float Vcx = localWheelVelocity.z; // Car speed in the wheel's local frame
            float tyreSurfaceSpeed = wheelAngularVelocity * wheelRadius; // Tyre surface speed (m/s)

            if (Mathf.Abs(Vcx) < float.Epsilon) return 0f; // Avoid division by zero

            return (tyreSurfaceSpeed - Vcx) / Mathf.Abs(Vcx); // Longitudinal slip ratio
        }

        // Calculate slip angle
        private float CalculateSideSlip()
        {
            Vector3 localWheelVelocity = transform.InverseTransformDirection(wheelVelocity);
            float Vcx = localWheelVelocity.z;
            float Vcy = localWheelVelocity.x;
            if (Mathf.Abs(Vcx) < float.Epsilon) return 0f;
            return Mathf.Atan2(-Vcy, Mathf.Abs(Vcx)) * Mathf.Rad2Deg;
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
        // Calculate road force based on slip ratio and slip angle
        private Vector3 CalculateRoadForce()
        {
            if (normalLoad < float.Epsilon) return Vector3.zero;

            Pacejka tireModel = new();
            float longitudinalForce = tireModel.CombinedLongitudinal(slipRatio, slipAngle, normalLoad, hub.camberAngle, nominalLoad);
            float lateralForce = tireModel.CombinedLateral(slipRatio, slipAngle, normalLoad, hub.camberAngle, nominalLoad);

            Vector3 forwardForce = transform.forward * longitudinalForce;
            Vector3 lateralForceVector = transform.right * lateralForce;

            return forwardForce + lateralForceVector;
        }
        public void ApplyTorque(float torque)
        {
            wheelTorque += torque;
        }
        public void ApplyBrake(float brakeTorque)
        {
            // Apply braking torque to wheel
            float brakeAngularDeceleration = brakeTorque / (0.5f * hub.parentSuspension.wheelMass * wheelRadius * wheelRadius);
            wheelAngularVelocity -= brakeAngularDeceleration * Time.fixedDeltaTime;
            if (wheelAngularVelocity < 0f) wheelAngularVelocity = 0f; // Prevent negative spin
        }
        void UpdateTireSqueal()
        {
            // Check if slip exceeds thresholds
            bool slipActive = Mathf.Abs(slipRatio) > slipRatioThreshold || Mathf.Abs(slipAngle) > slipAngleThreshold;

            if (slipActive && isGrounded)
            {
                // Normalize slip for volume/pitch mapping
                float slip = Mathf.Max(
                    Mathf.InverseLerp(slipRatioThreshold, 0.3f, Mathf.Abs(slipRatio)),
                    Mathf.InverseLerp(slipAngleThreshold, 15f, Mathf.Abs(slipAngle))
                );
                float volume = Mathf.Clamp(slip, 0, maxSquealVolume);
                tireSquealSource.volume = volume;
                tireSquealSource.pitch = Mathf.Lerp(minSquealPitch, maxSquealPitch, volume);

                if (!tireSquealSource.isPlaying)
                    tireSquealSource.Play();
            }
            else
            {
                tireSquealSource.Stop();
            }
        }
        private void DrawRays(Ray ray, RaycastHit hit)
        {
            float normalizedForce = Mathf.Clamp01(springForce / hub.parentSuspension.springConstant * 2);
            Color rayColor = Color.Lerp(Color.green, Color.red, normalizedForce);
            Debug.DrawRay(ray.origin, hub.vehicleBody.transform.up * wheelRadius, rayColor);
            Debug.DrawRay(hit.point, hit.normal * 0.2f, Color.blue);
        }
        private void DrawTireForces(RaycastHit hit, ref Vector3 roadForce)
        {
            float forceScaleZ = 0.001f; // Scale down for visualization
            float forceScaleY = 0.005f; // Scale down for visualization
            // Draw Fx
            Debug.DrawRay(hit.point, forceScaleZ * roadForce.z * transform.forward, Color.red);
            // Draw Fy
            Debug.DrawRay(hit.point, forceScaleY * roadForce.y * transform.right, Color.green);
        }

    }
    public class Brush
    {
        // Single Contact Brush tire model

    }
    public class Pacejka
    {
        // Pac2004 'Magic Formula' tire model
        // Coefficients
        // Pure Longitudinal
        public float p_Cx1 = 1.65f;
        public float p_Dx1 = 1.0f, p_Dx2 = 0.0f;
        public float p_Ex1 = -0.5f, p_Ex2 = 0.0f, p_Ex3 = 0.0f, p_Ex4 = 0.0f;
        public float p_Kx1 = 12.0f, p_Kx2 = 10.0f, p_Kx3 = -0.6f;
        public float p_Hx1 = 0.0f, p_Hx2 = 0.0f;
        public float p_Vx1 = 0.0f, p_Vx2 = 0.0f;

        // Pure Lateral
        public float p_Cy1 = 1.3f;
        public float p_Dy1 = 1.0f, p_Dy2 = 0.0f, p_Dy3 = 0.0f;
        public float p_Ey1 = -1.0f, p_Ey2 = 0.0f, p_Ey3 = 0.0f, p_Ey4 = 0.0f;
        public float p_Ky1 = 10.0f, p_Ky2 = 1.5f, p_Ky3 = 0.0f, p_Ky4 = 2.0f, p_Ky5 = 0.0f, p_Ky6 = 2.5f, p_Ky7 = 0.0f;
        public float p_Hy1 = 0.0f, p_Hy2 = 0.0f;
        public float p_Vy1 = 0.0f, p_Vy2 = 0.0f, p_Vy3 = 0.15f, p_Vy4 = 0.0f;

        // Pure Aligning Torque
        public float q_Bz1 = 6.0f, q_Bz2 = -4.0f, q_Bz3 = 0.6f, q_Bz4 = 0.0f, q_Bz5 = 0.0f, q_Bz9 = 0.0f, q_Bz10 = 0.7f;
        public float q_Cz1 = 1.05f;
        public float q_Dz1 = 0.12f, q_Dz2 = -0.03f, q_Dz3 = 0.0f, q_Dz4 = -1.0f, q_Dz6 = 0.0f, q_Dz7 = 0.0f, q_Dz8 = 0.6f, q_Dz9 = 0.2f, q_Dz10 = 0.0f, q_Dz11 = 0.0f;
        public float q_Ez1 = -10.0f, q_Ez2 = 0.0f, q_Ez3 = 0.0f, q_Ez4 = 0.0f, q_Ez5 = 0.0f;
        public float q_Hz1 = 0.0f, q_Hz2 = 0.0f, q_Hz3 = 0.0f, q_Hz4 = 0.0f;

        // Combined Longitudinal
        public float r_Bx1 = 5.0f, r_Bx2 = 8.0f, r_Bx3 = 0.0f;
        public float r_Cx1 = 1.0f;
        public float r_Hx1 = 0.0f;

        // Combined Lateral
        public float r_By1 = 7.0f, r_By2 = 2.5f, r_By3 = 0.0f, r_By4 = 0.0f;
        public float r_Cy1 = 1.0f;
        public float r_Hy1 = 0.02f;
        public float r_Vy1 = 0.0f, r_Vy2 = 0.0f, r_Vy3 = -0.2f, r_Vy4 = 14.0f, r_Vy5 = 1.9f, r_Vy6 = 10.0f;

        // Combined Aligning Torque
        public float s_Sz1 = 0.0f, s_Sz2 = -0.1f, s_Sz3 = -1.0f, s_Sz4 = 0.0f;

        public void LoadParametersFromJSON(string jsonString)
        {
            // Load parameters from JSON string
            JsonUtility.FromJsonOverwrite(jsonString, this);
        }

        public float PureLongitudinal(
    float kappa,  // Longitudinal slip
    float Fz,     // Vertical load [N]
    float gamma,  // Camber angle [deg]
    float dpi,    // Pressure variation (usually 0)
    float Fz0,    // Nominal vertical load
                  // Scalars
    float LFZO = 1.0f, float LMUX = 1.0f, float LKX = 1.0f, float LCX = 1.0f, float LEX = 1.0f, float LHX = 1.0f, float LVX = 1.0f
)
        {
            // Adapted nominal load
            float Fz0_prime = LFZO * Fz0;
            float dfz = (Fz - Fz0_prime) / Fz0_prime;

            // Shape factor
            float Cx = p_Cx1 * LCX;

            // Friction coefficient (with scaling and pressure)
            float mux = (p_Dx1 + p_Dx2 * dfz) * (1 + 0 * dpi + 0 * dpi * dpi) * (1 - 0 * gamma * gamma) * LMUX;
            if (Fz == 0) mux = 0;

            // Peak factor
            float Dx = mux * Fz;

            // Stiffness
            float Kxk = Fz * (p_Kx1 + p_Kx2 * dfz) * Mathf.Exp(p_Kx3 * dfz) * (1 + 0 * dpi + 0 * dpi * dpi) * LKX;

            // Sign handling
            float signDx = Mathf.Sign(Dx);
            if (signDx == 0) signDx = 1;

            // Bx
            float Bx = Kxk / (Cx * Dx + Mathf.Epsilon * signDx);

            // Horizontal shift
            float SHx = (p_Hx1 + p_Hx2 * dfz) * LHX;
            float kappax = kappa + SHx;

            // Curvature
            float Ex = (p_Ex1 + p_Ex2 * dfz + p_Ex3 * dfz * dfz) * (1 - p_Ex4 * Mathf.Sign(kappax)) * LEX;
            if (Ex > 1) Ex = 1;

            // Vertical shift
            float SVx = Fz * (p_Vx1 + p_Vx2 * dfz) * LVX * LMUX;

            // Final force
            float Fx0 = Dx * Mathf.Sin(Cx * Mathf.Atan(Bx * kappax - Ex * (Bx * kappax - Mathf.Atan(Bx * kappax)))) + SVx;

            return Fx0;
        }
        public float PureLateral(
    float alpha,  // slip angle in degrees
    float Fz,     // vertical load [N]
    float gamma,  // camber angle in degrees
    float dpi,    // inflation pressure variation
    float Fz0,    // nominal vertical load
                  // Scalars
    float LFZO = 1.0f, float LMUY = 1.0f, float LKYC = 1.0f, float LCY = 1.0f, float LEY = 1.0f, float LHY = 1.0f, float LVY = 1.0f
)
        {
            // Adapted nominal load
            float Fz0_prime = LFZO * Fz0;
            float dfz = (Fz - Fz0_prime) / Fz0_prime;

            // Convert slip angle and camber to radians
            float alphaRad = alpha * Mathf.Deg2Rad;
            float gammaRad = gamma * Mathf.Deg2Rad;

            // Shape factor
            float Cy = p_Cy1 * LCY;

            // Friction coefficient (with scaling and pressure)
            float muy = (p_Dy1 + p_Dy2 * dfz) * (1 + 0 * dpi + 0 * dpi * dpi) * (1 - 0 * gammaRad * gammaRad) * LMUY;

            // Peak factor
            float Dy = muy * Fz * 1.0f; // ζ2 assumed 1

            // Cornering stiffness (Kya) - book formula
            float Kya = p_Ky1 * Fz0_prime * (1 + 0 * dpi) * (1 - p_Ky3 * Mathf.Abs(gammaRad)) *
                Mathf.Sin(p_Ky4 * Mathf.Atan((Fz / Fz0_prime) / ((p_Ky2 + p_Ky5 * gammaRad * gammaRad) * (1 + p_Ky7 * dpi)))) * LKYC;

            // Sign handling
            float signDy = Mathf.Sign(Dy);
            if (signDy == 0) signDy = 1;

            // Stiffness factor
            float By = Kya / (Cy * Dy + Mathf.Epsilon * signDy);

            // Curvature
            float Ey = (p_Ey1 + p_Ey2 * dfz) * (1 + 0 * gammaRad * gammaRad - (p_Ey3 + p_Ey4 * gammaRad) * Mathf.Sign(alphaRad)) * LEY;
            if (Ey > 1) Ey = 1;

            // Horizontal shift
            float SHy = (p_Hy1 + p_Hy2 * dfz) * LHY;

            // Shifted slip angle
            float alpha_y = alphaRad + SHy;

            // Vertical shift
            float SVy = Fz * (p_Vy1 + p_Vy2 * dfz + (p_Vy3 + p_Vy4 * dfz) * gammaRad) * LVY * LMUY;

            // Final lateral force
            float Fy0 = Dy * Mathf.Sin(Cy * Mathf.Atan(By * alpha_y - Ey * (By * alpha_y - Mathf.Atan(By * alpha_y)))) + SVy;
            return Fy0;
        }

        public float PureAligningTorque(float slipAngle, float normalLoad, float camberAngle)
        {
            // TODO: Mz force
            return 0f; // Placeholder
        }
        public float CombinedLongitudinal(
            float slipRatio,
            float slipAngle,
            float normalLoad,
            float camberAngle,
            float nominalLoad)
        {
            float dfz = (normalLoad - nominalLoad) / nominalLoad;
            // Shape factor
            float Cxa = r_Cx1;

            // Curvature factor
            float Exa = 0 + 0 * dfz;
            if (Exa > 1) Exa = 1;

            // Shift factor
            float SHxa = r_Hx1;

            // Stiffness factor
            float Bxa = (r_Bx1 + r_Bx3 * Mathf.Pow(camberAngle, 2)) * Mathf.Cos(Mathf.Atan(r_Bx2 * slipRatio));

            // Adjusted slip angle
            float alphaStar = slipAngle + SHxa;

            // Base reduction factor
            float Gxa0 = Mathf.Cos(Cxa * Mathf.Atan(Bxa * SHxa - Exa * (Bxa * SHxa - Mathf.Atan(Bxa * SHxa))));

            // Combined reduction factor
            float Gxa = Mathf.Cos(Cxa * Mathf.Atan(Bxa * alphaStar - Exa * (Bxa * alphaStar - Mathf.Atan(Bxa * alphaStar)))) / Gxa0;

            // Base longitudinal force
            float Fx0 = PureLongitudinal(slipRatio, normalLoad, camberAngle, 0, nominalLoad);

            // Combined longitudinal force
            return Gxa * Fx0;
        }
        public float CombinedLateral(
            float slipRatio,
            float slipAngle,
            float normalLoad,
            float camberAngle,
            float nominalLoad)
        {
            float dfz = (normalLoad - nominalLoad) / nominalLoad;
            // Friction coefficient
            float muy = normalLoad > 0 ? (r_Vy1 + r_Vy2 * dfz + r_Vy3 * camberAngle) : 0;

            // Scaling factors
            float DVyk = muy * normalLoad * Mathf.Cos(Mathf.Atan(r_Vy4 * slipAngle));
            float SVyk = DVyk * Mathf.Sin(r_Vy5 * Mathf.Atan(r_Vy6 * slipRatio));

            // Shift and curvature factors
            float SHyk = 0 + 0 * dfz;
            float Eyk = Mathf.Min(0 + 0 * dfz, 1.0f);
            float Cyk = r_Cy1;

            // Stiffness factor
            float Byk = (r_By1 + r_By4 * Mathf.Pow(camberAngle, 2)) * Mathf.Cos(Mathf.Atan(r_By2 * (slipAngle - r_By3)));

            // Adjusted slip ratio
            float kappas = slipRatio + SHyk;

            // Base reduction factor
            float Gyk0 = Mathf.Cos(Cyk * Mathf.Atan(Byk * SHyk - Eyk * (Byk * SHyk - Mathf.Atan(Byk * SHyk))));

            // Combined reduction factor
            float Gyk = Mathf.Cos(Cyk * Mathf.Atan(Byk * kappas - Eyk * (Byk * kappas - Mathf.Atan(Byk * kappas)))) / Gyk0;

            // Base lateral force
            float Fy0 = PureLateral(slipAngle, normalLoad, camberAngle, 0, nominalLoad);

            // Combined lateral force
            return Gyk * Fy0 + SVyk;
        }
    }
}

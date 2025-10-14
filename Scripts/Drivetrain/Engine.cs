using UnityEngine;

namespace VehicleDynamics
{
    public class Engine : MonoBehaviour
    {
        const float RPM_TO_RADS = Mathf.PI / 30f; // 1 RPM = π/30 rad/s
        const float RADS_TO_RPM = 30f / Mathf.PI; // 1 rad/s = 30/π RPM
        [Header("Engine Parameters")]
        public float rpmIdle = 800f;
        public float rpmMax = 9000f;
        public bool hasRevLimiter = false;
        public float rpmLimiter = 8000f;
        public float flywheelInertia = 0.3f; // kg*m^2
        public float frictionTorque = 5f; // constant friction torque (Nm)
        public float dynamicFriction = 0.001f; // scaling with RPM (Nm per RPM)
        public AnimationCurve torqueCurve = new(
            new Keyframe(0, 0),
            new Keyframe(500, 50),
            new Keyframe(1000, 65),
            new Keyframe(1500, 75),
            new Keyframe(2000, 85),
            new Keyframe(3000, 95),
            new Keyframe(4000, 100),
            new Keyframe(5000, 110),
            new Keyframe(6000, 95),
            new Keyframe(7000, 85),
            new Keyframe(7500, 70),
            new Keyframe(8000, 40),
            new Keyframe(9000, 25),
            new Keyframe(10000, 0)
        );
        public AnimationCurve throttleResponseCurve = new(
            new Keyframe(0, 0),
            new Keyframe(0.5f, 0.3f),
            new Keyframe(1f, 1f)
        );
        [Header("Magic Torque Formula Parameters")]
        public float A = -10f; // Magnitude factor
        public float B = 0.15f; // Throttle interval factor
        public float C = 2f; // Low speed magnitude factor
        public float D = 1.5f; // High speed magnitude factor
        [Header("Engine Output")]
        public float engineRpm = 0f;
        public float grossTorque = 0f;
        public float netTorque = 0f;
        public float engineAngularVelocity = 0f;
        public float engineAngularAcceleration = 0f;
        public float engineThrottle = 0f;
        [Header("Engine Audio")]
        public AudioSource engineSound;
        public float audioPitchMultiplier = 2f;
        void Start()
        {
            engineAngularVelocity = rpmIdle * RPM_TO_RADS;

            // Smoothen curves
            for (int i = 0; i < torqueCurve.length; i++)
                torqueCurve.SmoothTangents(i, 0f);
            for (int i = 0; i < throttleResponseCurve.length; i++)
                throttleResponseCurve.SmoothTangents(i, 0f);
        }
        public void Step(float dt, float throttleInput=0f, float loadTorque = 0f)
        {
            // Throttle mapping
            float throttleMapped = throttleResponseCurve.Evaluate(throttleInput);

            // Rev limiter
            if (hasRevLimiter && engineRpm > rpmLimiter)
                throttleMapped = 0f;

            // Calculate engine torque using magic formula
            engineThrottle = throttleMapped * 100f;
            grossTorque = CalculateMagicTorque(engineRpm, engineThrottle);

            // Friction losses
            float frictionFromRPM = frictionTorque + engineRpm * dynamicFriction;
            float maxFriction = Mathf.Abs(engineAngularVelocity / dt * flywheelInertia);
            float frictionLosses = Mathf.Min(Mathf.Abs(frictionFromRPM), maxFriction) * Mathf.Sign(engineAngularVelocity);

            // Net torque
            netTorque = grossTorque - frictionLosses - loadTorque;

            // Angular acceleration
            engineAngularAcceleration = netTorque / flywheelInertia;
            // Update angular velocity
            engineAngularVelocity += engineAngularAcceleration * dt;

            engineRpm = engineAngularVelocity * RADS_TO_RPM;
            
            UpdateEngineAudio();

        }
        private void UpdateEngineAudio()
        {
            if (engineSound == null) return;
            engineSound.pitch = 0.5f + engineRpm / rpmMax * audioPitchMultiplier;
            engineSound.volume = 0.3f + engineThrottle / 100f * 0.7f;
        }
        private float CalculateMagicTorque(float rpm, float throttlePercent)
        {
            float fullLoadTorque = torqueCurve.Evaluate(rpm);
            float scale = Mathf.Pow(
                1f + Mathf.Exp(A - B * throttlePercent),
                C * Mathf.Pow(rpm, D)
            );
            if (float.IsNaN(scale) || float.IsInfinity(scale))
                scale = 1f;
            float result = fullLoadTorque / scale;
            if (float.IsNaN(result) || float.IsInfinity(result))
                result = 0f;
            return result;
        }
    }
}
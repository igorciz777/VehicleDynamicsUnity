using System.Collections;
using UnityEngine;

namespace VehicleDynamics
{
    public class Engine : MonoBehaviour
    {
        public enum EngineState
        {
            Off,
            Ignition,
            Running,
        }
        const float RPM_TO_RADS = Mathf.PI / 30f; // 1 RPM = π/30 rad/s
        const float RADS_TO_RPM = 30f / Mathf.PI; // 1 rad/s = 30/π RPM
        [Header("Engine Parameters")]
        public float rpmIdle = 800f;
        public float rpmMax = 9000f; // Physical limit
        public bool hasRevLimiter = false;
        public float rpmLimiter = 8000f;
        public float limiterDuration = 0.1f; // seconds
        public float flywheelInertia = 0.3f; // kg*m^2
        public float frictionTorque = 5f; // constant friction torque (Nm)
        public float dynamicFriction = 0.001f; // scaling with RPM (Nm per RPM)
        private bool isThrottleLimiterActive = false; // coroutine flag
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
        [Header("Turbo Settings")]
        public bool turboEnabled = false;
        public float turboGain = 1.0f; // max boost fraction (0..1) relative to engine torque
        public float turboBoostFactor = 0.5f; // how much extra torque relative to grossTorque when fully boosted
        public float LagRate1 = 10f; // lag1 rate
        public float LagRate2 = 5f; // base lag2 rate
        private float lag1 = 0f;
        private float lag2 = 0f;
        private float prevEngineThrottle = 0f;
        [Header("Engine Output")]
        public float engineRpm = 0f;
        public float grossTorque = 0f;
        public float netTorque = 0f;
        public float engineAngularVelocity = 0f;
        public float engineAngularAcceleration = 0f;
        public float engineThrottle = 0f;
        [Header("Engine Audio")]
        public AudioClip engineSoundClip;
        private AudioSource engineSound;
        public float audioPitchMultiplier = 2f;
        [Header("Turbo Audio")]
        public AudioClip turboSoundClip;
        private AudioSource turboSound;
        public float turboSoundPitchMultiplier = 1.5f;
        public void Init()
        {
            engineAngularVelocity = rpmIdle * RPM_TO_RADS;

            // Smoothen curves
            for (int i = 0; i < torqueCurve.length; i++)
                torqueCurve.SmoothTangents(i, 0f);
            for (int i = 0; i < throttleResponseCurve.length; i++)
                throttleResponseCurve.SmoothTangents(i, 0f);

            // Setup engine audio
            if (engineSoundClip != null)
            {
                engineSound = gameObject.AddComponent<AudioSource>();
                engineSound.clip = engineSoundClip;
                engineSound.loop = true;
                engineSound.playOnAwake = true;
                engineSound.spatialBlend = 1.0f;
                engineSound.volume = 0.0f;
                engineSound.dopplerLevel = 0f;
                engineSound.Play();
            }
            // Setup turbo audio
            if (turboSoundClip != null)
            {
                turboSound = gameObject.AddComponent<AudioSource>();
                turboSound.clip = turboSoundClip;
                turboSound.loop = true;
                turboSound.playOnAwake = true;
                turboSound.spatialBlend = 1.0f;
                turboSound.volume = 0.0f;
                turboSound.dopplerLevel = 0f;
                turboSound.Play();
            }
        }
        public void Step(float dt, float throttleInput=0f, float loadTorque = 0f)
        {
            // Throttle mapping
            float throttleMapped = throttleResponseCurve.Evaluate(throttleInput);

            // Rev limiter
            if (hasRevLimiter && engineRpm > rpmLimiter && !isThrottleLimiterActive)
                StartCoroutine(ThrottleLimiterCoroutine(limiterDuration));
            if (isThrottleLimiterActive)
                throttleMapped = 0f;

            // Calculate engine torque using magic formula
            engineThrottle = throttleMapped * 100f;
            float clampRPM = Mathf.Max(0f, engineRpm);
            grossTorque = CalculateMagicTorque(clampRPM, engineThrottle);

            if (turboEnabled)
            {
                // target boost value in same scale as the ADAMS example (0..100)
                float boostTarget = throttleMapped * turboGain * 100f;

                // optional dynamic K2 influenced by throttle derivative (simple approximation)
                float throttleDerivative = 0f;
                if (dt > 0f) throttleDerivative = (engineThrottle - prevEngineThrottle) / dt; // units: %/s

                float K2 = LagRate2 + Mathf.Clamp(throttleDerivative, -LagRate2, LagRate2);

                // integrate first-order lags (explicit Euler)
                lag1 += dt * LagRate1 * (boostTarget - lag1);
                lag2 += dt * K2 * (lag1 - lag2);

                // boost scaling
                float boost_torque_scaling = Mathf.Clamp01(lag2 / 100f);

                // combine normally aspirated torque and boosted component
                // grossTorque from magic formula treated as baseline (NA); boosted extra torque added
                float boostedExtra = grossTorque * turboBoostFactor * boost_torque_scaling;
                grossTorque += boostedExtra;

                prevEngineThrottle = engineThrottle;
            }

            // Valve float
            if (engineRpm > rpmMax)
            {
                float excessRpm = engineRpm - rpmMax;
                grossTorque -= excessRpm * frictionTorque; // arbitrary to use frictionTorque but works
            }

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

            // Prevent negative engine rotation
            engineAngularVelocity = Mathf.Max(0f, engineAngularVelocity);

            engineRpm = engineAngularVelocity * RADS_TO_RPM;

            UpdateEngineAudio();
            if (turboEnabled)
                UpdateTurboAudio(lag2);
            else if (turboSound != null)    
                turboSound.volume = 0f;
        }
        private void UpdateEngineAudio()
        {
            if (engineSound == null) return;
            engineSound.pitch = 0.5f + engineRpm / rpmMax * audioPitchMultiplier;
            engineSound.volume = 0.3f + engineThrottle / 100f * 0.7f;
            if (engineRpm < 100f) engineSound.volume = (0.3f + engineThrottle / 100f * 0.7f) * (engineRpm / 100f);
        }
        private void UpdateTurboAudio(float boostLevel)
        {
            if (turboSound == null) return;
            turboSound.pitch = 0.5f + boostLevel / 100f * turboSoundPitchMultiplier;
            turboSound.volume = boostLevel / 100f * 0.2f;
            if (engineRpm < 100f) turboSound.volume = boostLevel / 100f * 0.2f * (engineRpm / 100f);
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
        public IEnumerator ThrottleLimiterCoroutine(float duration)
        {
            isThrottleLimiterActive = true;
            yield return new WaitForSeconds(duration);
            isThrottleLimiterActive = false;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class Engine : MonoBehaviour
    {
        public enum EnergyType
        {
            Gasoline,
            Diesel,
            Electric,
            Hybrid
        }
        const float RPM_TO_RADS = Mathf.PI / 30f; // 1 RPM = π/30 rad/s
        const float RADS_TO_RPM = 30f / Mathf.PI; // 1 rad/s = 30/π RPM

        // Engine parameters
        [Header("Engine Parameters")]
        public float rpmIdle = 800f;
        public float rpmMax = 9000f;
        public bool hasRevLimiter = false;
        public float rpmLimiter = 8000f;
        public float rpmLimiterBounce = 200f;
        public float flywheelInertia = 0.08f;
        public float friction = 0.015f;
        public float dynamicFriction = 0.01f; // Scales with RPM - engine braking
        public float throttle = 0f;
        public float audioPitchMultiplier = 2f;
        public bool drivetrainEngaged = false; // For clutch engagement or neutral gear

        public EnergyType energyType = EnergyType.Gasoline;
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
        public float enginePower = 0f;
        public float engineTorque = 0f; // Nm
        public float engineRpm = 0f;
        private bool limiterBounce = false;
        public AudioSource engineSound;
        public float engineAngularVelocity = 0f;

        private void Start()
        {
            if (engineSound == null)
            {
                engineSound = GetComponent<AudioSource>();
            }
            engineAngularVelocity = rpmIdle * RPM_TO_RADS;

            // Smoothen curves
            for (int i = 0; i < torqueCurve.length; i++)
            {
                torqueCurve.SmoothTangents(i, 0f);
            }
            for (int i = 0; i < throttleResponseCurve.length; i++)
            {
                throttleResponseCurve.SmoothTangents(i, 0f);
            }
        }
        public void Step(float throttleInput, float driveshaftAngularVelocity = 0f)
        {
            throttleInput = Mathf.Clamp(throttleInput, 0f, 1f);
            throttle = throttleResponseCurve.Evaluate(throttleInput) * 100f;
            if (engineRpm > rpmLimiter && hasRevLimiter)
            {
                limiterBounce = true;
                throttle = 0f;
            }
            if (engineRpm > rpmLimiter - rpmLimiterBounce && limiterBounce)
            {
                throttle = 0f;
            }
            else
            {
                limiterBounce = false;
            }
            engineTorque = CalculateMagicTorque(engineRpm, throttle);

            // float engineBrakingTorque = dynamicFriction * engineRpm;
            // engineTorque -= engineBrakingTorque;

            // Prevent negative torque and NaN
            if (float.IsNaN(engineTorque) || float.IsInfinity(engineTorque) || engineTorque < float.Epsilon)
                engineTorque = 0f;

            // Update engine angular velocity if drivetrain is not engaged
            if (!drivetrainEngaged)
            {
                float engineAcceleration = (engineTorque - friction * engineRpm) / flywheelInertia;
                engineAngularVelocity += engineAcceleration * Time.fixedDeltaTime;
                engineRpm = engineAngularVelocity * RADS_TO_RPM;
            }
            else
            {
                engineAngularVelocity = driveshaftAngularVelocity;
                engineRpm = engineAngularVelocity * RADS_TO_RPM;
            }

            // Prevent negative RPM and NaN
            if (float.IsNaN(engineRpm) || float.IsInfinity(engineRpm)
                || engineRpm < float.Epsilon)
            {
                engineRpm = 0f;
                engineAngularVelocity = 0f;
            }

            UpdateEngineAudio();
        }

        private void UpdateEngineAudio()
        {
            if (engineSound == null) return;
            engineSound.pitch = 0.5f + engineRpm / rpmMax * audioPitchMultiplier;
            if (engineRpm < rpmIdle / 2f)
            {
                engineSound.volume = 0f;
            }
            else
            {
                engineSound.volume = 0.3f + throttle / 100f * 0.7f;
            }
        }

        private float CalculateMagicTorque(float rpm, float throttlePercent)
        {
            float fullLoadTorque = torqueCurve.Evaluate(rpm);
            float scale = Mathf.Pow(
                1f + Mathf.Exp(A - B * throttlePercent),
                C * Mathf.Pow(rpm, D)
            );
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < float.Epsilon)
                scale = 1f;
            float result = fullLoadTorque / scale;
            if (float.IsNaN(result) || float.IsInfinity(result) || result < float.Epsilon)
                result = 0f;
            return result;
        }
    }
}
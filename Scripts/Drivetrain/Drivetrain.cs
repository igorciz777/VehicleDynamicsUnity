using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class Drivetrain : MonoBehaviour
    {
        const float RPM_TO_RADS = Mathf.PI / 30f; // 1 RPM = π/30 rad/s
        const float RADS_TO_RPM = 30f / Mathf.PI; // 1 rad/s = 30/π RPM

        [Header("Engine Parameters")]
        public float rpmIdle = 800f;
        public float rpmMax = 9000f;
        public bool hasRevLimiter = false;
        public float rpmLimiter = 8000f;
        public float flywheelInertia = 0.3f; // kg*m^2
        public float friction = 0.015f;
        public float dynamicFriction = 0.01f; // Scales with RPM - engine braking
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

        [Header("Transmission Parameters")]
        public int currentGear = 1;
        public float[] gearRatios = new float[] { -3.5f, 0f, 3.5f, 2.5f, 1.5f, 1.0f, 0.75f };
        public float finalDriveRatio = 4.1f; // Overall ratio
        public float transmissionInertia = 0.05f; // kg*m^2
        [Header("Drivetrain Parameters")]
        public float clutchTorqueCapacity = 400f; // Example clutch torque limit
        [Range(0f, 1f)]
        public float drivetrainLoss = 0.15f; // Fractional loss (e.g. 0.15 = 15% loss)

        [Header("Drivetrain Inputs")]
        [Range(0f, 1f)] public float throttleInput = 0f;
        [Range(0f, 1f)] public float clutchInput = 0f;
        [Header("References")]
        public Rigidbody vehicleBody;
        public Differential[] differentials;

        [Header("Engine Output")]
        public float engineRpm = 0f;
        public float engineTorque = 0f;
        public float engineAngularVelocity = 0f;
        public float engineThrottle = 0f;
        [Header("Engine Audio")]
        public AudioSource engineSound;
        public float audioPitchMultiplier = 2f;

        private void Start()
        {
            // Initialize components
            if (vehicleBody == null && TryGetComponent(out Rigidbody rb))
                vehicleBody = rb;
            engineAngularVelocity = rpmIdle * RPM_TO_RADS;

            // Smoothen curves
            for (int i = 0; i < torqueCurve.length; i++)
                torqueCurve.SmoothTangents(i, 0f);
            for (int i = 0; i < throttleResponseCurve.length; i++)
                throttleResponseCurve.SmoothTangents(i, 0f);
        }

        private void FixedUpdate()
        {
            // Clamp inputs
            throttleInput = Mathf.Clamp01(throttleInput);
            clutchInput = Mathf.Clamp01(clutchInput);

            // Throttle mapping
            float throttleMapped = throttleResponseCurve.Evaluate(throttleInput);

            // Rev limiter
            if (hasRevLimiter && engineRpm > rpmLimiter)
                throttleMapped = 0f;

            // Calculate engine torque using magic formula
            engineThrottle = throttleMapped * 100f;
            engineTorque = CalculateMagicTorque(engineRpm, engineThrottle);

            // Engine braking
            float engineBrakingTorque = dynamicFriction * engineRpm;

            // Transmission ratio
            float gearRatio = (currentGear >= 0 && currentGear < gearRatios.Length) ? gearRatios[currentGear] : 0f;

            // Calculate average wheel speed from all differentials
            float avgWheelAngularVelocity = 0f;
            int poweredWheels = 0;
            foreach (var diff in differentials)
            {
                if (diff == null) continue;
                avgWheelAngularVelocity += diff.GetAverageWheelSpeed();
                poweredWheels += diff.PoweredWheelCount();
            }
            if (poweredWheels > 0)
                avgWheelAngularVelocity /= poweredWheels;

            // Engine-wheel coupling (simple clutch slip model)
            float totalRatio = gearRatio * finalDriveRatio;
            float wheelRpm = avgWheelAngularVelocity * RADS_TO_RPM;

            // TODO: better clutch model
            if (clutchInput < 0.1f && totalRatio != 0f)
            {
                float targetEngineRpm = wheelRpm * totalRatio;
                float rpmDiff = targetEngineRpm - engineRpm;
                float clutchTorque = Mathf.Clamp(rpmDiff * 0.1f, -clutchTorqueCapacity, clutchTorqueCapacity); // slip-based torque
                engineAngularVelocity += clutchTorque / flywheelInertia * Time.fixedDeltaTime;
            }
            else
            {
                // Free revving
                engineAngularVelocity += engineTorque * Time.fixedDeltaTime / flywheelInertia;
                engineAngularVelocity -= Mathf.Abs(engineAngularVelocity) * friction * Time.fixedDeltaTime / flywheelInertia;
            }

            engineRpm = engineAngularVelocity * RADS_TO_RPM;
            // TODO: engine stalling mechanic, right now clamps to rpmidle to prevent stalling
            engineRpm = Mathf.Clamp(engineRpm, rpmIdle, rpmMax); // Clamp RPM

            // TODO: better drivetrain losses calculations
            // Apply torque to differentials (scale by gear and final drive, and losses)
            float outputTorque = engineTorque * totalRatio * (1f - drivetrainLoss); // Use editable parameter
            foreach (var diff in differentials)
            {
                if (diff == null) continue;
                diff.Step(outputTorque, engineBrakingTorque);
            }

            // Update engine sound
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
        public void ShiftUp()
        {
            if (currentGear < gearRatios.Length - 1)
                currentGear++;
        }
        public void ShiftDown()
        {
            if (currentGear > 0)
                currentGear--;
        }
    }
}
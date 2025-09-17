using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace VehicleDynamics
{
    public class Drivetrain : MonoBehaviour
    {
        public enum TransmissionType
        {
            Manual,
            Automatic,
            CVT
        }
        public Engine engine;
        public Rigidbody vehicleBody;
        public Differential[] differentials;
        [Header("Drivetrain Inputs")]
        [Range(0f, 1f)] public float throttleInput = 0f;
        [Range(0f, 1f)] public float brakeInput = 0f;
        [Range(0f, 1f)] public float clutchInput = 0f;

        [Header("Transmission Parameters")]
        public TransmissionType transmissionType = TransmissionType.Manual;
        public int currentGear = 1;
        public float[] gearRatios = new float[] { -3.5f, 0f, 3.5f, 2.5f, 1.5f, 1.0f, 0.75f };
        public float finalDriveRatio = 3.42f;
        public float transmissionInertia = 0.1f;
        [Header("Clutch Parameters")]
        public float clutchStiffness = 500f; // nm/rad
        public float clutchDamping = 10f; // nm/(rad/s)
        public float clutchInertia = 0.01f; // kg*m^2
        const float RPM_TO_RADS = Mathf.PI / 30f; // 1 RPM = π/30 rad/s
        const float RADS_TO_RPM = 30f / Mathf.PI; // 1 rad/s = 30/π RPM

        [Header("Wheel Parameters")]
        public float driveWheelRadius = 0.34105f; // meters (example value)
        [Header("Vehicle Parameters")]
        private float vehicleMass; // kg
        private float dragCoefficient; // example value
        public float frontalArea = 2.2f; // m^2, example value
        public float rollingResistance = 0.015f; // typical value
        private const float airDensity = 1.225f; // kg/m^3

        private void Start()
        {
            // Initialize components
            if (engine == null && TryGetComponent(out Engine eng))
            {
                engine = eng;
            }
            else if (engine == null)
            {
                engine = this.AddComponent<Engine>();
            }

            if (vehicleBody == null && TryGetComponent(out Rigidbody rb))
            {
                vehicleBody = rb;
            }
            else
            {
                Debug.LogError("Could not find main vehicle Rigidbody.");
            }

            vehicleMass = vehicleBody.mass;
            dragCoefficient = vehicleBody.drag;
        }

        private void FixedUpdate()
        {
            // Update engine state (engine handles torque, throttle, rpm, etc.)
            engine.Step(throttleInput);

            // Calculate effective gear ratio
            float gearRatio = (currentGear >= 0 && currentGear < gearRatios.Length) ? gearRatios[currentGear] : 0f;
            float effectiveGearRatio = gearRatio * finalDriveRatio;
            if (Mathf.Abs(effectiveGearRatio) < 1e-3f) return; // Neutral or invalid gear

            // Calculate wheel RPM from vehicle speed
            float vehicleSpeed = vehicleBody.velocity.magnitude; // m/s
            float wheelCircumference = 2f * Mathf.PI * driveWheelRadius;
            float wheelRpm = (vehicleSpeed / wheelCircumference) * 60f;

            // Calculate engine RPM from wheel RPM and gear ratio
            float expectedEngineRpm = wheelRpm * effectiveGearRatio;
            // Optionally, sync engine RPM to drivetrain if clutch is engaged
            if (clutchInput > 0.99f)
            {
                engine.engineRpm = expectedEngineRpm;
                engine.engineAngularVelocity = expectedEngineRpm * RPM_TO_RADS;
            }

            // Get engine torque from engine
            float engineTorque = engine.engineTorque;
            // Apply throttle and gear ratio
            float outputTorque = engineTorque * effectiveGearRatio;
            // Convert torque to force at wheels
            float driveForce = outputTorque / driveWheelRadius;

            foreach (var diff in differentials)
            {
                diff.Step(outputTorque, engine.engineAngularVelocity);
            }
        }
    }
}
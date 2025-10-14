using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class Drivetrain : MonoBehaviour
    {
        const float RPM_TO_RADS = Mathf.PI / 30f; // 1 RPM = π/30 rad/s
        const float RADS_TO_RPM = 30f / Mathf.PI; // 1 rad/s = 30/π RPM

        [Header("Drivetrain Inputs")]
        [Range(0f, 1f)] public float throttleInput = 0f;
        [Range(0f, 1f)] public float clutchInput = 0f;
        [Header("References")]
        public Rigidbody vehicleBody;
        public Differential differential;
        public Transmission transmission;
        public Clutch clutch;
        public Engine engine;

        private void Start()
        {
            // Initialize components
            if (vehicleBody == null && TryGetComponent(out Rigidbody rb))
                vehicleBody = rb;
            if (transmission == null && TryGetComponent(out Transmission trans))
                transmission = trans;
            if (clutch == null && TryGetComponent(out Clutch cl))
                clutch = cl;
            if (engine == null && TryGetComponent(out Engine eng))
                engine = eng;
        }

        public void Step(float dt)
        {
            // Clamp inputs
            throttleInput = Mathf.Clamp01(throttleInput);
            clutchInput = Mathf.Clamp01(clutchInput);

            // Step engine
            engine.Step(dt, throttleInput, clutch.clutchTorque);

            // wheel -> differential -> driveshaft
            float driveshaftAngularVelocity = differential.GetUpVelocity();
            // driveshaft -> transmission/engine side via gear ratio
            float transmissionAngularVelocity = transmission.GetUpVelocity(driveshaftAngularVelocity); 

            // Step clutch
            clutch.Step(dt, clutchInput, engine.engineAngularVelocity, transmissionAngularVelocity, transmission.GetCurrentGearRatio());

            // Compute torque transmitted through transmission once and distribute to wheels
            float driveshaftTorque = transmission.GetDownTorque(clutch.clutchTorque);
            var (leftWheelTorque, rightWheelTorque) = differential.GetDownTorque(driveshaftTorque);
            differential.leftWheel.ApplyDriveTorque(leftWheelTorque);
            differential.rightWheel.ApplyDriveTorque(rightWheelTorque);
        }
    }
}
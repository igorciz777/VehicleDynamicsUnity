using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class DragForces : MonoBehaviour
    {
        public float dragCoefficient = 0.3f; // Example value for a typical car
        public float dragYawCoefficient = 0.1f; // Example value for yaw drag
        public float frontalArea = 2.2f; // m^2, example value
        private float vehicleVelocity = 0f;
        private float airDensity = 1.225f; // kg/m^3
        private Rigidbody vehicleBody;
        private float vehicleYawAngle = 0f;
        private SimulationSettings simSettings;

        void Start()
        {
            vehicleBody = GetComponent<Rigidbody>();
            simSettings = Resources.Load<SimulationSettings>("Settings/SimulationSettings");
            Debug.Assert(simSettings != null, "SimulationSettings asset not found in Resources folder.");
            airDensity = simSettings.airDensity;
        }

        void FixedUpdate()
        {
            if (vehicleBody == null) return;

            // TODO: fix weird behavior with yaw drag
            vehicleVelocity = vehicleBody.linearVelocity.magnitude;
            // yaw angle = body slip angle surrogate
            vehicleYawAngle = Vector3.SignedAngle(vehicleBody.transform.forward, vehicleBody.linearVelocity.normalized, Vector3.up) * Mathf.Deg2Rad;

            // Calculate drag force
            Vector3 Fd = 0.5f * airDensity * vehicleVelocity * vehicleVelocity * (dragCoefficient + dragYawCoefficient * vehicleYawAngle) * frontalArea * -vehicleBody.linearVelocity.normalized;

            vehicleBody.AddForce(Fd, ForceMode.Force);
        }
    }
}
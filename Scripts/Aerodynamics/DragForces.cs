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
        private const float airDensity = 1.225f; // kg/m^3
        private Rigidbody vehicleBody;
        private float vehicleYawAngle = 0f;

        void Start()
        {
            vehicleBody = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (vehicleBody == null) return;

            vehicleVelocity = vehicleBody.velocity.magnitude;
            vehicleYawAngle = Vector3.SignedAngle(vehicleBody.transform.forward, vehicleBody.velocity.normalized, Vector3.up);

            // Calculate drag force
            Vector3 Fd = 0.5f * airDensity * vehicleVelocity * vehicleVelocity * (dragCoefficient + dragYawCoefficient * vehicleYawAngle) * frontalArea * -vehicleBody.velocity.normalized;

            vehicleBody.AddForce(Fd, ForceMode.Force);
        }
    }
}
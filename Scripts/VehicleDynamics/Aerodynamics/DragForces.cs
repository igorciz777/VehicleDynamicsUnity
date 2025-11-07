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

            vehicleVelocity = vehicleBody.linearVelocity.magnitude;
            vehicleYawAngle = Vector3.Dot(vehicleBody.transform.forward, vehicleBody.linearVelocity.normalized);

            // Calculate drag force
            Vector3 Fd = 0.5f * airDensity * vehicleVelocity * vehicleVelocity * (dragCoefficient + dragYawCoefficient * vehicleYawAngle) * frontalArea * -vehicleBody.linearVelocity.normalized;

            vehicleBody.AddForce(Fd, ForceMode.Force);
        }
    }
}
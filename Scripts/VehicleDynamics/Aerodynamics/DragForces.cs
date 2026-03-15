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

        private void Start()
        {
            vehicleBody = GetComponent<Rigidbody>();
            simSettings = Resources.Load<SimulationSettings>("Settings/SimulationSettings");
            Debug.Assert(simSettings != null, "SimulationSettings asset not found in Resources folder.");
            airDensity = simSettings.airDensity;
        }

        private void FixedUpdate()
        {
            if (vehicleBody == null) return;

            vehicleVelocity = vehicleBody.linearVelocity.magnitude;
            if (vehicleVelocity < 0.1f) return;

            Vector3 localVelocity = vehicleBody.transform.InverseTransformDirection(vehicleBody.linearVelocity);
            vehicleYawAngle = Mathf.Atan2(localVelocity.x, Mathf.Abs(localVelocity.z) + 0.1f);

            float yawDragScale = 1f + dragYawCoefficient * vehicleYawAngle * vehicleYawAngle;
            float effectiveCd = Mathf.Max(0f, dragCoefficient * yawDragScale);

            // Calculate drag force
            Vector3 Fd = 0.5f * airDensity * vehicleVelocity * vehicleVelocity * effectiveCd * frontalArea * -vehicleBody.linearVelocity.normalized;

            vehicleBody.AddForce(Fd, ForceMode.Force);
        }
    }
}
using UnityEngine;

namespace VehicleDynamics
{
    public class WingSurface : MonoBehaviour
    {
        public float length = 1.0f; // Length of the wing surface in meters
        public float width = 0.3f; // Width of the wing surface in meters
        public float angle = 5.0f; // Angle of attack in degrees
        public float liftCoefficient = 1.2f; // Lift coefficient (Cl)
        public float dragCoefficient = 0.1f; // Drag coefficient (Cd)
        private float airDensity = 1.225f; // kg/m^3
        private Rigidbody vehicleBody;
        private float vehicleVelocity = 0f;
        private Vector3 wingNormal;
        private Vector3 wingAreaVector;
        private float wingArea;
        private SimulationSettings simSettings;

        private void Start()
        {
            vehicleBody = GetComponentInParent<Rigidbody>();
            wingArea = length * width;
            wingNormal = Quaternion.AngleAxis(angle, transform.right) * transform.up;
            simSettings = Resources.Load<SimulationSettings>("Settings/SimulationSettings");
            Debug.Assert(simSettings != null, "SimulationSettings asset not found in Resources folder.");
            airDensity = simSettings.airDensity;
            wingAreaVector = wingNormal * wingArea;
        }
        private void FixedUpdate()
        {
            if (vehicleBody == null) return;

            vehicleVelocity = vehicleBody.linearVelocity.magnitude;

            // Calculate the wing normal based on angle of attack
            Vector3 wingNormal = Quaternion.AngleAxis(angle, transform.right) * transform.up;

            // Calculate lift and drag directions
            Vector3 velocityDir = vehicleBody.linearVelocity.normalized;
            Vector3 liftDirection = wingNormal; // Use the rotated normal
            Vector3 dragDirection = -velocityDir;

            float dynamicPressure = 0.5f * airDensity * vehicleVelocity * vehicleVelocity;
            Vector3 liftForce = dynamicPressure * liftCoefficient * wingArea * liftDirection;
            Vector3 dragForce = dynamicPressure * dragCoefficient * wingArea * dragDirection;

            vehicleBody.AddForceAtPosition(liftForce + dragForce, transform.position, ForceMode.Force);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position;
            Vector3 right = 0.5f * width * transform.right;
            Vector3 forward = Quaternion.AngleAxis(angle, transform.right) * transform.forward * length * 0.5f;
            Vector3 p1 = center - right - forward;
            Vector3 p2 = center + right - forward;
            Vector3 p3 = center + right + forward;
            Vector3 p4 = center - right + forward;
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4);
            Gizmos.DrawLine(p4, p1);
        }
    }
}
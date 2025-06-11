using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class WheelHub : MonoBehaviour
    {
        [HideInInspector] public float wheelHubRadius = 0.05f;
        [SerializeField] public float wheelRadius = 0.5f;
        [SerializeField] public float wheelWidth = 0.2f;
        [SerializeField] public float wheelMass = 10f;
        [SerializeField] public float steeringAngle = 0f;
        [SerializeField] public float maxSteeringAngle = 45f;
        private KinematicSuspension parentSuspension;
        private Rigidbody vehicleBody;
        private Rigidbody wheelHubBody;
        private float previousSuspensionDistance = 0f;
        private float currentSuspensionDistance = 0f;
        private float springVelocity = 0f;
        private float springForce = 0f;
        private float damperForce = 0f;

        void Awake()
        {
            parentSuspension = GetComponentInParent<KinematicSuspension>();
            vehicleBody = parentSuspension.GetComponentInParent<Rigidbody>();
            wheelHubBody = GetComponent<Rigidbody>();
        }
        private void FixedUpdate()
        {
            float suspensionMountToHubDistance = Vector3.Distance(parentSuspension.springChassisMount.position, transform.position);
            Vector3 rayOrigin = transform.position;
            rayOrigin.y = parentSuspension.springChassisMount.position.y;
            Ray ray = new(rayOrigin, -vehicleBody.transform.up);
            Debug.DrawRay(ray.origin, ray.direction * suspensionMountToHubDistance, Color.red);
            LayerMask layerMask = LayerMask.GetMask("Default");
            if (Physics.SphereCast(ray, wheelRadius, out RaycastHit hit, suspensionMountToHubDistance, layerMask))
            {
                previousSuspensionDistance = currentSuspensionDistance;
                currentSuspensionDistance = suspensionMountToHubDistance - hit.distance;
                springVelocity = (currentSuspensionDistance - previousSuspensionDistance) / Time.fixedDeltaTime;
                // Hooke's Law
                springForce = parentSuspension.springConstant * currentSuspensionDistance;
                damperForce = parentSuspension.damperConstant * springVelocity;
                Vector3 force = transform.up * (springForce + damperForce);
                wheelHubBody.AddForceAtPosition(force, transform.position, ForceMode.Force);
                //vehicleBody.AddForceAtPosition(-force, parentSuspension.springChassisMount.position, ForceMode.Force);
            }
        }
        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position - transform.up * wheelHubRadius, transform.position + transform.up * wheelHubRadius);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, wheelRadius);
        }
    }
}

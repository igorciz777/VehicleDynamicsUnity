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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class WheelSpindle : MonoBehaviour
    {
        [HideInInspector] public float spindleRadius = 0.05f;
        public bool oppositeSpindle = false;
        [SerializeField] public bool isSteerable = false;
        [SerializeField] public float spindleSpacing = 0.1f; // Distance between the spindle and the wheel center
        [SerializeField] public float wheelRadius = 0.5f;
        [SerializeField] public float wheelWidth = 0.2f;
        [SerializeField] public float wheelMass = 10f;
        [SerializeField] public float steeringAngle = 0f;
        [SerializeField] public float maxSteeringAngle = 45f;
        [SerializeField] public GameObject visualWheelObject; // Reference to the visual wheel object
        private KinematicSuspension parentSuspension;
        private Rigidbody vehicleBody;
        private Rigidbody spindleBody;
        private float previousSuspensionDistance = 0f;
        private float currentSuspensionDistance = 0f;
        private float springVelocity = 0f;
        private float springForce = 0f;
        private float damperForce = 0f;
        private float wheelRate = 0f;
        private Vector3 springChassisMount;
        private Vector3 springHubMount;

        void Awake()
        {
            parentSuspension = GetComponentInParent<KinematicSuspension>();
            vehicleBody = parentSuspension.GetComponentInParent<Rigidbody>();
            spindleBody = GetComponent<Rigidbody>();
            
        }
        void Start()
        {
            if (parentSuspension.suspensionType == KinematicSuspension.SuspensionType.LeafSpring)
            {
                if (oppositeSpindle)
                {
                    springChassisMount = parentSuspension.rightSpringChassisMount;
                    springHubMount = parentSuspension.rightSpringHubMount;
                }
                else
                {
                    springChassisMount = parentSuspension.leftSpringChassisMount.position;
                    springHubMount = parentSuspension.leftSpringHubMount.position;
                }
            }
            else
            {
                springChassisMount = parentSuspension.springChassisMount.position;
                springHubMount = parentSuspension.springHubMount.position;
            }
            // Calculate wheel rate
            float Ls = Vector3.Distance(springChassisMount, springHubMount);
            float Lw = Vector3.Distance(springHubMount, transform.position + transform.right * spindleSpacing);
            wheelRate = Mathf.Pow(Ls / Lw, 2) * parentSuspension.springConstant;
        }
        private void FixedUpdate()
        {
            if (parentSuspension.suspensionType == KinematicSuspension.SuspensionType.LeafSpring)
            {
                if (oppositeSpindle)
                {
                    springChassisMount = parentSuspension.rightSpringChassisMount;
                    springHubMount = parentSuspension.rightSpringHubMount;
                }
                else
                {
                    springChassisMount = parentSuspension.leftSpringChassisMount.position;
                    springHubMount = parentSuspension.leftSpringHubMount.position;
                }
            }
            else
            {
                springChassisMount = parentSuspension.springChassisMount.position;
                springHubMount = parentSuspension.springHubMount.position;
            }
            // Offset the spindle position by spindleSpacing along the local right axis
            Vector3 spindleOffsetPosition = transform.position + transform.right * spindleSpacing;

            Vector3 rayOrigin = spindleOffsetPosition;
            rayOrigin.y = springChassisMount.y;
            Ray ray = new(rayOrigin, -parentSuspension.transform.up);

            float rayTospindleDistance = springChassisMount.y - spindleOffsetPosition.y;

            LayerMask layerMask = LayerMask.GetMask("Default");
            if (Physics.SphereCast(ray, wheelRadius, out RaycastHit hit, rayTospindleDistance, layerMask))
            {
                previousSuspensionDistance = currentSuspensionDistance;
                currentSuspensionDistance = rayTospindleDistance - hit.distance;
                springVelocity = (currentSuspensionDistance - previousSuspensionDistance) / Time.fixedDeltaTime;

                // Hooke's Law
                springForce = wheelRate * currentSuspensionDistance;
                damperForce = parentSuspension.damperConstant * springVelocity;
                Vector3 force = parentSuspension.transform.up * (springForce + damperForce);
                spindleBody.AddForceAtPosition(force, spindleOffsetPosition, ForceMode.Force);

                float normalizedForce = Mathf.Clamp01(springForce / parentSuspension.springConstant * 2);
                Color rayColor = Color.Lerp(Color.green, Color.red, normalizedForce);
                Debug.DrawRay(ray.origin, vehicleBody.transform.up * rayTospindleDistance, rayColor);
                Debug.DrawRay(hit.point, hit.normal * 0.2f, Color.blue);
            }

            visualWheelObject.transform.position = transform.position + transform.right * spindleSpacing;
            visualWheelObject.transform.rotation = Quaternion.Euler(0, steeringAngle, 0) * transform.rotation;
        }
        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Vector3 spindleOffsetPosition = transform.position + transform.right * spindleSpacing;
            Gizmos.DrawLine(spindleOffsetPosition - transform.up * spindleRadius, spindleOffsetPosition + transform.up * spindleRadius);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;

    #if UNITY_EDITOR
            // Draw a cylinder to represent the wheel
            UnityEditor.Handles.color = Color.cyan;
            Quaternion rotation = transform.rotation * Quaternion.Euler(90, 0, 0); // Align cylinder with wheel axis
            Vector3 spindleOffsetPosition = transform.position + transform.right * spindleSpacing;
            UnityEditor.Handles.DrawWireDisc(spindleOffsetPosition, transform.right, wheelRadius);
            UnityEditor.Handles.DrawWireDisc(spindleOffsetPosition + transform.right * (wheelWidth * 0.5f), transform.right, wheelRadius);
            UnityEditor.Handles.DrawWireDisc(spindleOffsetPosition - transform.right * (wheelWidth * 0.5f), transform.right, wheelRadius);
            UnityEditor.Handles.DrawLine(
                spindleOffsetPosition + transform.up * wheelRadius + transform.right * (wheelWidth * 0.5f),
                spindleOffsetPosition + transform.up * wheelRadius - transform.right * (wheelWidth * 0.5f)
            );
            UnityEditor.Handles.DrawLine(
                spindleOffsetPosition - transform.up * wheelRadius + transform.right * (wheelWidth * 0.5f),
                spindleOffsetPosition - transform.up * wheelRadius - transform.right * (wheelWidth * 0.5f)
            );
            UnityEditor.Handles.DrawLine(
                spindleOffsetPosition + transform.forward * wheelRadius + transform.right * (wheelWidth * 0.5f),
                spindleOffsetPosition + transform.forward * wheelRadius - transform.right * (wheelWidth * 0.5f)
            );
            UnityEditor.Handles.DrawLine(
                spindleOffsetPosition - transform.forward * wheelRadius + transform.right * (wheelWidth * 0.5f),
                spindleOffsetPosition - transform.forward * wheelRadius - transform.right * (wheelWidth * 0.5f)
            );
    #endif
        }
    }
}

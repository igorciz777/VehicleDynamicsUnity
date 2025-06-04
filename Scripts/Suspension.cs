using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace VehicleDynamics
{
    public enum SuspensionType
    {
        SolidAxle,
        MacPherson,
        DoubleWishbone
    }
    public class Suspension : MonoBehaviour
    {
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] public WheelHub wheelHub;
        [Tooltip("Only for SolidAxle suspension type")]
        [SerializeField] public Suspension oppositeSuspension; // Tylko gdy typ zawieszenia to SolidAxle
        [Header("Suspension Settings")]
        [SerializeField] private SuspensionType suspensionType = SuspensionType.SolidAxle;
        [SerializeField] public float suspensionDistanceConstant = 1f;
        [SerializeField] private float springConstant = 30000f;
        [SerializeField] private float damperConstant = 4000f;
        [SerializeField] private float antirollBarConstant = 1000f;
        [SerializeField] private float camberAngle = 0f;
        [SerializeField] private float toeAngle = 0f;
        [SerializeField] private Vector3 suspensionMount;

        private float previousSuspensionDistance;
        private float currentSuspensionDistance;
        private float springVelocity;
        private float springForce;
        private float damperForce;

        private void FixedUpdate()
        {
            Ray ray = new(transform.position, -transform.up);
            if (Physics.SphereCast(ray, wheelHub.wheelRadius, out RaycastHit hit, suspensionDistanceConstant))
            {
                previousSuspensionDistance = currentSuspensionDistance;
                currentSuspensionDistance = suspensionDistanceConstant - hit.distance;
                springVelocity = (currentSuspensionDistance - previousSuspensionDistance) / Time.fixedDeltaTime;
                // Hooke's Law
                springForce = springConstant * currentSuspensionDistance;
                damperForce = damperConstant * springVelocity;

                vehicleBody.AddForceAtPosition(transform.up * (springForce + damperForce), transform.position, ForceMode.Force);

                Vector3 wheelPosition = transform.position - transform.up * (suspensionDistanceConstant - currentSuspensionDistance);
                wheelHub.transform.position = wheelPosition;
            }
            else
            {
                wheelHub.transform.position = transform.position - transform.up * suspensionDistanceConstant;
            }

            float forceMagnitude = Mathf.Clamp01((springForce + damperForce) / (springConstant * suspensionDistanceConstant));
            Color rayColor = Color.Lerp(Color.green, Color.red, forceMagnitude);
            Debug.DrawRay(transform.position + transform.up, -transform.up * suspensionDistanceConstant - transform.up, rayColor, 0f);

            switch (suspensionType)
            {
                case SuspensionType.MacPherson:
                    // MacPherson suspension logic
                    // Calculate camber angle based on suspension mount and visual wheel position
                    Vector3 suspensionToWheel = wheelHub.transform.position - (transform.position + suspensionMount);
                    camberAngle = Mathf.Atan2(suspensionToWheel.y, suspensionToWheel.x) * Mathf.Rad2Deg;
                    wheelHub.transform.localRotation = Quaternion.Euler(wheelHub.transform.localRotation.eulerAngles.x, wheelHub.transform.localRotation.eulerAngles.y, camberAngle);
                    break;
                case SuspensionType.DoubleWishbone:
                    // Double wishbone suspension logic
                    // Calculate wheel offset x axis based on suspension mount and visual wheel position

                    break;
                case SuspensionType.SolidAxle:{
                    // Solid axle suspension logic
                    if (oppositeSuspension == null) break;
                    ApplyAntiroll(oppositeSuspension);
                    oppositeSuspension.ApplyAntiroll(this);
                    // Calculate wheel camber angle based on suspension roll
                    Vector3 wheelHubToOpposite = oppositeSuspension.wheelHub.transform.position - wheelHub.transform.position;
                    camberAngle = Mathf.Atan2(wheelHubToOpposite.y, wheelHubToOpposite.x) * Mathf.Rad2Deg;
                    wheelHub.transform.localRotation = Quaternion.Euler(wheelHub.transform.localRotation.eulerAngles.x, wheelHub.transform.localRotation.eulerAngles.y, camberAngle);
                    break;
                }
            }
        }
        void ApplyAntiroll(Suspension otherSuspension)
        {
            if (otherSuspension == null) return;
            float antirollForce = antirollBarConstant * (currentSuspensionDistance - otherSuspension.currentSuspensionDistance);
            vehicleBody.AddForceAtPosition(transform.up * antirollForce, transform.position, ForceMode.Force);
            vehicleBody.AddForceAtPosition(-transform.up * antirollForce, otherSuspension.transform.position, ForceMode.Force);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position - transform.up * suspensionDistanceConstant);

            if (wheelHub != null)
            {
                Gizmos.DrawWireSphere(transform.position - transform.up * suspensionDistanceConstant, wheelHub.wheelRadius);
            }
        }
        void OnDrawGizmos()
        {
            if (wheelHub == null) return;
            switch (suspensionType)
            {
                case SuspensionType.MacPherson:
                    Gizmos.color = Color.green;
                    Vector3 suspensionMountLeft = transform.position + suspensionMount + transform.forward * 0.1f;
                    Vector3 suspensionMountRight = transform.position + suspensionMount - transform.forward * 0.1f;
                    Gizmos.DrawLine(suspensionMountLeft, suspensionMountRight);
                    Gizmos.DrawLine(suspensionMountLeft, wheelHub.transform.position - transform.up * wheelHub.wheelHubRadius);
                    Gizmos.DrawLine(suspensionMountRight, wheelHub.transform.position - transform.up * wheelHub.wheelHubRadius);
                    break;
                case SuspensionType.DoubleWishbone:
                    Gizmos.color = Color.cyan;
                    Vector3 topWishboneLeft = transform.position + suspensionMount + transform.forward * 0.1f + transform.up * wheelHub.wheelHubRadius;
                    Vector3 topWishboneRight = transform.position + suspensionMount - transform.forward * 0.1f + transform.up * wheelHub.wheelHubRadius;
                    Vector3 bottomWishboneLeft = transform.position + suspensionMount + transform.forward * 0.1f - transform.up * wheelHub.wheelHubRadius;
                    Vector3 bottomWishboneRight = transform.position + suspensionMount - transform.forward * 0.1f - transform.up * wheelHub.wheelHubRadius;
                    Gizmos.DrawLine(topWishboneLeft, topWishboneRight);
                    Gizmos.DrawLine(bottomWishboneLeft, bottomWishboneRight);
                    Gizmos.DrawLine(topWishboneLeft, wheelHub.transform.position + transform.up * wheelHub.wheelHubRadius);
                    Gizmos.DrawLine(topWishboneRight, wheelHub.transform.position + transform.up * wheelHub.wheelHubRadius);
                    Gizmos.DrawLine(bottomWishboneLeft, wheelHub.transform.position - transform.up * wheelHub.wheelHubRadius);
                    Gizmos.DrawLine(bottomWishboneRight, wheelHub.transform.position - transform.up * wheelHub.wheelHubRadius);
                    break;
                case SuspensionType.SolidAxle:
                    if (oppositeSuspension == null)
                    {
                        break;
                    }
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(wheelHub.transform.position + transform.up * wheelHub.wheelHubRadius, oppositeSuspension.wheelHub.transform.position + transform.up * oppositeSuspension.wheelHub.wheelHubRadius);
                    Gizmos.DrawLine(wheelHub.transform.position - transform.up * wheelHub.wheelHubRadius, oppositeSuspension.wheelHub.transform.position - transform.up * oppositeSuspension.wheelHub.wheelHubRadius);
                    break;
            }
        }
    }
}

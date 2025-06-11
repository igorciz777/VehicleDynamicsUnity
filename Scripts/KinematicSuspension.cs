using System.Collections;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VehicleDynamics
{
    
    public class KinematicSuspension : MonoBehaviour
    {
        public enum SuspensionType
        {
            SolidAxle,
            MacPherson,
            DoubleWishbone
        }
        [Header("General Settings")]
        [SerializeField] private bool updateValuesEveryFrame = true;
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private Rigidbody wheelHubBody;
        [SerializeField] private SuspensionType suspensionType = SuspensionType.DoubleWishbone;
        [Header("Suspension Geometry")]
        [SerializeField] private Transform lowerWishboneChassisMount;
        [SerializeField] private Transform lowerWishboneHubMount;

        [SerializeField] private Transform upperWishboneChassisMount;
        [SerializeField] private Transform upperWishboneHubMount;

        [SerializeField] public Transform springChassisMount;
        [SerializeField] private Transform springHubMount;
        [Header("Suspension Settings")]
        [SerializeField] public float suspensionDistanceConstant = 0.1f;
        [SerializeField] public float springConstant = 36000f;
        [SerializeField] public float damperConstant = 2000f;
        [SerializeField] public float antirollBarConstant = 1000f;
        [SerializeField] public float camberAngle = 0f;
        [SerializeField] public float toeAngle = 0f;

        // Suspension joints
        private ConfigurableJoint lowerWishboneBar;
        private ConfigurableJoint upperWishboneBar;
        private ConfigurableJoint wheelHubJointHinge;
        private SpringJoint springJoint;

        void Awake()
        {
            lowerWishboneBar = CustomJoints.CreateBarJoint(
                vehicleBody.gameObject,
                wheelHubBody.gameObject,
                vehicleBody.transform.InverseTransformPoint(lowerWishboneChassisMount.position),
                lowerWishboneChassisMount.position - lowerWishboneHubMount.position
            );
            lowerWishboneBar.xMotion = ConfigurableJointMotion.Locked;
            lowerWishboneBar.lowAngularXLimit = new SoftJointLimit { limit = -15f };
            lowerWishboneBar.highAngularXLimit = new SoftJointLimit { limit = 15f };
            lowerWishboneBar.axis = new(0, 0, 1);

            springJoint = vehicleBody.gameObject.AddComponent<SpringJoint>();
            springJoint.autoConfigureConnectedAnchor = false;
            springJoint.connectedBody = wheelHubBody;
            springJoint.anchor = vehicleBody.transform.InverseTransformPoint(springHubMount.position);
            springJoint.connectedAnchor = wheelHubBody.transform.InverseTransformPoint(springChassisMount.position);
            springJoint.spring = springConstant;
            springJoint.damper = damperConstant;
            springJoint.minDistance = 0.1f;
            springJoint.maxDistance = 0.2f;
            springJoint.enableCollision = false;


            if (suspensionType != SuspensionType.DoubleWishbone) return;
            upperWishboneBar = CustomJoints.CreateBarJoint(
                vehicleBody.gameObject,
                wheelHubBody.gameObject,
                vehicleBody.transform.InverseTransformPoint(upperWishboneChassisMount.position),
                upperWishboneChassisMount.position - upperWishboneHubMount.position
            );
            upperWishboneBar.xMotion = ConfigurableJointMotion.Locked;
            upperWishboneBar.angularYMotion = ConfigurableJointMotion.Free;
            //upperWishboneBar.lowAngularXLimit = new SoftJointLimit { limit = -15f };
            //upperWishboneBar.highAngularXLimit = new SoftJointLimit { limit = 15f };
            upperWishboneBar.axis = new(0, 0, 1);
        }
        void FixedUpdate()
        {
            if (updateValuesEveryFrame)
            {
                springJoint.spring = springConstant;
                springJoint.damper = damperConstant;
            }
        }
        void OnDrawGizmos()
        {
            Vector3 thisForward = transform.forward;
            if (lowerWishboneChassisMount != null && lowerWishboneHubMount != null && suspensionType != SuspensionType.SolidAxle)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    lowerWishboneChassisMount.position,
                    lowerWishboneChassisMount.position
                );
                Gizmos.DrawLine(
                    lowerWishboneChassisMount.position,
                    lowerWishboneHubMount.position
                );
                Gizmos.DrawLine(
                    lowerWishboneChassisMount.position,
                    lowerWishboneHubMount.position
                );
            }

            if (upperWishboneChassisMount != null && upperWishboneHubMount != null && suspensionType == SuspensionType.DoubleWishbone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    upperWishboneChassisMount.position,
                    upperWishboneChassisMount.position
                );
                Gizmos.DrawLine(
                    upperWishboneChassisMount.position,
                    upperWishboneHubMount.position
                );
                Gizmos.DrawLine(
                    upperWishboneChassisMount.position,
                    upperWishboneHubMount.position
                );
            }

            float sphereSize = 0.025f;
            Gizmos.color = Color.green;
            if(suspensionType != SuspensionType.SolidAxle)
            {
                if (lowerWishboneChassisMount != null) Gizmos.DrawSphere(lowerWishboneChassisMount.position, sphereSize);
                if (lowerWishboneHubMount != null) Gizmos.DrawSphere(lowerWishboneHubMount.position, sphereSize);
            }
            Gizmos.color = Color.cyan;
            if(suspensionType == SuspensionType.DoubleWishbone)
            {
                if (upperWishboneChassisMount != null) Gizmos.DrawSphere(upperWishboneChassisMount.position, sphereSize);
                if (upperWishboneHubMount != null) Gizmos.DrawSphere(upperWishboneHubMount.position, sphereSize);
            }
            if (springChassisMount != null && springHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(springChassisMount.position, springHubMount.position);
                Gizmos.DrawSphere(springChassisMount.position, sphereSize);
                Gizmos.DrawSphere(springHubMount.position, sphereSize);
            }
        }
    }
    public class CustomJoints
    {
        public static ConfigurableJoint CreateBarJoint(GameObject bodyA, GameObject bodyB, Vector3 anchorA, Vector3 anchorB)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = anchorA;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = anchorB;

            SoftJointLimit limit = new SoftJointLimit();
            limit.limit = .01f;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.linearLimit = limit;

            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            return joint;
        }
        public static ConfigurableJoint CreateBallJoint(GameObject bodyA, GameObject bodyB, Vector3 anchor)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = anchor;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = anchor;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            return joint;
        }
    }
}

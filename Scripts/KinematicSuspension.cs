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
            MacPherson,
            DoubleWishbone,
            LeafSpring
        }
        [Header("General Settings")]
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private Rigidbody spindleBody;
        [SerializeField] public SuspensionType suspensionType = SuspensionType.DoubleWishbone;
        [Header("Independent Suspension Geometry")]
        [SerializeField] private Transform lowerWishboneChassisMount;
        [SerializeField] private Transform lowerWishboneHubMount;

        [SerializeField] private Transform upperWishboneChassisMount;
        [SerializeField] private Transform upperWishboneHubMount;
        [SerializeField] public Transform springChassisMount;
        [SerializeField] public Transform springHubMount;
        [Header("Solid Axle Suspension Geometry")]
        [SerializeField] private Rigidbody oppositeSpindleBody;
        [SerializeField] private Transform frontLeftLeafChassisMount;
        [SerializeField] private Transform rearLeftLeafChassisMount;
        [SerializeField] public Transform centerLeftLeafChassisMount;
        [SerializeField] public Transform leftLeafHubMount;
        [SerializeField] public Transform leftSpringHubMount;
        [SerializeField] public Transform leftSpringChassisMount;
        private Vector3 frontRightLeafChassisMount;
        private Vector3 rearRightLeafChassisMount;
        public Vector3 centerRightLeafChassisMount;
        public Vector3 rightLeafHubMount;
        public Vector3 rightSpringHubMount;
        public Vector3 rightSpringChassisMount;

        [Header("Suspension Settings")]
        [SerializeField] public float suspensionDistanceConstant = 0.5f;
        [SerializeField] public float springConstant = 36000f;
        [SerializeField] public float damperConstant = 2000f;
        [SerializeField] public float antirollBarConstant = 1000f;
        [SerializeField] public float camberAngle = 0f;
        [SerializeField] public float toeAngle = 0f;

        // Independent Suspension joints
        private ConfigurableJoint lowerWishboneHinge;
        private ConfigurableJoint lowerWishboneBall;
        private ConfigurableJoint upperWishboneHinge;
        private ConfigurableJoint upperWishboneBall;
        // Solid Axle Suspension joints
        private ConfigurableJoint frontLeftLeafSpringJoint;
        private ConfigurableJoint rearLeftLeafSpringJoint;
        private ConfigurableJoint leftSpringJoint;
        private ConfigurableJoint frontRightLeafSpringJoint;
        private ConfigurableJoint rearRightLeafSpringJoint;
        private ConfigurableJoint rightSpringJoint;
        private ConfigurableJoint axleJoint;
        // Spring joint
        private ConfigurableJoint springJoint;

        // New gameobjects for joints
        private GameObject lowerWishbone;
        private GameObject upperWishbone;

        // Wheel travel
        public float wheelTravel = 0f;

        void Awake()
        {
            if (suspensionType != SuspensionType.LeafSpring)
            {
                springJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, spindleBody.gameObject, springChassisMount.position, springHubMount.position, springConstant, damperConstant, suspensionDistanceConstant);

                lowerWishbone = new GameObject("LowerWishbone");
                lowerWishbone.transform.SetParent(transform);
                lowerWishbone.transform.position = (lowerWishboneChassisMount.position + lowerWishboneHubMount.position) * 0.5f;
                lowerWishbone.AddComponent<Rigidbody>().mass = 5f;
                lowerWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, lowerWishbone, lowerWishboneChassisMount.position, Vector3.right);
                lowerWishboneBall = CustomJoints.CreateSphereJoint(lowerWishbone, spindleBody.gameObject, lowerWishboneHubMount.position);
                lowerWishboneBall.angularZMotion = ConfigurableJointMotion.Limited;
                lowerWishboneBall.angularZLimit = new SoftJointLimit { limit = 45f };

                // Set axis to be relative to the spring
                lowerWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(springChassisMount.position - lowerWishboneHubMount.position).normalized;

                if (suspensionType == SuspensionType.DoubleWishbone)
                {
                    upperWishbone = new GameObject("UpperWishbone");
                    upperWishbone.transform.SetParent(transform);
                    upperWishbone.transform.position = (upperWishboneChassisMount.position + upperWishboneHubMount.position) * 0.5f;
                    upperWishbone.AddComponent<Rigidbody>().mass = 5f;
                    upperWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, upperWishbone, upperWishboneChassisMount.position, Vector3.right);
                    upperWishboneBall = CustomJoints.CreateSphereJoint(upperWishbone, spindleBody.gameObject, upperWishboneHubMount.position);
                    upperWishboneBall.angularZMotion = ConfigurableJointMotion.Limited;
                    upperWishboneBall.angularZLimit = new SoftJointLimit { limit = 45f };

                    // Set axis to be relative to the spring
                    upperWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(springChassisMount.position - upperWishboneHubMount.position).normalized;
                }

                if (spindleBody != null)
                {
                    if (!spindleBody.gameObject.GetComponent<WheelSpindle>().isSteerable)
                    {
                        lowerWishboneBall.angularYMotion = ConfigurableJointMotion.Locked;
                        if (suspensionType == SuspensionType.DoubleWishbone) upperWishboneBall.angularYMotion = ConfigurableJointMotion.Locked;
                    }
                }
                if (suspensionType == SuspensionType.MacPherson)
                {
                    //springJoint.angularXMotion = ConfigurableJointMotion.Locked;
                    //springJoint.angularZMotion = ConfigurableJointMotion.Locked;
                }
            }
            else
            {
                oppositeSpindleBody.gameObject.GetComponent<WheelSpindle>().oppositeSpindle = true;
                // Left side joints
                leftSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, spindleBody.gameObject, leftSpringChassisMount.position, leftSpringHubMount.position, springConstant, damperConstant, suspensionDistanceConstant);

                frontLeftLeafSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, spindleBody.gameObject, frontLeftLeafChassisMount.position, leftLeafHubMount.position, springConstant, damperConstant, suspensionDistanceConstant);
                rearLeftLeafSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, spindleBody.gameObject, rearLeftLeafChassisMount.position, leftLeafHubMount.position, springConstant, damperConstant, suspensionDistanceConstant);

                // Right side joints (mirrored)
                frontRightLeafChassisMount = MirrorPosition(frontLeftLeafChassisMount.position);
                rearRightLeafChassisMount = MirrorPosition(rearLeftLeafChassisMount.position);
                centerRightLeafChassisMount = MirrorPosition(centerLeftLeafChassisMount.position);
                rightLeafHubMount = MirrorPosition(leftLeafHubMount.position);
                rightSpringHubMount = MirrorPosition(leftSpringHubMount.position);
                rightSpringChassisMount = MirrorPosition(leftSpringChassisMount.position);

                rightSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, oppositeSpindleBody.gameObject, rightSpringChassisMount, rightSpringHubMount, springConstant, damperConstant, suspensionDistanceConstant);

                frontRightLeafSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, oppositeSpindleBody.gameObject, frontRightLeafChassisMount, rightLeafHubMount, springConstant, damperConstant, suspensionDistanceConstant);
                rearRightLeafSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, oppositeSpindleBody.gameObject, rearRightLeafChassisMount, rightLeafHubMount, springConstant, damperConstant, suspensionDistanceConstant);

                axleJoint = CustomJoints.CreateAxleJoint(spindleBody.gameObject, oppositeSpindleBody.gameObject);
            }
        }
        void FixedUpdate()
        {
            // Calculate wheel travel based on the current distance between spring mounts and the rest length
            if (springChassisMount != null && springHubMount != null)
            {
                float currentLength = Vector3.Distance(springChassisMount.position, springHubMount.position);
                wheelTravel = suspensionDistanceConstant - currentLength;
            }
            if (suspensionType == SuspensionType.LeafSpring && rightSpringChassisMount != null && rightSpringHubMount != null)
            {
                rightSpringHubMount = MirrorPosition(leftSpringHubMount.position);
                rightSpringChassisMount = MirrorPosition(leftSpringChassisMount.position);
            }
        }
        void OnDrawGizmos()
        {
            float sphereSize = 0.025f;
            if (lowerWishboneHinge != null && lowerWishboneBall != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    lowerWishboneHinge.transform.TransformPoint(lowerWishboneHinge.anchor),
                    lowerWishboneBall.transform.TransformPoint(lowerWishboneBall.anchor)
                );
                Gizmos.DrawCube(lowerWishboneHinge.transform.TransformPoint(lowerWishboneHinge.anchor), sphereSize * Vector3.one);
                Gizmos.DrawSphere(lowerWishboneBall.transform.TransformPoint(lowerWishboneBall.anchor), sphereSize);
            }
            else if (lowerWishboneChassisMount != null && lowerWishboneHubMount != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    lowerWishboneChassisMount.position,
                    lowerWishboneHubMount.position
                );
                Gizmos.DrawCube(lowerWishboneChassisMount.position, sphereSize * Vector3.one);
                Gizmos.DrawSphere(lowerWishboneHubMount.position, sphereSize);
            }

            if (upperWishboneHinge != null && upperWishboneBall != null && suspensionType == SuspensionType.DoubleWishbone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    upperWishboneHinge.transform.TransformPoint(upperWishboneHinge.anchor),
                    upperWishboneBall.transform.TransformPoint(upperWishboneBall.anchor)
                );
                Gizmos.DrawCube(upperWishboneHinge.transform.TransformPoint(upperWishboneHinge.anchor), Vector3.one * sphereSize);
                Gizmos.DrawSphere(upperWishboneBall.transform.TransformPoint(upperWishboneBall.anchor), sphereSize);
            }
            else if (upperWishboneChassisMount != null && upperWishboneHubMount != null && suspensionType == SuspensionType.DoubleWishbone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    upperWishboneChassisMount.position,
                    upperWishboneHubMount.position
                );
                Gizmos.DrawCube(upperWishboneChassisMount.position, Vector3.one * sphereSize);
                Gizmos.DrawSphere(upperWishboneHubMount.position, sphereSize);
            }

            if (springJoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    springJoint.transform.TransformPoint(springJoint.anchor),
                    springJoint.connectedBody.transform.TransformPoint(springJoint.connectedAnchor)
                );

                // Draw the target position relative to the spring's axis
                // Gizmos.color = Color.red;
                // Vector3 springAnchorWorld = springJoint.transform.TransformPoint(springJoint.anchor);
                // Vector3 targetPositionWorld = springAnchorWorld + springJoint.axis * springJoint.targetPosition.magnitude;
                // Gizmos.DrawSphere(targetPositionWorld, sphereSize);
            }
            else if (springChassisMount != null && springHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    springChassisMount.position,
                    springHubMount.position
                );
            }

            Gizmos.color = Color.yellow;
            if (springJoint != null)
            {
                Gizmos.DrawSphere(springJoint.transform.TransformPoint(springJoint.anchor), sphereSize);
                Gizmos.DrawSphere(springJoint.connectedBody.transform.TransformPoint(springJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (springChassisMount != null) Gizmos.DrawSphere(springChassisMount.position, sphereSize);
                if (springHubMount != null) Gizmos.DrawSphere(springHubMount.position, sphereSize);
            }

            // Leaf spring joints
            // Draw front leaf spring joint
            Gizmos.color = Color.blue;
            if (frontLeftLeafSpringJoint != null)
            {
                Gizmos.DrawLine(
                    frontLeftLeafSpringJoint.transform.TransformPoint(frontLeftLeafSpringJoint.anchor),
                    frontLeftLeafSpringJoint.connectedBody.transform.TransformPoint(frontLeftLeafSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(frontLeftLeafSpringJoint.transform.TransformPoint(frontLeftLeafSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(frontLeftLeafSpringJoint.connectedBody.transform.TransformPoint(frontLeftLeafSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (frontLeftLeafChassisMount != null && frontLeftLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(frontLeftLeafChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(leftLeafHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        frontLeftLeafChassisMount.position,
                        leftLeafHubMount.position
                    );
                }

            }
            if (frontRightLeafSpringJoint != null)
            {
                Gizmos.DrawLine(
                    frontRightLeafSpringJoint.transform.TransformPoint(frontRightLeafSpringJoint.anchor),
                    frontRightLeafSpringJoint.connectedBody.transform.TransformPoint(frontRightLeafSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(frontRightLeafSpringJoint.transform.TransformPoint(frontRightLeafSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(frontRightLeafSpringJoint.connectedBody.transform.TransformPoint(frontRightLeafSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (frontLeftLeafChassisMount != null && frontLeftLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(MirrorPosition(frontLeftLeafChassisMount.position), sphereSize);
                    Gizmos.DrawSphere(MirrorPosition(leftLeafHubMount.position), sphereSize);
                    Gizmos.DrawLine(
                        MirrorPosition(frontLeftLeafChassisMount.position),
                        MirrorPosition(leftLeafHubMount.position)
                    );
                }
            }
            // Draw rear leaf spring joint
            if (rearLeftLeafSpringJoint != null)
            {
                Gizmos.DrawLine(
                    rearLeftLeafSpringJoint.transform.TransformPoint(rearLeftLeafSpringJoint.anchor),
                    rearLeftLeafSpringJoint.connectedBody.transform.TransformPoint(rearLeftLeafSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(rearLeftLeafSpringJoint.transform.TransformPoint(rearLeftLeafSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(rearLeftLeafSpringJoint.connectedBody.transform.TransformPoint(rearLeftLeafSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (rearLeftLeafChassisMount != null && rearLeftLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(rearLeftLeafChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(leftLeafHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        rearLeftLeafChassisMount.position,
                        leftLeafHubMount.position
                    );
                }
            }
            if (rearRightLeafSpringJoint != null)
            {
                Gizmos.DrawLine(
                    rearRightLeafSpringJoint.transform.TransformPoint(rearRightLeafSpringJoint.anchor),
                    rearRightLeafSpringJoint.connectedBody.transform.TransformPoint(rearRightLeafSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(rearRightLeafSpringJoint.transform.TransformPoint(rearRightLeafSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(rearRightLeafSpringJoint.connectedBody.transform.TransformPoint(rearRightLeafSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (rearLeftLeafChassisMount != null && rearLeftLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(MirrorPosition(rearLeftLeafChassisMount.position), sphereSize);
                    Gizmos.DrawSphere(MirrorPosition(leftLeafHubMount.position), sphereSize);
                    Gizmos.DrawLine(
                        MirrorPosition(rearLeftLeafChassisMount.position),
                        MirrorPosition(leftLeafHubMount.position)
                    );
                }
            }
            // Draw left spring joint
            Gizmos.color = Color.yellow;
            if (leftSpringJoint != null)
            {
                Gizmos.DrawLine(
                    leftSpringJoint.transform.TransformPoint(leftSpringJoint.anchor),
                    leftSpringJoint.connectedBody.transform.TransformPoint(leftSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(leftSpringJoint.transform.TransformPoint(leftSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(leftSpringJoint.connectedBody.transform.TransformPoint(leftSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (leftSpringChassisMount != null && leftSpringChassisMount.position != null)
                {
                    Gizmos.DrawSphere(leftSpringChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(leftSpringHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        leftSpringChassisMount.position,
                        leftSpringHubMount.position
                    );
                }
            }
            // Draw right spring joint
            if (rightSpringJoint != null)
            {
                Gizmos.DrawLine(
                    rightSpringJoint.transform.TransformPoint(rightSpringJoint.anchor),
                    rightSpringJoint.connectedBody.transform.TransformPoint(rightSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(rightSpringJoint.transform.TransformPoint(rightSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(rightSpringJoint.connectedBody.transform.TransformPoint(rightSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (leftSpringChassisMount != null && leftSpringChassisMount.position != null)
                {
                    Gizmos.DrawSphere(MirrorPosition(leftSpringChassisMount.position), sphereSize);
                    Gizmos.DrawSphere(MirrorPosition(leftSpringHubMount.position), sphereSize);
                    Gizmos.DrawLine(
                        MirrorPosition(leftSpringChassisMount.position),
                        MirrorPosition(leftSpringHubMount.position)
                    );
                }
            }
            // Draw axle joint
            if (spindleBody != null && oppositeSpindleBody != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(
                    spindleBody.transform.TransformPoint(spindleBody.centerOfMass),
                    oppositeSpindleBody.transform.TransformPoint(oppositeSpindleBody.centerOfMass)
                );
            }

        }
        private Vector3 MirrorPosition(Vector3 position)
        {
            Vector3 bodyPosition = vehicleBody.transform.InverseTransformPoint(position);
            return vehicleBody.transform.TransformPoint(new Vector3(-bodyPosition.x, bodyPosition.y, bodyPosition.z));
        }
    }
}

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
        [SerializeField] private bool steerable = false;
        [Range(-1f, 1f)] public float steeringInput = 0f; // -1 (full left) to 1 (full right)
        [SerializeField] private GameObject leftVisualHub;
        [SerializeField] private GameObject rightVisualHub;
        [SerializeField] private GameObject leftVisualWheel;
        [SerializeField] private GameObject rightVisualWheel;
        public SuspensionType suspensionType = SuspensionType.DoubleWishbone;
        [Header("Independent Suspension Geometry")]
        [SerializeField] private Transform leftLowerWishboneChassisMount;
        [SerializeField] private Transform leftLowerWishboneHubMount;
        [SerializeField] private Transform leftUpperWishboneChassisMount;
        [SerializeField] private Transform leftUpperWishboneHubMount;
        [SerializeField] private Transform rightLowerWishboneChassisMount;
        [SerializeField] private Transform rightLowerWishboneHubMount;
        [SerializeField] private Transform rightUpperWishboneChassisMount;
        [SerializeField] private Transform rightUpperWishboneHubMount;

        [Header("Solid Axle Suspension Geometry")]
        [SerializeField] private Transform leftFrontLeafChassisMount;
        [SerializeField] private Transform leftRearLeafChassisMount;

        public Transform leftLeafHubMount;
        [SerializeField] private Transform rightFrontLeafChassisMount;
        [SerializeField] private Transform rightRearLeafChassisMount;
        public Transform rightLeafHubMount;

        [Header("Spring and Damper Geometry")]
        public Transform leftSpringChassisMount;
        public Transform leftSpringHubMount;
        public Transform rightSpringChassisMount;
        public Transform rightSpringHubMount;
        [Header("Wheel Hub Geometry")]
        public Transform leftWheelHubMount;
        public Transform rightWheelHubMount;

        [Header("Suspension Parameters")]
        public float springLength = 0.5f;
        public float springConstant = 36000f;
        public float damperConstant = 2000f;

        [Header("Wheel Parameters")]
        // public float hubSpacing = 0.1f; // Distance between the spindle and the wheel center
        // public float wheelRadius = 0.3f;
        // public float wheelWidth = 0.2f;                                          // Moved to wheelhub
        // public float wheelMass = 10f;
        // public float wheelRollingResistance = 0.25f; // aka wheel damping rate
        public float hubSpacing = 0.1f; // Distance between the spindle and the wheel center
        public float ackermanPercentage = 0.25f;
        public float maxSteeringAngle = 45f;
        public float camberAdjustment = 0f;
        public float toeAdjustment = 0f;
        [Header("Anti-Roll Bar Settings")]
        public bool hasAntirollBar = false;
        public float antirollBarStiffness = 5000f;

        // Independent Suspension joints
        private ConfigurableJoint leftLowerWishboneHinge;
        private ConfigurableJoint leftLowerWishboneBall;
        private ConfigurableJoint leftUpperWishboneHinge;
        private ConfigurableJoint leftUpperWishboneBall;
        private ConfigurableJoint rightLowerWishboneHinge;
        private ConfigurableJoint rightLowerWishboneBall;
        private ConfigurableJoint rightUpperWishboneHinge;
        private ConfigurableJoint rightUpperWishboneBall;
        // Solid Axle Suspension joints
        private ConfigurableJoint frontLeftLeafSpringJoint;
        private ConfigurableJoint rearLeftLeafSpringJoint;
        private ConfigurableJoint rightFrontLeafSpringJoint;
        private ConfigurableJoint rightRearLeafSpringJoint;
        private ConfigurableJoint axleJoint;
        // Spring joints
        private ConfigurableJoint leftSpringJoint;
        private ConfigurableJoint rightSpringJoint;
        // Anti-roll bar torsion spring
        private ConfigurableJoint leftAntirollBarJoint;
        private ConfigurableJoint rightAntirollBarJoint;

        // New gameobjects for joints
        private GameObject leftLowerWishbone;
        private GameObject leftUpperWishbone;
        private GameObject rightLowerWishbone;
        private GameObject rightUpperWishbone;
        private GameObject antiRollBar;

        // WheelHub objects
        private WheelHub leftWheelHub;
        private WheelHub rightWheelHub;

        // Wheel rate
        [HideInInspector] public float leftWheelRate = 0f;
        [HideInInspector] public float rightWheelRate = 0f;
        // Wheel damping rate
        [HideInInspector] public float leftWheelDampingRate = 0f;
        [HideInInspector] public float rightWheelDampingRate = 0f;

        void Start()
        {
            // Get vehicle body
            if (vehicleBody == null)
            {
                vehicleBody = GetComponentInParent<Rigidbody>();
                Debug.Assert(vehicleBody != null, "Rigidbody component not found on parent VehicleModel GameObject.");
            }
            // Setup hubs
            leftWheelHub = leftWheelHubMount.GetComponent<WheelHub>();
            rightWheelHub = rightWheelHubMount.GetComponent<WheelHub>();

            if (leftVisualHub != null)
            {
                leftWheelHub.visualHub = leftVisualHub;
            }

            if (rightVisualHub != null)
            {
                rightWheelHub.visualHub = rightVisualHub;
            }
            if (leftVisualWheel != null)
            {
                leftWheelHub.visualWheel = leftVisualWheel;
            }
            if (rightVisualWheel != null)
            {
                rightWheelHub.visualWheel = rightVisualWheel;
            }
            if (suspensionType != SuspensionType.LeafSpring)
            {
                // Left side joints
                leftSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, leftWheelHubMount.gameObject, leftSpringChassisMount.position, leftSpringHubMount.position, springConstant, damperConstant, springLength);
                leftLowerWishbone = new GameObject("L_LOWER_WB");
                leftLowerWishbone.transform.SetParent(transform);
                leftLowerWishbone.transform.position = (leftLowerWishboneChassisMount.position + leftLowerWishboneHubMount.position) * 0.5f;
                leftLowerWishbone.AddComponent<Rigidbody>().mass = 5f;
                leftLowerWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, leftLowerWishbone, leftLowerWishboneChassisMount.position, Vector3.right, 30f);
                leftLowerWishboneBall = CustomJoints.CreateSphereJoint(leftLowerWishbone, leftWheelHubMount.gameObject, leftLowerWishboneHubMount.position);
                // leftLowerWishboneBall.angularZMotion = ConfigurableJointMotion.Limited;
                // leftLowerWishboneBall.angularZLimit = new SoftJointLimit { limit = 10f };
                leftLowerWishboneHinge.angularXMotion = ConfigurableJointMotion.Locked;
                leftLowerWishboneBall.angularYMotion = ConfigurableJointMotion.Locked;

                // Right side joints
                rightSpringJoint = CustomJoints.CreateSpringJoint(vehicleBody.gameObject, rightWheelHubMount.gameObject, rightSpringChassisMount.position, rightSpringHubMount.position, springConstant, damperConstant, springLength);
                rightLowerWishbone = new GameObject("R_LOWER_WB");
                rightLowerWishbone.transform.SetParent(transform);
                rightLowerWishbone.transform.position = (rightLowerWishboneChassisMount.position + rightLowerWishboneHubMount.position) * 0.5f;
                rightLowerWishbone.AddComponent<Rigidbody>().mass = 5f;
                rightLowerWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, rightLowerWishbone, rightLowerWishboneChassisMount.position, Vector3.right, 30f);
                rightLowerWishboneBall = CustomJoints.CreateSphereJoint(rightLowerWishbone, rightWheelHubMount.gameObject, rightLowerWishboneHubMount.position);
                // rightLowerWishboneBall.angularZMotion = ConfigurableJointMotion.Limited;
                // rightLowerWishboneBall.angularZLimit = new SoftJointLimit { limit = 10f };
                rightLowerWishboneHinge.angularXMotion = ConfigurableJointMotion.Locked;
                rightLowerWishboneBall.angularYMotion = ConfigurableJointMotion.Locked;

                // Set axis to be relative to the spring
                leftLowerWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(leftSpringChassisMount.position - leftLowerWishboneHubMount.position).normalized;
                rightLowerWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(rightSpringChassisMount.position - rightLowerWishboneHubMount.position).normalized;

                if (suspensionType == SuspensionType.DoubleWishbone)
                {
                    // Left upper wishbone
                    leftUpperWishbone = new GameObject("L_UPPER_WB");
                    leftUpperWishbone.transform.SetParent(transform);
                    leftUpperWishbone.transform.position = (leftUpperWishboneChassisMount.position + leftUpperWishboneHubMount.position) * 0.5f;
                    leftUpperWishbone.AddComponent<Rigidbody>().mass = 5f;
                    leftUpperWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, leftUpperWishbone, leftUpperWishboneChassisMount.position, Vector3.right);
                    leftUpperWishboneBall = CustomJoints.CreateSphereJoint(leftUpperWishbone, leftWheelHubMount.gameObject, leftUpperWishboneHubMount.position);
                    // leftUpperWishboneBall.angularZMotion = ConfigurableJointMotion.Limited;
                    // leftUpperWishboneBall.angularZLimit = new SoftJointLimit { limit = 45f };
                    leftUpperWishboneBall.angularYMotion = ConfigurableJointMotion.Locked;

                    // Right upper wishbone
                    rightUpperWishbone = new GameObject("R_UPPER_WB");
                    rightUpperWishbone.transform.SetParent(transform);
                    rightUpperWishbone.transform.position = (rightUpperWishboneChassisMount.position + rightUpperWishboneHubMount.position) * 0.5f;
                    rightUpperWishbone.AddComponent<Rigidbody>().mass = 5f;
                    rightUpperWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, rightUpperWishbone, rightUpperWishboneChassisMount.position, Vector3.right);
                    rightUpperWishboneBall = CustomJoints.CreateSphereJoint(rightUpperWishbone, rightWheelHubMount.gameObject, rightUpperWishboneHubMount.position);
                    // rightUpperWishboneBall.angularZMotion = ConfigurableJointMotion.Limited;
                    // rightUpperWishboneBall.angularZLimit = new SoftJointLimit { limit = 45f };
                    rightUpperWishboneBall.angularYMotion = ConfigurableJointMotion.Locked;

                    // Set axis to be relative to the spring
                    leftUpperWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(leftUpperWishboneHubMount.position - leftLowerWishboneHubMount.position).normalized;
                    rightUpperWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(rightUpperWishboneHubMount.position - rightLowerWishboneHubMount.position).normalized;
                }
            }
            else // Leaf Spring Suspension
                 // TODO: sideways force of the leafs are too weak, axle moves sideways too much
                 // TODO: rethink wheel rate for leafs
                 // TODO: use one spring instead? same effect as leafs but limit YZ movement
                 // TODO: merge leaf hub mount with spring hub mount?
            {
                float leafSpringLength = Vector3.Distance(leftFrontLeafChassisMount.position, leftRearLeafChassisMount.position) * 0.5f;
                // Left side joints
                leftSpringJoint = CustomJoints.CreateSpringJoint(
                    vehicleBody.gameObject, leftWheelHubMount.gameObject,
                    leftSpringChassisMount.position, leftSpringHubMount.position,
                    springConstant, damperConstant, springLength);

                // frontLeftLeafSpringJoint = CustomJoints.CreateSpringJoint(
                //     vehicleBody.gameObject, leftWheelHubMount.gameObject,
                //     leftFrontLeafChassisMount.position, leftLeafHubMount.position,
                //     springConstant / 2f, damperConstant / 2f, leafSpringLength);

                // rearLeftLeafSpringJoint = CustomJoints.CreateSpringJoint(
                //     vehicleBody.gameObject, leftWheelHubMount.gameObject,
                //     leftRearLeafChassisMount.position, leftLeafHubMount.position,
                //     springConstant / 2f, damperConstant / 2f, leafSpringLength);


                // Right side joints
                rightSpringJoint = CustomJoints.CreateSpringJoint(
                    vehicleBody.gameObject, rightWheelHubMount.gameObject,
                    rightSpringChassisMount.position, rightSpringHubMount.position,
                    springConstant, damperConstant, springLength);

                // rightFrontLeafSpringJoint = CustomJoints.CreateSpringJoint(
                //     vehicleBody.gameObject, rightWheelHubMount.gameObject,
                //     rightFrontLeafChassisMount.position, rightLeafHubMount.position,
                //     springConstant / 2f, damperConstant / 2f, leafSpringLength);

                // rightRearLeafSpringJoint = CustomJoints.CreateSpringJoint(
                //     vehicleBody.gameObject, rightWheelHubMount.gameObject,
                //     rightRearLeafChassisMount.position, rightLeafHubMount.position,
                //     springConstant / 2f, damperConstant / 2f, leafSpringLength);

                // Disable spring angular drive for solid axle (the axle already prevents camber and toe)
                // leftSpringJoint.angularXDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = 0f };
                // leftSpringJoint.angularYZDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = 0f };
                // rightSpringJoint.angularXDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = 0f };
                // rightSpringJoint.angularYZDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = 0f };
                leftSpringJoint.angularYMotion = ConfigurableJointMotion.Locked;
                rightSpringJoint.angularYMotion = ConfigurableJointMotion.Locked;

                axleJoint = CustomJoints.CreateAxleJoint(leftWheelHubMount.gameObject, rightWheelHubMount.gameObject);
                axleJoint = CustomJoints.CreateAxleJoint(rightWheelHubMount.gameObject, leftWheelHubMount.gameObject);
            }

            // Simple wheel damper rate solution
            // Works better with PhysX dampers than getting the rate from motion ratio
            leftWheelDampingRate = leftWheelHub.wheelMass + leftWheelHub.tireMass;
            rightWheelDampingRate = rightWheelHub.wheelMass + rightWheelHub.tireMass;
        }
        void FixedUpdate()
        {
            leftWheelHub.UpdateSteering(steerable ? steeringInput : 0f);
            rightWheelHub.UpdateSteering(steerable ? steeringInput : 0f);
            leftWheelHub.Step();
            rightWheelHub.Step();

            Vector3 leftSpringChassisAnchor = leftSpringJoint.transform.TransformPoint(leftSpringJoint.anchor);
            Vector3 leftSpringHubAnchor = leftSpringJoint.connectedBody.transform.TransformPoint(leftSpringJoint.connectedAnchor);
            Vector3 rightSpringChassisAnchor = rightSpringJoint.transform.TransformPoint(rightSpringJoint.anchor);
            Vector3 rightSpringHubAnchor = rightSpringJoint.connectedBody.transform.TransformPoint(rightSpringJoint.connectedAnchor);

            // Calculate wheel rates
            float Ls_left = Vector3.Distance(leftSpringChassisAnchor, leftSpringHubAnchor);
            float Lw_left = Vector3.Distance(leftSpringHubAnchor, leftWheelHub.wheelCenter);
            leftWheelRate = Mathf.Pow(Ls_left / Lw_left, 2) * springConstant;

            float Ls_right = Vector3.Distance(rightSpringChassisAnchor, rightSpringHubAnchor);
            float Lw_right = Vector3.Distance(rightSpringHubAnchor, rightWheelHub.wheelCenter);
            rightWheelRate = Mathf.Pow(Ls_right / Lw_right, 2) * springConstant;

            // Calculate wheel damping rates
            // Cwheel = Cdamp * motion ratio^2
            // leftWheelDampingRate = Mathf.Pow(Ls_left / Lw_left, 2) * damperConstant;
            // rightWheelDampingRate = Mathf.Pow(Ls_right / Lw_right, 2) * damperConstant;

            if (hasAntirollBar)
            {
                float leftCompression = Ls_left - springLength;
                float rightCompression = Ls_right - springLength;
                float antirollForce = (leftCompression - rightCompression) * antirollBarStiffness;

                // leftWheelHub.hubBody.AddForceAtPosition(leftWheelHub.hubBody.transform.up * -antirollForce, leftSpringHubAnchor);
                // rightWheelHub.hubBody.AddForceAtPosition(rightWheelHub.hubBody.transform.up * antirollForce, rightSpringHubAnchor);
                vehicleBody.AddTorque(vehicleBody.transform.forward * antirollForce);
            }
        }
        public void SetBrakeInput(float brakeInput)
        {
            float leftBrakeTorque = brakeInput * leftWheelHub.maxBrakeTorque;
            float rightBrakeTorque = brakeInput * rightWheelHub.maxBrakeTorque;
            leftWheelHub.ApplyBrakeTorque(leftBrakeTorque);
            rightWheelHub.ApplyBrakeTorque(rightBrakeTorque);
        }
        [ContextMenu("Find Geometry Objects")]
        public void FindGameObjects()
        {
            // Find suspension geometry gameobjects in children
            foreach (Transform child in transform)
            {
                // Wishbone mounts
                if (child.name == "L_LOWER_WB_CH") leftLowerWishboneChassisMount = child;
                else if (child.name == "L_LOWER_WB_HUB") leftLowerWishboneHubMount = child;
                else if (child.name == "L_UPPER_WB_CH") leftUpperWishboneChassisMount = child;
                else if (child.name == "L_UPPER_WB_HUB") leftUpperWishboneHubMount = child;
                else if (child.name == "R_LOWER_WB_CH") rightLowerWishboneChassisMount = child;
                else if (child.name == "R_LOWER_WB_HUB") rightLowerWishboneHubMount = child;
                else if (child.name == "R_UPPER_WB_CH") rightUpperWishboneChassisMount = child;
                else if (child.name == "R_UPPER_WB_HUB") rightUpperWishboneHubMount = child;

                // Leaf spring mounts
                else if (child.name == "L_FRONT_LEAF_CH") leftFrontLeafChassisMount = child;
                else if (child.name == "L_REAR_LEAF_CH") leftRearLeafChassisMount = child;
                else if (child.name == "L_LEAF_HUB") leftLeafHubMount = child;
                else if (child.name == "R_FRONT_LEAF_CH") rightFrontLeafChassisMount = child;
                else if (child.name == "R_REAR_LEAF_CH") rightRearLeafChassisMount = child;
                else if (child.name == "R_LEAF_HUB") rightLeafHubMount = child;

                // Spring mounts
                else if (child.name == "L_SPRING_CH") leftSpringChassisMount = child;
                else if (child.name == "L_SPRING_HUB") leftSpringHubMount = child;
                else if (child.name == "R_SPRING_CH") rightSpringChassisMount = child;
                else if (child.name == "R_SPRING_HUB") rightSpringHubMount = child;

                // Hubs
                else if (child.name == "L_WHEEL_HUB") leftWheelHubMount = child;
                else if (child.name == "R_WHEEL_HUB") rightWheelHubMount = child;
            }
        }
        void OnDrawGizmos()
        {
            const float sphereSize = 0.025f;
            // Left lower wishbone
            if (leftLowerWishboneHinge != null && leftLowerWishboneBall != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    leftLowerWishboneHinge.transform.TransformPoint(leftLowerWishboneHinge.anchor),
                    leftLowerWishboneBall.transform.TransformPoint(leftLowerWishboneBall.anchor)
                );
                Gizmos.DrawCube(leftLowerWishboneHinge.transform.TransformPoint(leftLowerWishboneHinge.anchor), sphereSize * Vector3.one);
                Gizmos.DrawSphere(leftLowerWishboneBall.transform.TransformPoint(leftLowerWishboneBall.anchor), sphereSize);
            }
            else if (leftLowerWishboneChassisMount != null && leftLowerWishboneHubMount != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    leftLowerWishboneChassisMount.position,
                    leftLowerWishboneHubMount.position
                );
                Gizmos.DrawCube(leftLowerWishboneChassisMount.position, sphereSize * Vector3.one);
                Gizmos.DrawSphere(leftLowerWishboneHubMount.position, sphereSize);
            }
            // Left upper wishbone
            if (leftUpperWishboneHinge != null && leftUpperWishboneBall != null && suspensionType == SuspensionType.DoubleWishbone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    leftUpperWishboneHinge.transform.TransformPoint(leftUpperWishboneHinge.anchor),
                    leftUpperWishboneBall.transform.TransformPoint(leftUpperWishboneBall.anchor)
                );
                Gizmos.DrawCube(leftUpperWishboneHinge.transform.TransformPoint(leftUpperWishboneHinge.anchor), Vector3.one * sphereSize);
                Gizmos.DrawSphere(leftUpperWishboneBall.transform.TransformPoint(leftUpperWishboneBall.anchor), sphereSize);
            }
            else if (leftUpperWishboneChassisMount != null && leftUpperWishboneHubMount != null && suspensionType == SuspensionType.DoubleWishbone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    leftUpperWishboneChassisMount.position,
                    leftUpperWishboneHubMount.position
                );
                Gizmos.DrawCube(leftUpperWishboneChassisMount.position, Vector3.one * sphereSize);
                Gizmos.DrawSphere(leftUpperWishboneHubMount.position, sphereSize);
            }
            // Right lower wishbone
            if (rightLowerWishboneHinge != null && rightLowerWishboneBall != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    rightLowerWishboneHinge.transform.TransformPoint(rightLowerWishboneHinge.anchor),
                    rightLowerWishboneBall.transform.TransformPoint(rightLowerWishboneBall.anchor)
                );
                Gizmos.DrawCube(rightLowerWishboneHinge.transform.TransformPoint(rightLowerWishboneHinge.anchor), sphereSize * Vector3.one);
                Gizmos.DrawSphere(rightLowerWishboneBall.transform.TransformPoint(rightLowerWishboneBall.anchor), sphereSize);
            }
            else if (rightLowerWishboneChassisMount != null && rightLowerWishboneHubMount != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    rightLowerWishboneChassisMount.position,
                    rightLowerWishboneHubMount.position
                );
                Gizmos.DrawCube(rightLowerWishboneChassisMount.position, sphereSize * Vector3.one);
                Gizmos.DrawSphere(rightLowerWishboneHubMount.position, sphereSize);
            }
            // Right upper wishbone
            if (rightUpperWishboneHinge != null && rightUpperWishboneBall != null && suspensionType == SuspensionType.DoubleWishbone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    rightUpperWishboneHinge.transform.TransformPoint(rightUpperWishboneHinge.anchor),
                    rightUpperWishboneBall.transform.TransformPoint(rightUpperWishboneBall.anchor)
                );
                Gizmos.DrawCube(rightUpperWishboneHinge.transform.TransformPoint(rightUpperWishboneHinge.anchor), Vector3.one * sphereSize);
                Gizmos.DrawSphere(rightUpperWishboneBall.transform.TransformPoint(rightUpperWishboneBall.anchor), sphereSize);
            }
            else if (rightUpperWishboneChassisMount != null && rightUpperWishboneHubMount != null && suspensionType == SuspensionType.DoubleWishbone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(
                    rightUpperWishboneChassisMount.position,
                    rightUpperWishboneHubMount.position
                );
                Gizmos.DrawCube(rightUpperWishboneChassisMount.position, Vector3.one * sphereSize);
                Gizmos.DrawSphere(rightUpperWishboneHubMount.position, sphereSize);
            }

            // Left spring joint
            if (leftSpringJoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    leftSpringJoint.transform.TransformPoint(leftSpringJoint.anchor),
                    leftSpringJoint.connectedBody.transform.TransformPoint(leftSpringJoint.connectedAnchor)
                );

                // Draw the target position relative to the spring's axis
                // Gizmos.color = Color.red;
                // Vector3 springAnchorWorld = springJoint.transform.TransformPoint(springJoint.anchor);
                // Vector3 targetPositionWorld = springAnchorWorld + springJoint.axis * springJoint.targetPosition.magnitude;
                // Gizmos.DrawSphere(targetPositionWorld, sphereSize);
            }
            else if (leftSpringChassisMount != null && leftSpringHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    leftSpringChassisMount.position,
                    leftSpringHubMount.position
                );
            }
            Gizmos.color = Color.yellow;
            if (leftSpringJoint != null)
            {
                Gizmos.DrawSphere(leftSpringJoint.transform.TransformPoint(leftSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(leftSpringJoint.connectedBody.transform.TransformPoint(leftSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (leftSpringChassisMount != null) Gizmos.DrawSphere(leftSpringChassisMount.position, sphereSize);
                if (leftSpringHubMount != null) Gizmos.DrawSphere(leftSpringHubMount.position, sphereSize);
            }

            // Right spring joint
            if (rightSpringJoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    rightSpringJoint.transform.TransformPoint(rightSpringJoint.anchor),
                    rightSpringJoint.connectedBody.transform.TransformPoint(rightSpringJoint.connectedAnchor)
                );

                // Draw the target position relative to the spring's axis
                // Gizmos.color = Color.red;
                // Vector3 springAnchorWorld = springJoint.transform.TransformPoint(springJoint.anchor);
                // Vector3 targetPositionWorld = springAnchorWorld + springJoint.axis * springJoint.targetPosition.magnitude;
                // Gizmos.DrawSphere(targetPositionWorld, sphereSize);
            }
            else if (rightSpringChassisMount != null && rightSpringHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    rightSpringChassisMount.position,
                    rightSpringHubMount.position
                );
            }
            Gizmos.color = Color.yellow;
            if (rightSpringJoint != null)
            {
                Gizmos.DrawSphere(rightSpringJoint.transform.TransformPoint(rightSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(rightSpringJoint.connectedBody.transform.TransformPoint(rightSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (rightSpringChassisMount != null) Gizmos.DrawSphere(rightSpringChassisMount.position, sphereSize);
                if (rightSpringHubMount != null) Gizmos.DrawSphere(rightSpringHubMount.position, sphereSize);
            }

            // Leaf spring joints
            // Draw front leaf spring joints
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
                if (leftFrontLeafChassisMount != null && leftFrontLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(leftFrontLeafChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(leftLeafHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        leftFrontLeafChassisMount.position,
                        leftLeafHubMount.position
                    );
                }

            }
            if (rightFrontLeafSpringJoint != null)
            {
                Gizmos.DrawLine(
                    rightFrontLeafSpringJoint.transform.TransformPoint(rightFrontLeafSpringJoint.anchor),
                    rightFrontLeafSpringJoint.connectedBody.transform.TransformPoint(rightFrontLeafSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(rightFrontLeafSpringJoint.transform.TransformPoint(rightFrontLeafSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(rightFrontLeafSpringJoint.connectedBody.transform.TransformPoint(rightFrontLeafSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (rightFrontLeafChassisMount != null && rightFrontLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(rightFrontLeafChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(rightLeafHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        rightFrontLeafChassisMount.position,
                        rightLeafHubMount.position
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
                if (leftRearLeafChassisMount != null && leftRearLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(leftRearLeafChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(leftLeafHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        leftRearLeafChassisMount.position,
                        leftLeafHubMount.position
                    );
                }
            }
            if (rightRearLeafSpringJoint != null)
            {
                Gizmos.DrawLine(
                    rightRearLeafSpringJoint.transform.TransformPoint(rightRearLeafSpringJoint.anchor),
                    rightRearLeafSpringJoint.connectedBody.transform.TransformPoint(rightRearLeafSpringJoint.connectedAnchor)
                );
                Gizmos.DrawSphere(rightRearLeafSpringJoint.transform.TransformPoint(rightRearLeafSpringJoint.anchor), sphereSize);
                Gizmos.DrawSphere(rightRearLeafSpringJoint.connectedBody.transform.TransformPoint(rightRearLeafSpringJoint.connectedAnchor), sphereSize);
            }
            else
            {
                if (rightRearLeafChassisMount != null && rightRearLeafChassisMount.position != null)
                {
                    Gizmos.DrawSphere(rightRearLeafChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(rightLeafHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        rightRearLeafChassisMount.position,
                        rightLeafHubMount.position
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
                if (rightSpringChassisMount != null && rightSpringChassisMount.position != null)
                {
                    Gizmos.DrawSphere(rightSpringChassisMount.position, sphereSize);
                    Gizmos.DrawSphere(rightSpringHubMount.position, sphereSize);
                    Gizmos.DrawLine(
                        rightSpringChassisMount.position,
                        rightSpringHubMount.position
                    );
                }
            }
            // Draw axle joint
            if (leftWheelHubMount != null && rightWheelHubMount != null && suspensionType == SuspensionType.LeafSpring)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(
                    leftWheelHubMount.position,
                    rightWheelHubMount.position
                );
            }

            // Draw anti-roll bar
            if (hasAntirollBar)
            {
                Gizmos.color = Color.red;
                if (leftAntirollBarJoint != null)
                {
                    Gizmos.DrawLine(
                        leftAntirollBarJoint.transform.TransformPoint(leftAntirollBarJoint.anchor),
                        leftAntirollBarJoint.connectedBody.transform.TransformPoint(leftAntirollBarJoint.connectedAnchor)
                    );
                    Gizmos.DrawSphere(leftAntirollBarJoint.transform.TransformPoint(leftAntirollBarJoint.anchor), sphereSize);
                    Gizmos.DrawSphere(leftAntirollBarJoint.connectedBody.transform.TransformPoint(leftAntirollBarJoint.connectedAnchor), sphereSize);
                }
                if (rightAntirollBarJoint != null)
                {
                    Gizmos.DrawLine(
                        rightAntirollBarJoint.transform.TransformPoint(rightAntirollBarJoint.anchor),
                        rightAntirollBarJoint.connectedBody.transform.TransformPoint(rightAntirollBarJoint.connectedAnchor)
                    );
                    Gizmos.DrawSphere(rightAntirollBarJoint.transform.TransformPoint(rightAntirollBarJoint.anchor), sphereSize);
                    Gizmos.DrawSphere(rightAntirollBarJoint.connectedBody.transform.TransformPoint(rightAntirollBarJoint.connectedAnchor), sphereSize);
                }
            }
        }
        void OnDrawGizmosSelected()
        {
            // Draw wheel hubs
            const float sphereSize = 0.1f;
            if (leftWheelHubMount != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(leftWheelHubMount.position, sphereSize);
            }
            if (rightWheelHubMount != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(rightWheelHubMount.position, sphereSize);
            }
            // Draw wheel radius and width
            if (leftWheelHub != null)
            {
                Gizmos.color = Color.gray;
                Vector3 wheelCenter = leftWheelHubMount.position + leftWheelHubMount.right * (leftWheelHub.rightSided ? hubSpacing : -hubSpacing);
                //Gizmos.DrawWireSphere(wheelCenter, wheelRadius);
                Vector3 wheelEdge1 = wheelCenter + leftWheelHubMount.forward * (leftWheelHub.wheelWidth * 0.5f);
                Vector3 wheelEdge2 = wheelCenter - leftWheelHubMount.forward * (leftWheelHub.wheelWidth * 0.5f);
                Gizmos.DrawLine(wheelEdge1, wheelEdge2);
            }
            if (rightWheelHub != null)
            {
                Gizmos.color = Color.gray;
                Vector3 wheelCenter = rightWheelHubMount.position + rightWheelHubMount.right * (rightWheelHub.rightSided ? hubSpacing : -hubSpacing);
                //Gizmos.DrawWireSphere(wheelCenter, wheelRadius);
                Vector3 wheelEdge1 = wheelCenter + rightWheelHubMount.forward * (rightWheelHub.wheelWidth * 0.5f);
                Vector3 wheelEdge2 = wheelCenter - rightWheelHubMount.forward * (rightWheelHub.wheelWidth * 0.5f);
                Gizmos.DrawLine(wheelEdge1, wheelEdge2);
            }
        }
    }
}

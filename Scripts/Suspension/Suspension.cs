using System;
using UnityEngine;

namespace VehicleDynamics
{

    public class Suspension : MonoBehaviour
    {
        public enum SuspensionType
        {
            MacPherson,
            DoubleWishbone,
            SolidAxle
        }
        [Header("General Settings")]
        private Rigidbody vehicleBody;
        public VehicleModel vehicleModel;
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
        [SerializeField] private float wishboneMaxAngle = 45f;

        [Header("Spring and Damper Geometry")]
        public Transform leftSpringChassisMount;
        public Transform leftSpringHubMount;
        public Transform rightSpringChassisMount;
        public Transform rightSpringHubMount;
        [Header("Wheel Hub Geometry")]
        public Transform leftWheelHubMount;
        public Transform rightWheelHubMount;

        [Header("Spring Parameters")]
        public float springRestLength = 0.5f;
        public float springStiffness = 39000f; // N/m
        public float bumpStopLength = 0.05f; // Length from spring chassis mount to bump stop engagement
        public float bumpStopStiffness = 200000f; // N/m
        public float bumpStopBumpDamping = 20000f; // N/m/s
        public float bumpStopReboundDamping = 20000f; // N/m/s
        [Header("Damper Parameters")]
        public float bumpStiffness = 2500f; // N/m/s
        public float reboundStiffness = 3000f; // N/m/s
        public float fastBumpStiffness = 2000f; // N/m/s
        public float fastReboundStiffness = 2500f; // N/m/s
        public float fastBumpThreshold = 0.3f; // m/s
        public float fastReboundThreshold = 0.3f; // m/s

        [Header("Steering Parameters")]
        public bool steerable = false;
        [HideInInspector]public float steeringInput = 0f; // -1 (full left) to 1 (full right)
        public float maxSteeringAngle = 45f;
        [Range(-1f, 1f)]
        public float ackermanPercentage = 1f;
        public bool setAckermannFromGeometry = false;
        [Tooltip("For calculating Ackermann from suspension geometry, assign the opposite suspension here.")]
        public Suspension oppositeSuspension; // For Ackermann steering calculations
        private float turningRadius = 6f;

        [Header("Wheel Parameters")]
        public float hubSpacing = 0.1f; // Distance between the hub and the wheel center
        public float camberAdjustment = 0f;
        public float toeAdjustment = 0f;
        [Header("Brake Parameters")]
        public float brakePressure = 5000f; // kPa
        public float handbrakePressure = 0f; // kPa
        public float brakePadCount = 2f;
        public float brakeFrictionCoefficient = 0.5f;
        public float brakeRotorRadius = 0.15f; // m
        public float brakePistonArea = 5f; // cm^2

        [Header("Anti-Roll Bar Settings")]
        public bool hasAntirollBar = false;
        public float antirollBarStiffness = 5000f;
        [HideInInspector] public float antirollForce = 0f;

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
        private ConfigurableJoint leftAxleJoint;
        private ConfigurableJoint rightAxleJoint;
        // Struts
        [HideInInspector] public Strut leftStrut;
        [HideInInspector] public Strut rightStrut;

        // New gameobjects for joints
        private GameObject leftLowerWishbone;
        private GameObject leftUpperWishbone;
        private GameObject rightLowerWishbone;
        private GameObject rightUpperWishbone;
        private GameObject axleObject;

        // WheelHub objects
        private Hub leftWheelHub;
        private Hub rightWheelHub;

        private float trackWidth; // Calculated from wheel hub positions
        private float wheelBase;  // Distance from COM to suspension

        void Awake()
        {
            // Get vehicle body
            if (vehicleBody == null)
            {
                vehicleBody = GetComponentInParent<Rigidbody>();
                Debug.Assert(vehicleBody != null, "Rigidbody component not found on parent VehicleModel GameObject.");
            }
            vehicleModel = vehicleBody.GetComponent<VehicleModel>();
            // Setup hubs
            leftWheelHub = leftWheelHubMount.GetComponent<Hub>();
            rightWheelHub = rightWheelHubMount.GetComponent<Hub>();

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
        }
        void Start()
        {
            if (suspensionType != SuspensionType.SolidAxle)
            {
                // Left side joints
                leftStrut = new Strut(
                    vehicleBody,
                    leftWheelHub.hubBody,
                    leftSpringChassisMount.position,
                    leftSpringHubMount.position,
                    springStiffness,
                    springRestLength,
                    bumpStopLength,
                    bumpStopStiffness,
                    bumpStopBumpDamping,
                    bumpStopReboundDamping,
                    bumpStiffness,
                    reboundStiffness,
                    fastBumpStiffness,
                    fastReboundStiffness,
                    fastBumpThreshold,
                    fastReboundThreshold
                );
                leftLowerWishbone = new GameObject("L_LOWER_WB");
                leftLowerWishbone.transform.SetParent(transform);
                leftLowerWishbone.transform.position = (leftLowerWishboneChassisMount.position + leftLowerWishboneHubMount.position) * 0.5f;
                leftLowerWishbone.AddComponent<Rigidbody>().mass = 5f;
                leftLowerWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, leftLowerWishbone, leftLowerWishboneChassisMount.position, Vector3.right, wishboneMaxAngle);
                leftLowerWishboneBall = CustomJoints.CreateSphereJoint(leftLowerWishbone, leftWheelHubMount.gameObject, leftLowerWishboneHubMount.position);

                // Right side joints
                rightStrut = new Strut(
                    vehicleBody,
                    rightWheelHub.hubBody,
                    rightSpringChassisMount.position,
                    rightSpringHubMount.position,
                    springStiffness,
                    springRestLength,
                    bumpStopLength,
                    bumpStopStiffness,
                    bumpStopBumpDamping,
                    bumpStopReboundDamping,
                    bumpStiffness,
                    reboundStiffness,
                    fastBumpStiffness,
                    fastReboundStiffness,
                    fastBumpThreshold,
                    fastReboundThreshold
                );
                rightLowerWishbone = new GameObject("R_LOWER_WB");
                rightLowerWishbone.transform.SetParent(transform);
                rightLowerWishbone.transform.position = (rightLowerWishboneChassisMount.position + rightLowerWishboneHubMount.position) * 0.5f;
                rightLowerWishbone.AddComponent<Rigidbody>().mass = 5f;
                rightLowerWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, rightLowerWishbone, rightLowerWishboneChassisMount.position, Vector3.right, wishboneMaxAngle);
                rightLowerWishboneBall = CustomJoints.CreateSphereJoint(rightLowerWishbone, rightWheelHubMount.gameObject, rightLowerWishboneHubMount.position);

                // Set axis to be relative to the spring
                leftLowerWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(leftLowerWishboneChassisMount.position - leftSpringHubMount.position).normalized;
                rightLowerWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(rightLowerWishboneChassisMount.position - rightSpringHubMount.position).normalized;

                if (suspensionType == SuspensionType.DoubleWishbone)
                {
                    // Left upper wishbone
                    leftUpperWishbone = new GameObject("L_UPPER_WB");
                    leftUpperWishbone.transform.SetParent(transform);
                    leftUpperWishbone.transform.position = (leftUpperWishboneChassisMount.position + leftUpperWishboneHubMount.position) * 0.5f;
                    leftUpperWishbone.AddComponent<Rigidbody>().mass = 5f;
                    leftUpperWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, leftUpperWishbone, leftUpperWishboneChassisMount.position, Vector3.right);
                    leftUpperWishboneBall = CustomJoints.CreateSphereJoint(leftUpperWishbone, leftWheelHubMount.gameObject, leftUpperWishboneHubMount.position);

                    // Right upper wishbone
                    rightUpperWishbone = new GameObject("R_UPPER_WB");
                    rightUpperWishbone.transform.SetParent(transform);
                    rightUpperWishbone.transform.position = (rightUpperWishboneChassisMount.position + rightUpperWishboneHubMount.position) * 0.5f;
                    rightUpperWishbone.AddComponent<Rigidbody>().mass = 5f;
                    rightUpperWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, rightUpperWishbone, rightUpperWishboneChassisMount.position, Vector3.right);
                    rightUpperWishboneBall = CustomJoints.CreateSphereJoint(rightUpperWishbone, rightWheelHubMount.gameObject, rightUpperWishboneHubMount.position);

                    // Set axis to be relative to the spring
                    leftUpperWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(leftUpperWishboneChassisMount.position - leftSpringHubMount.position).normalized;
                    rightUpperWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(rightUpperWishboneChassisMount.position - rightSpringHubMount.position).normalized;
                }
            }
            else // Solid Axle Suspension
            {
                axleObject = new GameObject("AXLE");
                axleObject.transform.SetParent(transform);
                axleObject.transform.position = (leftWheelHubMount.position + rightWheelHubMount.position) * 0.5f;
                Rigidbody axleBody = axleObject.AddComponent<Rigidbody>();
                axleBody.mass = 20f;
                axleBody.linearDamping = 0f;
                axleBody.angularDamping = 0f;
                leftAxleJoint = CustomJoints.CreateAxleJoint(leftWheelHubMount.gameObject, axleObject);
                rightAxleJoint = CustomJoints.CreateAxleJoint(rightWheelHubMount.gameObject, axleObject);
                // Left side joints
                leftStrut = new Strut(
                    vehicleBody,
                    leftWheelHub.hubBody,
                    leftSpringChassisMount.position,
                    leftSpringHubMount.position,
                    springStiffness,
                    springRestLength,
                    bumpStopLength,
                    bumpStopStiffness,
                    bumpStopBumpDamping,
                    bumpStopReboundDamping,
                    bumpStiffness,
                    reboundStiffness,
                    fastBumpStiffness,
                    fastReboundStiffness,
                    fastBumpThreshold,
                    fastReboundThreshold,
                    true
                );
                // Right side joints
                rightStrut = new Strut(
                    vehicleBody,
                    rightWheelHub.hubBody,
                    rightSpringChassisMount.position,
                    rightSpringHubMount.position,
                    springStiffness,
                    springRestLength,
                    bumpStopLength,
                    bumpStopStiffness,
                    bumpStopBumpDamping,
                    bumpStopReboundDamping,
                    bumpStiffness,
                    reboundStiffness,
                    fastBumpStiffness,
                    fastReboundStiffness,
                    fastBumpThreshold,
                    fastReboundThreshold,
                    true
                );
            }

            // Calculate track width
            trackWidth = Vector3.Distance(leftWheelHub.wheelCenter, rightWheelHub.wheelCenter);
            // Calculate wheelbase
            float COMzAxis = vehicleBody.transform.InverseTransformPoint(vehicleBody.worldCenterOfMass).z;
            wheelBase = Mathf.Abs(COMzAxis - vehicleBody.transform.InverseTransformPoint(leftWheelHubMount.position).z);
        }
        public void Step(float dt)
        {
            leftWheelHub.Step(dt);
            rightWheelHub.Step(dt);

            // Update strut params (debug)
            leftStrut.SetSpringParameters(springStiffness, springRestLength, bumpStopLength, bumpStopStiffness, bumpStopBumpDamping, bumpStopReboundDamping);
            leftStrut.SetDamperParameters(bumpStiffness, reboundStiffness, fastBumpStiffness, fastReboundStiffness, fastBumpThreshold, fastReboundThreshold);
            rightStrut.SetSpringParameters(springStiffness, springRestLength, bumpStopLength, bumpStopStiffness, bumpStopBumpDamping, bumpStopReboundDamping);
            rightStrut.SetDamperParameters(bumpStiffness, reboundStiffness, fastBumpStiffness, fastReboundStiffness, fastBumpThreshold, fastReboundThreshold);

            leftStrut.Step();
            rightStrut.Step();

            if (hasAntirollBar)
            {
                // float G = 79e9f;           // Pa
                // float r = 0.01f;           // m
                // float J = Mathf.PI * Mathf.Pow(r, 4) / 2f;
                // float a = 0.3f;            // m (drop link length)
                // float Lr = 1.5f;           // m (distance between wheels)
                // float arbStiffness = (Lr * G * J) / (a * a); // Units: N·m/rad

                float leftCompression = leftStrut.GetCompression();
                float rightCompression = rightStrut.GetCompression();

                antirollForce = (leftCompression - rightCompression) * -antirollBarStiffness;

                // Use hub center positions for force application points
                // Vector3 leftHubPos = leftWheelHub.hubBody.transform.position;
                // Vector3 rightHubPos = rightWheelHub.hubBody.transform.position;

                leftWheelHub.hubBody.AddForceAtPosition(vehicleBody.transform.up * -antirollForce, leftSpringHubMount.position);
                rightWheelHub.hubBody.AddForceAtPosition(vehicleBody.transform.up * antirollForce, rightSpringHubMount.position);

                vehicleBody.AddForceAtPosition(vehicleBody.transform.up * antirollForce, leftSpringChassisMount.position);
                vehicleBody.AddForceAtPosition(vehicleBody.transform.up * -antirollForce, rightSpringChassisMount.position);
            }

            // Ackermann steering
            if (steerable && oppositeSuspension != null && setAckermannFromGeometry)
            {
                float totalWheelBase = wheelBase + oppositeSuspension.wheelBase;
                turningRadius = totalWheelBase / maxSteeringAngle * Mathf.Rad2Deg;
                float inner = totalWheelBase / (turningRadius - trackWidth * 0.5f) * Mathf.Rad2Deg;
                float outer = totalWheelBase / (turningRadius + trackWidth * 0.5f) * Mathf.Rad2Deg;
                if (steeringInput > 0f) // Right
                {
                    leftWheelHub.steeringAngle = inner * steeringInput;
                    rightWheelHub.steeringAngle = outer * steeringInput;
                }
                else if (steeringInput < 0f) // Left
                {
                    leftWheelHub.steeringAngle = outer * steeringInput;
                    rightWheelHub.steeringAngle = inner * steeringInput;
                }
                else // Straight
                {
                    leftWheelHub.steeringAngle = 0f;
                    rightWheelHub.steeringAngle = 0f;
                }
                leftStrut.SetSteeringAngle(leftWheelHub.steeringAngle + leftWheelHub.toeAngle);
                rightStrut.SetSteeringAngle(rightWheelHub.steeringAngle + rightWheelHub.toeAngle);
            }
            else if (setAckermannFromGeometry == false && steerable)
            {
                float ackermannOffset = maxSteeringAngle * Mathf.Abs(steeringInput) * ackermanPercentage / 2f;
                if (steeringInput > 0f) // Right
                {
                    leftWheelHub.steeringAngle = maxSteeringAngle * steeringInput + ackermannOffset;
                    rightWheelHub.steeringAngle = maxSteeringAngle * steeringInput - ackermannOffset;
                }
                else if (steeringInput < 0f) // Left
                {
                    leftWheelHub.steeringAngle = maxSteeringAngle * steeringInput - ackermannOffset;
                    rightWheelHub.steeringAngle = maxSteeringAngle * steeringInput + ackermannOffset;
                }
                else // Straight
                {
                    leftWheelHub.steeringAngle = 0f;
                    rightWheelHub.steeringAngle = 0f;
                }
                leftStrut.SetSteeringAngle(leftWheelHub.steeringAngle + leftWheelHub.toeAngle);
                rightStrut.SetSteeringAngle(rightWheelHub.steeringAngle + rightWheelHub.toeAngle);
            }
            else
            {
                leftStrut.SetSteeringAngle(leftWheelHub.toeAngle);
                rightStrut.SetSteeringAngle(rightWheelHub.toeAngle);
            }
        }

        public void PostDrivetrainStep(float dt)
        {
            leftWheelHub.PostDrivetrainStep(dt);
            rightWheelHub.PostDrivetrainStep(dt);
        }
        public void SetBrakeInput(float brakeInput)
        {
            leftWheelHub.ApplyBrakePressure(brakeInput * brakePressure);
            rightWheelHub.ApplyBrakePressure(brakeInput * brakePressure);
            if(handbrakePressure > 0f)
            {
                leftWheelHub.ApplyBrakePressure(vehicleModel.handbrakeInput * handbrakePressure);
                rightWheelHub.ApplyBrakePressure(vehicleModel.handbrakeInput * handbrakePressure);
            }
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
            if (leftLowerWishboneHinge != null && leftLowerWishboneBall != null && suspensionType != SuspensionType.SolidAxle)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    leftLowerWishboneHinge.transform.TransformPoint(leftLowerWishboneHinge.anchor),
                    leftLowerWishboneBall.transform.TransformPoint(leftLowerWishboneBall.anchor)
                );
                Gizmos.DrawCube(leftLowerWishboneHinge.transform.TransformPoint(leftLowerWishboneHinge.anchor), sphereSize * Vector3.one);
                Gizmos.DrawSphere(leftLowerWishboneBall.transform.TransformPoint(leftLowerWishboneBall.anchor), sphereSize);
            }
            else if (leftLowerWishboneChassisMount != null && leftLowerWishboneHubMount != null && suspensionType != SuspensionType.SolidAxle)
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
            if (rightLowerWishboneHinge != null && rightLowerWishboneBall != null && suspensionType != SuspensionType.SolidAxle)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(
                    rightLowerWishboneHinge.transform.TransformPoint(rightLowerWishboneHinge.anchor),
                    rightLowerWishboneBall.transform.TransformPoint(rightLowerWishboneBall.anchor)
                );
                Gizmos.DrawCube(rightLowerWishboneHinge.transform.TransformPoint(rightLowerWishboneHinge.anchor), sphereSize * Vector3.one);
                Gizmos.DrawSphere(rightLowerWishboneBall.transform.TransformPoint(rightLowerWishboneBall.anchor), sphereSize);
            }
            else if (rightLowerWishboneChassisMount != null && rightLowerWishboneHubMount != null && suspensionType != SuspensionType.SolidAxle)
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

            // Left strut
            if (leftStrut != null)
            {
                float compressionNorm = Mathf.InverseLerp(-springRestLength, 0f, leftStrut.GetCompression());
                Gizmos.color = Color.Lerp(Color.red, Color.green, compressionNorm);
                Gizmos.DrawLine(
                    leftStrut.GetSpringChassisAnchor(),
                    leftStrut.GetSpringHubAnchor()
                );
                Gizmos.DrawSphere(leftStrut.GetSpringChassisAnchor(), sphereSize);
                Gizmos.DrawSphere(leftStrut.GetSpringHubAnchor(), sphereSize);
                // Bump stop
                Vector3 leftBumpStopPos = leftStrut.GetSpringChassisAnchor() + -leftStrut.GetSpringDirection() * bumpStopLength;
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(leftBumpStopPos, sphereSize * Vector3.one);
                Gizmos.DrawLine(leftBumpStopPos, leftStrut.GetSpringChassisAnchor());
            }
            else if (leftSpringChassisMount != null && leftSpringHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    leftSpringChassisMount.position,
                    leftSpringHubMount.position
                );
                Gizmos.DrawSphere(leftSpringChassisMount.position, sphereSize);
                Gizmos.DrawSphere(leftSpringHubMount.position, sphereSize);
                // Bump stop
                Vector3 leftBumpStopPos = leftSpringChassisMount.position + (leftSpringHubMount.position - leftSpringChassisMount.position).normalized * bumpStopLength;
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(leftBumpStopPos, sphereSize * Vector3.one);
                Gizmos.DrawLine(leftBumpStopPos, leftSpringChassisMount.position);
            }

            // Right strut
            if (rightStrut != null)
            {
                float compressionNorm = Mathf.InverseLerp(-springRestLength, 0f, rightStrut.GetCompression());
                Gizmos.color = Color.Lerp(Color.red, Color.green, compressionNorm);
                Gizmos.DrawLine(
                    rightStrut.GetSpringChassisAnchor(),
                    rightStrut.GetSpringHubAnchor()
                );
                Gizmos.DrawSphere(rightStrut.GetSpringChassisAnchor(), sphereSize);
                Gizmos.DrawSphere(rightStrut.GetSpringHubAnchor(), sphereSize);
                // Bump stop
                Vector3 rightBumpStopPos = rightStrut.GetSpringChassisAnchor() + -rightStrut.GetSpringDirection() * bumpStopLength;
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(rightBumpStopPos, sphereSize * Vector3.one);
                Gizmos.DrawLine(rightBumpStopPos, rightStrut.GetSpringChassisAnchor());
            }
            else if (rightSpringChassisMount != null && rightSpringHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    rightSpringChassisMount.position,
                    rightSpringHubMount.position
                );
                Gizmos.DrawSphere(rightSpringChassisMount.position, sphereSize);
                Gizmos.DrawSphere(rightSpringHubMount.position, sphereSize);
                // Bump stop
                Vector3 rightBumpStopPos = rightSpringChassisMount.position + (rightSpringHubMount.position - rightSpringChassisMount.position).normalized * bumpStopLength;
                Gizmos.color = Color.blue;
                Gizmos.DrawCube(rightBumpStopPos, sphereSize * Vector3.one);
                Gizmos.DrawLine(rightBumpStopPos, rightSpringChassisMount.position);
            }
            // Draw axle joint
            if (leftWheelHubMount != null && rightWheelHubMount != null && suspensionType == SuspensionType.SolidAxle)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(
                    leftWheelHubMount.position,
                    rightWheelHubMount.position
                );
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
                Vector3 wheelEdge1 = wheelCenter + leftWheelHubMount.forward * (leftWheelHub.wheelWidth * 0.5f);
                Vector3 wheelEdge2 = wheelCenter - leftWheelHubMount.forward * (leftWheelHub.wheelWidth * 0.5f);
                Gizmos.DrawLine(wheelEdge1, wheelEdge2);
            }
            if (rightWheelHub != null)
            {
                Gizmos.color = Color.gray;
                Vector3 wheelCenter = rightWheelHubMount.position + rightWheelHubMount.right * (rightWheelHub.rightSided ? hubSpacing : -hubSpacing);
                Vector3 wheelEdge1 = wheelCenter + rightWheelHubMount.forward * (rightWheelHub.wheelWidth * 0.5f);
                Vector3 wheelEdge2 = wheelCenter - rightWheelHubMount.forward * (rightWheelHub.wheelWidth * 0.5f);
                Gizmos.DrawLine(wheelEdge1, wheelEdge2);
            }

            // Draw suspension rest points
            if (leftSpringChassisMount != null && leftSpringHubMount != null)
            {
                Gizmos.color = Color.red;
                Vector3 springDir = (leftSpringHubMount.position - leftSpringChassisMount.position).normalized;
                Vector3 restPoint = leftSpringChassisMount.position + springDir * springRestLength;
                Gizmos.DrawSphere(restPoint, sphereSize * 0.5f);
            }
            if (rightSpringChassisMount != null && rightSpringHubMount != null)
            {
                Gizmos.color = Color.red;
                Vector3 springDir = (rightSpringHubMount.position - rightSpringChassisMount.position).normalized;
                Vector3 restPoint = rightSpringChassisMount.position + springDir * springRestLength;
                Gizmos.DrawSphere(restPoint, sphereSize * 0.5f);
            }
        }
        // Getters
        public float GetTrackWidth()
        {
            return trackWidth;
        }
        public float GetWheelBase()
        {
            return wheelBase;
        }
        public float GetAlignmentTorque()
        {
            float leftTorque = leftWheelHub.GetWheel().GetAlignmentTorque();
            float rightTorque = rightWheelHub.GetWheel().GetAlignmentTorque();
            return leftTorque + rightTorque;
        }

        public Vector3 GetContactForce()
        {
            return leftWheelHub.GetWheel().GetContactForce() + rightWheelHub.GetWheel().GetContactForce();
        }

        public Vector3 GetLeverArm()
        {
            Vector3 leftLeverArm = leftWheelHub.transform.position - vehicleBody.worldCenterOfMass;
            Vector3 rightLeverArm = rightWheelHub.transform.position - vehicleBody.worldCenterOfMass;
            return leftLeverArm + rightLeverArm;
        }
    }
}

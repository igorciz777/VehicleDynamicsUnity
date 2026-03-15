using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{

    public class Suspension : MonoBehaviour
    {
        public enum SuspensionType
        {
            MacPherson,
            DoubleWishbone
        }
        [Header("General Settings")]
        private Rigidbody vehicleBody;
        public VehicleModel vehicleModel;
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
        public Transform leftStrutChassisMount;
        public Transform leftStrutHubMount;
        public Transform rightStrutChassisMount;
        public Transform rightStrutHubMount;
        [Header("Wheel Hub Geometry")]
        public Transform leftWheelHubMount;
        public Transform rightWheelHubMount;

        [Header("Spring Parameters")]
        public float springLength = 0.5f;
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
        [HideInInspector] public float steeringInput = 0f; // -1 (full left) to 1 (full right)
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

        [Header("Debug Settings")]
        public bool drawGizmos = true;
        public bool drawSteeringAxis = false;
        [Header("Runtime Debug Visualization")]
        public bool drawRuntimeDebug = false;
        private static Material runtimeDebugLineMaterial;
        private readonly List<RuntimeDebugLine> runtimeDebugLines = new List<RuntimeDebugLine>(256);

        

        // Independent Suspension joints
        private ConfigurableJoint leftLowerWishboneHinge;
        private ConfigurableJoint leftLowerWishboneBall;
        private ConfigurableJoint leftUpperWishboneHinge;
        private ConfigurableJoint leftUpperWishboneBall;
        private ConfigurableJoint rightLowerWishboneHinge;
        private ConfigurableJoint rightLowerWishboneBall;
        private ConfigurableJoint rightUpperWishboneHinge;
        private ConfigurableJoint rightUpperWishboneBall;

        // Struts
        private Strut leftStrut;
        private Strut rightStrut;

        // New gameobjects for joints
        private GameObject leftLowerWishbone;
        private GameObject leftUpperWishbone;
        private GameObject rightLowerWishbone;
        private GameObject rightUpperWishbone;

        // WheelHub objects
        private Hub leftWheelHub;
        private Hub rightWheelHub;

        private float trackWidth; // Calculated from wheel hub positions
        private float distToCOM;  // Distance from COM to suspension

        private float leftSuspensionTrail;
        private float rightSuspensionTrail;

        public void Init()
        {
            // Get vehicle body
            vehicleBody = GetComponentInParent<Rigidbody>();
            Debug.Assert(vehicleBody != null, "Rigidbody component not found on parent VehicleModel GameObject.");
            // Get vehicle model
            vehicleModel = vehicleBody.GetComponent<VehicleModel>();
            Debug.Assert(vehicleModel != null, "VehicleModel component not found on parent Rigidbody GameObject.");
            // Setup hubs
            leftWheelHub = leftWheelHubMount.GetComponent<Hub>();
            rightWheelHub = rightWheelHubMount.GetComponent<Hub>();
            leftWheelHub.Init(this);
            rightWheelHub.Init(this);
            Debug.Assert(leftWheelHub != null, "Hub component not found on leftWheelHubMount GameObject.");
            Debug.Assert(rightWheelHub != null, "Hub component not found on rightWheelHubMount GameObject.");
            leftStrut = new Strut(
                vehicleBody,
                leftWheelHub.hubBody,
                leftStrutChassisMount.position,
                leftStrutHubMount.position,
                springLength,
                springStiffness,
                bumpStiffness,
                reboundStiffness,
                fastBumpStiffness,
                fastReboundStiffness,
                fastBumpThreshold,
                fastReboundThreshold,
                bumpStopLength,
                bumpStopStiffness,
                bumpStopBumpDamping,
                bumpStopReboundDamping
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
                rightStrutChassisMount.position,
                rightStrutHubMount.position,
                springLength,
                springStiffness,
                bumpStiffness,
                reboundStiffness,
                fastBumpStiffness,
                fastReboundStiffness,
                fastBumpThreshold,
                fastReboundThreshold,
                bumpStopLength,
                bumpStopStiffness,
                bumpStopBumpDamping,
                bumpStopReboundDamping
            );
            rightLowerWishbone = new GameObject("R_LOWER_WB");
            rightLowerWishbone.transform.SetParent(transform);
            rightLowerWishbone.transform.position = (rightLowerWishboneChassisMount.position + rightLowerWishboneHubMount.position) * 0.5f;
            rightLowerWishbone.AddComponent<Rigidbody>().mass = 5f;
            rightLowerWishboneHinge = CustomJoints.CreateRevoluteJoint(vehicleBody.gameObject, rightLowerWishbone, rightLowerWishboneChassisMount.position, Vector3.right, wishboneMaxAngle);
            rightLowerWishboneBall = CustomJoints.CreateSphereJoint(rightLowerWishbone, rightWheelHubMount.gameObject, rightLowerWishboneHubMount.position);

            // Set axis to be relative to the spring
            leftLowerWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(leftLowerWishboneChassisMount.position - leftStrutHubMount.position).normalized;
            rightLowerWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(rightLowerWishboneChassisMount.position - rightStrutHubMount.position).normalized;

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
                leftUpperWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(leftUpperWishboneChassisMount.position - leftStrutHubMount.position).normalized;
                rightUpperWishboneHinge.axis = vehicleBody.transform.InverseTransformDirection(rightUpperWishboneChassisMount.position - rightStrutHubMount.position).normalized;
            }

            // Calculate track width
            trackWidth = Vector3.Distance(leftWheelHub.wheelCenter, rightWheelHub.wheelCenter);
            // Calculate wheelbase
            float COMzAxis = vehicleBody.transform.InverseTransformPoint(vehicleBody.worldCenterOfMass).z;
            distToCOM = Mathf.Abs(COMzAxis - vehicleBody.transform.InverseTransformPoint(leftWheelHubMount.position).z);
        }
        public void Step(float dt)
        {
            leftStrut.Step();
            rightStrut.Step();

            if (hasAntirollBar)
            {
                float leftCompression = leftStrut.GetSpringCompression();
                float rightCompression = rightStrut.GetSpringCompression();

                antirollForce = (leftCompression - rightCompression) * -antirollBarStiffness;

                leftWheelHub.hubBody.AddForceAtPosition(vehicleBody.transform.up * -antirollForce, leftStrutHubMount.position);
                rightWheelHub.hubBody.AddForceAtPosition(vehicleBody.transform.up * antirollForce, rightStrutHubMount.position);

                vehicleBody.AddForceAtPosition(vehicleBody.transform.up * antirollForce, leftStrutChassisMount.position);
                vehicleBody.AddForceAtPosition(vehicleBody.transform.up * -antirollForce, rightStrutChassisMount.position);
            }

            // Ackermann steering
            if (steerable && oppositeSuspension != null && setAckermannFromGeometry)
            {
                float totalWheelBase = distToCOM + oppositeSuspension.distToCOM;
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

            leftWheelHub.Step(dt);
            rightWheelHub.Step(dt);

            // Suspension trail
            if (leftWheelHub.GetWheel().isGrounded)
            {
                Vector3 chassisAnchor = vehicleBody.transform.InverseTransformPoint(leftStrut.GetStrutChassisAnchor());
                Vector3 hubAnchor = vehicleBody.transform.InverseTransformPoint(leftStrut.GetStrutHubAnchor());
                Vector3 contactPoint = vehicleBody.transform.InverseTransformPoint(leftWheelHub.GetWheel().GetContactPoint());

                leftSuspensionTrail = (chassisAnchor.z - hubAnchor.z) / (chassisAnchor.y - hubAnchor.y);
                leftSuspensionTrail *= hubAnchor.y - contactPoint.y;
                leftSuspensionTrail += contactPoint.z - hubAnchor.z;
            }
            else
            {
                leftSuspensionTrail = 0f;
            }

            if (rightWheelHub.GetWheel().isGrounded)
            {
                Vector3 chassisAnchor = vehicleBody.transform.InverseTransformPoint(rightStrut.GetStrutChassisAnchor());
                Vector3 hubAnchor = vehicleBody.transform.InverseTransformPoint(rightStrut.GetStrutHubAnchor());
                Vector3 contactPoint = vehicleBody.transform.InverseTransformPoint(rightWheelHub.GetWheel().GetContactPoint());

                rightSuspensionTrail = (chassisAnchor.z - hubAnchor.z) / (chassisAnchor.y - hubAnchor.y);
                rightSuspensionTrail *= hubAnchor.y - contactPoint.y;
                rightSuspensionTrail += contactPoint.z - hubAnchor.z;
            }
            else
            {
                rightSuspensionTrail = 0f;
            }
        }

        public void PostDrivetrainStep(float dt)
        {
            leftWheelHub.PostDrivetrainStep(dt);
            rightWheelHub.PostDrivetrainStep(dt);
        }
        public void SetBrakeInput(float brakeInput, float handbrakeInput)
        {
            if (brakeInput > 0f)
            {
                leftWheelHub.ApplyBrakePressure(brakeInput * brakePressure);
                rightWheelHub.ApplyBrakePressure(brakeInput * brakePressure);
            }

            if (handbrakePressure > 0f && handbrakeInput > 0f)
            {
                leftWheelHub.ApplyBrakePressure(handbrakeInput * handbrakePressure, true);
                rightWheelHub.ApplyBrakePressure(handbrakeInput * handbrakePressure, true);
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
                else if (child.name == "L_SPRING_CH") leftStrutChassisMount = child;
                else if (child.name == "L_SPRING_HUB") leftStrutHubMount = child;
                else if (child.name == "R_SPRING_CH") rightStrutChassisMount = child;
                else if (child.name == "R_SPRING_HUB") rightStrutHubMount = child;

                // Hubs
                else if (child.name == "L_WHEEL_HUB") leftWheelHubMount = child;
                else if (child.name == "R_WHEEL_HUB") rightWheelHubMount = child;
            }
        }
        void OnDrawGizmos()
        {
            if (!drawGizmos) return;
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

            // Left strut
            if (leftStrut != null)
            {
                Vector3 strutChassisAnchor = leftStrut.GetStrutChassisAnchor();
                Vector3 strutHubAnchor = leftStrut.GetStrutHubAnchor();
                Vector3 springChassisAnchor = leftStrut.GetSpringChassisAnchor();
                Vector3 springHubAnchor = leftStrut.GetSpringHubAnchor();
                // Bump stop
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(strutChassisAnchor, sphereSize);
                Gizmos.DrawLine(strutChassisAnchor, springChassisAnchor);
                // Spring
                float compressionNorm = Mathf.InverseLerp(-springLength, 0f, leftStrut.GetSpringCompression());
                Gizmos.color = Color.Lerp(Color.red, Color.green, compressionNorm);
                Gizmos.DrawLine(springChassisAnchor, springHubAnchor);
                Vector3 springRightDir = leftStrutChassisMount.transform.right;
                Vector3 springForwardDir = leftStrutChassisMount.transform.forward;
                CustomGizmos.DrawCoilSpringGizmo(springChassisAnchor, springHubAnchor, springRightDir, springForwardDir);
                // Rest of strut / damper
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(springHubAnchor, strutHubAnchor);
                Gizmos.DrawSphere(strutHubAnchor, sphereSize);
            }
            else if (leftStrutChassisMount != null && leftStrutHubMount != null)
            {
                Vector3 strutDir = (leftStrutHubMount.position - leftStrutChassisMount.position).normalized;
                Vector3 springChassisAnchor = leftStrutChassisMount.position + strutDir * bumpStopLength;
                Vector3 springHubAnchor = springChassisAnchor + strutDir * springLength;
                // Bump stop
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(leftStrutChassisMount.position, springChassisAnchor);
                Gizmos.DrawSphere(leftStrutChassisMount.position, sphereSize);
                // Spring
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(springChassisAnchor, springHubAnchor);
                Vector3 springRightDir = leftStrutChassisMount.transform.right;
                Vector3 springForwardDir = leftStrutChassisMount.transform.forward;
                CustomGizmos.DrawCoilSpringGizmo(springChassisAnchor, springHubAnchor, springRightDir, springForwardDir);
                // Rest of strut / damper
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(springHubAnchor, leftStrutHubMount.position);
                Gizmos.DrawSphere(leftStrutHubMount.position, sphereSize);
            }

            // Right strut
            if (rightStrut != null)
            {
                Vector3 strutChassisAnchor = rightStrut.GetStrutChassisAnchor();
                Vector3 strutHubAnchor = rightStrut.GetStrutHubAnchor();
                Vector3 springChassisAnchor = rightStrut.GetSpringChassisAnchor();
                Vector3 springHubAnchor = rightStrut.GetSpringHubAnchor();
                // Bump stop
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(strutChassisAnchor, springChassisAnchor);
                Gizmos.DrawSphere(strutChassisAnchor, sphereSize);
                // Spring
                float compressionNorm = Mathf.InverseLerp(-springLength, 0f, rightStrut.GetSpringCompression());
                Gizmos.color = Color.Lerp(Color.red, Color.green, compressionNorm);
                Gizmos.DrawLine(springChassisAnchor, springHubAnchor);
                Vector3 springRightDir = rightStrutChassisMount.transform.right;
                Vector3 springForwardDir = rightStrutChassisMount.transform.forward;
                CustomGizmos.DrawCoilSpringGizmo(springChassisAnchor, springHubAnchor, springRightDir, springForwardDir);
                // Rest of strut / damper
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(springHubAnchor, strutHubAnchor);
                Gizmos.DrawSphere(strutHubAnchor, sphereSize);
            }
            else if (rightStrutChassisMount != null && rightStrutHubMount != null)
            {
                Vector3 strutDir = (rightStrutHubMount.position - rightStrutChassisMount.position).normalized;
                Vector3 springChassisAnchor = rightStrutChassisMount.position + strutDir * bumpStopLength;
                Vector3 springHubAnchor = springChassisAnchor + strutDir * springLength;
                // Bump stop
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(rightStrutChassisMount.position, springChassisAnchor);
                Gizmos.DrawSphere(rightStrutChassisMount.position, sphereSize);
                // Spring
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(springChassisAnchor, springHubAnchor);
                Vector3 springRightDir = rightStrutChassisMount.transform.right;
                Vector3 springForwardDir = rightStrutChassisMount.transform.forward;
                CustomGizmos.DrawCoilSpringGizmo(springChassisAnchor, springHubAnchor, springRightDir, springForwardDir);
                // Rest of strut / damper
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(springHubAnchor, rightStrutHubMount.position);
                Gizmos.DrawSphere(rightStrutHubMount.position, sphereSize);
            }

            if (!drawSteeringAxis) return;
            // Draw strut steering axis
            if (leftStrutChassisMount != null && leftStrutHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 springDir = (leftStrutHubMount.position - leftStrutChassisMount.position).normalized;
                Gizmos.DrawLine(leftStrutChassisMount.position, leftStrutChassisMount.position + springDir * 2f);
            }
            if (rightStrutChassisMount != null && rightStrutHubMount != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 springDir = (rightStrutHubMount.position - rightStrutChassisMount.position).normalized;
                Gizmos.DrawLine(rightStrutChassisMount.position, rightStrutChassisMount.position + springDir * 2f);
            }
        }
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
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
        }
        // Getters
        public float GetTrackWidth() => trackWidth;

        public float GetDistanceToCOM() => distToCOM;
        public float GetTireAlignmentTorque()
        {
            float torqueLeft = leftWheelHub.GetWheel().GetAlignmentTorque();
            float torqueRight = rightWheelHub.GetWheel().GetAlignmentTorque();
            return torqueLeft + torqueRight;
        }
        public float GetSteeringArmTorque()
        {
            float torqueLeft = leftSuspensionTrail * leftWheelHub.GetWheel().GetTireForces().y;
            float torqueRight = rightSuspensionTrail * rightWheelHub.GetWheel().GetTireForces().y;
            return torqueLeft + torqueRight;
        }

        public (Strut, Strut) GetStruts() => (leftStrut, rightStrut);

        private void LateUpdate()
        {
            if (!Application.isPlaying || !drawRuntimeDebug) return;
            RebuildRuntimeDebugLines();
        }

        private void OnRenderObject()
        {
            if (!ShouldRenderRuntimeDebug()) return;

            if (!Application.isPlaying)
            {
                RebuildRuntimeDebugLines();
            }

            if (runtimeDebugLines.Count == 0) return;

            EnsureRuntimeLineMaterial();
            runtimeDebugLineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            for (int i = 0; i < runtimeDebugLines.Count; i++)
            {
                RuntimeDebugLine line = runtimeDebugLines[i];
                GL.Color(line.color);
                GL.Vertex(line.start);
                GL.Vertex(line.end);
            }
            GL.End();
            GL.PopMatrix();
        }

        private bool ShouldRenderRuntimeDebug()
        {
            if (!drawRuntimeDebug) return false;
            return Application.isPlaying;
        }

        private void EnsureRuntimeLineMaterial()
        {
            if (runtimeDebugLineMaterial != null) return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return;

            runtimeDebugLineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeDebugLineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            runtimeDebugLineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            runtimeDebugLineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            runtimeDebugLineMaterial.SetInt("_ZWrite", 0);
        }

        private void RebuildRuntimeDebugLines()
        {
            runtimeDebugLines.Clear();
            if (!drawRuntimeDebug) return;

            float anchorSize = 0.025f;

            DrawWishboneRuntime(
                leftLowerWishboneHinge,
                leftLowerWishboneBall,
                leftLowerWishboneChassisMount,
                leftLowerWishboneHubMount,
                Color.green,
                anchorSize);

            if (suspensionType == SuspensionType.DoubleWishbone)
            {
                DrawWishboneRuntime(
                    leftUpperWishboneHinge,
                    leftUpperWishboneBall,
                    leftUpperWishboneChassisMount,
                    leftUpperWishboneHubMount,
                    Color.cyan,
                    anchorSize);
            }

            DrawWishboneRuntime(
                rightLowerWishboneHinge,
                rightLowerWishboneBall,
                rightLowerWishboneChassisMount,
                rightLowerWishboneHubMount,
                Color.green,
                anchorSize);

            if (suspensionType == SuspensionType.DoubleWishbone)
            {
                DrawWishboneRuntime(
                    rightUpperWishboneHinge,
                    rightUpperWishboneBall,
                    rightUpperWishboneChassisMount,
                    rightUpperWishboneHubMount,
                    Color.cyan,
                    anchorSize);
            }

            DrawStrutRuntime(leftStrut, leftStrutChassisMount, leftStrutHubMount, springLength, anchorSize);
            DrawStrutRuntime(rightStrut, rightStrutChassisMount, rightStrutHubMount, springLength, anchorSize);

            if (drawSteeringAxis)
            {
                DrawSteeringAxisRuntime(leftStrutChassisMount, leftStrutHubMount);
                DrawSteeringAxisRuntime(rightStrutChassisMount, rightStrutHubMount);
            }
        }

        private void DrawWishboneRuntime(
            ConfigurableJoint hinge,
            ConfigurableJoint ball,
            Transform chassisMount,
            Transform hubMount,
            Color color,
            float markerSize)
        {
            if (hinge != null && ball != null)
            {
                Vector3 hingePoint = hinge.transform.TransformPoint(hinge.anchor);
                Vector3 ballPoint = ball.transform.TransformPoint(ball.anchor);
                AddRuntimeLine(hingePoint, ballPoint, color);
                RuntimeDebugDraw.DrawCross(hingePoint, markerSize, color, AddRuntimeLine);
                RuntimeDebugDraw.DrawCross(ballPoint, markerSize, color, AddRuntimeLine);
                return;
            }

            if (chassisMount != null && hubMount != null)
            {
                AddRuntimeLine(chassisMount.position, hubMount.position, color);
                RuntimeDebugDraw.DrawCross(chassisMount.position, markerSize, color, AddRuntimeLine);
                RuntimeDebugDraw.DrawCross(hubMount.position, markerSize, color, AddRuntimeLine);
            }
        }

        private void DrawStrutRuntime(Strut strut, Transform chassisMount, Transform hubMount, float currentSpringLength, float markerSize)
        {
            if (strut != null)
            {
                Vector3 strutChassisAnchor = strut.GetStrutChassisAnchor();
                Vector3 strutHubAnchor = strut.GetStrutHubAnchor();
                Vector3 springChassisAnchor = strut.GetSpringChassisAnchor();
                Vector3 springHubAnchor = strut.GetSpringHubAnchor();

                AddRuntimeLine(strutChassisAnchor, springChassisAnchor, Color.blue);
                RuntimeDebugDraw.DrawCross(strutChassisAnchor, markerSize, Color.blue, AddRuntimeLine);

                float compressionNorm = Mathf.InverseLerp(-currentSpringLength, 0f, strut.GetSpringCompression());
                Color springColor = Color.Lerp(Color.red, Color.green, compressionNorm);
                AddRuntimeLine(springChassisAnchor, springHubAnchor, springColor);

                if (chassisMount != null)
                {
                    RuntimeDebugDraw.DrawCoilSpring(
                        springChassisAnchor,
                        springHubAnchor,
                        chassisMount.right,
                        chassisMount.forward,
                        AddRuntimeLine,
                        springColor);
                }

                AddRuntimeLine(springHubAnchor, strutHubAnchor, Color.gray);
                RuntimeDebugDraw.DrawCross(strutHubAnchor, markerSize, Color.gray, AddRuntimeLine);
                return;
            }

            if (chassisMount != null && hubMount != null)
            {
                Vector3 strutDir = (hubMount.position - chassisMount.position).normalized;
                Vector3 springChassisAnchor = chassisMount.position + strutDir * bumpStopLength;
                Vector3 springHubAnchor = springChassisAnchor + strutDir * currentSpringLength;

                AddRuntimeLine(chassisMount.position, springChassisAnchor, Color.blue);
                RuntimeDebugDraw.DrawCross(chassisMount.position, markerSize, Color.blue, AddRuntimeLine);

                AddRuntimeLine(springChassisAnchor, springHubAnchor, Color.yellow);
                RuntimeDebugDraw.DrawCoilSpring(
                    springChassisAnchor,
                    springHubAnchor,
                    chassisMount.right,
                    chassisMount.forward,
                    AddRuntimeLine,
                    Color.yellow);

                AddRuntimeLine(springHubAnchor, hubMount.position, Color.gray);
                RuntimeDebugDraw.DrawCross(hubMount.position, markerSize, Color.gray, AddRuntimeLine);
            }
        }

        private void DrawSteeringAxisRuntime(Transform chassisMount, Transform hubMount)
        {
            if (chassisMount == null || hubMount == null) return;
            Vector3 springDir = (hubMount.position - chassisMount.position).normalized;
            AddRuntimeLine(chassisMount.position, chassisMount.position + springDir * 2f, Color.yellow);
        }

        private void AddRuntimeLine(Vector3 start, Vector3 end, Color color)
        {
            runtimeDebugLines.Add(new RuntimeDebugLine(start, end, color));
        }
    }
}

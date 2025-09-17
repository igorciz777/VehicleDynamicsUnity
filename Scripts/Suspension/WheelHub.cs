using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace VehicleDynamics
{
    [RequireComponent(typeof(Rigidbody))]
    public class WheelHub : MonoBehaviour
    {
        [HideInInspector] public bool rightSided = false;
        [HideInInspector] public float wheelRate = 0f;
        private float steeringAngle = 0f;
        public float camberAngle = 0f;
        public float toeAngle = 0f;

        public GameObject visualHub;
        public GameObject visualWheel;
        public KinematicSuspension parentSuspension;
        [HideInInspector] public Rigidbody vehicleBody;
        [HideInInspector] public Rigidbody hubBody;
        private Transform springChassisMount;
        private Transform springHubMount;
        private GameObject dummyWheel;
        public Wheel wheel;
        public Vector3 wheelCenter = Vector3.zero;
        private float wheelInertia = 0.0f;

        void Awake()
        {
            parentSuspension = GetComponentInParent<KinematicSuspension>();
            vehicleBody = parentSuspension.GetComponentInParent<Rigidbody>();
            hubBody = GetComponent<Rigidbody>();

            // Check if right sided from vehicle body transform
            if (vehicleBody.transform.InverseTransformPoint(transform.position).x > 0f)
            {
                rightSided = true;
            }

            dummyWheel = new GameObject("dummyWheel");
            dummyWheel.transform.SetParent(parentSuspension.transform);
            wheel = dummyWheel.AddComponent<Wheel>();
            wheel.hub = this;

            wheelCenter = transform.position + transform.right * (rightSided ? parentSuspension.hubSpacing : -parentSuspension.hubSpacing);
            wheelInertia = 0.5f * parentSuspension.wheelMass * parentSuspension.wheelRadius * parentSuspension.wheelRadius;
        }
        void Start()
        {
            if (rightSided)
            {
                springChassisMount = parentSuspension.rightSpringChassisMount;
                springHubMount = parentSuspension.rightSpringHubMount;
            }
            else
            {
                springChassisMount = parentSuspension.leftSpringChassisMount;
                springHubMount = parentSuspension.leftSpringHubMount;
            }
            // Calculate wheel rate
            // float Ls = Vector3.Distance(springChassisMount.position, springHubMount.position);
            // float Lw = Vector3.Distance(springHubMount.position, wheelCenter);
            // wheelRate = Mathf.Pow(Ls / Lw, 2) * parentSuspension.springConstant;

            // Set visual wheel parent to visual hub
            if (visualWheel != null && visualHub != null)
            {
                visualWheel.transform.SetParent(visualHub.transform);
                visualWheel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }
        public void Step()
        {
            wheelCenter = transform.position + transform.right * (rightSided ? parentSuspension.hubSpacing : -parentSuspension.hubSpacing);

            if (rightSided)
            {
                wheelRate = parentSuspension.rightWheelRate;
            }
            else
            {
                wheelRate = parentSuspension.leftWheelRate;
            }

            // Calculate camber relative to vehicle body
            Vector3 wheelUp = transform.up;
            Vector3 bodyUp = vehicleBody.transform.up;
            Vector3 bodyRight = vehicleBody.transform.right;
            camberAngle = Vector3.SignedAngle(wheelUp, bodyUp, bodyRight);

            // Calculate toe relative to vehicle body
            Vector3 wheelForward = transform.forward;
            Vector3 bodyForward = vehicleBody.transform.forward;
            Vector3 bodyUpAxis = vehicleBody.transform.up;
            toeAngle = Vector3.SignedAngle(wheelForward, bodyForward, bodyUpAxis);

            // Rotate dummy wheel to match steering angle
            dummyWheel.transform.localEulerAngles = new Vector3(0f, steeringAngle, 0f);
            dummyWheel.transform.position = wheelCenter;

            if (visualHub != null)
            {
                visualHub.transform.position = transform.position;
                visualHub.transform.localEulerAngles = new Vector3(0f, steeringAngle + (rightSided ? -parentSuspension.toeAdjustment : parentSuspension.toeAdjustment), rightSided ? -parentSuspension.camberAdjustment : parentSuspension.camberAdjustment);
            }
            if (visualWheel != null)
            {
                visualWheel.transform.position = wheelCenter;
                // Rotate wheel based on angular velocity
                visualWheel.transform.Rotate(Vector3.right, wheel.wheelAngularVelocity * Time.fixedDeltaTime * Mathf.Rad2Deg, Space.Self);
            }
            // if (rightSided) camberAngle = -camberAngle;
            if (rightSided) toeAngle = -toeAngle;

            // Camber adjustment
            camberAngle += parentSuspension.camberAdjustment;
            // Toe adjustment
            toeAngle += parentSuspension.toeAdjustment;

            wheel.Step();
        }
        public void UpdateSteering(float steerInput)
        {
            float ackermannOffset = parentSuspension.maxSteeringAngle * Mathf.Abs(steerInput) * parentSuspension.ackermanPercentage / 2f * Mathf.Sign(transform.localPosition.x);
            steeringAngle = parentSuspension.maxSteeringAngle * steerInput + ackermannOffset;
        }
        public void ApplyDriveTorque(float driveTorque)
        {
            wheel.ApplyTorque(driveTorque);
        }
    }
}

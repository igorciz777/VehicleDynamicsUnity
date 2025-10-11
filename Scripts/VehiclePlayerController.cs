//#define USING_DIRECT_INPUT

using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using DirectInputManager;

namespace VehicleDynamics
{
    public class VehiclePlayerController : MonoBehaviour
    {
        public VehicleModel vehicleModel;
        public float steeringAxis = 0f;
        public bool invertSteering = false;
        public float throttleAxis = 0f;
        public bool invertThrottle = false;
        public float brakeAxis = 0f;
        public bool invertBrake = false;
        public float clutchAxis = 0f;
        public bool invertClutch = false;
        public bool handbrakeButton = false;
        public bool shiftUpButton = false;
        public bool shiftDownButton = false;
        public bool keyboardInput = true;
        public bool mouseInput = false;

        public InputAction steeringAction;
        public InputAction throttleAction;
        public InputAction brakeAction;
        public InputAction clutchAction;
        public InputAction handbrakeAction;
        public InputAction shiftUpAction;
        public InputAction shiftDownAction;
        private DirectInputDevice ffbDevice;
        public bool enableFFB = true;

        [Header("FFB Settings")]
        public bool constantForceEnabled = true;
        [Range(-10000f, 10000f)] public int constantForceMagnitude = 0;

        public bool damperForceEnabled = false;
        [Range(-10000f, 10000f)] public int damperMagnitude = 3000;

        public bool frictionForceEnabled = false;
        [Range(-10000f, 10000f)] public int frictionMagnitude = 2000;

        [Header("Advanced FFB Settings")]
        public float alignTorqueScale = 1.0f;
        public float scrubTorqueScale = 0.5f;
        public float geometryTorqueScale = 1.0f;
        public float dampingCoefficient = 0.1f;
        public float inertiaCoefficient = 0.05f;
        public float maxFFBTorque = 10000f;

        private float lastSteeringAxis = 0f;

        private void OnEnable()
        {
            steeringAction.Enable();
            throttleAction.Enable();
            brakeAction.Enable();
            clutchAction.Enable();
            handbrakeAction.Enable();
            shiftUpAction.Enable();
            shiftDownAction.Enable();
        }

        private void OnDisable()
        {
            steeringAction.Disable();
            throttleAction.Disable();
            brakeAction.Disable();
            clutchAction.Disable();
            handbrakeAction.Disable();
            shiftUpAction.Disable();
            shiftDownAction.Disable();
        }

        void Start()
        {
            if (enableFFB)
            {
                InitializeFFBDevice();
            }
        }

        void Update()
        {


            steeringAxis = steeringAction.ReadValue<float>();
            if (invertSteering) steeringAxis = 1f - steeringAxis;
            throttleAxis = throttleAction.ReadValue<float>();
            if (invertThrottle) throttleAxis = 1f - throttleAxis;
            brakeAxis = brakeAction.ReadValue<float>();
            if (invertBrake) brakeAxis = 1f - brakeAxis;
            clutchAxis = clutchAction.ReadValue<float>();
            if (invertClutch) clutchAxis = 1f - clutchAxis;
            handbrakeButton = handbrakeAction.ReadValue<float>() > 0.5f;
            shiftUpButton = shiftUpAction.triggered;
            shiftDownButton = shiftDownAction.triggered;

            if (mouseInput)
            {
                steeringAxis = (Input.mousePosition.x / Screen.width - 0.5f) * 2f; // Normalize to [-1, 1]
                throttleAxis = Input.GetMouseButton(0) ? 1f : 0f; // Left click for throttle
                brakeAxis = Input.GetMouseButton(1) ? 1f : 0f;    // Right click for brake
                clutchAxis = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
                handbrakeButton = Input.GetKey(KeyCode.Space);
                shiftUpButton = Input.GetKeyDown(KeyCode.E);
                shiftDownButton = Input.GetKeyDown(KeyCode.Q);
            }

            // Gear shifting

            if (shiftDownButton)
            {
                vehicleModel.drivetrain.ShiftDown();
            }
            if (shiftUpButton)
            {
                vehicleModel.drivetrain.ShiftUp();
            }


            // Apply inputs to vehicle model
            if (vehicleModel != null)
            {
                vehicleModel.steeringInput = steeringAxis;
                vehicleModel.throttleInput = throttleAxis;
                vehicleModel.brakeInput = brakeAxis;
                vehicleModel.clutchInput = clutchAxis;
            }
        }

        void FixedUpdate()
        {
            if (enableFFB && ffbDevice != null)
            {
                UpdateFFBEffects();
            }
        }

        private void InitializeFFBDevice()
        {
            ffbDevice = steeringAction.controls
                .Select(control => control.device)
                .OfType<DirectInputDevice>()
                .Where(device => device.description.capabilities.Contains("\"FFBCapable\":true"))
                .Where(device => DIManager.Attach(device.description.serial))
                .FirstOrDefault();

            if (ffbDevice != null)
            {
                Debug.Log($"FFB Device: {ffbDevice.name}, Serial: {ffbDevice.description.serial}");
                DIManager.EnableFFBEffect(ffbDevice.description.serial, FFBEffects.ConstantForce);
                DIManager.EnableFFBEffect(ffbDevice.description.serial, FFBEffects.Damper);
                DIManager.EnableFFBEffect(ffbDevice.description.serial, FFBEffects.Friction);
            }
            else
            {
                Debug.LogWarning("No FFB-capable device found.");
            }
        }

        private void UpdateFFBEffects()
        {
            string serial = ffbDevice.description.serial;

            float alignTorque = vehicleModel.alignmentTorque * alignTorqueScale;
            float geometryTorque = CalculateGeometryTorque() * geometryTorqueScale;
            float dampingTorque = -dampingCoefficient * steeringAxis;
            float inertiaTorque = -inertiaCoefficient * (steeringAxis - lastSteeringAxis) / Time.deltaTime;

            float totalTorque = alignTorque + geometryTorque + dampingTorque + inertiaTorque;
            totalTorque = Mathf.Clamp(totalTorque, -maxFFBTorque, maxFFBTorque);

            if (constantForceEnabled)
            {
                DIManager.UpdateConstantForceSimple(serial, (int)totalTorque);
            }

            if (damperForceEnabled)
            {
                DIManager.UpdateDamperSimple(serial, damperMagnitude);
            }

            if (frictionForceEnabled)
            {
                DIManager.UpdateFrictionSimple(serial, frictionMagnitude);
            }

            lastSteeringAxis = steeringAxis;
        }

        private float CalculateGeometryTorque()
        {
            // Approximate geometry torque using suspension forces and steering axis
            Vector3 contactForce = vehicleModel.GetContactForce();
            Vector3 leverArm = vehicleModel.GetLeverArm();
            Vector3 torque = Vector3.Cross(leverArm, contactForce);
            return Vector3.Dot(torque, transform.up); // Project onto steering axis
        }

        private void OnDestroy()
        {
            if (ffbDevice != null)
            {
                DIManager.StopAllFFBEffects(ffbDevice.description.serial);
            }
        }
    }
}
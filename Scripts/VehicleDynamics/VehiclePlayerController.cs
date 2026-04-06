
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;


#if UNITY_STANDALONE_WIN
using DirectInputManager;
#endif

namespace VehicleDynamics
{
    /// <summary>
    /// Translates player input (keyboard/gamepad/steering wheel) to vehicle control and manages force feedback.
    /// </summary>
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
        public float handbrakeAxis = 0f;
        public bool invertHandbrake = false;
        public bool shiftUpButton = false;
        public bool shiftDownButton = false;
        public bool starterButtonHeld = false;
        public bool mouseInput = false;

        public InputAction steeringAction;
        public InputAction throttleAction;
        public InputAction brakeAction;
        public InputAction clutchAction;
        public InputAction handbrakeAction;
        public InputAction shiftUpAction;
        public InputAction shiftDownAction;
        public InputAction starterAction;

#if UNITY_STANDALONE_WIN
        private DirectInputDevice ffbDevice;
#endif
        public bool enableFFB = true;

        [Header("FFB Settings")]
        public bool constantForceEnabled = true;
        [Range(-10000f, 10000f)] public int constantForceMagnitude = 0;

        public bool damperForceEnabled = false;
        [Range(-10000f, 10000f)] public int damperMagnitude = 3000;

        public bool frictionForceEnabled = false;
        [Range(-10000f, 10000f)] public int frictionMagnitude = 2000;

        [Header("Advanced FFB Settings")]
        public float tireAlignTorqueScale = 1.0f;
        public float steeringArmTorqueScale = 1.0f;
        public float dampingCoefficient = 0.1f;
        public float inertiaCoefficient = 0.05f;

        private float lastSteeringAxis = 0f;

        private static void SetActionState(InputAction action, bool enabled)
        {
            if (action == null)
            {
                return;
            }

            if (enabled)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }

        private void OnEnable()
        {
            SetActionState(steeringAction, true);
            SetActionState(throttleAction, true);
            SetActionState(brakeAction, true);
            SetActionState(clutchAction, true);
            SetActionState(handbrakeAction, true);
            SetActionState(shiftUpAction, true);
            SetActionState(shiftDownAction, true);
            SetActionState(starterAction, true);
        }

        private void OnDisable()
        {
            SetActionState(steeringAction, false);
            SetActionState(throttleAction, false);
            SetActionState(brakeAction, false);
            SetActionState(clutchAction, false);
            SetActionState(handbrakeAction, false);
            SetActionState(shiftUpAction, false);
            SetActionState(shiftDownAction, false);
            SetActionState(starterAction, false);
        }

        private void Start()
        {
            if (enableFFB)
            {
                InitializeFFBDevice();
            }
        }

        private void Update()
        {
            if (steeringAction == null || throttleAction == null || brakeAction == null || clutchAction == null || handbrakeAction == null || shiftUpAction == null || shiftDownAction == null)
            {
                return;
            }

            steeringAxis = steeringAction.ReadValue<float>();
            if (invertSteering) steeringAxis = 1f - steeringAxis;
            throttleAxis = throttleAction.ReadValue<float>();
            if (invertThrottle) throttleAxis = 1f - throttleAxis;
            brakeAxis = brakeAction.ReadValue<float>();
            if (invertBrake) brakeAxis = 1f - brakeAxis;
            clutchAxis = clutchAction.ReadValue<float>();
            if (invertClutch) clutchAxis = 1f - clutchAxis;
            handbrakeAxis = handbrakeAction.ReadValue<float>();
            if (invertHandbrake) handbrakeAxis = 1f - handbrakeAxis;
            shiftUpButton = shiftUpAction.triggered;
            shiftDownButton = shiftDownAction.triggered;
            starterButtonHeld = starterAction != null && starterAction.IsPressed();

            if (mouseInput)
            {
                steeringAxis = (steeringAxis / Screen.width - 0.5f) * 2f; // Normalize to [-1, 1]
            }

            // Gear shifting

            if (shiftDownButton)
            {
                vehicleModel.drivetrain.transmission.ShiftDown();
            }
            if (shiftUpButton)
            {
                vehicleModel.drivetrain.transmission.ShiftUp();
            }


            // Apply inputs to vehicle model
            if (vehicleModel != null)
            {
                vehicleModel.steeringInput = steeringAxis;
                vehicleModel.throttleInput = throttleAxis;
                vehicleModel.brakeInput = brakeAxis;
                vehicleModel.clutchInput = clutchAxis;
                vehicleModel.handbrakeInput = handbrakeAxis;
                vehicleModel.starterHeld = starterButtonHeld;
            }
        }

        private void FixedUpdate()
        {
#if UNITY_STANDALONE_WIN
            if (enableFFB && ffbDevice == null)
            {
                InitializeFFBDevice();
            }

            if (enableFFB && ffbDevice != null)
            {
                UpdateFFBEffects();
            }
#endif
        }

        private void InitializeFFBDevice()
        {
#if UNITY_STANDALONE_WIN
            if (steeringAction == null)
            {
                return;
            }

            ffbDevice = steeringAction.controls
                .Select(control => control.device)
                .OfType<DirectInputDevice>()
                .Where(device => device.description.capabilities.Contains("\"FFBCapable\":true"))
                .FirstOrDefault(device => DIManager.Attach(device.description.serial));

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
#endif
        }

        private void UpdateFFBEffects()
        {
#if UNITY_STANDALONE_WIN
            string serial = ffbDevice.description.serial;

            float alignTorque = vehicleModel.tireAlignTorque * tireAlignTorqueScale;
            alignTorque += vehicleModel.steeringArmTorque * steeringArmTorqueScale;
            float dampingTorque = -dampingCoefficient * steeringAxis;
            float inertiaTorque = -inertiaCoefficient * (steeringAxis - lastSteeringAxis) / Time.deltaTime;

            float totalTorque = alignTorque + dampingTorque + inertiaTorque;

            if (constantForceEnabled)
            {
                DIManager.UpdateConstantForceSimple(serial, (int)totalTorque);
            }
            else
            {
                DIManager.UpdateConstantForceSimple(serial, 0);
            }

            if (damperForceEnabled)
            {
                DIManager.UpdateDamperSimple(serial, damperMagnitude);
            }
            else
            {
                DIManager.UpdateDamperSimple(serial, 0);
            }

            if (frictionForceEnabled)
            {
                DIManager.UpdateFrictionSimple(serial, frictionMagnitude);
            }
            else
            {
                DIManager.UpdateFrictionSimple(serial, 0);
            }
#endif

            lastSteeringAxis = steeringAxis;
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN
            if (ffbDevice != null)
            {
                DIManager.StopAllFFBEffects(ffbDevice.description.serial);
            }
#endif
        }
    }
}
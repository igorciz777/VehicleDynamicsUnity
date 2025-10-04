//#define USING_DIRECT_INPUT

using UnityEngine;

namespace VehicleDynamics
{
    public class VehiclePlayerController : MonoBehaviour
    {
#if USING_DIRECT_INPUT
        public DirectInputDevice directInputDevice;
#endif
        public VehicleModel vehicleModel;
        public float steeringAxis = 0f;
        public float throttleAxis = 0f;
        public float brakeAxis = 0f;
        public float clutchAxis = 0f;
        public bool handbrakeButton = false;
        public bool shiftUpButton = false;
        public bool shiftDownButton = false;
        public bool keyboardInput = true;
        public bool mouseInput = false;

        private float gearShiftCooldown = 0.5f; // Cooldown time in seconds
        private float lastShiftTime = 0f;
        void Update()
        {
#if USING_DIRECT_INPUT
            if (directInputDevice != null)
            {
                steeringAxis = directInputDevice.GetAxis(0); // Assuming axis 0 is steering
                throttleAxis = directInputDevice.GetAxis(5); // Assuming axis 5 is throttle
                brakeAxis = directInputDevice.GetAxis(1);    // Assuming axis 1 is brake
                clutchAxis = directInputDevice.GetAxis(6);   // Assuming axis 6 is clutch

                handbrakeButton = directInputDevice.GetButton(8); // Assuming button 8 is handbrake
                shiftUpButton = directInputDevice.GetButton(1);   // Assuming button 1 is shift up
                shiftDownButton = directInputDevice.GetButton(0); // Assuming button 0 is shift down

                // Remap throttle from [-1:1] to [0:1]
                throttleAxis = (1f - throttleAxis) / 2f;

                // Remap brake from [-1:1] to [0:1]
                brakeAxis = (1f - brakeAxis) / 2f;

                clutchAxis = -clutchAxis; // Invert clutch axis if necessary
            }
#endif
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
            else if (keyboardInput)
            {
                steeringAxis = Input.GetAxis("Horizontal");
                float throttleBrakeAxis = Input.GetAxis("Vertical");
                throttleAxis = Mathf.Clamp01(throttleBrakeAxis);
                brakeAxis = Mathf.Clamp01(-throttleBrakeAxis);
                clutchAxis = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
                handbrakeButton = Input.GetKey(KeyCode.Space);
                shiftUpButton = Input.GetKeyDown(KeyCode.E);
                shiftDownButton = Input.GetKeyDown(KeyCode.Q);
            }
            // Gear shifting with delay
            if (vehicleModel != null && vehicleModel.drivetrain != null)
            {
                if (Time.time - lastShiftTime >= gearShiftCooldown)
                {
                    if (shiftUpButton)
                    {
                        vehicleModel.drivetrain.ShiftUp();
                        lastShiftTime = Time.time;
                    }
                    if (shiftDownButton)
                    {
                        vehicleModel.drivetrain.ShiftDown();
                        lastShiftTime = Time.time;
                    }
                }
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
    }
}
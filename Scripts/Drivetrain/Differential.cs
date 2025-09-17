using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public class Differential : MonoBehaviour
    {
        public enum DifferentialType
        {
            Open,
            LimitedSlip,
            Locking
        }
        [Header("Differential Parameters")]
        public DifferentialType differentialType = DifferentialType.Open;
        [Range(0f, 1f)] public float lockRatio = 0.2f; // For limited slip differentials
        public WheelHub leftWheel;
        public WheelHub rightWheel;
        public float differentialInertia = 0.05f; // kg*m^2
        private float leftWheelSpeed = 0f; // rad/s
        private float rightWheelSpeed = 0f; // rad/s
        private float differentialSpeed = 0f; // rad/s
        private float differentialTorque = 0f; // Nm
        private float leftWheelTorque = 0f; // Nm
        private float rightWheelTorque = 0f; // Nm
        private float differentialAngularVelocity = 0f; // rad/s
        void Start()
        {
            leftWheel.wheel.isPowered = true;
            rightWheel.wheel.isPowered = true;
        }
        public void Step(float inputTorque, float driveshaftAngularVelocity)
        {
            // Update wheel speeds
            leftWheelSpeed = leftWheel.wheel.wheelAngularVelocity;
            rightWheelSpeed = rightWheel.wheel.wheelAngularVelocity;

            // Calculate differential speed
            differentialSpeed = (leftWheelSpeed + rightWheelSpeed) / 2f;

            // Calculate speed difference
            float speedDifference = Mathf.Abs(leftWheelSpeed - rightWheelSpeed);

            // Calculate torque distribution based on differential type
            switch (differentialType)
            {
                case DifferentialType.Open:
                    leftWheelTorque = inputTorque * 0.5f;
                    rightWheelTorque = inputTorque * 0.5f;
                    break;

                case DifferentialType.LimitedSlip:
                    if (speedDifference < lockRatio)
                    {
                        leftWheelTorque = inputTorque * 0.5f;
                        rightWheelTorque = inputTorque * 0.5f;
                    }
                    else
                    {
                        float slipFactor = Mathf.Clamp01((speedDifference - lockRatio) / lockRatio);
                        leftWheelTorque = inputTorque * (0.5f - slipFactor * 0.25f);
                        rightWheelTorque = inputTorque * (0.5f + slipFactor * 0.25f);
                    }
                    break;

                case DifferentialType.Locking:
                    if (speedDifference > 0.1f)
                    {
                        leftWheelTorque = inputTorque * 0.5f;
                        rightWheelTorque = inputTorque * 0.5f;
                    }
                    else
                    {
                        leftWheelTorque = inputTorque * 0.5f;
                        rightWheelTorque = inputTorque * 0.5f;
                    }
                    break;
            }

            // Apply torques to wheels
            leftWheel.ApplyDriveTorque(leftWheelTorque);
            rightWheel.ApplyDriveTorque(rightWheelTorque);

            // Update differential angular velocity
            differentialAngularVelocity += (inputTorque - (leftWheelTorque + rightWheelTorque)) / differentialInertia * Time.fixedDeltaTime;
        }
    }
}
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
            Locked
        }
        [Header("Differential Parameters")]
        public DifferentialType differentialType = DifferentialType.Open;
        public Hub leftWheel;
        public Hub rightWheel;
        public float differentialRatio = 3.5f; // Gear ratio of the differential
        void Start()
        {
            leftWheel.wheel.isPowered = true;
            rightWheel.wheel.isPowered = true;
        }

        public (float leftWheelTorque, float rightWheelTorque) GetDownTorque(float inputTorque)
        {
            inputTorque *= differentialRatio; // Torque at the differential
            float leftWheelTorque = 0f;
            float rightWheelTorque = 0f;
            // Calculate torque distribution based on differential type
            switch (differentialType)
            {
                case DifferentialType.Open:
                    // Open diff: split torque
                    leftWheelTorque = inputTorque * 0.5f;
                    rightWheelTorque = inputTorque * 0.5f;
                    break;

                case DifferentialType.LimitedSlip:
                    //TODO: Implement limited slip differential behavior
                    break;

                case DifferentialType.Locked:
                    // TODO: Implement locked differential behavior
                    break;
            }
            return (leftWheelTorque, rightWheelTorque);
        }
        public float GetUpVelocity()
        {
            return (leftWheel.wheel.wheelAngularVelocity + rightWheel.wheel.wheelAngularVelocity) * differentialRatio * 0.5f;
        }
    }
}
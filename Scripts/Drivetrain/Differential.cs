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
        void Start()
        {
            leftWheel.wheel.isPowered = true;
            rightWheel.wheel.isPowered = true;
        }
        public int PoweredWheelCount()
        {
            int count = 0;
            if (leftWheel != null && leftWheel.wheel.isPowered) count++;
            if (rightWheel != null && rightWheel.wheel.isPowered) count++;
            return count;
        }

        public float GetAverageWheelSpeed()
        {
            float sum = 0f;
            int count = 0;
            if (leftWheel != null)
            {
                sum += leftWheel.wheel.wheelAngularVelocity;
                count++;
            }
            if (rightWheel != null)
            {
                sum += rightWheel.wheel.wheelAngularVelocity;
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        public void Step(float inputTorque, float brakingTorque)
        {
            //inputTorque *= differentialRatio; // Torque at the differential

            // Calculate torque distribution based on differential type
            switch (differentialType)
            {
                case DifferentialType.Open:
                    // Open diff: split torque
                    leftWheel.ApplyDriveTorque(inputTorque * 0.5f);
                    rightWheel.ApplyDriveTorque(inputTorque * 0.5f);
                    break;

                case DifferentialType.LimitedSlip:
                    //TODO: Implement limited slip differential behavior
                    break;

                case DifferentialType.Locked:
                    // TODO: Implement locked differential behavior
                    break;
            }

            // Apply braking torque to both wheels
            leftWheel.ApplyBrakeTorque(brakingTorque * 0.5f);
            rightWheel.ApplyBrakeTorque(brakingTorque * 0.5f);
        }
    }
}
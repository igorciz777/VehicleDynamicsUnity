using UnityEngine;

namespace VehicleDynamics
{
    public class Differential : MonoBehaviour
    {
        public enum DifferentialType
        {
            Open,
            Locked
        }

        [Header("Differential Parameters")]
        public DifferentialType differentialType = DifferentialType.Open;
        public Hub leftWheelHub;
        public Hub rightWheelHub;
        public float differentialRatio = 3.5f; // Final drive ratio
        public (float leftWheelTorque, float rightWheelTorque) GetDownTorque(float inputTorque)
        {
            inputTorque *= differentialRatio;

            float leftOmega = leftWheelHub.GetWheel().wheelAngularVelocity;
            float rightOmega = rightWheelHub.GetWheel().wheelAngularVelocity;

            return differentialType switch
            {
                DifferentialType.Open => CalculateOpenDiff(inputTorque),
                DifferentialType.Locked => CalculateLockedDiff(inputTorque, leftOmega, rightOmega),
                _ => (inputTorque * 0.5f, inputTorque * 0.5f),
            };
        }

        private (float left, float right) CalculateOpenDiff(float inputTorque)
        {
            // Open differential, equal torque split
            return (inputTorque * 0.5f, inputTorque * 0.5f);
        }

        private (float left, float right) CalculateLockedDiff(float inputTorque, float leftOmega, float rightOmega)
        {
            // Locked differential, equal wheel speeds
            float avgOmega = (leftOmega + rightOmega) * 0.5f;
            float leftError = avgOmega - leftOmega;
            float rightError = avgOmega - rightOmega;
            
            // Apply correction torque to equalize speeds
            float correctionGain = 10f;
            float leftCorrection = leftError * correctionGain;
            float rightCorrection = rightError * correctionGain;
            
            float baseLeftTorque = inputTorque * 0.5f + leftCorrection;
            float baseRightTorque = inputTorque * 0.5f + rightCorrection;
            
            float totalTorque = baseLeftTorque + baseRightTorque;
            if (Mathf.Abs(totalTorque) > Mathf.Epsilon)
            {
                float scale = inputTorque / totalTorque;
                baseLeftTorque *= scale;
                baseRightTorque *= scale;
            }
            else
            {
                baseLeftTorque = inputTorque * 0.5f;
                baseRightTorque = inputTorque * 0.5f;
            }
            
            return (baseLeftTorque, baseRightTorque);
        }

        public float GetUpVelocity()
        {
            return (leftWheelHub.GetWheel().wheelAngularVelocity + rightWheelHub.GetWheel().wheelAngularVelocity) *
                   differentialRatio * 0.5f;
        }
        
        public static (float frontTorque, float rearTorque) CalculateFixedDiffTorque(float inputTorque, float frontOmega, float rearOmega, float powerSplitFront)
        {
            float avgOmega = (frontOmega + rearOmega) * 0.5f;
            float frontError = avgOmega - frontOmega;
            float rearError = avgOmega - rearOmega;

            float correctionGain = 10f;
            float frontCorrection = frontError * correctionGain;
            float rearCorrection = rearError * correctionGain;

            float baseFront = inputTorque * Mathf.Clamp01(powerSplitFront) + frontCorrection;
            float baseRear = inputTorque * Mathf.Clamp01(1f - powerSplitFront) + rearCorrection;

            // Sanity cap
            float maxAllowed = Mathf.Abs(inputTorque) * 10f + 1f;
            baseFront = Mathf.Clamp(baseFront, -maxAllowed, maxAllowed);
            baseRear = Mathf.Clamp(baseRear, -maxAllowed, maxAllowed);

            float total = baseFront + baseRear;
            if (Mathf.Abs(total) > Mathf.Epsilon)
            {
                float scale = inputTorque / total;
                baseFront *= scale;
                baseRear *= scale;
            }
            else
            {
                // Fallback to the static split if corrections zeroed total
                baseFront = inputTorque * Mathf.Clamp01(powerSplitFront);
                baseRear = inputTorque * Mathf.Clamp01(1f - powerSplitFront);
            }

            return (baseFront, baseRear);
        }
    }
}
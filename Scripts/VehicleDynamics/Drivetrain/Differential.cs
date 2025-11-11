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
        public Hub leftWheelHub;
        public Hub rightWheelHub;
        public float differentialRatio = 3.5f; // Final drive ratio
        
        [Header("LSD Parameters")]
        [Range(0f, 1f)]
        public float lockRatio = 0.25f; // 0 = open, 1 = locked
        public float preloadTorque = 50f; // Base locking torque
        public float maxLockTorque = 500f; // Maximum LSD torque
        public float viscosity = 0.8f; // For viscous LSD behavior
        public float rampAngle = 45f; // Torque ramp-up rate
        public (float leftWheelTorque, float rightWheelTorque) GetDownTorque(float inputTorque)
        {
            inputTorque *= differentialRatio;

            float leftOmega = leftWheelHub.GetWheel().wheelAngularVelocity;
            float rightOmega = rightWheelHub.GetWheel().wheelAngularVelocity;
            float leftSlip = leftWheelHub.GetWheel().slipRatio;
            float rightSlip = rightWheelHub.GetWheel().slipRatio;

            return differentialType switch
            {
                DifferentialType.Open => CalculateOpenDiff(inputTorque),
                DifferentialType.LimitedSlip => CalculateLimitedSlipDiff(inputTorque, leftOmega, rightOmega, leftSlip, rightSlip),
                DifferentialType.Locked => CalculateLockedDiff(inputTorque, leftOmega, rightOmega),
                _ => (inputTorque * 0.5f, inputTorque * 0.5f),
            };
        }

        private (float left, float right) CalculateOpenDiff(float inputTorque)
        {
            // Open differential, equal torque split
            return (inputTorque * 0.5f, inputTorque * 0.5f);
        }

        private (float left, float right) CalculateLimitedSlipDiff(float inputTorque, float leftOmega, 
            float rightOmega, float leftSlip, float rightSlip)
        {
            float baseLeftTorque = inputTorque * 0.5f;
            float baseRightTorque = inputTorque * 0.5f;
            
            // Speed difference
            float speedDiff = leftOmega - rightOmega;
            bool leftFaster = speedDiff > 0;
            
            // Slip based bias
            float slipBias = CalculateSlipBias(leftSlip, rightSlip);
            
            // LSD Torques
            float lsdTorque = CalculateMechanicalLSDTorque(speedDiff);
            float viscousTorque = CalculateViscousLSDTorque(speedDiff);
            
            float totalLSDTorque = Mathf.Clamp(lsdTorque + viscousTorque, 0, maxLockTorque);
            
            // Apply bias
            if (leftFaster)
            {
                baseLeftTorque -= totalLSDTorque * slipBias;
                baseRightTorque += totalLSDTorque * slipBias;
            }
            else
            {
                baseLeftTorque += totalLSDTorque * (1f - slipBias);
                baseRightTorque -= totalLSDTorque * (1f - slipBias);
            }
            
            // Normalize to maintain total input torque
            float totalTorque = baseLeftTorque + baseRightTorque;
            if (totalTorque > 0)
            {
                float scale = inputTorque / totalTorque;
                baseLeftTorque *= scale;
                baseRightTorque *= scale;
            }
            return (baseLeftTorque, baseRightTorque);
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
            if (totalTorque > 0)
            {
                float scale = inputTorque / totalTorque;
                baseLeftTorque *= scale;
                baseRightTorque *= scale;
            }
            
            return (baseLeftTorque, baseRightTorque);
        }

        private float CalculateSlipBias(float leftSlip, float rightSlip)
        {
            // Bias towards less slip
            float totalSlip = leftSlip + rightSlip;
            if (totalSlip <= 0) return 0.5f;
            
            float leftBias = 1f - (leftSlip / totalSlip);
            return Mathf.Clamp(leftBias, 0.1f, 0.9f);
        }

        private float CalculateMechanicalLSDTorque(float speedDifference)
        {
            // Mechanical LSD, preload + speed-dependent ramp
            float speedSensitivity = Mathf.Tan(rampAngle * Mathf.Deg2Rad);
            float speedBasedTorque = Mathf.Abs(speedDifference) * speedSensitivity;
            
            return preloadTorque + speedBasedTorque * lockRatio;
        }

        private float CalculateViscousLSDTorque(float speedDifference)
        {
            // Viscous LSD, torque proportional to speed difference
            return Mathf.Abs(speedDifference) * viscosity * lockRatio * 10f;
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
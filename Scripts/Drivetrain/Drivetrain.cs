using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public enum DrivetrainLayout { RWD, FWD, CenterDiffAWD, Fixed4WD }
    public class Drivetrain : MonoBehaviour
    {
        const float RPM_TO_RADS = Mathf.PI / 30f; // 1 RPM = π/30 rad/s
        const float RADS_TO_RPM = 30f / Mathf.PI; // 1 rad/s = 30/π RPM
        [Header("Drivetrain Layout")]
        public DrivetrainLayout layout = DrivetrainLayout.RWD;
        public float powerSplitFront = 0.5f; // for AWD with fixed center differential

        [Header("Drivetrain Inputs")]
        [Range(0f, 1f)] public float throttleInput = 0f;
        [Range(0f, 1f)] public float clutchInput = 0f;
        [Header("References")]
        public Rigidbody vehicleBody;
        [Header("Differentials")]
        public Differential frontDifferential; // can be null for RWD
        public Differential rearDifferential;  // can be null for FWD
        public Differential centerDifferential; // can be null for non-AWD
        public Transmission transmission;
        public Clutch clutch;
        public Engine engine;

        private void Start()
        {
            // Initialize components
            if (vehicleBody == null && TryGetComponent(out Rigidbody rb))
                vehicleBody = rb;
            if (transmission == null && TryGetComponent(out Transmission trans))
                transmission = trans;
            if (clutch == null && TryGetComponent(out Clutch cl))
                clutch = cl;
            if (engine == null && TryGetComponent(out Engine eng))
                engine = eng;

            // Check if differentials are assigned for the selected layout
            switch (layout)
            {
                case DrivetrainLayout.RWD:
                    Debug.Assert(rearDifferential != null, "Rear differential must be assigned for RWD layout.");
                    break;
                case DrivetrainLayout.FWD:
                    Debug.Assert(frontDifferential != null, "Front differential must be assigned for FWD layout.");
                    break;
                case DrivetrainLayout.CenterDiffAWD:
                    Debug.Assert(frontDifferential != null && rearDifferential != null && centerDifferential != null,
                    "Front, rear, and center differentials must be assigned for AWD with center differential.");
                    centerDifferential.differentialType = Differential.DifferentialType.Center;
                    break;
                case DrivetrainLayout.Fixed4WD:
                    Debug.Assert(frontDifferential != null && rearDifferential != null, "Both front and rear differentials must be assigned for AWD with fixed center.");
                    break;
            }
        }

        public void Step(float dt)
        {
            // Clamp inputs
            throttleInput = Mathf.Clamp01(throttleInput);
            clutchInput = Mathf.Clamp01(clutchInput);

            // Step engine
            engine.Step(dt, throttleInput, clutch.clutchTorque);

            // wheel -> differential -> driveshaft
            float driveshaftAngularVelocity = 0f;

            switch (layout)
            {
                case DrivetrainLayout.RWD:
                    driveshaftAngularVelocity = rearDifferential.GetUpVelocity();
                    break;
                case DrivetrainLayout.FWD:
                    driveshaftAngularVelocity = frontDifferential.GetUpVelocity();
                    break;
                case DrivetrainLayout.CenterDiffAWD:
                    driveshaftAngularVelocity = centerDifferential.GetUpVelocity();
                    break;
                case DrivetrainLayout.Fixed4WD:
                    driveshaftAngularVelocity = (frontDifferential.GetUpVelocity() + rearDifferential.GetUpVelocity()) * 0.5f;
                    break;
            }

            // driveshaft -> transmission/engine side via gear ratio
            float transmissionAngularVelocity = transmission.GetUpVelocity(driveshaftAngularVelocity);

            // Step clutch
            clutch.Step(dt, clutchInput, engine.engineAngularVelocity, transmissionAngularVelocity, transmission.GetCurrentGearRatio());

            // Compute torque transmitted through transmission
            float driveshaftTorque = transmission.GetDownTorque(clutch.clutchTorque);

            // Distribute torque based on drivetrain layout
            switch (layout)
            {
                case DrivetrainLayout.RWD:
                    var (rearLeftTorque, rearRightTorque) = rearDifferential.GetDownTorque(driveshaftTorque);
                    rearDifferential.leftWheel.ApplyDriveTorque(rearLeftTorque);
                    rearDifferential.rightWheel.ApplyDriveTorque(rearRightTorque);
                    break;

                case DrivetrainLayout.FWD:
                    var (frontLeftTorque, frontRightTorque) = frontDifferential.GetDownTorque(driveshaftTorque);
                    frontDifferential.leftWheel.ApplyDriveTorque(frontLeftTorque);
                    frontDifferential.rightWheel.ApplyDriveTorque(frontRightTorque);
                    break;

                case DrivetrainLayout.CenterDiffAWD:
                    // var (centerFrontTorque, centerRearTorque) = centerDifferential.GetDownTorque(driveshaftTorque);
                    // var (awdFrontLeftTorque, awdFrontRightTorque) = frontDifferential.GetDownTorque(centerFrontTorque);
                    // var (awdRearLeftTorque, awdRearRightTorque) = rearDifferential.GetDownTorque(centerRearTorque);
                    // frontDifferential.leftWheel.ApplyDriveTorque(awdFrontLeftTorque);
                    // frontDifferential.rightWheel.ApplyDriveTorque(awdFrontRightTorque);
                    // rearDifferential.leftWheel.ApplyDriveTorque(awdRearLeftTorque);
                    // rearDifferential.rightWheel.ApplyDriveTorque(awdRearRightTorque);
                    break;

                case DrivetrainLayout.Fixed4WD:
                    float frontTorque = driveshaftTorque * powerSplitFront;
                    float rearTorque = driveshaftTorque * (1f - powerSplitFront);
                    var (fixedFrontLeftTorque, fixedFrontRightTorque) = frontDifferential.GetDownTorque(frontTorque);
                    var (fixedRearLeftTorque, fixedRearRightTorque) = rearDifferential.GetDownTorque(rearTorque);
                    frontDifferential.leftWheel.ApplyDriveTorque(fixedFrontLeftTorque);
                    frontDifferential.rightWheel.ApplyDriveTorque(fixedFrontRightTorque);
                    rearDifferential.leftWheel.ApplyDriveTorque(fixedRearLeftTorque);
                    rearDifferential.rightWheel.ApplyDriveTorque(fixedRearRightTorque);
                    break;
            }
        }
    }
}
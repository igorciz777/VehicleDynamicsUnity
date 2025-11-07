using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics
{
    public enum DrivetrainLayout { RWD, FWD, CenterDiffAWD, Fixed4WD }
    public class Drivetrain : MonoBehaviour
    {
        [Header("Drivetrain Layout")]
        public DrivetrainLayout layout = DrivetrainLayout.RWD;
        public float powerSplitFront = 0.5f; // for AWD with center diff

        [Header("Drivetrain Inputs")]
        [Range(0f, 1f)] public float throttleInput = 0f;
        [Range(0f, 1f)] public float clutchInput = 0f;
        [Header("References")]
        public Rigidbody vehicleBody;
        [Header("Differentials")]
        public Differential frontDifferential; // can be null for RWD
        public Differential rearDifferential;  // can be null for FWD
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
                    Debug.Assert(frontDifferential != null && rearDifferential != null, "Both front and rear differentials must be assigned for AWD with center differential.");
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
                    driveshaftAngularVelocity = (frontDifferential.GetUpVelocity() + rearDifferential.GetUpVelocity()) * 0.5f;
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

                case DrivetrainLayout.CenterDiffAWD: // Power split
                    var (centerFrontTorque, centerRearTorque) = Differential.CalculateCenterDiffTorque(
                        driveshaftTorque,
                        frontDifferential.GetUpVelocity(),
                        rearDifferential.GetUpVelocity(),
                        powerSplitFront);
                    var (cdFrontLeftTorque, cdFrontRightTorque) = frontDifferential.GetDownTorque(centerFrontTorque);
                    var (cdRearLeftTorque, cdRearRightTorque) = rearDifferential.GetDownTorque(centerRearTorque);
                    frontDifferential.leftWheel.ApplyDriveTorque(cdFrontLeftTorque);
                    frontDifferential.rightWheel.ApplyDriveTorque(cdFrontRightTorque);
                    rearDifferential.leftWheel.ApplyDriveTorque(cdRearLeftTorque);
                    rearDifferential.rightWheel.ApplyDriveTorque(cdRearRightTorque);
                    break;

                case DrivetrainLayout.Fixed4WD: // Equal split
                    float frontTorque = driveshaftTorque * 0.5f;
                    float rearTorque = driveshaftTorque * 0.5f;
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
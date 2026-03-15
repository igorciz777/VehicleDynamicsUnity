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
        public bool starterHeld = false;
        [Header("References")]
        public Rigidbody vehicleBody;
        [Header("Differentials")]
        public Differential frontDifferential; // can be null for RWD
        public Differential rearDifferential;  // can be null for FWD
        [Header("Subsystems")]
        public Transmission transmission;
        public Clutch clutch;
        public Engine engine;
        private VehicleModel vM;

        public void Init(VehicleModel vehicleModel)
        {
            vM = vehicleModel;
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
            engine.Init();
            clutch.Init();
        }

        public void Step(float dt)
        {
            // Clamp inputs
            throttleInput = Mathf.Clamp01(throttleInput);
            clutchInput = Mathf.Clamp01(clutchInput);

            if (vM.hasTCS)
            {
                float vehicleSpeed = vM.vehicleRigidbody.linearVelocity.magnitude;
                if (vehicleSpeed > vM.tcsMinVelocity)
                {
                    float slipOpt = vM.tcsSlipOpt;
                    float slipTol = vM.tcsSlipTol;

                    float currentSlip = 0f;

                    switch (layout)
                    {
                        case DrivetrainLayout.RWD:
                            currentSlip = rearDifferential.leftWheelHub.GetWheel().slipRatio;
                            currentSlip = Mathf.Max(currentSlip, rearDifferential.rightWheelHub.GetWheel().slipRatio);
                            break;

                        case DrivetrainLayout.FWD:
                            currentSlip = frontDifferential.leftWheelHub.GetWheel().slipRatio;
                            currentSlip = Mathf.Max(currentSlip, frontDifferential.rightWheelHub.GetWheel().slipRatio);
                            break;

                        case DrivetrainLayout.CenterDiffAWD:
                        case DrivetrainLayout.Fixed4WD:
                            currentSlip = frontDifferential.leftWheelHub.GetWheel().slipRatio;
                            currentSlip = Mathf.Max(currentSlip, frontDifferential.rightWheelHub.GetWheel().slipRatio);
                            currentSlip = Mathf.Max(currentSlip, rearDifferential.leftWheelHub.GetWheel().slipRatio);
                            currentSlip = Mathf.Max(currentSlip, rearDifferential.rightWheelHub.GetWheel().slipRatio);
                            break;
                    }

                    if (currentSlip > slipOpt + slipTol && throttleInput > 0f)
                    {
                        engine.RequestThrottleLimit(0.1f);
                        // Debug.Log("TCS activated");
                    }
                }
            }

            // Step engine
            engine.Step(dt, throttleInput, clutch.clutchTorque, starterHeld);

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
                    rearDifferential.leftWheelHub.ApplyDriveTorque(rearLeftTorque);
                    rearDifferential.rightWheelHub.ApplyDriveTorque(rearRightTorque);
                    break;

                case DrivetrainLayout.FWD:
                    var (frontLeftTorque, frontRightTorque) = frontDifferential.GetDownTorque(driveshaftTorque);
                    frontDifferential.leftWheelHub.ApplyDriveTorque(frontLeftTorque);
                    frontDifferential.rightWheelHub.ApplyDriveTorque(frontRightTorque);
                    break;

                case DrivetrainLayout.CenterDiffAWD:
                    float frontTorque = driveshaftTorque * powerSplitFront;
                    float rearTorque = driveshaftTorque * (1f - powerSplitFront);
                    var (fixedFrontLeftTorque, fixedFrontRightTorque) = frontDifferential.GetDownTorque(frontTorque);
                    var (fixedRearLeftTorque, fixedRearRightTorque) = rearDifferential.GetDownTorque(rearTorque);
                    frontDifferential.leftWheelHub.ApplyDriveTorque(fixedFrontLeftTorque);
                    frontDifferential.rightWheelHub.ApplyDriveTorque(fixedFrontRightTorque);
                    rearDifferential.leftWheelHub.ApplyDriveTorque(fixedRearLeftTorque);
                    rearDifferential.rightWheelHub.ApplyDriveTorque(fixedRearRightTorque);
                    break;

                case DrivetrainLayout.Fixed4WD:
                    var (centerFrontTorque, centerRearTorque) = Differential.CalculateFixedDiffTorque(
                        driveshaftTorque,
                        frontDifferential.GetUpVelocity(),
                        rearDifferential.GetUpVelocity(),
                        powerSplitFront);
                    var (cdFrontLeftTorque, cdFrontRightTorque) = frontDifferential.GetDownTorque(centerFrontTorque);
                    var (cdRearLeftTorque, cdRearRightTorque) = rearDifferential.GetDownTorque(centerRearTorque);
                    frontDifferential.leftWheelHub.ApplyDriveTorque(cdFrontLeftTorque);
                    frontDifferential.rightWheelHub.ApplyDriveTorque(cdFrontRightTorque);
                    rearDifferential.leftWheelHub.ApplyDriveTorque(cdRearLeftTorque);
                    rearDifferential.rightWheelHub.ApplyDriveTorque(cdRearRightTorque);
                    break;
            }
        }
    }
}
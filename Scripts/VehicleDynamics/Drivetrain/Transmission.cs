using UnityEngine;

namespace VehicleDynamics
{
    public class Transmission : MonoBehaviour
    {
        [Header("Transmission Parameters")]
        public int currentGear = 1;
        public float[] gearRatios = new float[] { -3.5f, 0f, 3.5f, 2.5f, 1.5f, 1.0f, 0.75f };
        public float transmissionInertia = 0.05f; // kg*m^2
        [Header("Transmission State")]
        public float transmissionAngularVelocity = 0f;
        public float transmissionAngularAcceleration = 0f;
        public float transmissionTorque = 0f;
        public float GetDownTorque(float inputTorque)
        {
            if (currentGear < 0 || currentGear >= gearRatios.Length)
                return 0f;

            // neutral if the gear ratio is zero
            if (Mathf.Approximately(gearRatios[currentGear], 0f))
                return 0f;

            return inputTorque * gearRatios[currentGear];
        }
        public float GetUpVelocity(float inputVelocity)
        {
            if (currentGear < 0 || currentGear >= gearRatios.Length)
                return 0f;

            // neutral if the gear ratio is zero
            if (Mathf.Approximately(gearRatios[currentGear], 0f))
                return 0f;

            return inputVelocity * gearRatios[currentGear];
        }
        public void ShiftUp()
        {
            if (currentGear < gearRatios.Length - 1)
                currentGear++;
        }
        public void ShiftDown()
        {
            if (currentGear > 0)
                currentGear--;
        }
        public float GetCurrentGearRatio()
        {
            return gearRatios[currentGear];
        }
    }
}

using UnityEngine;

public class Clutch : MonoBehaviour
{
    [Header("Clutch Parameters")]
    public float clutchMaxTorque = 400f; // Maximum torque the clutch can transmit
    public float clutchStiffness = 10f; // Stiffness factor for engagement
    public float clutchDamping = 1f; // Damping factor for engagement
    [Header("Clutch State")]
    public float clutchEngagement = 0f; // 0 = disengaged, 1 = fully engaged
    public float engineAngularVelocity = 0f;
    public float transmissionAngularVelocity = 0f;
    public float slip = 0f;
    public float clutchTorque = 0f;

    public void Step(float dt, float clutchInput, float engineAngularVel, float transmissionAngularVel, float gearRatio)
    {
        clutchInput = 1f - Mathf.Clamp01(clutchInput);
        clutchEngagement = clutchInput;

        engineAngularVelocity = engineAngularVel;
        transmissionAngularVelocity = transmissionAngularVel;

        // Calculate slip
        if(gearRatio != 0f)
            slip = engineAngularVelocity - transmissionAngularVelocity;
        else
            slip = 0f;

        // Calculate torque
        float torque = clutchEngagement * slip * clutchStiffness;
        clutchTorque += (torque - clutchTorque) * clutchDamping;
        clutchTorque = Mathf.Clamp(clutchTorque, -clutchMaxTorque, clutchMaxTorque);
    }
}

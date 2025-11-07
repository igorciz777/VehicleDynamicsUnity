using System;
using UnityEngine;

namespace VehicleDynamics
{
    public class Clutch : MonoBehaviour
    {
        [Header("Clutch Parameters")]
        public float bitingPoint = 0.2f; // Clutch engagement threshold (1f - bitingPoint)
        public float clutchMaxTorque = 600f; // Maximum torque the clutch can transmit
        public float clutchStiffness = 500f; // Stiffness factor for engagement
        public float clutchDamping = 10f; // Damping factor for engagement
        [Header("Clutch State")]
        public float clutchEngagement = 0f; // 0 = disengaged, 1 = fully engaged
        public float engineAngularVelocity = 0f;
        public float transmissionAngularVelocity = 0f;
        public float slip = 0f;
        public float clutchTorque = 0f;
        public float thetac = 0f; // clutch angle
        [Header("Internal")]
        private AnimationCurve engagementCurve;

        void Start()
        {
            engagementCurve = new AnimationCurve();
            engagementCurve.AddKey(0f, 0f);
            Keyframe middleKey = new(1f - bitingPoint, 1f, 0f, 0f); // Flat tangent
            engagementCurve.AddKey(middleKey);
            Keyframe endKey = new(1f, 1f, 0f, 0f); // Flat tangent
            engagementCurve.AddKey(endKey);
        }

        public void Step(float dt, float clutchInput, float engineAngularVel, float transmissionAngularVel, float gearRatio)
        {
            clutchInput = 1f - Mathf.Clamp01(clutchInput);
            clutchEngagement = engagementCurve.Evaluate(clutchInput);

            engineAngularVelocity = engineAngularVel;
            transmissionAngularVelocity = transmissionAngularVel;

            if (gearRatio == 0f) // Neutral
            {
                slip = 0f;
                clutchTorque = 0f;
                thetac = 0f;
                clutchEngagement = 0f;
                return;
            }

            slip = engineAngularVelocity - transmissionAngularVelocity;

            // clutch spring angle d(thetac)/dt
            float relOmega = slip;
            thetac += relOmega * dt * clutchEngagement;

            // effective stiffness/damping scale with engagement
            float keff = Mathf.Max(0f, clutchStiffness * clutchEngagement);
            float deff = Mathf.Max(0f, clutchDamping * clutchEngagement);

            if (keff > 0f && clutchMaxTorque > 0f)
            {
                float thetaMax = Mathf.Abs(clutchMaxTorque) / keff;
                thetac = Mathf.Clamp(thetac, -thetaMax, thetaMax);
            }

            // spring + viscous damping torque
            float springTorque = keff * thetac + deff * relOmega;
            float targetTorque = Mathf.Clamp(springTorque, -Mathf.Abs(clutchMaxTorque), Mathf.Abs(clutchMaxTorque));

            clutchTorque = targetTorque;

            // If clutch is fully disengaged no torque transmits and allow spring to relax
            if (clutchEngagement <= 1e-3f)
            {
                clutchTorque = 0f;
                thetac *= Mathf.Clamp01(1f - 5f * dt);
            }
        }
    }
}
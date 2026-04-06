using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics.TestsRuntime
{
    public enum RuntimeTestCaseKind
    {
        TC1_DrivetrainAcceleration,
        TC2_Braking,
        TC3_Cornering,
        TC4_Suspension,
    }

    [Serializable]
    public class RuntimeInputSegment
    {
        [Min(0f)] public float startTime = 0f;
        [Min(0f)] public float endTime = 1f;

        [Range(-1f, 1f)] public float steering = 0f;
        [Range(0f, 1f)] public float throttle = 0f;
        [Range(0f, 1f)] public float brake = 0f;
        [Range(0f, 1f)] public float clutch = 0f;
        [Range(0f, 1f)] public float handbrake = 0f;

        public bool Contains(float time) => time >= startTime && time < endTime;
    }

    [Serializable]
    public class SurfaceOverride
    {
        public bool apply = false;
        public float longitudinalMu = 1.0f;
        public float lateralMu = 1.0f;
        public float rollingResistance = 0.015f;
        public float decayingFriction = 0.0f;
    }

    [Serializable]
    public class RuntimeMetricThreshold
    {
        public string metricName = "maxSpeedKmh";
        public bool hasMin = false;
        public float minValue = 0f;
        public bool hasMax = false;
        public float maxValue = 0f;
        public bool required = true;
    }

    [CreateAssetMenu(fileName = "RuntimeTestCase", menuName = "VehicleDynamics/Runtime Test Case")]
    public class RuntimeTestCaseDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string testId = "TC1";
        public RuntimeTestCaseKind testKind = RuntimeTestCaseKind.TC1_DrivetrainAcceleration;
        [TextArea(2, 5)] public string description;

        [Header("Execution")]
        [Min(0.1f)] public float durationSeconds = 10f;
        [Min(1)] public int repeatCount = 1;

        [Header("Initial Vehicle State")]
        public bool overrideInitialPose = false;
        public Vector3 initialPosition = Vector3.zero;
        public Vector3 initialEulerAngles = Vector3.zero;
        public Vector3 initialLinearVelocity = Vector3.zero;
        public Vector3 initialAngularVelocity = Vector3.zero;
        public int startingGear = 1;

        [Header("Feature Flags")]
        public bool absEnabled = true;
        public bool tcsEnabled = true;
        public bool frontAntiRollEnabled = true;
        public bool rearAntiRollEnabled = true;

        [Header("Environment")]
        public SurfaceOverride surfaceOverride = new SurfaceOverride();

        [Header("Input Profile")]
        public List<RuntimeInputSegment> inputSegments = new List<RuntimeInputSegment>();

        [Header("Pass/Fail Thresholds")]
        public List<RuntimeMetricThreshold> thresholds = new List<RuntimeMetricThreshold>();

        public RuntimeInputSegment ResolveSegment(float time)
        {
            for (int i = 0; i < inputSegments.Count; i++)
            {
                RuntimeInputSegment segment = inputSegments[i];
                if (segment != null && segment.Contains(time))
                {
                    return segment;
                }
            }

            return null;
        }
    }
}

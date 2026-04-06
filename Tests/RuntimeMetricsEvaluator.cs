using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleDynamics.TestsRuntime
{
    [Serializable]
    public class RuntimeMetricEntry
    {
        public string name;
        public float value;
    }

    [Serializable]
    public class RuntimeCaseEvaluation
    {
        public bool passed;
        public string failureReason;
        public List<RuntimeMetricEntry> metrics = new List<RuntimeMetricEntry>();

        public bool TryGetMetric(string name, out float value)
        {
            for (int i = 0; i < metrics.Count; i++)
            {
                if (string.Equals(metrics[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = metrics[i].value;
                    return true;
                }
            }

            value = 0f;
            return false;
        }
    }

    public static class RuntimeMetricsEvaluator
    {
        public static RuntimeCaseEvaluation Evaluate(IReadOnlyList<TelemetrySample> samples, RuntimeTestCaseDefinition definition)
        {
            RuntimeCaseEvaluation result = new RuntimeCaseEvaluation();
            if (samples == null || samples.Count == 0)
            {
                result.passed = false;
                result.failureReason = "No telemetry samples captured.";
                return result;
            }

            float maxSpeed = 0f;
            float peakSlipRatio = 0f;
            float peakSlipAngle = 0f;
            float speedSum = 0f;
            float speedSquaredSum = 0f;
            float rpmSquaredSum = 0f;

            TelemetrySample first = samples[0];
            TelemetrySample last = samples[samples.Count - 1];

            float timeTo100 = -1f;
            int firstBrakeIndex = -1;
            int stopAfterBrakeIndex = -1;

            for (int i = 0; i < samples.Count; i++)
            {
                TelemetrySample s = samples[i];
                maxSpeed = Mathf.Max(maxSpeed, s.speedKmh);
                peakSlipRatio = Mathf.Max(peakSlipRatio, s.maxAbsSlipRatio);
                peakSlipAngle = Mathf.Max(peakSlipAngle, s.maxAbsSlipAngleDeg);
                speedSum += s.speedKmh;
                speedSquaredSum += s.speedKmh * s.speedKmh;
                rpmSquaredSum += s.engineRpm * s.engineRpm;

                if (timeTo100 < 0f && s.speedKmh >= 100f)
                {
                    timeTo100 = s.time;
                }

                if (firstBrakeIndex < 0 && s.brake > 0.05f)
                {
                    firstBrakeIndex = i;
                }
                if (firstBrakeIndex >= 0 && stopAfterBrakeIndex < 0 && s.speedMs <= 0.5f)
                {
                    stopAfterBrakeIndex = i;
                }
            }

            int n = samples.Count;
            float meanSpeed = speedSum / n;
            float variance = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                float d = samples[i].speedKmh - meanSpeed;
                variance += d * d;
            }
            variance = n > 1 ? variance / (n - 1) : 0f;
            float sigmaSpeed = Mathf.Sqrt(Mathf.Max(0f, variance));

            float rmsSpeed = Mathf.Sqrt(speedSquaredSum / n);
            float rmsRpm = Mathf.Sqrt(rpmSquaredSum / n);

            float brakingDistance = -1f;
            if (firstBrakeIndex >= 0 && stopAfterBrakeIndex > firstBrakeIndex)
            {
                Vector3 p0 = samples[firstBrakeIndex].position;
                Vector3 p1 = samples[stopAfterBrakeIndex].position;
                brakingDistance = Vector3.Distance(p0, p1);
            }

            float duration = Mathf.Max(0f, last.time - first.time);

            Add(result.metrics, "durationSec", duration);
            Add(result.metrics, "maxSpeedKmh", maxSpeed);
            Add(result.metrics, "timeTo100Kmh", timeTo100);
            Add(result.metrics, "peakAbsSlipRatio", peakSlipRatio);
            Add(result.metrics, "peakAbsSlipAngleDeg", peakSlipAngle);
            Add(result.metrics, "sigmaSpeedKmh", sigmaSpeed);
            Add(result.metrics, "rmsSpeedKmh", rmsSpeed);
            Add(result.metrics, "rmsEngineRpm", rmsRpm);
            Add(result.metrics, "brakingDistanceM", brakingDistance);
            Add(result.metrics, "finalSpeedKmh", last.speedKmh);
            Add(result.metrics, "mseSpeedKmh", ComputeMseSpeed(samples, meanSpeed));

            result.passed = EvaluateThresholds(result, definition);
            return result;
        }

        private static bool EvaluateThresholds(RuntimeCaseEvaluation evaluation, RuntimeTestCaseDefinition definition)
        {
            if (definition == null || definition.thresholds == null || definition.thresholds.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < definition.thresholds.Count; i++)
            {
                RuntimeMetricThreshold threshold = definition.thresholds[i];
                if (!evaluation.TryGetMetric(threshold.metricName, out float value))
                {
                    if (threshold.required)
                    {
                        evaluation.failureReason = "Missing required metric: " + threshold.metricName;
                        return false;
                    }

                    continue;
                }

                if (threshold.hasMin && value < threshold.minValue)
                {
                    evaluation.failureReason = threshold.metricName + " below minimum (" + value + " < " + threshold.minValue + ")";
                    return false;
                }

                if (threshold.hasMax && value > threshold.maxValue)
                {
                    evaluation.failureReason = threshold.metricName + " above maximum (" + value + " > " + threshold.maxValue + ")";
                    return false;
                }
            }

            return true;
        }

        private static float ComputeMseSpeed(IReadOnlyList<TelemetrySample> samples, float reference)
        {
            if (samples == null || samples.Count == 0)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                float d = samples[i].speedKmh - reference;
                sum += d * d;
            }

            return sum / samples.Count;
        }

        private static void Add(List<RuntimeMetricEntry> list, string name, float value)
        {
            list.Add(new RuntimeMetricEntry { name = name, value = value });
        }
    }
}

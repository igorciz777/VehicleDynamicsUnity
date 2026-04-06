using System.Text;
using TMPro;
using UnityEngine;

namespace VehicleDynamics.TestsRuntime
{
    public class RuntimeTestStatusPanel : MonoBehaviour
    {
        [Header("References")]
        public TestRunnerRuntime testRunner;

        [Header("UI Fields")]
        public TextMeshProUGUI testNameText;
        public TextMeshProUGUI testDescriptionText;
        public TextMeshProUGUI testStatusText;
        public TextMeshProUGUI trackedValuesText;

        [Header("Display Settings")]
        public bool showWhileIdle = true;
        public string idleTitle = "Runtime Test Harness";
        public string idleDescription = "Awaiting test start...";

        private void LateUpdate()
        {
            if (testRunner == null)
            {
                RenderFallback("No test runner assigned.", string.Empty, RuntimeRunStatus.Idle, string.Empty);
                return;
            }

            RuntimeTestCaseDefinition currentCase = testRunner.CurrentTestCase;
            if (currentCase == null)
            {
                if (showWhileIdle)
                {
                    RenderFallback(idleTitle, idleDescription, testRunner.CurrentStatus, string.Empty);
                }
                else
                {
                    ClearPanel();
                }

                return;
            }

            RuntimeRunStatus status = testRunner.CurrentStatus;
            string statusLabel = status == RuntimeRunStatus.Passed
                ? "SUCCESS"
                : status == RuntimeRunStatus.Failed
                    ? "FAIL"
                    : status.ToString().ToUpperInvariant();

            if (testNameText != null)
            {
                testNameText.text = currentCase.testId + " - " + currentCase.name;
            }

            if (testDescriptionText != null)
            {
                testDescriptionText.text = currentCase.description;
            }

            if (testStatusText != null)
            {
                testStatusText.text = "Status: " + statusLabel;
                testStatusText.color = status == RuntimeRunStatus.Failed
                    ? Color.red
                    : status == RuntimeRunStatus.Passed
                        ? new Color(0.2f, 0.9f, 0.35f)
                        : Color.white;
            }

            if (trackedValuesText != null)
            {
                trackedValuesText.text = BuildTrackedValuesText(testRunner);
            }
        }

        private void RenderFallback(string title, string description, RuntimeRunStatus status, string values)
        {
            if (testNameText != null)
            {
                testNameText.text = title;
            }

            if (testDescriptionText != null)
            {
                testDescriptionText.text = description;
            }

            if (testStatusText != null)
            {
                testStatusText.text = "Status: " + status.ToString().ToUpperInvariant();
                testStatusText.color = Color.white;
            }

            if (trackedValuesText != null)
            {
                trackedValuesText.text = values;
            }
        }

        private void ClearPanel()
        {
            if (testNameText != null)
            {
                testNameText.text = string.Empty;
            }

            if (testDescriptionText != null)
            {
                testDescriptionText.text = string.Empty;
            }

            if (testStatusText != null)
            {
                testStatusText.text = string.Empty;
            }

            if (trackedValuesText != null)
            {
                trackedValuesText.text = string.Empty;
            }
        }

        private static string BuildTrackedValuesText(TestRunnerRuntime runner)
        {
            StringBuilder sb = new StringBuilder(512);
            TelemetrySample latest = runner.CurrentLatestSample;
            if (latest == null)
            {
                sb.AppendLine("No telemetry captured yet.");
                return sb.ToString();
            }

            sb.AppendLine("Tracked values:");
            string[] labels = runner.trackedValueLabels;
            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                sb.Append(label).Append(": ").Append(FormatTrackedValue(label, latest)).AppendLine();
            }

            if (runner.CurrentEvaluation != null)
            {
                sb.Clear();
                sb.AppendLine("Final metrics:");
                for (int i = 0; i < runner.CurrentEvaluation.metrics.Count; i++)
                {
                    RuntimeMetricEntry metric = runner.CurrentEvaluation.metrics[i];
                    sb.Append(metric.name).Append(": ").Append(metric.value.ToString("0.###")).AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string FormatTrackedValue(string label, TelemetrySample sample)
        {
            return label switch
            {
                "speedKmh" => sample.speedKmh.ToString("0.###"),
                "engineRpm" => sample.engineRpm.ToString("0"),
                "maxAbsSlipRatio" => sample.maxAbsSlipRatio.ToString("0.###"),
                "maxAbsSlipAngleDeg" => sample.maxAbsSlipAngleDeg.ToString("0.###"),
                "frontAntiRollForce" => sample.frontAntiRollForce.ToString("0.###"),
                "rearAntiRollForce" => sample.rearAntiRollForce.ToString("0.###"),
                "tireAlignTorque" => sample.tireAlignTorque.ToString("0.###"),
                "steeringArmTorque" => sample.steeringArmTorque.ToString("0.###"),
                "gear" => sample.gear.ToString(),
                _ => "<unknown>",
            };
        }
    }
}
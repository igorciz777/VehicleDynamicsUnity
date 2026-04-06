using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VehicleDynamics.TestsRuntime
{
    public enum RuntimeRunStatus
    {
        Idle,
        Preparing,
        Running,
        Evaluating,
        Passed,
        Failed,
    }

    public class TestRunnerRuntime : MonoBehaviour
    {
        [Header("References")]
        public RuntimeScenarioDriver scenarioDriver;
        public RuntimeTelemetryRecorder telemetryRecorder;

        [Header("Cases")]
        public List<RuntimeTestCaseDefinition> testCases = new List<RuntimeTestCaseDefinition>();

        [Header("Run Options")]
        public bool autoRunOnStart = true;
        public string outputFolderName = "RuntimeTestReports";
        [Min(0f)] public float postCaseDelaySeconds = 5f;

        [Header("UI Labels")]
        public string[] trackedValueLabels =
        {
            "speedKmh",
            "engineRpm",
            "maxAbsSlipRatio",
            "maxAbsSlipAngleDeg",
            "frontAntiRollForce",
            "rearAntiRollForce",
            "tireAlignTorque",
            "steeringArmTorque",
        };

        private bool isRunning;

        public RuntimeRunStatus CurrentStatus { get; private set; } = RuntimeRunStatus.Idle;
        public RuntimeTestCaseDefinition CurrentTestCase { get; private set; }
        public int CurrentRunIndex { get; private set; }
        public RuntimeCaseEvaluation CurrentEvaluation { get; private set; }
        public IReadOnlyList<TelemetrySample> CurrentSamples => telemetryRecorder != null ? telemetryRecorder.Samples : null;
        public TelemetrySample CurrentLatestSample => telemetryRecorder != null ? telemetryRecorder.LatestSample : null;
        public string CurrentStatusText => CurrentStatus.ToString();

        private void Start()
        {
            if (autoRunOnStart)
            {
                StartCoroutine(RunAllCases());
            }
        }

        [ContextMenu("Run Runtime Tests")]
        public void RunFromInspector()
        {
            if (!isRunning)
            {
                StartCoroutine(RunAllCases());
            }
        }

        private IEnumerator RunAllCases()
        {
            if (isRunning)
            {
                yield break;
            }

            if (scenarioDriver == null || telemetryRecorder == null)
            {
                Debug.LogError("TestRunnerRuntime: Assign scenarioDriver and telemetryRecorder.");
                yield break;
            }

            isRunning = true;
            CurrentStatus = RuntimeRunStatus.Preparing;
            string root = ResolveOutputDirectory();
            RuntimeCampaignReport campaign = new RuntimeCampaignReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("o"),
            };

            int failedRuns = 0;
            for (int i = 0; i < testCases.Count; i++)
            {
                RuntimeTestCaseDefinition definition = testCases[i];
                if (definition == null)
                {
                    continue;
                }

                int repeats = Mathf.Max(1, definition.repeatCount);
                for (int runIndex = 1; runIndex <= repeats; runIndex++)
                {
                    yield return RunSingleCase(definition, runIndex, root, campaign, () => failedRuns++);
                }
            }

            campaign.totalRuns = campaign.runs.Count;
            campaign.failedRuns = failedRuns;
            campaign.passedRuns = Mathf.Max(0, campaign.totalRuns - campaign.failedRuns);

            string jsonPath = RuntimeReportWriter.WriteJson(root, campaign);
            Debug.Log("Runtime tests finished. JSON: " + jsonPath);
            if (campaign.failedRuns > 0)
            {
                Debug.LogError("Runtime tests FAILED. Failed runs: " + campaign.failedRuns);
            }
            else
            {
                Debug.Log("Runtime tests PASSED.");
            }

            CurrentStatus = campaign.failedRuns > 0 ? RuntimeRunStatus.Failed : RuntimeRunStatus.Passed;
            isRunning = false;
        }

        private IEnumerator RunSingleCase(
            RuntimeTestCaseDefinition definition,
            int runIndex,
            string root,
            RuntimeCampaignReport campaign,
            Action onFailed)
        {
            CurrentStatus = RuntimeRunStatus.Preparing;
            CurrentTestCase = definition;
            CurrentRunIndex = runIndex;
            CurrentEvaluation = null;

            scenarioDriver.PrepareCase(definition);
            telemetryRecorder.BeginRun();

            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, definition.durationSeconds);

            yield return null;

            CurrentStatus = RuntimeRunStatus.Running;
            while (elapsed < duration)
            {
                scenarioDriver.ApplyInputs(elapsed, definition);
                telemetryRecorder.Capture(elapsed);
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            scenarioDriver.ApplyInputs(duration, definition);
            telemetryRecorder.Capture(duration);

            CurrentStatus = RuntimeRunStatus.Evaluating;
            RuntimeCaseEvaluation evaluation = RuntimeMetricsEvaluator.Evaluate(telemetryRecorder.Samples, definition);
            CurrentEvaluation = evaluation;

            string caseDirectory = Path.Combine(root, Sanitize(definition.testId));
            Directory.CreateDirectory(caseDirectory);
            string csvName = Sanitize(definition.testId) + "_run" + runIndex + ".csv";
            string csvPath = telemetryRecorder.WriteCsv(caseDirectory, csvName);

            RuntimeCaseRunResult runResult = new RuntimeCaseRunResult
            {
                testId = definition.testId,
                testKind = definition.testKind.ToString(),
                runIndex = runIndex,
                passed = evaluation.passed,
                failureReason = evaluation.failureReason,
                telemetryCsvPath = csvPath,
                metrics = evaluation.metrics,
            };

            campaign.runs.Add(runResult);
            if (!evaluation.passed)
            {
                onFailed?.Invoke();
                CurrentStatus = RuntimeRunStatus.Failed;
            }
            else
            {
                CurrentStatus = RuntimeRunStatus.Passed;
            }

            Debug.Log(definition.testId + " run " + runIndex + " => " + (evaluation.passed ? "PASS" : "FAIL"));

            float holdDuration = Mathf.Max(0f, postCaseDelaySeconds);
            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return null;
        }

        private string ResolveOutputDirectory()
        {
            string root = Path.Combine(Application.persistentDataPath, outputFolderName);
            Directory.CreateDirectory(root);
            return root;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "case";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = value;
            for (int i = 0; i < invalid.Length; i++)
            {
                sanitized = sanitized.Replace(invalid[i], '_');
            }

            return sanitized;
        }
    }
}

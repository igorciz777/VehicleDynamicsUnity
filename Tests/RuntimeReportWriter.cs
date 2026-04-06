using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace VehicleDynamics.TestsRuntime
{
    [Serializable]
    public class RuntimeCaseRunResult
    {
        public string testId;
        public string testKind;
        public int runIndex;
        public bool passed;
        public string failureReason;
        public string telemetryCsvPath;
        public List<RuntimeMetricEntry> metrics = new List<RuntimeMetricEntry>();
    }

    [Serializable]
    public class RuntimeCampaignReport
    {
        public string generatedAtUtc;
        public int totalRuns;
        public int passedRuns;
        public int failedRuns;
        public List<RuntimeCaseRunResult> runs = new List<RuntimeCaseRunResult>();
    }

    public static class RuntimeReportWriter
    {
        public static string WriteJson(string directoryPath, RuntimeCampaignReport report)
        {
            Directory.CreateDirectory(directoryPath);
            string path = Path.Combine(directoryPath, "runtime-test-report.json");
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(path, json);
            return path;
        }
    }
}

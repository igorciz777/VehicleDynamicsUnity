using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

namespace VehicleDynamics
{
    public class TireEditor : MonoBehaviour
    {
        // Public references
        public MFTireObject tireData;
        public PlotView longitudinalPlotView;
        public PlotView lateralPlotView;

        // Sampling / plot options
        public int sampleCount = 256;
        public float slipRange = 0.25f; // +/- kappa range for longitudinal
        public float angleRangeDeg = 15f; // +/- alpha range for lateral (degrees)
        public float Fz = 4000f; // vertical load [N]
        public float Fz0 = 4000f; // nominal load [N]
        public float r0 = 0.33f; // wheel radius [m]
        public Vector2 mu = new Vector2(1f, 1f); // friction multipliers (longitudinal, lateral)

        // UI elements
        public GameObject parameterPanel;
        public GameObject inputPanel;
        public GameObject textInputFieldPrefab;
        void Start()
        {
            CreateEditFields();
        }
        void Update()
        {
            RefreshPlot();
        }
        public void CreateEditFields()
        {
            foreach (Transform child in inputPanel.transform)
            {
                Destroy(child.gameObject);
            }
            var fields = tireData.GetType().GetFields();
            float yPos = 0f;
            foreach (var field in fields)
            {
                GameObject inputFieldObj = Instantiate(textInputFieldPrefab, parameterPanel.transform);
                GameObject titleObj = inputFieldObj.transform.Find("Title").gameObject;
                TMP_InputField inputField = inputFieldObj.GetComponentInChildren<TMP_InputField>();
                TextMeshProUGUI label = titleObj.GetComponent<TextMeshProUGUI>();
                RectTransform rt = inputFieldObj.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, yPos);
                yPos -= rt.sizeDelta.y + 5f;

                inputFieldObj.name = field.Name;
                label.text = field.Name;
                inputField.text = field.GetValue(tireData).ToString();

                inputField.onEndEdit.AddListener(value =>
                {
                    if (float.TryParse(value, out float floatValue))
                    {
                        field.SetValue(tireData, floatValue);
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid input for {field.Name}: {value}");
                    }
                });
            }
        }
        public void RefreshPlot()
        {
            if (longitudinalPlotView == null) { Debug.LogWarning("LongitudinalPlotView not assigned."); return; }
            if (lateralPlotView == null) { Debug.LogWarning("LateralPlotView not assigned."); return; }
            if (tireData == null) { Debug.LogWarning("MFTireObject not assigned."); return; }

            // Ensure sampleCount positive
            int n = Mathf.Max(4, sampleCount);

            // Prepare Tire model
            MagicFormula mf = new MagicFormula(tireData);

            float[] values = new float[n];

            // Sweep kappa from -slipRange .. +slipRange
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : i / (float)(n - 1);
                float kap = Mathf.Lerp(-slipRange, slipRange, t);
                TireInput input = new(
                    kap,
                    0f,
                    Fz,
                    0f,
                    Fz0,
                    r0,
                    r0,
                    0f,
                    mu,
                    mu,
                    mu
                );

                mf.tr = input;
                mf.UpdateSharedParams();
                float Fx = mf.GetPureLongitudinal(); // N
                                                     // Normalize to coefficient Fx/Fz
                float mu_eff = (Mathf.Approximately(Fz, 0f) ? 0f : Fx / Fz);
                values[i] = mu_eff;
            }
            longitudinalPlotView.plotTitle = $"Longitudinal mu vs kappa (@Fz={Fz} N)";
            longitudinalPlotView.plotColor = Color.cyan;

            longitudinalPlotView.dataBufferSize = values.Length;
            longitudinalPlotView.SetData(values);

            float angleRangeRad = Mathf.Deg2Rad * angleRangeDeg;
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : i / (float)(n - 1);
                float alph = Mathf.Lerp(-angleRangeRad, angleRangeRad, t);
                TireInput input = new(
                    0f,
                    alph,
                    Fz,
                    0f,
                    Fz0,
                    r0,
                    r0,
                    0f,
                    mu,
                    mu,
                    mu
                );

                mf.tr = input;
                mf.UpdateSharedParams();
                float Fy = mf.GetPureLateral(); // N
                float mu_eff = (Mathf.Approximately(Fz, 0f) ? 0f : Fy / Fz);
                values[i] = mu_eff;
            }
            lateralPlotView.plotTitle = $"Lateral mu vs alpha (@Fz={Fz} N)";
            lateralPlotView.plotColor = Color.magenta;

            // Ensure the PlotView knows the correct buffer size
            lateralPlotView.dataBufferSize = values.Length;
            lateralPlotView.SetData(values);

        }
    }
}
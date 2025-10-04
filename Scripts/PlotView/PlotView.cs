using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlotView : MonoBehaviour
{
    [Header("Plot Settings")]
    public string plotTitle = "Data Plot";
    public Color plotColor = Color.cyan;
    public int dataBufferSize = 200;
    public float updateInterval = 0.1f;

    [Header("Display Settings")]
    public Vector2 plotSize = new(400, 200);
    public bool autoScaleY = true;
    public Vector2 manualYRange = new(0, 100);
    public bool showGrid = true;
    public bool showCurrentValue = true;

    [Header("UI References")]
    public RectTransform plotRoot;
    public RectTransform plotContainer;
    public RawImage plotImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI valueText;
    public TextMeshProUGUI maxYLabel;
    public TextMeshProUGUI minYLabel;

    private Queue<float> dataPoints = new();
    private float lastUpdateTime;
    private float currentValue;

    private float newDataValue;
    private bool hasNewData;

    private Texture2D plotTexture;

    // Internal data for Static mode
    private float[] staticXData;
    private float[] staticYData;

    void Start()
    {
        InitializeDataBuffer();
        plotTexture = new Texture2D((int)plotSize.x, (int)plotSize.y, TextureFormat.RGBA32, false);
        plotRoot.sizeDelta = new Vector2(plotSize.x + 40, plotSize.y + 40);
        plotContainer.sizeDelta = plotSize;
        plotTexture.filterMode = FilterMode.Point;
        plotImage.texture = plotTexture;
        titleText.text = plotTitle;
    }
    void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            if (hasNewData)
            {
                AddDataPoint(newDataValue);
                hasNewData = false;
            }
            lastUpdateTime = Time.time;
            DrawPlotToTexture();
        }
    }

    private void InitializeDataBuffer()
    {
        dataPoints.Clear();
        for (int i = 0; i < dataBufferSize; i++)
        {
            dataPoints.Enqueue(0f);
        }
    }

    private void AddDataPoint(float value)
    {
        currentValue = value;
        dataPoints.Dequeue();
        dataPoints.Enqueue(value);
    }

    private void DrawPlotToTexture()
    {
        // Clear
        Color32 bgColor = new(25, 25, 25, 255);
        Color32[] bgPixels = new Color32[(int)plotSize.x * (int)plotSize.y];
        for (int i = 0; i < bgPixels.Length; i++) bgPixels[i] = bgColor;
        plotTexture.SetPixels32(bgPixels);

        // Draw grid
        if (showGrid)
            DrawGridOnTexture();

        // Draw plot line
        DrawPlotLineOnTexture();

        plotTexture.Apply();

        // Update labels
        titleText.text = plotTitle;
        if (showCurrentValue)
            valueText.text = $"{currentValue:F2}";
        else
            valueText.text = "";

        float[] dataArray = dataPoints.ToArray();
        float minY = autoScaleY ? GetMinValue(dataArray) : manualYRange.x;
        float maxY = autoScaleY ? GetMaxValue(dataArray) : manualYRange.y;

        maxYLabel.text = maxY.ToString("F1");
        minYLabel.text = minY.ToString("F1");
    }

    private void DrawGridOnTexture()
    {
        Color gridColor = new(0.5f, 0.5f, 0.5f, 0.3f);

        int w = (int)plotSize.x;
        int h = (int)plotSize.y;

        // Horizontal grid lines
        for (int i = 1; i < 5; i++)
        {
            int y = (int)(h / 5f * i);
            DrawHorizontalLine(y, gridColor);
        }

        // Vertical grid lines
        for (int i = 1; i < 5; i++)
        {
            int x = (int)(w / 5f * i);
            DrawVerticalLine(x, gridColor);
        }
    }

    private void DrawHorizontalLine(int y, Color color)
    {
        int w = (int)plotSize.x;
        for (int x = 0; x < w; x++)
        {
            plotTexture.SetPixel(x, y, color);
        }
    }

    private void DrawVerticalLine(int x, Color color)
    {
        int h = (int)plotSize.y;
        for (int y = 0; y < h; y++)
        {
            plotTexture.SetPixel(x, y, color);
        }
    }

    private void DrawPlotLineOnTexture()
    {
        if (dataPoints.Count == 0) return;

        float[] dataArray = dataPoints.ToArray();
        float minY = autoScaleY ? GetMinValue(dataArray) : manualYRange.x;
        float maxY = autoScaleY ? GetMaxValue(dataArray) : manualYRange.y;

        if (Mathf.Approximately(minY, maxY))
        {
            minY -= 1f;
            maxY += 1f;
        }

        int w = (int)plotSize.x;
        int h = (int)plotSize.y;

        Vector2Int prev = Vector2Int.zero;
        bool first = true;

        for (int i = 0; i < dataArray.Length; i++)
        {
            int x = (int)(w / (float)dataBufferSize * i);
            float normalizedY = Mathf.InverseLerp(minY, maxY, dataArray[i]);
            int y = (int)(normalizedY * h);

            Vector2Int curr = new(x, y);

            if (!first)
            {
                DrawLineOnTexture(prev, curr, plotColor);
            }
            prev = curr;
            first = false;
        }
    }

    // Bresenham's line algorithm for Texture2D
    private void DrawLineOnTexture(Vector2Int p0, Vector2Int p1, Color color)
    {
        int dx = Mathf.Abs(p1.x - p0.x);
        int dy = Mathf.Abs(p1.y - p0.y);
        int sx = p0.x < p1.x ? 1 : -1;
        int sy = p0.y < p1.y ? 1 : -1;
        int err = dx - dy;

        int x = p0.x;
        int y = p0.y;

        while (true)
        {
            if (x >= 0 && x < plotTexture.width && y >= 0 && y < plotTexture.height)
                plotTexture.SetPixel(x, y, color);

            if (x == p1.x && y == p1.y) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
        }
    }

    private float GetMinValue(float[] data)
    {
        float min = float.MaxValue;
        foreach (float value in data)
        {
            if (value < min) min = value;
        }
        return min;
    }

    private float GetMaxValue(float[] data)
    {
        float max = float.MinValue;
        foreach (float value in data)
        {
            if (value > max) max = value;
        }
        return max;
    }

    // Public API
    public void AddData(float value)
    {
        newDataValue = value;
        hasNewData = true;
    }

    // For Scrolling: values = y array; For Static: x and y arrays
    public void SetData(float[] values)
    {
        dataPoints.Clear();
        foreach (float value in values)
        {
            dataPoints.Enqueue(value);
        }
        while (dataPoints.Count < dataBufferSize)
        {
            dataPoints.Enqueue(0f);
        }
    }

    public void ClearData()
    {
        InitializeDataBuffer();
    }

    public void SetSize(Vector2 size)
    {
        plotSize = size;
        plotTexture = new Texture2D((int)plotSize.x, (int)plotSize.y, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        plotImage.texture = plotTexture;
    }
}
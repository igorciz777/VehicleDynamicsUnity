using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

public class DirectInputDevice : MonoBehaviour
{
    const string DllName = "DirectInputManager";

    [Header("Device Settings")]
    public string targetDeviceName = "wheel";
    public bool autoInitialize = true;

    [Header("Input Settings")]
    public int maxAxes = 8;
    public int maxButtons = 128;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool logInputChanges = false;

    // Input state
    [SerializeField] private float[] axes;
    private byte[] buttons;
    private float[] prevAxes;
    private byte[] prevButtons;

    // Device info
    private string deviceGuid;
    private int deviceId = -1;
    private bool deviceInitialized = false;

    // Events for input changes
    public event Action<int, float> OnAxisChanged;
    public event Action<int, bool> OnButtonChanged;
    public event Action OnDeviceInitialized;
    public event Action OnDeviceDisconnected;

    // Public properties
    public int DeviceId => deviceId;
    public bool IsInitialized => deviceInitialized;
    public string DeviceGuid => deviceGuid;

    void Start()
    {
        axes = new float[maxAxes];
        buttons = new byte[maxButtons];
        prevAxes = new float[maxAxes];
        prevButtons = new byte[maxButtons];

        if (autoInitialize)
        {
            InitializeDevice();
        }
    }

    void Update()
    {
        if (deviceInitialized && deviceId >= 0)
        {
            // Check if device is still valid
            if (!DirectInputManager.IsDeviceValid(deviceId))
            {
                Debug.LogWarning($"Device {deviceId} is no longer valid. Attempting to reinitialize.");
                OnDeviceDisconnected?.Invoke();
                ReinitializeDevice();
                return;
            }

            // Save previous state
            Array.Copy(axes, prevAxes, maxAxes);
            Array.Copy(buttons, prevButtons, maxButtons);

            // Read new state
            bool success = DirectInputManager.ReadDeviceState(deviceId, axes, maxAxes, buttons, maxButtons);

            if (!success)
            {
                Debug.LogWarning("Failed to read device state. Attempting to reinitialize.");
                ReinitializeDevice();
                return;
            }

            // Check for changes and trigger events
            CheckForInputChanges();

            // Debug output
            if (showDebugInfo)
            {
                DisplayDebugInfo();
            }
        }
    }

    public bool InitializeDevice()
    {
        return InitializeDevice(targetDeviceName);
    }

    public bool InitializeDevice(string deviceNameFilter)
    {
        var devices = DirectInputManager.GetDeviceList();
        string foundDeviceGuid = null;
        string foundDeviceName = null;

        foreach (var device in devices)
        {
            if (device.deviceName.ToLower().Contains(deviceNameFilter.ToLower()))
            {
                foundDeviceGuid = device.instanceGuid;
                foundDeviceName = device.deviceName;
                break;
            }
        }

        if (string.IsNullOrEmpty(foundDeviceGuid))
        {
            Debug.LogWarning($"No device containing '{deviceNameFilter}' found.");
            return false;
        }

        deviceGuid = foundDeviceGuid;
        deviceId = DirectInputManager.InitializeDevice(deviceGuid);

        if (deviceId >= 0)
        {
            deviceInitialized = true;
            Debug.Log($"Device initialized successfully: {foundDeviceName} (ID: {deviceId})");
            OnDeviceInitialized?.Invoke();
            return true;
        }
        else
        {
            Debug.LogError($"Failed to initialize device: {foundDeviceName}");
            deviceInitialized = false;
            return false;
        }
    }

    public bool InitializeDeviceByGuid(string guid)
    {
        deviceGuid = guid;
        deviceId = DirectInputManager.InitializeDevice(deviceGuid);

        if (deviceId >= 0)
        {
            deviceInitialized = true;
            Debug.Log($"Device initialized successfully by GUID: {deviceGuid} (ID: {deviceId})");
            OnDeviceInitialized?.Invoke();
            return true;
        }
        else
        {
            Debug.LogError($"Failed to initialize device by GUID: {deviceGuid}");
            deviceInitialized = false;
            return false;
        }
    }

    void ReinitializeDevice()
    {
        ShutdownDevice();
        if (!string.IsNullOrEmpty(deviceGuid))
        {
            InitializeDeviceByGuid(deviceGuid);
        }
        else
        {
            InitializeDevice();
        }
    }

    void CheckForInputChanges()
    {
        // Check for axis changes
        for (int i = 0; i < maxAxes; i++)
        {
            if (Math.Abs(axes[i] - prevAxes[i]) > 0.01f)
            {
                OnAxisChanged?.Invoke(i, axes[i]);

                if (logInputChanges)
                {
                    Debug.Log($"Axis {i} changed: {axes[i]:F3}");
                }
            }
        }

        // Check for button changes
        for (int i = 0; i < maxButtons; i++)
        {
            if (buttons[i] != prevButtons[i])
            {
                OnButtonChanged?.Invoke(i, buttons[i] > 0);

                if (logInputChanges)
                {
                    Debug.Log($"Button {i} {(buttons[i] > 0 ? "pressed" : "released")}");
                }
            }
        }
    }

    void DisplayDebugInfo()
    {
        string debugText = $"DirectInput Device (ID: {deviceId}) Status:\n";
        debugText += $"Initialized: {deviceInitialized}\n";
        debugText += $"GUID: {deviceGuid}\n\n";

        debugText += "Axes:\n";
        for (int i = 0; i < maxAxes; i++)
        {
            debugText += $"{i}: {axes[i]:F3}\n";
        }

        debugText += "\nButtons:\n";
        for (int i = 0; i < Mathf.Min(16, maxButtons); i++) // Only show first 16 buttons
        {
            debugText += $"{i}: {(buttons[i] > 0 ? "X" : "O")} ";
            if ((i + 1) % 8 == 0) debugText += "\n";
        }

        Debug.Log(debugText);
    }

    public float GetAxis(int index)
    {
        if (index < 0 || index >= maxAxes || !deviceInitialized) return 0;
        return axes[index];
    }

    public bool GetButton(int index)
    {
        if (index < 0 || index >= maxButtons || !deviceInitialized) return false;
        return buttons[index] > 0;
    }

    public bool GetButtonDown(int index)
    {
        if (index < 0 || index >= maxButtons || !deviceInitialized) return false;
        return buttons[index] > 0 && prevButtons[index] == 0;
    }

    public bool GetButtonUp(int index)
    {
        if (index < 0 || index >= maxButtons || !deviceInitialized) return false;
        return buttons[index] == 0 && prevButtons[index] > 0;
    }

    void OnDestroy()
    {
        ShutdownDevice();
    }

    public void ShutdownDevice()
    {
        if (deviceInitialized && deviceId >= 0)
        {
            DirectInputManager.ShutdownDevice(deviceId);
            deviceInitialized = false;
            deviceId = -1;
        }
    }

    public void ApplyConstantForce(long magnitude)
    {
        if (!deviceInitialized || deviceId < 0) return;

        // Clamp magnitude to FFB valid range
        magnitude = Math.Clamp(magnitude, -10000, 10000);
        bool success = DirectInputManager.SetConstantForce(deviceId, magnitude);

        if (!success)
        {
            Debug.LogError($"Failed to apply constant force to device {deviceId}");
        }
    }
    
    public void ApplySpringForce(long offset, long saturation)
    {
        if (!deviceInitialized || deviceId < 0) return;

        saturation = Math.Clamp(saturation, 0, 10000);
        bool success = DirectInputManager.SetSpringForce(deviceId, offset, saturation);

        if (!success)
        {
            Debug.LogError($"Failed to apply spring force to device {deviceId}");
        }
    }

    // Static method to get active device count
    public static int GetActiveDevicesCount()
    {
        return DirectInputManager.GetActiveDeviceCount();
    }

    // Method to check if this specific device is still connected
    public bool IsConnected()
    {
        return deviceInitialized && deviceId >= 0 && DirectInputManager.IsDeviceValid(deviceId);
    }
}
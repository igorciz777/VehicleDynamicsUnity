using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class DirectInputManager : MonoBehaviour
{
    const string DllName = "DirectInputManager";

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int GetDirectInputDeviceCount();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern void GetDirectInputDeviceInfo(int index, 
        StringBuilder deviceName, int nameSize,
        StringBuilder instanceGuid, int guidSize);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int InitializeDevice(string instanceGuid);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ReadDeviceState(int deviceId,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] float[] axes, int maxAxes,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buttons, int maxButtons);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void ShutdownDevice(int deviceId);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void ShutdownAllDevices();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void ShutdownDirectInput();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool SetConstantForce(int deviceId, long magnitude);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool SetSpringForce(int deviceId, long offset, long saturation);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int GetActiveDeviceCount();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern bool IsDeviceValid(int deviceId);

    public class DeviceInfo
    {
        public string deviceName;
        public string instanceGuid;
    }

    public static List<DeviceInfo> GetDeviceList()
    {
        List<DeviceInfo> deviceList = new List<DeviceInfo>();
        
        try
        {
            int deviceCount = GetDirectInputDeviceCount();
            
            for (int i = 0; i < deviceCount; i++)
            {
                StringBuilder deviceName = new StringBuilder(256);
                StringBuilder instanceGuid = new StringBuilder(64);
                
                GetDirectInputDeviceInfo(i, deviceName, deviceName.Capacity, instanceGuid, instanceGuid.Capacity);
                
                deviceList.Add(new DeviceInfo
                {
                    deviceName = deviceName.ToString(),
                    instanceGuid = instanceGuid.ToString()
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting device list: {e.Message}");
        }

        return deviceList;
    }

    void OnApplicationQuit()
    {
        ShutdownDirectInput();
    }
}
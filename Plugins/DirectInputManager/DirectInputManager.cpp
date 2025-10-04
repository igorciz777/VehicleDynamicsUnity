#include <windows.h>
#include <dinput.h>
#include <vector>
#include <string>
#include <sstream>
#include <map>

// Global DirectInput interface
static IDirectInput8W* g_pDI = nullptr;

// Device context structure
struct DeviceContext {
    IDirectInputDevice8W* device = nullptr;
    bool acquired = false;
    std::vector<IDirectInputEffect*> effects; // Store created effects for cleanup
};

// Device info structure
struct DeviceInfo {
    char deviceName[256];
    char instanceGuid[64];
    char productGuid[64];
};

static std::vector<DeviceInfo> g_deviceList;
static std::map<int, DeviceContext> g_activeDevices;
static int g_nextDeviceId = 1;

//-------------------------------------------------------------------
// Callback function for device enumeration
//-------------------------------------------------------------------
BOOL CALLBACK EnumDevicesCallback(LPCDIDEVICEINSTANCEW lpddi, LPVOID pvRef) {
    auto* devices = static_cast<std::vector<DeviceInfo>*>(pvRef);

    DeviceInfo info = {};

    // Convert wide char to multi-byte string
    WideCharToMultiByte(CP_UTF8, 0, lpddi->tszProductName, -1,
        info.deviceName, sizeof(info.deviceName), nullptr, nullptr);

    // Convert GUIDs to strings
    sprintf_s(info.instanceGuid, "{%08X-%04X-%04X-%02X%02X-%02X%02X-%02X%02X%02X%02X}",
        lpddi->guidInstance.Data1, lpddi->guidInstance.Data2, lpddi->guidInstance.Data3,
        lpddi->guidInstance.Data4[0], lpddi->guidInstance.Data4[1],
        lpddi->guidInstance.Data4[2], lpddi->guidInstance.Data4[3],
        lpddi->guidInstance.Data4[4], lpddi->guidInstance.Data4[5],
        lpddi->guidInstance.Data4[6], lpddi->guidInstance.Data4[7]);

    sprintf_s(info.productGuid, "{%08X-%04X-%04X-%02X%02X-%02X%02X-%02X%02X%02X%02X}",
        lpddi->guidProduct.Data1, lpddi->guidProduct.Data2, lpddi->guidProduct.Data3,
        lpddi->guidProduct.Data4[0], lpddi->guidProduct.Data4[1],
        lpddi->guidProduct.Data4[2], lpddi->guidProduct.Data4[3],
        lpddi->guidProduct.Data4[4], lpddi->guidProduct.Data4[5],
        lpddi->guidProduct.Data4[6], lpddi->guidProduct.Data4[7]);

    devices->push_back(info);
    return DIENUM_CONTINUE;
}

//-------------------------------------------------------------------
// Device enumeration
//-------------------------------------------------------------------
extern "C" __declspec(dllexport) int __stdcall GetDirectInputDeviceCount() {
    g_deviceList.clear();

    if (g_pDI == nullptr) {
        HRESULT hr = DirectInput8Create(GetModuleHandle(nullptr),
            DIRECTINPUT_VERSION,
            IID_IDirectInput8W,
            (LPVOID*)&g_pDI,
            nullptr);
        if (FAILED(hr)) return 0;
    }

    HRESULT hr = g_pDI->EnumDevices(DI8DEVCLASS_ALL,
        EnumDevicesCallback,
        &g_deviceList,
        DIEDFL_ATTACHEDONLY);

    if (FAILED(hr)) return 0;
    return (int)g_deviceList.size();
}

extern "C" __declspec(dllexport) void __stdcall GetDirectInputDeviceInfo(int index,
    char* deviceName, int nameSize,
    char* instanceGuid, int guidSize) {
    if (index < 0 || index >= g_deviceList.size()) {
        if (deviceName && nameSize > 0) deviceName[0] = '\0';
        if (instanceGuid && guidSize > 0) instanceGuid[0] = '\0';
        return;
    }

    const DeviceInfo& info = g_deviceList[index];
    strcpy_s(deviceName, nameSize, info.deviceName);
    strcpy_s(instanceGuid, guidSize, info.instanceGuid);
}

//-------------------------------------------------------------------
// Device initialization and input reading
//-------------------------------------------------------------------
extern "C" __declspec(dllexport) int __stdcall InitializeDevice(const char* instanceGuidStr) {
    if (g_pDI == nullptr) return -1;

    // Convert string back to GUID
    GUID deviceGuid;
    sscanf_s(instanceGuidStr,
        "{%8X-%4hX-%4hX-%2hhX%2hhX-%2hhX%2hhX-%2hhX%2hhX%2hhX%2hhX}",
        &deviceGuid.Data1, &deviceGuid.Data2, &deviceGuid.Data3,
        &deviceGuid.Data4[0], &deviceGuid.Data4[1], &deviceGuid.Data4[2], &deviceGuid.Data4[3],
        &deviceGuid.Data4[4], &deviceGuid.Data4[5], &deviceGuid.Data4[6], &deviceGuid.Data4[7]);

    // Create device context
    DeviceContext context;

    // Create device
    HRESULT hr = g_pDI->CreateDevice(deviceGuid, &context.device, nullptr);
    if (FAILED(hr)) {
        OutputDebugStringA("InitializeDevice: CreateDevice failed\n");
        return -1;
    }

    // Set data format for joystick
    hr = context.device->SetDataFormat(&c_dfDIJoystick);
    if (FAILED(hr)) {
        context.device->Release();
        OutputDebugStringA("InitializeDevice: SetDataFormat failed\n");
        return -1;
    }

    // Set cooperative level - use exclusive mode for force feedback
    //hr = context.device->SetCooperativeLevel(GetDesktopWindow(), DISCL_BACKGROUND | DISCL_EXCLUSIVE);
    //if (FAILED(hr)) {
    //    // Try non-exclusive as fallback
    //    hr = context.device->SetCooperativeLevel(GetDesktopWindow(), DISCL_BACKGROUND | DISCL_NONEXCLUSIVE);
    //    if (FAILED(hr)) {
    //        context.device->Release();
    //        OutputDebugStringA("InitializeDevice: SetCooperativeLevel failed\n");
    //        return -1;
    //    }
    //}

    // Set axis ranges
    DIPROPRANGE dipRange;
    dipRange.diph.dwSize = sizeof(DIPROPRANGE);
    dipRange.diph.dwHeaderSize = sizeof(DIPROPHEADER);
    dipRange.diph.dwHow = DIPH_DEVICE;
    dipRange.diph.dwObj = 0;
    dipRange.lMin = -1000;
    dipRange.lMax = 1000;

    hr = context.device->SetProperty(DIPROP_RANGE, &dipRange.diph);
    // Continue even if this fails

    // Check if device supports force feedback
    DIDEVCAPS capabilities;
    capabilities.dwSize = sizeof(DIDEVCAPS);
    hr = context.device->GetCapabilities(&capabilities);
    if (SUCCEEDED(hr) && (capabilities.dwFlags & DIDC_FORCEFEEDBACK)) {
        OutputDebugStringA("InitializeDevice: Device supports force feedback\n");

        // Set force feedback gain
        DIPROPDWORD dipGain;
        dipGain.diph.dwSize = sizeof(DIPROPDWORD);
        dipGain.diph.dwHeaderSize = sizeof(DIPROPHEADER);
        dipGain.diph.dwHow = DIPH_DEVICE;
        dipGain.diph.dwObj = 0;
        dipGain.dwData = 10000; // 100% gain

        context.device->SetProperty(DIPROP_FFGAIN, &dipGain.diph);
    }

    // Acquire the device
    hr = context.device->Acquire();
    if (FAILED(hr)) {
        context.device->Release();
        OutputDebugStringA("InitializeDevice: Acquire failed\n");
        return -1;
    }

    context.acquired = true;

    // Store device context and return ID
    int deviceId = g_nextDeviceId++;
    g_activeDevices[deviceId] = context;

    char debugMsg[256];
    sprintf_s(debugMsg, "InitializeDevice: Successfully initialized device %d\n", deviceId);
    OutputDebugStringA(debugMsg);

    return deviceId;
}

extern "C" __declspec(dllexport) bool __stdcall DeviceSupportsForceFeedback(int deviceId) {
    auto it = g_activeDevices.find(deviceId);
    if (it == g_activeDevices.end() || !it->second.acquired) return false;

    DeviceContext& context = it->second;

    DIDEVCAPS capabilities;
    capabilities.dwSize = sizeof(DIDEVCAPS);
    HRESULT hr = context.device->GetCapabilities(&capabilities);

    return SUCCEEDED(hr) && (capabilities.dwFlags & DIDC_FORCEFEEDBACK);
}

extern "C" __declspec(dllexport) bool __stdcall ReadDeviceState(int deviceId, float* axes, int maxAxes, unsigned char* buttons, int maxButtons) {
    auto it = g_activeDevices.find(deviceId);
    if (it == g_activeDevices.end() || !it->second.acquired) return false;

    DeviceContext& context = it->second;

    DIJOYSTATE state;
    HRESULT hr = context.device->GetDeviceState(sizeof(DIJOYSTATE), &state);

    // Try to reacquire if needed
    if (hr == DIERR_INPUTLOST || hr == DIERR_NOTACQUIRED) {
        context.device->Acquire();
        hr = context.device->GetDeviceState(sizeof(DIJOYSTATE), &state);
    }

    if (FAILED(hr)) return false;

    // Copy axis data (normalized to -1.0 to 1.0)
    int axesToCopy = min(maxAxes, 8);
    long axisValues[8] = {
        state.lX, state.lY, state.lZ,
        state.lRx, state.lRy, state.lRz,
        state.rglSlider[0], state.rglSlider[1]
    };

    for (int i = 0; i < axesToCopy; i++) {
        axes[i] = axisValues[i] / 1000.0f; // Normalize to -1.0 to 1.0
    }

    // Copy button data
    int buttonsToCopy = min(maxButtons, 128);
    for (int i = 0; i < buttonsToCopy; i++) {
        buttons[i] = (state.rgbButtons[i] & 0x80) ? 1 : 0;
    }

    return true;
}

extern "C" __declspec(dllexport) void __stdcall ShutdownDevice(int deviceId) {
    auto it = g_activeDevices.find(deviceId);
    if (it != g_activeDevices.end()) {
        DeviceContext& context = it->second;

        // Clean up all effects
        for (auto* effect : context.effects) {
            if (effect) {
                effect->Stop();
                effect->Release();
            }
        }
        context.effects.clear();

        if (context.device) {
            context.device->Unacquire();
            context.device->Release();
        }
        g_activeDevices.erase(it);
    }
}

extern "C" __declspec(dllexport) void __stdcall ShutdownAllDevices() {
    for (auto& pair : g_activeDevices) {
        DeviceContext& context = pair.second;

        // Clean up all effects
        for (auto* effect : context.effects) {
            if (effect) {
                effect->Stop();
                effect->Release();
            }
        }

        if (context.device) {
            context.device->Unacquire();
            context.device->Release();
        }
    }
    g_activeDevices.clear();
}

extern "C" __declspec(dllexport) void __stdcall ShutdownDirectInput() {
    ShutdownAllDevices();
    if (g_pDI) {
        g_pDI->Release();
        g_pDI = nullptr;
    }
    g_deviceList.clear();
    g_nextDeviceId = 1;
}

//-------------------------------------------------------------------
// Force Feedback Functions
//-------------------------------------------------------------------
extern "C" __declspec(dllexport) bool __stdcall SetConstantForce(int deviceId, long magnitude) {
    auto it = g_activeDevices.find(deviceId);
    if (it == g_activeDevices.end() || !it->second.acquired) {
        OutputDebugStringA("SetConstantForce: Device not found or not acquired\n");
        return false;
    }

    DeviceContext& context = it->second;

    // First, check if the device supports force feedback
    DIDEVCAPS capabilities;
    capabilities.dwSize = sizeof(DIDEVCAPS);
    HRESULT hr = context.device->GetCapabilities(&capabilities);
    if (FAILED(hr)) {
        OutputDebugStringA("SetConstantForce: Failed to get device capabilities\n");
        return false;
    }

    if (!(capabilities.dwFlags & DIDC_FORCEFEEDBACK)) {
        OutputDebugStringA("SetConstantForce: Device does not support force feedback\n");
        return false;
    }

    // Stop any existing effects first
    for (auto* effect : context.effects) {
        if (effect) {
            effect->Stop();
            effect->Release();
        }
    }
    context.effects.clear();

    // Set up effect parameters
    DIEFFECT effect = { 0 };
    effect.dwSize = sizeof(DIEFFECT);
    effect.dwFlags = DIEFF_CARTESIAN | DIEFF_OBJECTOFFSETS;
    effect.dwDuration = INFINITE;
    effect.dwSamplePeriod = 0;
    effect.dwGain = DI_FFNOMINALMAX;
    effect.dwTriggerButton = DIEB_NOTRIGGER;
    effect.dwTriggerRepeatInterval = 0;
    effect.cAxes = 2; // Important: Most wheels need 2 axes for proper FFB
    effect.rgdwAxes = new DWORD[2]{ DIJOFS_X, DIJOFS_Y };
    effect.rglDirection = new LONG[2]{ 0, 0 };
    effect.lpEnvelope = NULL;
    effect.cbTypeSpecificParams = sizeof(DICONSTANTFORCE);

    // Fix: Create a named variable instead of temporary
    DICONSTANTFORCE constantForce = {};
    constantForce.lMagnitude = magnitude;
    effect.lpvTypeSpecificParams = &constantForce;
    effect.dwStartDelay = 0;

    // Create the effect
    IDirectInputEffect* pEffect = nullptr;
    hr = context.device->CreateEffect(GUID_ConstantForce, &effect, &pEffect, NULL);

    // Clean up allocated arrays
    delete[] effect.rgdwAxes;
    delete[] effect.rglDirection;

    if (SUCCEEDED(hr) && pEffect != nullptr) {
        hr = pEffect->Start(1, 0);
        if (SUCCEEDED(hr)) {
            context.effects.push_back(pEffect);

            char debugMsg[256];
            sprintf_s(debugMsg, "SetConstantForce: Successfully applied force %ld to device %d\n", magnitude, deviceId);
            OutputDebugStringA(debugMsg);
            return true;
        }
        else {
            pEffect->Release();
            OutputDebugStringA("SetConstantForce: Failed to start effect\n");
        }
    }
    else {
        char debugMsg[256];
        sprintf_s(debugMsg, "SetConstantForce: CreateEffect failed with HRESULT: 0x%08X\n", hr);
        OutputDebugStringA(debugMsg);
    }

    return false;
}

extern "C" __declspec(dllexport) bool __stdcall SetSpringForce(int deviceId, long offset, long saturation) {
    auto it = g_activeDevices.find(deviceId);
    if (it == g_activeDevices.end() || !it->second.acquired) {
        OutputDebugStringA("SetSpringForce: Device not found or not acquired\n");
        return false;
    }

    DeviceContext& context = it->second;

    // Check if device supports force feedback
    DIDEVCAPS capabilities;
    capabilities.dwSize = sizeof(DIDEVCAPS);
    HRESULT hr = context.device->GetCapabilities(&capabilities);
    if (FAILED(hr)) {
        OutputDebugStringA("SetSpringForce: Failed to get device capabilities\n");
        return false;
    }

    if (!(capabilities.dwFlags & DIDC_FORCEFEEDBACK)) {
        OutputDebugStringA("SetSpringForce: Device does not support force feedback\n");
        return false;
    }

    // Stop any existing effects first
    for (auto* effect : context.effects) {
        if (effect) {
            effect->Stop();
            effect->Release();
        }
    }
    context.effects.clear();

    // Set up spring effect parameters
    DIEFFECT effect = { 0 };
    effect.dwSize = sizeof(DIEFFECT);
    effect.dwFlags = DIEFF_CARTESIAN | DIEFF_OBJECTOFFSETS;
    effect.dwDuration = INFINITE;
    effect.dwSamplePeriod = 0;
    effect.dwGain = DI_FFNOMINALMAX;
    effect.dwTriggerButton = DIEB_NOTRIGGER;
    effect.dwTriggerRepeatInterval = 0;
    effect.cAxes = 2;
    effect.rgdwAxes = new DWORD[2]{ DIJOFS_X, DIJOFS_Y };
    effect.rglDirection = new LONG[2]{ 0, 0 };
    effect.lpEnvelope = NULL;
    effect.cbTypeSpecificParams = sizeof(DICONDITION) * 2; // Two conditions, one for each axis

    // Setup spring condition for both axes
    DICONDITION springConditions[2] = {
        {static_cast<LONG>(offset), static_cast<LONG>(saturation), static_cast<LONG>(saturation), static_cast<DWORD>(saturation), static_cast<DWORD>(saturation), 0}, // X-axis
        {static_cast<LONG>(offset), static_cast<LONG>(saturation), static_cast<LONG>(saturation), static_cast<DWORD>(saturation), static_cast<DWORD>(saturation), 0}  // Y-axis
    };
    effect.lpvTypeSpecificParams = springConditions;
    effect.dwStartDelay = 0;

    // Create the effect
    IDirectInputEffect* pEffect = nullptr;
    hr = context.device->CreateEffect(GUID_Spring, &effect, &pEffect, NULL);

    // Clean up allocated arrays
    delete[] effect.rgdwAxes;
    delete[] effect.rglDirection;

    if (SUCCEEDED(hr) && pEffect != nullptr) {
        hr = pEffect->Start(1, 0);
        if (SUCCEEDED(hr)) {
            context.effects.push_back(pEffect);

            char debugMsg[256];
            sprintf_s(debugMsg, "SetSpringForce: Successfully applied spring to device %d\n", deviceId);
            OutputDebugStringA(debugMsg);
            return true;
        }
        else {
            pEffect->Release();
            OutputDebugStringA("SetSpringForce: Failed to start effect\n");
        }
    }
    else {
        char debugMsg[256];
        sprintf_s(debugMsg, "SetSpringForce: CreateEffect failed with HRESULT: 0x%08X\n", hr);
        OutputDebugStringA(debugMsg);
    }

    return false;
}

// Helper function to get number of active devices
extern "C" __declspec(dllexport) int __stdcall GetActiveDeviceCount() {
    return (int)g_activeDevices.size();
}

// Helper function to check if device is still valid
extern "C" __declspec(dllexport) bool __stdcall IsDeviceValid(int deviceId) {
    return g_activeDevices.find(deviceId) != g_activeDevices.end();
}
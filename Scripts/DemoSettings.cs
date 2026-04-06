using UnityEngine;

/// <summary>
/// Applies global demo configuration settings such as screen resolution.
/// </summary>
public class DemoSettings : MonoBehaviour
{
    private void Start()
    {
        // set to windowed 1600x900
        #if !UNITY_WEBGL
        Screen.SetResolution(1600, 900, false);
        #endif
    }
}

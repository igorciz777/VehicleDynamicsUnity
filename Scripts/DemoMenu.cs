using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu controller handling vehicle and track selection with UI navigation.
/// </summary>
public class DemoMenu : MonoBehaviour
{
    private void Start()
    {
        if (SelectionManager.Instance == null)
        {
            new GameObject("SelectionManager").AddComponent<SelectionManager>();
        }

        if (playButton != null) playButton.onClick.AddListener(OnPlay);
        if (testCasesButton != null) testCasesButton.onClick.AddListener(onTestCases);
#if !UNITY_WEBGL
        Screen.SetResolution(1600, 900, false);
#endif
    }
    public Toggle[] vehicleToggles;
    public Toggle[] trackToggles;
    public string[] trackSceneNames;
    public Button playButton;
    public Button testCasesButton;

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnPlay);
        if (testCasesButton != null) testCasesButton.onClick.RemoveListener(onTestCases);
    }

    private int GetSelectedIndex(Toggle[] toggles)
    {
        if (toggles == null) return -1;
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i] != null && toggles[i].isOn) return i;
        }
        return -1;
    }

    private void OnPlay()
    {
        int vehicleIndex = GetSelectedIndex(vehicleToggles);
        int trackIndex = GetSelectedIndex(trackToggles);

        if (vehicleIndex < 0 || trackIndex < 0)
        {
            Debug.LogWarning("Please select a vehicle and a track before playing.");
            return;
        }

        if (trackSceneNames == null || trackIndex >= trackSceneNames.Length)
        {
            Debug.LogError("Track scene name is not set for the selected track index.");
            return;
        }

        // Store selections
        SelectionManager.Instance.selectedVehicleIndex = vehicleIndex;
        SelectionManager.Instance.selectedTrackIndex = trackIndex;

        // Load the selected track scene
        SceneManager.LoadScene(trackSceneNames[trackIndex]);
    }

    private void onTestCases()
    {
        SceneManager.LoadScene("TestCases");
    }
}

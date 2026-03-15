using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DemoMenu : MonoBehaviour
{
    private void Start()
    {
        if (SelectionManager.Instance == null)
        {
            new GameObject("SelectionManager").AddComponent<SelectionManager>();
        }

        if (playButton != null) playButton.onClick.AddListener(OnPlay);
    }
    public Toggle[] vehicleToggles;
    public Toggle[] trackToggles;
    public string[] trackSceneNames;
    public Button playButton;

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnPlay);
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
}

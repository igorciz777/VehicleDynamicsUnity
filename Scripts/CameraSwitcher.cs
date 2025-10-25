using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSwitcher : MonoBehaviour
{
    public InputAction cameraSwitchAction;
    public GameObject[] cameras;
    private int currentCameraIndex = 0;
    void Start()
    {
        foreach (GameObject cam in cameras)
        {
            cam.SetActive(false);
        }
        if (cameras.Length > 0)
        {
            cameras[0].SetActive(true);
        }
    }
    private void OnEnable()
    {
        cameraSwitchAction.Enable();
        cameraSwitchAction.performed += OnCameraSwitch;
    }
    private void OnDisable()
    {
        cameraSwitchAction.performed -= OnCameraSwitch;
        cameraSwitchAction.Disable();
    }
    private void OnCameraSwitch(InputAction.CallbackContext context)
    {
        cameras[currentCameraIndex].SetActive(false);
        currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
        cameras[currentCameraIndex].SetActive(true);
    }
}

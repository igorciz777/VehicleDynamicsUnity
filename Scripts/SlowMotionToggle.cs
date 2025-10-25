using UnityEngine;
using UnityEngine.InputSystem;

public class SlowMotionToggle : MonoBehaviour
{
    public InputAction slowMotionAction;
    public float slowMotionTimeScale = 0.2f;
    private bool isSlowMotion = false;
    private readonly float normalTimeScale = 1f;

    private void OnEnable()
    {
        slowMotionAction.Enable();
        slowMotionAction.performed += OnSlowMotionToggle;
    }

    private void OnDisable()
    {
        slowMotionAction.performed -= OnSlowMotionToggle;
        slowMotionAction.Disable();
    }

    private void OnSlowMotionToggle(InputAction.CallbackContext context)
    {
        isSlowMotion = !isSlowMotion;
        Time.timeScale = isSlowMotion ? slowMotionTimeScale : normalTimeScale;
    }
}

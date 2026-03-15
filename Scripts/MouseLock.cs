using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLock : MonoBehaviour
{
    [SerializeField] private InputAction toggleMouseLockAction;

    private bool isCursorLocked = true;

    private void OnEnable()
    {
        toggleMouseLockAction.Enable();
        toggleMouseLockAction.performed += ToggleMouseLock;
    }

    private void OnDisable()
    {
        toggleMouseLockAction.performed -= ToggleMouseLock;
        toggleMouseLockAction.Disable();
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        if (isCursorLocked && Cursor.lockState != CursorLockMode.Confined)
        {
            LockCursor();
        }
    }

    private void ToggleMouseLock(InputAction.CallbackContext context)
    {
        isCursorLocked = !isCursorLocked;
        if (isCursorLocked)
        {
            LockCursor();
        }
        else
        {
            UnlockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

using UnityEngine;

public class CursorManager : MonoBehaviour
{
    void Start()
    {
        UnlockCursor();  // Start unlocked, or change as needed
    }

    void Update()
    {
        // Press Escape to unlock cursor for UI
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        // Press Right Mouse Button (or custom key) to re-lock for gameplay
        if (Input.GetMouseButtonDown(1))
        {
            LockCursor();
        }
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

using UnityEngine;

public class MenuToggle : MonoBehaviour
{
    [Header("Menu Root")]
    [Tooltip("The main menu GameObject to toggle")]
    public GameObject menuRoot;

    [Header("Optional")]
    public bool startHidden = true;

    void Start()
    {
        if (menuRoot == null)
        {
            Debug.LogError("MenuToggle: menuRoot is not assigned!");
            return;
        }

        if (startHidden)
            menuRoot.SetActive(false);
    }

    // 🔘 Call this from Button -> OnClick
    public void ToggleMenu()
    {
        if (menuRoot == null) return;

        bool newState = !menuRoot.activeSelf;
        menuRoot.SetActive(newState);

        Debug.Log($"Menu {(newState ? "Opened" : "Closed")}");
    }

    // (Optional) Explicit controls
    public void OpenMenu()
    {
        if (menuRoot != null)
            menuRoot.SetActive(true);
    }

    public void CloseMenu()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }
}

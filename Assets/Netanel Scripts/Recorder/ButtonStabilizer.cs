using UnityEngine;
using UnityEngine.EventSystems; // חובה בשביל זיהוי מצביע

public class ButtonStabilizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Rigidbody camRb;
    private bool wasKinematic;

    void Start()
    {
        // מחפש את ה-Rigidbody של המצלמה (נמצא באבא או באבא של האבא)
        camRb = GetComponentInParent<Rigidbody>();
    }

    // כשהאצבע/סמן נכנסים לאזור הכפתור
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (camRb != null)
        {
            wasKinematic = camRb.isKinematic; // שומר את המצב המקורי
            camRb.isKinematic = true; // נועל את המצלמה במקום
        }
    }

    // כשהאצבע יוצאת מאזור הכפתור
    public void OnPointerExit(PointerEventData eventData)
    {
        if (camRb != null)
        {
            camRb.isKinematic = wasKinematic; // מחזיר למצב הקודם
        }
    }
}
using System;
using System.Collections;
using UnityEngine;
using TMPro; // Uncomment or remove if you use standard Text instead of TextMeshPro

public class CameraMove : MonoBehaviour
{
    [Serializable]
    public class ActivationTrigger
    {
        public string triggerLabel = "New Trigger Event"; // Helps organize in inspector
        public int triggerAtPointIndex;
        
        [Header("Object Toggling")]
        public GameObject targetObject;
        public enum ActionType { None, Activate, Deactivate }
        public ActionType action = ActionType.None;

        [Header("Movement Pause & UI Text")]
        public float waitTime = 0f;
        [TextArea(3, 5)] public string explanationText;
    }

    [Header("Movement Path")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform[] points;
    [SerializeField] private float speed = 5f;

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI uiTextBox; // Change to 'Text' if not using TMPro

    [Header("Dynamic Event Triggers")]
    [SerializeField] private ActivationTrigger[] customTriggers;

    private int currentPoint = 0;
    private bool isPaused = false;

    void OnEnable()
    {
        isPaused = false;

        if (startPoint != null)
        {
            this.transform.position = startPoint.position;
            this.transform.rotation = startPoint.rotation;
        }

        currentPoint = 0;
        
        // Hide UI box at the very start
        if (uiTextBox != null) uiTextBox.gameObject.SetActive(false);

        // Check for triggers at spawn point
        StartCoroutine(CheckAndExecuteTriggers(0));
    }

    void Update()
    {
        // If the camera is waiting/reading text, stop moving completely
        if (isPaused) return;

        if (currentPoint >= points.Length)
        {
            EndCutscene();
            return;
        }

        // Move execution
        transform.position = Vector3.MoveTowards(
            transform.position,
            points[currentPoint].position,
            speed * Time.deltaTime
        );

        transform.LookAt(points[currentPoint]);

        // Waypoint arrival detection
        if (Vector3.Distance(transform.position, points[currentPoint].position) < 0.2f)
        {
            currentPoint++;

            if (currentPoint < points.Length)
            {
                StartCoroutine(CheckAndExecuteTriggers(currentPoint));
            }
        }
    }

    private IEnumerator CheckAndExecuteTriggers(int index)
    {
        foreach (ActivationTrigger trigger in customTriggers)
        {
            if (trigger.triggerAtPointIndex != index) continue;

            // 1. Handle Game Object Show/Hide
            if (trigger.targetObject != null && trigger.action != ActivationTrigger.ActionType.None)
            {
                bool targetState = (trigger.action == ActivationTrigger.ActionType.Activate);
                trigger.targetObject.SetActive(targetState);
            }

            // 2. Handle Text Presentation & Movement Pauses
            if (trigger.waitTime > 0f || !string.IsNullOrEmpty(trigger.explanationText))
            {
                isPaused = true;

                // Update text box content and turn it on
                if (uiTextBox != null)
                {
                    if (!string.IsNullOrEmpty(trigger.explanationText))
                    {
                        uiTextBox.text = trigger.explanationText;
                        uiTextBox.gameObject.SetActive(true);
                    }
                }

                // Keep camera locked here for the designated time
                yield return new WaitForSeconds(trigger.waitTime);

                // Hide text box when timer expires and resume path
                if (uiTextBox != null)
                {
                    uiTextBox.gameObject.SetActive(false);
                }
                
                isPaused = false;
            }
        }
    }

    private void EndCutscene()
    {
        if (uiTextBox != null) uiTextBox.gameObject.SetActive(false);
        this.gameObject.SetActive(false); 
    }
}
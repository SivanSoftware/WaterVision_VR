using UnityEngine;
using TMPro;

public class MicDebugger : MonoBehaviour
{
    public TMP_Text debugText; // גרור לפה טקסט מהקנבס

    void Start()
    {
        string report = "Microphones found:\n";
        
        if (Microphone.devices.Length == 0)
        {
            report += "NO MICROPHONES DETECTED!";
        }
        else
        {
            foreach (var device in Microphone.devices)
            {
                report += "- " + device + "\n";
            }
        }
        
        Debug.Log(report);
        if (debugText != null) debugText.text = report;
    }
}
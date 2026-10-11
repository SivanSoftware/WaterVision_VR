using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XRMultiplayer
{
    /// <summary>
    /// Floating world-space panel next to a wastewater station.
    /// Reads its title/description from WastewaterPlantManager.stations[stationIndex]
    /// and wires the Activate button to fire the per-station guided tour.
    /// Also acts as the master panel when isMasterPanel = true (Activate All instead).
    /// </summary>
    public class StationInfoPanel : MonoBehaviour
    {
        [Header("Station Linkage")]
        [Tooltip("Index into WastewaterPlantManager.stations. Ignored when isMasterPanel is true.")]
        public int stationIndex;

        [Tooltip("If true, this panel triggers ActivateAll (plant entrance / overview). Otherwise it triggers a single station.")]
        public bool isMasterPanel;

        [Header("UI References")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button activateButton;
        [SerializeField] private Button stopButton;

        [Header("Master Panel Content (used when isMasterPanel)")]
        [TextArea(2, 6)]
        [SerializeField] private string masterTitle = "Beit Shean WWTP";
        [TextArea(3, 10)]
        [SerializeField] private string masterDescription =
            "Wastewater treatment plant. Design capacity 18,000 m³/day. " +
            "A five-stage process producing irrigation-grade reclaimed water and compost-quality biosolids. " +
            "Press <b>Activate All</b> for a full guided tour, or walk to a stage and press its panel for a focused walkthrough.";

        void Start()
        {
            ApplyContent();

            if (activateButton != null)
            {
                activateButton.onClick.RemoveAllListeners();
                activateButton.onClick.AddListener(OnActivateClicked);
            }
            if (stopButton != null)
            {
                stopButton.onClick.RemoveAllListeners();
                stopButton.onClick.AddListener(OnStopClicked);
            }
        }

        void ApplyContent()
        {
            if (isMasterPanel)
            {
                if (titleText != null) titleText.text = masterTitle;
                if (descriptionText != null) descriptionText.text = masterDescription;
                return;
            }

            var mgr = WastewaterPlantManager.Instance;
            if (mgr == null || stationIndex < 0 || stationIndex >= mgr.stations.Count)
            {
                if (titleText != null) titleText.text = $"Station {stationIndex}";
                if (descriptionText != null) descriptionText.text = "(WastewaterPlantManager not ready)";
                return;
            }

            var s = mgr.stations[stationIndex];
            if (titleText != null) titleText.text = !string.IsNullOrEmpty(s.nameEnglish) ? s.nameEnglish : s.nameHebrew;
            if (descriptionText != null) descriptionText.text = s.description;
        }

        public void OnActivateClicked()
        {
            var mgr = WastewaterPlantManager.Instance;
            if (mgr == null) return;
            if (isMasterPanel) mgr.ActivateAll();
            else mgr.ActivateStation(stationIndex);
        }

        public void OnStopClicked()
        {
            WastewaterPlantManager.Instance?.StopAll();
        }
    }
}

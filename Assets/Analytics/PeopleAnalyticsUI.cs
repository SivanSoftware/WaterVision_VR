using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PeopleAnalyticsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text listText;
    [SerializeField] private UIPieChart pie;

    private readonly List<PlayerAnalyticsState> players = new List<PlayerAnalyticsState>();

    void OnEnable()
    {
        RefreshPlayers();
        Refresh();
    }

    void Update()
    {
        RefreshPlayers();
        Refresh();
    }

    void RefreshPlayers()
    {
        players.Clear();

        var found = FindObjectsOfType<PlayerAnalyticsState>(true);
        if (found != null)
            players.AddRange(found);

        players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
    }

    void Refresh()
    {
        float totalTalk = 0f;
        for (int i = 0; i < players.Count; i++)
            totalTalk += Mathf.Max(0f, players[i].TalkSeconds.Value);

        if (listText != null)
        {
            if (players.Count == 0)
            {
                listText.text = "No data yet...";
            }
            else
            {
                var sb = new StringBuilder();

                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    float t = Mathf.Max(0f, p.TalkSeconds.Value);
                    int turns = Mathf.Max(0, p.Turns.Value);

                    float pct = totalTalk > 0f ? (t / totalTalk) * 100f : 0f;

                    Color c = UIPieChart.ColorForIndex(i);
                    string hex = ColorUtility.ToHtmlStringRGB(c);

                    sb.Append("<color=#");
                    sb.Append(hex);
                    sb.Append(">■</color> ");

                    sb.Append(p.DisplayName.Value.ToString());
                    sb.Append("  ");
                    sb.Append(pct.ToString("0"));
                    sb.Append("%  ");
                    sb.Append(t.ToString("0.0"));
                    sb.Append("s  turns ");
                    sb.Append(turns);
                    sb.AppendLine();
                }

                listText.text = sb.ToString();
            }
        }

        if (pie != null)
        {
            var vals = new List<float>();

            if (totalTalk > 0f)
            {
                for (int i = 0; i < players.Count; i++)
                    vals.Add(Mathf.Max(0f, players[i].TalkSeconds.Value));
            }
            else
            {
                for (int i = 0; i < players.Count; i++)
                    vals.Add(Mathf.Max(0.1f, players[i].Turns.Value));
            }

            pie.SetValues(vals);
        }
    }
}

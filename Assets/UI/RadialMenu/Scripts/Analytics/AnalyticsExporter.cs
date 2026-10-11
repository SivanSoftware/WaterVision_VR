// AnalyticsExporter.cs
// Saves a MeetingAnalytics object to disk in two formats:
//   1) JSON  — machine-readable, complete data (for archive / re-import / pipelines)
//   2) Markdown  — human-readable summary (for sharing / review)
// Both files go to Application.persistentDataPath, accessible on Quest via USB/MTP.
// Place under: Assets/UI/RadialMenu/Scripts/Analytics/

using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class AnalyticsExporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Save both a JSON file and a Markdown file. Returns the folder where they were saved.
    /// </summary>
    public static ExportResult SaveBoth(MeetingAnalytics data)
    {
        if (data == null)
            return new ExportResult { Success = false, ErrorMessage = "No data to export." };

        try
        {
            string baseName = $"meeting_analytics_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            string jsonName = baseName + ".json";
            string mdName   = baseName + ".md";

            // Route both files through DownloadsFolderWriter so they land in the user's
            // public Downloads folder (Quest: Internal Shared Storage › Download › WaterVisionVR;
            // Windows: %USERPROFILE%\Downloads\WaterVisionVR). No permission popups required.
            string jsonPath = DownloadsFolderWriter.WriteText(jsonName, BuildJson(data), "application/json");
            string mdPath   = DownloadsFolderWriter.WriteText(mdName,   BuildMarkdown(data), "text/markdown");

            if (jsonPath == null || mdPath == null)
                return new ExportResult { Success = false, ErrorMessage = "Failed to write export files." };

            return new ExportResult
            {
                Success = true,
                Folder = DownloadsFolderWriter.GetVisibleFolderHint(),
                JsonPath = jsonPath,
                MarkdownPath = mdPath,
            };
        }
        catch (System.Exception e)
        {
            return new ExportResult { Success = false, ErrorMessage = e.Message };
        }
    }

    // ------------------------- JSON -------------------------

    private static string BuildJson(MeetingAnalytics d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendFormat(Inv, "  \"roomName\": \"{0}\",\n", Esc(d.RoomName));
        sb.AppendFormat(Inv, "  \"date\": \"{0:yyyy-MM-dd}\",\n", d.Date);
        sb.AppendFormat(Inv, "  \"durationSeconds\": {0},\n", d.DurationSeconds);
        sb.AppendFormat(Inv, "  \"durationFormatted\": \"{0}\",\n", d.DurationFormatted);
        sb.AppendFormat(Inv, "  \"participantCount\": {0},\n", d.ParticipantCount);
        sb.AppendFormat(Inv, "  \"totalSpeechSeconds\": {0},\n", d.TotalSpeechSeconds);
        sb.AppendFormat(Inv, "  \"totalSpeechFormatted\": \"{0}\",\n", d.TotalSpeechFormatted);
        sb.AppendFormat(Inv, "  \"averageEngagement\": {0},\n", d.AverageEngagement);
        sb.AppendFormat(Inv, "  \"dropsDetected\": {0},\n", d.DropsDetected);

        // Participants
        sb.Append("  \"participants\": [");
        if (d.Participants != null)
        {
            for (int i = 0; i < d.Participants.Count; i++)
            {
                var p = d.Participants[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    {\n");
                sb.AppendFormat(Inv, "      \"id\": \"{0}\",\n", Esc(p.Id));
                sb.AppendFormat(Inv, "      \"displayName\": \"{0}\",\n", Esc(p.DisplayName));
                sb.AppendFormat(Inv, "      \"initials\": \"{0}\",\n", Esc(p.Initials));
                sb.AppendFormat(Inv, "      \"speakTimeSeconds\": {0},\n", p.SpeakTimeSeconds);
                sb.AppendFormat(Inv, "      \"speakTimeFormatted\": \"{0}\",\n", p.SpeakTimeFormatted);
                sb.AppendFormat(Inv, "      \"speakPercent\": {0},\n", p.SpeakPercent);
                sb.AppendFormat(Inv, "      \"engagement\": \"{0}\",\n", p.Engagement);
                sb.AppendFormat(Inv, "      \"speakerTurns\": {0},\n", p.SpeakerTurns);
                sb.AppendFormat(Inv, "      \"interruptedCount\": {0}\n", p.InterruptedCount);
                sb.Append("    }");
            }
        }
        sb.Append("\n  ],\n");

        // Topics
        sb.Append("  \"topics\": [");
        if (d.Topics != null)
        {
            for (int i = 0; i < d.Topics.Count; i++)
            {
                var t = d.Topics[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    {\n");
                sb.AppendFormat(Inv, "      \"name\": \"{0}\",\n", Esc(t.Name));
                sb.AppendFormat(Inv, "      \"percent\": {0},\n", t.Percent);
                sb.AppendFormat(Inv, "      \"durationSeconds\": {0},\n", t.DurationSeconds);
                sb.AppendFormat(Inv, "      \"durationFormatted\": \"{0}\"\n", t.DurationFormatted);
                sb.Append("    }");
            }
        }
        sb.Append("\n  ],\n");

        // Engagement timeline
        sb.Append("  \"engagementTimeline\": [");
        if (d.EngagementTimeline != null)
        {
            for (int i = 0; i < d.EngagementTimeline.Count; i++)
            {
                var s = d.EngagementTimeline[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.AppendFormat(Inv, "    {{ \"timeSeconds\": {0}, \"engagementPercent\": {1} }}",
                    s.TimeSeconds, s.EngagementPercent);
            }
        }
        sb.Append("\n  ],\n");

        // Drops
        sb.Append("  \"drops\": [");
        if (d.Drops != null)
        {
            for (int i = 0; i < d.Drops.Count; i++)
            {
                var dz = d.Drops[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.AppendFormat(Inv, "    {{ \"startSeconds\": {0}, \"endSeconds\": {1}, \"durationSeconds\": {2} }}",
                    dz.StartSeconds, dz.EndSeconds, dz.DurationSeconds);
            }
        }
        sb.Append("\n  ],\n");

        // Insights
        sb.Append("  \"insights\": [");
        if (d.Insights != null)
        {
            for (int i = 0; i < d.Insights.Count; i++)
            {
                var ins = d.Insights[i];
                sb.Append(i == 0 ? "\n" : ",\n");
                sb.Append("    {\n");
                sb.AppendFormat(Inv, "      \"type\": \"{0}\",\n", ins.Type);
                sb.AppendFormat(Inv, "      \"headline\": \"{0}\",\n", Esc(ins.Headline));
                sb.AppendFormat(Inv, "      \"detail\": \"{0}\"\n", Esc(ins.Detail));
                sb.Append("    }");
            }
        }
        sb.Append("\n  ]\n");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    // ------------------------- Markdown -------------------------

    private static string BuildMarkdown(MeetingAnalytics d)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {d.RoomName}");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {d.Date:MMM d, yyyy}  ");
        sb.AppendLine($"**Duration:** {d.DurationFormatted}  ");
        sb.AppendLine($"**Participants:** {d.ParticipantCount}");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Speech:** {d.TotalSpeechFormatted}");
        sb.AppendLine($"- **Average Engagement:** {Mathf.RoundToInt(d.AverageEngagement)}%");
        sb.AppendLine($"- **Drops Detected:** {d.DropsDetected}");
        sb.AppendLine();

        // Participants table
        if (d.Participants != null && d.Participants.Count > 0)
        {
            sb.AppendLine("## Participants");
            sb.AppendLine();
            sb.AppendLine("| Participant | Time | % | Engagement | Turns | Interrupted |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var p in d.Participants)
            {
                sb.AppendLine($"| [{p.Initials}] {p.DisplayName} | {p.SpeakTimeFormatted} | {p.SpeakPercent.ToString("0.#", Inv)}% | {p.Engagement} | {p.SpeakerTurns} | {p.InterruptedCount} |");
            }
            sb.AppendLine();
        }

        // Topics table
        if (d.Topics != null && d.Topics.Count > 0)
        {
            sb.AppendLine("## Discussion Topics");
            sb.AppendLine();
            sb.AppendLine("| Topic | % | Duration |");
            sb.AppendLine("|---|---|---|");
            foreach (var t in d.Topics)
            {
                sb.AppendLine($"| {t.Name} | {t.Percent.ToString("0.#", Inv)}% | {t.DurationFormatted} |");
            }
            sb.AppendLine();
        }

        // Drops
        if (d.Drops != null && d.Drops.Count > 0)
        {
            sb.AppendLine("## Engagement Drops");
            sb.AppendLine();
            for (int i = 0; i < d.Drops.Count; i++)
            {
                var dz = d.Drops[i];
                string startStr = MeetingAnalytics.FormatTime(dz.StartSeconds);
                string endStr = MeetingAnalytics.FormatTime(dz.EndSeconds);
                string durationStr = MeetingAnalytics.FormatTime(dz.DurationSeconds);
                sb.AppendLine($"- **Drop {i + 1}:** {startStr} → {endStr} (duration: {durationStr})");
            }
            sb.AppendLine();
        }

        // Insights
        if (d.Insights != null && d.Insights.Count > 0)
        {
            sb.AppendLine("## Detected Insights");
            sb.AppendLine();
            sb.AppendLine("_Auto-generated from speech patterns._");
            sb.AppendLine();
            for (int i = 0; i < d.Insights.Count; i++)
            {
                var ins = d.Insights[i];
                sb.AppendLine($"### {ins.Headline}");
                sb.AppendLine($"_Type: {ins.Type}_");
                sb.AppendLine();
                sb.AppendLine(ins.Detail);
                sb.AppendLine();
                if (i < d.Insights.Count - 1) sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        sb.AppendLine();
        sb.AppendLine($"_Exported {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}_");

        return sb.ToString();
    }

    public class ExportResult
    {
        public bool Success;
        public string Folder;
        public string JsonPath;
        public string MarkdownPath;
        public string ErrorMessage;
    }
}
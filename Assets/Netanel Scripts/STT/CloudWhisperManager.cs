using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;
using System.IO;

[Serializable]
public class GroqResponse
{
    public string text;
}

public class CloudWhisperManager : MonoBehaviour
{
    [Header("API Settings")]
    // PASTE YOUR GROQ KEY HERE INSIDE THE QUOTES
    public string apiKey = "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"; 
    
    public string model = "whisper-large-v3"; // Free and accurate
    public string language = "en"; 

    // Event to notify the Handler
    public Action<string> OnTranscriptionComplete;

    // Groq URL (Different from OpenAI)
    private const string ApiUrl = "https://api.groq.com/openai/v1/audio/transcriptions";

    public void SendAudioToCloud(AudioClip clip)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("HERE"))
        {
            Debug.LogError("[GroqWhisper] ❌ API Key is missing! Go to CloudWhisperManager.cs and paste your key.");
            return;
        }

        StartCoroutine(UploadAudio(clip));
    }

    IEnumerator UploadAudio(AudioClip clip)
    {
        Debug.Log("[GroqWhisper] Converting Audio to WAV...");
        byte[] wavData = ConvertToWav(clip);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "recording.wav", "audio/wav");
        form.AddField("model", model);
        form.AddField("language", language);
        form.AddField("response_format", "json"); 

        using (UnityWebRequest request = UnityWebRequest.Post(ApiUrl, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            Debug.Log("[GroqWhisper] 🚀 Sending to Groq Cloud...");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GroqWhisper] API Error: {request.error}\nResponse: {request.downloadHandler.text}");
            }
            else
            {
                string jsonResult = request.downloadHandler.text;
                GroqResponse response = JsonUtility.FromJson<GroqResponse>(jsonResult);
                
                if (response != null && !string.IsNullOrEmpty(response.text))
                {
                    Debug.Log($"[GroqWhisper] ✅ Success: {response.text}");
                    OnTranscriptionComplete?.Invoke(response.text);
                }
            }
        }
    }

    // --- WAV CONVERSION HELPERS ---
    private byte[] ConvertToWav(AudioClip clip)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                var data = new float[clip.samples];
                clip.GetData(data, 0);

                int sampleRate = clip.frequency;
                int channels = clip.channels;
                short bitsPerSample = 16;
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                int blockAlign = channels * (bitsPerSample / 8);

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + data.Length * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(data.Length * 2);

                foreach (var sample in data)
                {
                    writer.Write((short)(sample * short.MaxValue));
                }
            }
            return stream.ToArray();
        }
    }
}
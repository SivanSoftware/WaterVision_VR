using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using System.Text;
using Unity.Collections;
using Newtonsoft.Json;
using System; 

public class RunWhisper : MonoBehaviour
{
    public Action<string> OnTranscriptionComplete;

    public AudioClip audioClip;
    public ModelAsset audioDecoder1, audioDecoder2;
    public ModelAsset audioEncoder;
    public ModelAsset logMelSpectro;
    public TextAsset vocabAsset;

    Worker decoder1, decoder2, encoder, spectrogram;
    Worker argmax;

    const int maxTokens = 100;
    const int END_OF_TEXT = 50257;
    const int START_OF_TRANSCRIPT = 50258;
    const int ENGLISH = 50259;
    const int TRANSCRIBE = 50359;
    const int NO_TIME_STAMPS = 50363;
    const int maxSamples = 30 * 16000; 

    int numSamples;
    string[] tokens;
    int tokenCount = 0;
    
    NativeArray<int> outputTokens;
    NativeArray<int> lastToken;
    int[] whiteSpaceCharacters = new int[256];
    
    Tensor<float> encodedAudio;
    Tensor<int> lastTokenTensor;
    Tensor<float> audioInput;

    bool transcribe = false;
    string outputString = "";
    Awaitable m_Awaitable;

    public async void Start()
    {
        Debug.Log("[RunWhisper] Initializing Whisper models..."); // DEBUG
        SetupWhiteSpaceShifts();
        GetTokens();

        try 
        {
            decoder1 = new Worker(ModelLoader.Load(audioDecoder1), BackendType.GPUCompute);
            decoder2 = new Worker(ModelLoader.Load(audioDecoder2), BackendType.GPUCompute);
            encoder = new Worker(ModelLoader.Load(audioEncoder), BackendType.GPUCompute);
            spectrogram = new Worker(ModelLoader.Load(logMelSpectro), BackendType.GPUCompute);
            Debug.Log("[RunWhisper] Standard workers loaded."); // DEBUG
        }
        catch(Exception e)
        {
            Debug.LogError($"[RunWhisper] Error loading models: {e.Message}"); // DEBUG
        }

        FunctionalGraph graph = new FunctionalGraph();
        var input = graph.AddInput(DataType.Float, new DynamicTensorShape(1, 1, 51865));
        var amax = Functional.ArgMax(input, -1, false);
        var selectTokenModel = graph.Compile(amax);
        
        argmax = new Worker(selectTokenModel, BackendType.GPUCompute);

        outputTokens = new NativeArray<int>(maxTokens, Allocator.Persistent);
        lastToken = new NativeArray<int>(1, Allocator.Persistent); 
        
        lastTokenTensor = new Tensor<int>(new TensorShape(1, 1));
        
        if (audioClip != null) 
        {
            Debug.Log("[RunWhisper] AudioClip found on Start, beginning transcription..."); // DEBUG
            StartTranscription(audioClip);
        }

        while (true)
        {
            if (!transcribe || tokenCount >= (outputTokens.Length - 1))
            {
                await Awaitable.NextFrameAsync(); 
                continue;
            }
            
            m_Awaitable = InferenceStep();
            await m_Awaitable;
        }
    }

    public void StartTranscription(AudioClip clip)
    {
        if (clip == null) 
        {
            Debug.LogWarning("[RunWhisper] StartTranscription called with NULL clip!"); // DEBUG
            return;
        }

        Debug.Log($"[RunWhisper] Starting Transcription. Clip Length: {clip.length}s, Samples: {clip.samples}"); // DEBUG

        audioClip = clip;
        outputString = "";
        
        outputTokens[0] = START_OF_TRANSCRIPT;
        outputTokens[1] = ENGLISH;
        outputTokens[2] = TRANSCRIBE;
        tokenCount = 3;
        
        lastToken[0] = NO_TIME_STAMPS;

        LoadAudio();
        EncodeAudio();
        
        lastTokenTensor.dataOnBackend.Upload<int>(lastToken, 1);

        transcribe = true;
    }

   void LoadAudio()
    {
        if (audioClip == null) return;

        numSamples = audioClip.samples;
        
        var paddedData = new float[maxSamples]; 
        var clipData = new float[audioClip.samples];
        
        audioClip.GetData(clipData, 0);
        Array.Copy(clipData, paddedData, Mathf.Min(clipData.Length, maxSamples));
        
        audioInput?.Dispose();
        audioInput = new Tensor<float>(new TensorShape(1, maxSamples), paddedData);
        Debug.Log("[RunWhisper] Audio loaded and padded into Tensor."); // DEBUG
    }

    void EncodeAudio()
    {
        Debug.Log("[RunWhisper] Encoding audio (Spectrogram -> Encoder)..."); // DEBUG
        spectrogram.Schedule(audioInput);
        var logmel = spectrogram.PeekOutput() as Tensor<float>;
        
        encoder.Schedule(logmel);
        
        encodedAudio?.Dispose();
        encodedAudio = encoder.PeekOutput() as Tensor<float>;
        Debug.Log("[RunWhisper] Audio encoding complete."); // DEBUG
    }

    async Awaitable InferenceStep()
    {
        using var currentTokensTensor = new Tensor<int>(
            new TensorShape(1, tokenCount), 
            outputTokens.GetSubArray(0, tokenCount)
        );

        decoder1.SetInput("input_ids", currentTokensTensor);
        decoder1.SetInput("encoder_hidden_states", encodedAudio);
        decoder1.Schedule();

        await Awaitable.NextFrameAsync(); 

        var past_key_values_0_decoder_key = decoder1.PeekOutput("present.0.decoder.key") as Tensor<float>;
        var past_key_values_0_decoder_value = decoder1.PeekOutput("present.0.decoder.value") as Tensor<float>;
        var past_key_values_1_decoder_key = decoder1.PeekOutput("present.1.decoder.key") as Tensor<float>;
        var past_key_values_1_decoder_value = decoder1.PeekOutput("present.1.decoder.value") as Tensor<float>;
        var past_key_values_2_decoder_key = decoder1.PeekOutput("present.2.decoder.key") as Tensor<float>;
        var past_key_values_2_decoder_value = decoder1.PeekOutput("present.2.decoder.value") as Tensor<float>;
        var past_key_values_3_decoder_key = decoder1.PeekOutput("present.3.decoder.key") as Tensor<float>;
        var past_key_values_3_decoder_value = decoder1.PeekOutput("present.3.decoder.value") as Tensor<float>;

        var past_key_values_0_encoder_key = decoder1.PeekOutput("present.0.encoder.key") as Tensor<float>;
        var past_key_values_0_encoder_value = decoder1.PeekOutput("present.0.encoder.value") as Tensor<float>;
        var past_key_values_1_encoder_key = decoder1.PeekOutput("present.1.encoder.key") as Tensor<float>;
        var past_key_values_1_encoder_value = decoder1.PeekOutput("present.1.encoder.value") as Tensor<float>;
        var past_key_values_2_encoder_key = decoder1.PeekOutput("present.2.encoder.key") as Tensor<float>;
        var past_key_values_2_encoder_value = decoder1.PeekOutput("present.2.encoder.value") as Tensor<float>;
        var past_key_values_3_encoder_key = decoder1.PeekOutput("present.3.encoder.key") as Tensor<float>;
        var past_key_values_3_encoder_value = decoder1.PeekOutput("present.3.encoder.value") as Tensor<float>;

        decoder2.SetInput("input_ids", lastTokenTensor);
        
        decoder2.SetInput("past_key_values.0.decoder.key", past_key_values_0_decoder_key);
        decoder2.SetInput("past_key_values.0.decoder.value", past_key_values_0_decoder_value);
        decoder2.SetInput("past_key_values.1.decoder.key", past_key_values_1_decoder_key);
        decoder2.SetInput("past_key_values.1.decoder.value", past_key_values_1_decoder_value);
        decoder2.SetInput("past_key_values.2.decoder.key", past_key_values_2_decoder_key);
        decoder2.SetInput("past_key_values.2.decoder.value", past_key_values_2_decoder_value);
        decoder2.SetInput("past_key_values.3.decoder.key", past_key_values_3_decoder_key);
        decoder2.SetInput("past_key_values.3.decoder.value", past_key_values_3_decoder_value);

        decoder2.SetInput("past_key_values.0.encoder.key", past_key_values_0_encoder_key);
        decoder2.SetInput("past_key_values.0.encoder.value", past_key_values_0_encoder_value);
        decoder2.SetInput("past_key_values.1.encoder.key", past_key_values_1_encoder_key);
        decoder2.SetInput("past_key_values.1.encoder.value", past_key_values_1_encoder_value);
        decoder2.SetInput("past_key_values.2.encoder.key", past_key_values_2_encoder_key);
        decoder2.SetInput("past_key_values.2.encoder.value", past_key_values_2_encoder_value);
        decoder2.SetInput("past_key_values.3.encoder.key", past_key_values_3_encoder_key);
        decoder2.SetInput("past_key_values.3.encoder.value", past_key_values_3_encoder_value);

        decoder2.Schedule();

        var logits = decoder2.PeekOutput("logits") as Tensor<float>;
        argmax.Schedule(logits);
        
        using var t_Token = await argmax.PeekOutput().ReadbackAndCloneAsync() as Tensor<int>;
        int index = t_Token[0];

        outputTokens[tokenCount] = lastToken[0];
        lastToken[0] = index;
        tokenCount++;
        
        lastTokenTensor.dataOnBackend.Upload<int>(lastToken, 1);

        if (index == END_OF_TEXT)
        {
            transcribe = false;
            Debug.Log($"[RunWhisper] Transcription Complete. Result: '{outputString}'"); // DEBUG
            OnTranscriptionComplete?.Invoke(outputString);
        }
        else if (index < tokens.Length)
        {
            outputString += GetUnicodeText(tokens[index]);
        }
    }

    void GetTokens()
    {
        var vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabAsset.text);
        tokens = new string[vocab.Count];
        foreach (var item in vocab)
        {
            tokens[item.Value] = item.Key;
        }
    }

    string GetUnicodeText(string text)
    {
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
        return Encoding.UTF8.GetString(bytes);
    }

    string ShiftCharacterDown(string text)
    {
        string outText = "";
        foreach (char letter in text)
        {
            outText += ((int)letter <= 256) ? letter : (char)whiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }

    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) whiteSpaceCharacters[n++] = i;
        }
    }

    bool IsWhiteSpace(char c)
    {
        return !(('!' <= c && c <= '~') || ('¡' <= c && c <= '¬') || ('®' <= c && c <= 'ÿ'));
    }

    private void OnDestroy()
    {
        Debug.Log("[RunWhisper] Destroying... disposing workers."); // DEBUG
        decoder1?.Dispose();
        decoder2?.Dispose();
        encoder?.Dispose();
        spectrogram?.Dispose();
        argmax?.Dispose();
        
        audioInput?.Dispose();
        encodedAudio?.Dispose();
        lastTokenTensor?.Dispose();
        
        if (outputTokens.IsCreated) outputTokens.Dispose();
        if (lastToken.IsCreated) lastToken.Dispose();
    }
}
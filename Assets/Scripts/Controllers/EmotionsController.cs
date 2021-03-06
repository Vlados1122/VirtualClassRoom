using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;
using System;

public class EmotionsController : MonoBehaviour
{

    [SerializeField] VHPEmotions emotionsManager;
    [SerializeField] MoralSchema moralSchema;

    static Dictionary<string, Emotion> emotions = new Dictionary<string, Emotion>();

    private string JSON_PATH_ALL_EMOTIONS = Application.streamingAssetsPath + "\\Emotions.json";

    public class Emotion
    {
        public double[] VAD;

        Emotion()
        {
            VAD = new double[3];
        }

        public void setVAD(double[] VAD)
        {
            VAD.CopyTo(this.VAD, 0);
        }

        public double[] getBodyFactorForTarget()
        {
            return VAD;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            var emotion = getEmotion();
            Debug.Log($"emotion - {emotion}");
            setEmotion(emotion);
        }
    }

    void Start()
    {
        setupEmotions();
    }

    private void setupEmotions()
    {
        emotions = JsonConvert.DeserializeObject<Dictionary<string, Emotion>>(File.ReadAllText(JSON_PATH_ALL_EMOTIONS));
    }

    public string getEmotion()
    {
        var feelings = moralSchema.getStudentFeelings();
        string answer = "";
        double min = 1000;
        foreach (var emotion in emotions)
        {
            double difference = 0;

            for (int i = 0; i < feelings.Length; i++)
            {
                difference += Math.Pow(feelings[i] - emotion.Value.VAD[i], 2);
                if (difference < min)
                {
                    min = difference;
                    answer = emotion.Key;
                }
            }
        }
        return answer;
    }

    public void setEmotion(string emotion, float emotionExtent = 50f)
    {
        Debug.Log($"set emotion - {emotion}");
        switch (emotion)
        {
            case "Anger":
                emotionsManager.anger = emotionExtent;
                break;
            case "Disgust":
                emotionsManager.disgust = emotionExtent;
                break;
            case "Fear":
                emotionsManager.fear = emotionExtent;
                break;
            case "Happiness":
                emotionsManager.happiness = emotionExtent;
                break;
            case "Sadness":
                emotionsManager.sadness = emotionExtent;
                break;
            case "Surprise":
                emotionsManager.surprise = emotionExtent;
                break;
            case "Neutral":
                resetEmotions();
                break;
            default:
                break;
        }
    }

    public void resetEmotions()
    {
        emotionsManager.anger = 0;
        emotionsManager.disgust = 0;
        emotionsManager.fear = 0;
        emotionsManager.happiness = 0;
        emotionsManager.sadness = 0;
        emotionsManager.surprise = 0;
    }
}

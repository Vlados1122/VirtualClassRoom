using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
using System.Linq;
using Mono.Data.Sqlite;
using UnityEngine.UI;


public class GameController : MonoBehaviour {

    [Tooltip("������������ ��� ��������� ������������� ������� ������� ������������ �������������," +
        " � ����� ���������� ������ � ������� ������ ��� �������� �� ��������")]
    [SerializeField] private bool ActivateEmotionalBehaviour = false;

    [SerializeField] private UIController uiController;
    [SerializeField] private AudioController audioController;
    [SerializeField] private GameObject proyectorScreen;
    [SerializeField] private ScrollViewAdapter scrollViewAdapter;
    [SerializeField] private Animator animator;
    [SerializeField] private MouseLook mouseLook;
    [SerializeField] private MoralSchema moralSchema;
    [SerializeField] private EmotionsController emotionsController;
    [SerializeField] private SpeechRecognizerController speechRecognizerController;
    [SerializeField] private List<ChangeFieldOfView> changeFOVs;

    [SerializeField] private FPSInput playerFpsInput;

    [SerializeField] private TestingModule testingModule;

    [SerializeField] private GameObject sendQuestionButton;
    [SerializeField] private InputField sendQuestionTextInputFIeld;

    
    private static Boolean isEbicaEstimatesAlreadyLoaded = false;


    private PipeServer pipeServer;

    private Action<byte[]> YSKspeechSynthesizedHandler;

    string pathToAsksForQuestions = "/Resources/Music/GeneralSounds/AskForQuestions";

    private float lectureInterruptTime = 0.0f;

    private Coroutine playLectureCoroutine;
    private Coroutine startLectureCoroutine;

    bool isLectureInProgress = false;
    bool isStudentAskQuestion = false;
    bool isTeacherGivingLectureRightNow = false;

    float timeWhileTalking = 0.0f;


    public static System.Diagnostics.Process pipeClientProcess;

    public static int CurrentLessonNumber { get; private set; } = 1;
    public static int CurrentSlideNumber { get; private set; } = 1;


    private void Awake() {
        Messenger.AddListener(PlayerEvent.SIT, StartGameProcess);
        Messenger.AddListener(GameEvent.LECTURE_PART_FINISHED, OnLectureFinished);
        Messenger<int>.AddListener(GameEvent.STUDENT_ASK_QUESTION, OnStudentAskQuestion);
        Messenger<int>.AddListener(GameEvent.STUDENT_FINISHED_TESTING_MODULE, OnStudentFinishedTestingModule);
        Messenger.AddListener(GameEvent.STUDENT_PRESSED_TESTING_BUTTON, OnTestingButtonPressed);
        Messenger<AudioClip>.AddListener(GameEvent.STUDENT_AUDIO_RECORD_FINISHED, OnStudentAudioRecordFinished);

        YSKspeechSynthesizedHandler = (speechBytes) => {
            var audioClip = AudioConverter.Convert(speechBytes);
            setClipToAudioControllerAndPlay(audioClip);

            if (isLectureInProgress == true) {
                StopCoroutine("askStudentForQuestionDuringLecture");
                StartCoroutine(waitChatGPTAnswerForQuestion());
            }
        };
        YandexSpeechKit.onSpeechSynthesized += YSKspeechSynthesizedHandler;
    }

    private async void OnStudentAudioRecordFinished(AudioClip clip) {
        byte[] bytes = AudioConverter.ConvertClipToOGG(clip);
        string recognizedText = await YandexSpeechKit.SpeechToText(bytes);
        Debug.LogError("recognized text from gamecontroller: " + recognizedText);
        if (recognizedText != null && recognizedText.Length > 0) {
            Debug.LogError("length = " + recognizedText.Length + ", recognized text = " + recognizedText);
            sendQuestionActions(recognizedText);
        }
    }

    private IEnumerator waitChatGPTAnswerForQuestion() {

        yield return new WaitForSeconds(0.5f);
        uiController.HideQuestionButton();
        uiController.HideChatWithChatGPTButton();
        yield return new WaitForSeconds(audioController.getCurrentClipLength() + 1.5f);
        setClipToAudioControllerAndPlay($"Music/GeneralSounds/LetsContinue/lets_continue");
        yield return new WaitForSeconds(audioController.getCurrentClipLength() + 1.5f);
        playLectureCoroutine = StartCoroutine(PlayLectureFromCurrentSlideCoroutine());
        isStudentAskQuestion = false;
    }

    private void OnDestroy() {
        Messenger.RemoveListener(PlayerEvent.SIT, StartGameProcess);
        Messenger.RemoveListener(GameEvent.LECTURE_PART_FINISHED, OnLectureFinished);
        Messenger<int>.RemoveListener(GameEvent.STUDENT_ASK_QUESTION, OnStudentAskQuestion);
        Messenger<int>.RemoveListener(GameEvent.STUDENT_FINISHED_TESTING_MODULE, OnStudentFinishedTestingModule);
        Messenger.RemoveListener(GameEvent.STUDENT_PRESSED_TESTING_BUTTON, OnTestingButtonPressed);

        if (YSKspeechSynthesizedHandler != null) YandexSpeechKit.onSpeechSynthesized -= YSKspeechSynthesizedHandler;


    }

    void Start() {

        CurrentLessonNumber = MainMenuController.CurrentLessonNumber;

        //if (CurrentLessonNumber == 2) CurrentLessonNumber = 3;

        PlayerState.setPlayerState(PlayerStateEnum.WALK);

        changeFOVs.ForEach(it => it.enabled = true);

        // ��������� pipe server ������ ���� �� ��� �� ��� �������!
        if (PipeServer.Instance == null) {
            pipeServer = new PipeServer();
            pipeServer.onPipeCommandReceived += (pipeServer_, pipeCommand) => {
                Debug.LogError("��������� ��������� �� �������.\n���������: " + pipeCommand.command);

                double studentValence = moralSchema.getStudentFeelings()[0];

                YSKEmotion voiceEmotion = YSKEmotion.NEUTRAL;


                if (studentValence > 0.2) {
                    voiceEmotion = YSKEmotion.GOOD;
                }

                Debug.LogError("emotion = " + voiceEmotion);

                YandexSpeechKit.TextToSpeech(pipeCommand.command, YSKVoice.ERMIL, voiceEmotion);
            };
            pipeServer.Start();

            // ��������� pipe-������
            var pipeClientExePath = Application.dataPath.Replace("/Assets", "") + "/PipeClient/NamedPipeClient/NamedPipeClient/bin/Debug/net5.0/NamedPipeClient.exe";
            pipeClientProcess = new System.Diagnostics.Process();
            pipeClientProcess.StartInfo.FileName = pipeClientExePath;
            pipeClientProcess.Start();

        } else {
            pipeServer = PipeServer.Instance;
            pipeServer.CreateNewGameObjectPipeListener();
        }

        sendQuestionButton.GetComponent<Button>().onClick.AddListener(delegate {
            sendQuestionActions(sendQuestionTextInputFIeld.text);
        });

        if (MainMenuController.IsUserAuthorized) {
            Debug.LogError($"Username - {MainMenuController.Username}");

            int? userId = UserProgressUtils.getUserId(MainMenuController.Username);

            if (userId.HasValue && isEbicaEstimatesAlreadyLoaded == false) {

                using (var connection = new SqliteConnection(DBInfo.DataBaseName)) {
                    try {
                        connection.Open();

                        using (var command = connection.CreateCommand()) {

                            var query = $@"
                                SELECT 
	                                usinfo.appraisal_valence,
                                    usinfo.appraisal_initiative,
                                    usinfo.appraisal_learnability,
                                    usinfo.feeling_valence,
                                    usinfo.feeling_initiative,
                                    usinfo.feeling_learnability,
                                    usinfo.�haracteristic
                                FROM
	                                ebica_user_info as usinfo
                                WHERE 
	                                usinfo.user = {userId.Value}
                            ";
                            Debug.LogError("query = " + query);
                            command.CommandText = query;
                            using (var reader = command.ExecuteReader()) {
                                if (reader.HasRows) {

                                    reader.Read();

                                    double[] studentAppraisals = new double[3] {
                                        Convert.ToDouble(reader["appraisal_valence"]),
                                        Convert.ToDouble(reader["appraisal_initiative"]),
                                        Convert.ToDouble(reader["appraisal_learnability"])
                                    };

                                    double[] studentFeelings = new double[3] {
                                        Convert.ToDouble(reader["feeling_valence"]),
                                        Convert.ToDouble(reader["feeling_initiative"]),
                                        Convert.ToDouble(reader["feeling_learnability"])
                                    };

                                    string studentCharacteristic = reader["�haracteristic"] as string;

                                    MoralSchema.studentAppraisals = studentAppraisals;
                                    MoralSchema.studentFeelings = studentFeelings;
                                    MoralSchema.studentCharacteristic = studentCharacteristic;
                                }
                            }
                            isEbicaEstimatesAlreadyLoaded = true;
                        }
                    }
                    catch (Exception ex) {
                        Debug.LogError(ex);
                    }
                    finally {
                        connection.Close();
                    }
                }
            }
        }


        var currentStateId = UserProgressUtils.getUserStateId(MainMenuController.Username);

        // ������� �������� ������� ������, ���� ������� ����� �� �� ���� ����, ������� �������� �������������
        if (currentStateId.HasValue) {
            //    var learnedLessonNumbers = UserProgressUtils.getLearnedLessonsNumbers(currentStateId.Value);
            //    if (learnedLessonNumbers.Contains(CurrentLessonNumber)) {
            //        Debug.LogError("������� ����������� � ��������...");
            //        // ���������� ��������� ���� �������� ����� ������������������� � ��������� �����
            //        StartCoroutine(delayedMoralSchemaActionExecution("student_retakes_lectures"));
            //    }
            //    else {
            //        if (UserProgressUtils.LessonNumberToId.TryGetValue(CurrentLessonNumber, out int currentLessonId)) {
            //            var nextDefaultLectureId = UserProgressUtils.getNextDefaultLectureId(currentStateId.Value);
            //            if (nextDefaultLectureId.HasValue) {
            //                if (nextDefaultLectureId.Value != currentLessonId) {
            //                    Debug.LogError("������� ����� � ���� ���������...");
            //                    YandexSpeechKit.TextToSpeech("�� ������ ����� � ��������� � ����������� ������� ������ ������. �� �� �����.", YSKVoice.ERMIL, YSKEmotion.GOOD);
            //                    // ���������� ��������� ���� �������� ����� ������������������� � ��������� �����
            //                    StartCoroutine(delayedMoralSchemaActionExecution("student_takes_the_lecture_in_his_order"));
            //                }
            //            }
            //        }
            //    }
            //}
            if (UserProgressUtils.LessonNumberToId.TryGetValue(CurrentLessonNumber, out int currentLessonId)) {
                var nextDefaultLectureId = UserProgressUtils.getNextDefaultLectureId(currentStateId.Value);
                if (nextDefaultLectureId.HasValue) {
                    if (nextDefaultLectureId.Value != currentLessonId) {
                        Debug.LogError("������� ����� � ���� ���������...");
                        YandexSpeechKit.TextToSpeech("�� ������ ����� � ��������� � ����������� ������� ������ ������. �� �� �����.", YSKVoice.ERMIL, YSKEmotion.GOOD);
                        // ���������� ��������� ���� �������� ����� ������������������� � ��������� �����
                        StartCoroutine(delayedMoralSchemaActionExecution("student_takes_the_lecture_in_his_order"));
                    }
                }
            }
        }
    }

    private void sendQuestionActions(string userMessage) {

        //string studentCharacteristic = moralSchema.getStudentCharacteristic();

        double[] studentFeelings = moralSchema.getStudentFeelings();

        double valence = studentFeelings[0];
        double initiative = studentFeelings[1];
        double learnability = studentFeelings[2];

        //string systemMessage = null;

        if (ActivateEmotionalBehaviour) {
            List<string> additionMessages = new List<string>();

            if (valence > 0.2) {
                //systemMessage = "(���� ���� ��� ������ �������������. ������� �������)";
                additionMessages.Add("���� ���� ��� ������ �������������. ������� ������� � �����������");
            }
            else if (valence < -0.2) {
                //systemMessage = "���� ���� ��� ���� � ����������� �������������.";
                additionMessages.Add("���� ���� ��� ���� � ����������� �������������.");
            }

            if (initiative > 0.2) {
                //systemMessage += "������� ����������, �����������. ";
                additionMessages.Add(" ������� ������� ���������.");
            }
            else if (initiative < -0.2) {
                //systemMessage += "������� ����� ���������. ";
                additionMessages.Add(" ������� ��������.");
            }

            if (learnability > 0.2) {
                //systemMessage += "�������� ��� ����� � ����� ����� ���������. ";
                additionMessages.Add(" �������� ��� ����� � ����� ����� ���������. ");
            }
            else if (learnability < -0.2) {
                //systemMessage += "�������� ��� ����� ��� ���� ���. ";
                additionMessages.Add(" �������� ��� ����� ��� ���� ���. ");
            }

            if (additionMessages.Count > 0) {
                userMessage += "(" + String.Join(" ", additionMessages.ToArray()) + ")";
            }
        }

        Debug.LogError("userMessage = " + userMessage);


        //pipeServer.SendMessage(sendQuestionTextInputFIeld.text, systemMessage);
        pipeServer.SendMessage(userMessage);
        sendQuestionTextInputFIeld.text = "";

        return;
    }

    private IEnumerator delayedMoralSchemaActionExecution(string independentAction, float delay = 2.0f) {
        yield return new WaitForSeconds(delay);
        moralSchema.makeIndependentAction(independentAction);
    }

    private void Update() {

        if (Input.GetKeyDown(KeyCode.LeftControl) && isStudentAskQuestion == false && isTeacherGivingLectureRightNow == true) {

            //string responseAction = moralSchema.getResponseActionWithoutRecalculateAfterStudentAction("Student Ask Question During Lecture");
            moralSchema.makeIndependentAction("student_ask_question");
            audioController.StartMicrophoneRecord();
            if (ActivateEmotionalBehaviour) {
                emotionsController.setEmotion(emotionsController.getEmotion());
            }
            
            StartCoroutine("askStudentForQuestionDuringLecture");
            //if (responseAction == "Teacher answer students question") {
            //    isStudentAskQuestion = true;
            //    StartCoroutine("askStudentForQuestionDuringLecture");
            //}
            //else if (responseAction == "Teacher ignore students question") {
            //    StartCoroutine("resetEmotionsCoroutine");
            //}
        }

        if (isTeacherGivingLectureRightNow == true) {
            timeWhileTalking += Time.deltaTime;
            if (timeWhileTalking > 5.0f) {
                if (UnityEngine.Random.Range(0, 10) > 7) {
                    var randInt = UnityEngine.Random.Range(0, 2);
                    animator.SetInteger("TalkIndex", randInt);
                    animator.SetTrigger("Talk");
                }
            }
        }
    }

    private IEnumerator resetEmotionsCoroutine() {
        yield return new WaitForSeconds(2.0f);
        emotionsController.resetEmotions();
    }


    private IEnumerator askStudentForQuestionDuringLecture() {

        lectureInterruptTime = audioController.getClipTime();
        audioController.resetAudioSourceStartTime();

        isTeacherGivingLectureRightNow = false;
        try {
            StopCoroutine(playLectureCoroutine);
        }
        catch (Exception ex) {
            Debug.LogError(ex.Message);
        }
        setClipToAudioControllerAndPlay($"Music/GeneralSounds/AskQuestion/ask_question");
        yield return new WaitForSeconds(audioController.getCurrentClipLength());

        uiController.ShowQuestionButton();
        uiController.ShowChatWithChatGPTButton();

        speechRecognizerController.canAsk = true;

        yield return new WaitForSeconds(30f);


        uiController.HideQuestionButton();
        setClipToAudioControllerAndPlay($"Music/GeneralSounds/LetsContinue/lets_continue");
        yield return new WaitForSeconds(audioController.getCurrentClipLength());
        yield return new WaitForSeconds(0.5f);

        playLectureCoroutine = StartCoroutine(PlayLectureFromCurrentSlideCoroutine());

        emotionsController.resetEmotions();
        //isTalking = true;
        isStudentAskQuestion = false;
        speechRecognizerController.canAsk = false;
    }

    private float getNeighbourBreakpointOnSlide() {
        try {
            // Breakpoint'� ������������ � ��
            /*var breakpoints = File.ReadAllText(Application.dataPath + $"/Resources/CSV/Lessons/" +
                            $"{currentLessonNumber}/Slides/{currentSlideNumber}/breakpoints.csv").Split(',')
                                                                                     .Select(s => float.TryParse(s, out float n) ? n : (float?)null)
                                                                                     .Where(n => n.HasValue)
                                                                                     .Select(n => n.Value)
                                                                                     .ToList();*/

            using (var connection = new SqliteConnection(DBInfo.DataBaseName)) {
                connection.Open();

                using (var command = connection.CreateCommand()) {
                    try {
                        var query = $@"
                            SELECT 
	                            bp.time_point
                            FROM
	                            breakpoints as bp
                            INNER JOIN
	                            slides as sl
		                            ON sl.id = bp.slide_id
                            INNER JOIN
	                            lessons as ls
		                            ON sl.lesson_id = ls.id
                            WHERE 
	                            (ls.number = {CurrentLessonNumber} AND sl.number = {CurrentSlideNumber})
                            ORDER by bp.time_point ASC
                        ";
                        command.CommandText = query;
                        using (var reader = command.ExecuteReader()) {
                            if (reader.HasRows) {
                                List<float> breakpoints = new List<float>();
                                while (reader.Read()) {
                                    breakpoints.Add((float)reader["time_point"]);
                                }
                                var neighbourBreakpoint = breakpoints
                                                                    .Where(x => x < lectureInterruptTime)
                                                                    .Max(o => (float?)o);

                                return neighbourBreakpoint.GetValueOrDefault(0.0f);
                            }
                            else {
                                return 0.0f;
                            }
                        }
                    }
                    catch (Exception ex) {
                        Debug.LogError(ex);
                        return 0.0f;
                    }
                    finally {
                        connection.Close();
                    }
                }
            }
        }
        catch (Exception ex) {
            Debug.LogError(ex);
            return 0.0f;
        }
    }


    private void StartGameProcess() {
        try {
            startLectureCoroutine = StartCoroutine(startLecture());
            uiController.HideChatWithChatGPTButton();
        }
        catch (Exception ex) {
            Debug.LogError($"Exception - {ex}");
        }
    }

    private IEnumerator startLecture() {
        audioController.setClipByPath($"Music/Lessons/{CurrentLessonNumber}/Intro");
        audioController.PlayCurrentClip();
        yield return new WaitForSeconds(audioController.getCurrentClipLength());
        //audioController.PlayLecture(currentLesson, currentPart);
        yield return new WaitForSeconds(0.5f);
        //isTalking = true;
        playLectureCoroutine = StartCoroutine(PlayLectureFromCurrentSlideCoroutine());
        //StartCoroutine(changingSlides());
    }

    public IEnumerator PlayLectureFromCurrentSlideCoroutine() {

        float shift = getNeighbourBreakpointOnSlide();

        isLectureInProgress = true;
        isTeacherGivingLectureRightNow = true;

        // ������� ������� - ������� � ����������� � ���������� ������.
        var slidesCount = DirInfo.getCountOfFilesInFolder($"/Resources/Materials/Lessons/{CurrentLessonNumber}/Slides", ".mat");

        setSlideToBoard(GameController.CurrentSlideNumber);
        OnSlideChanged();
        audioController.setClipByPath($"Music/Lessons/{CurrentLessonNumber}/Slides/{CurrentSlideNumber}/Lecture/lecture");
        audioController.setAudioSourceStartTime(shift);
        audioController.PlayCurrentClip();
        yield return new WaitForSeconds(audioController.getCurrentClipLength() - shift);
        audioController.StopCurrentClip();
        yield return new WaitForSeconds(0.5f);
        audioController.resetAudioSourceStartTime();

        for (int i = CurrentSlideNumber + 1; i <= slidesCount; i++) {
            CurrentSlideNumber = i;
            setSlideToBoard(CurrentSlideNumber);
            OnSlideChanged();
            audioController.setClipByPath($"Music/Lessons/{CurrentLessonNumber}/Slides/{CurrentSlideNumber}/Lecture/lecture");
            audioController.PlayCurrentClip();
            yield return new WaitForSeconds(audioController.getCurrentClipLength());
            audioController.StopCurrentClip();
            yield return new WaitForSeconds(0.5f);
        }
        isLectureInProgress = false;
        isTeacherGivingLectureRightNow = false;
        Messenger.Broadcast(GameEvent.LECTURE_PART_FINISHED);
    }

    public void setSlideToBoard(int slideNumber) {
        setMaterial($"Materials/Lessons/{CurrentLessonNumber}/Slides/{slideNumber}");
    }

    private void setMaterial(string pathToMaterial) {
        Material newMaterial = Resources.Load(pathToMaterial, typeof(Material)) as Material;
        var materials = proyectorScreen.gameObject.GetComponent<MeshRenderer>().materials;
        materials[1] = newMaterial;
        proyectorScreen.gameObject.GetComponent<MeshRenderer>().materials = materials;
    }

    private void OnLectureFinished() {
        var askingForQuestionCount = DirInfo.getCountOfFilesInFolder(pathToAsksForQuestions, ".mp3");
        int number = UnityEngine.Random.Range(1, askingForQuestionCount + 1);
        string path = $"Music/GeneralSounds/AskForQuestions/ask_for_questions_{number}";
        audioController.playShortSound(path);
        scrollViewAdapter.UpdateQuestions();
        StartCoroutine("WaitingForQuestionsAfterLecture");
    }

    private void OnSlideChanged() {
        scrollViewAdapter.UpdateQuestions();
    }

    private IEnumerator WaitingForQuestionsAfterLecture() {
        yield return new WaitForSeconds(14.0f);
        YandexSpeechKit.TextToSpeech("������� �������� � ������������.", YSKVoice.ERMIL, YSKEmotion.NEUTRAL);
        yield return new WaitForSeconds(4.0f);
        uiController.HideQuestionButton();
        uiController.ShowTestingScrollViewAdapter();
        uiController.HideUserEstimatesButtons();
    }

    private void OnTestingButtonPressed() {

        playerFpsInput.speed = 0.0f;
        changeFOVs.ForEach(it => it.enabled = false);

        int lectureSlidesCount = DirInfo.getCountOfFilesInFolder($"/Resources/Materials/Lessons/{CurrentLessonNumber}/Slides", ".mat");
        if (CurrentSlideNumber < lectureSlidesCount) {
            moralSchema.makeIndependentAction("student_interrupt_lecture");
        }

        uiController.HideStartTestingModuleButton();
        uiController.ShowTestingScrollViewAdapter();
        uiController.HideChatWithChatGPTButton();

        audioController.stopSound();

        YandexSpeechKit.TextToSpeech("������� �������� � ������������, ��� �� ��� ������", YSKVoice.ERMIL, YSKEmotion.NEUTRAL);

        isLectureInProgress = false;
        isTeacherGivingLectureRightNow = false;
        try {
            if (playLectureCoroutine != null) StopCoroutine(playLectureCoroutine);
            if (startLectureCoroutine != null) StopCoroutine(startLectureCoroutine);
        }
        catch (Exception ex) {
            Debug.LogError(ex.Message);
        }
        
    }

    private void OnStudentFinishedTestingModule(int moduleScoreInPercent) {

        if (moduleScoreInPercent >= 60) {

            var userStateId = UserProgressUtils.getUserStateId(MainMenuController.Username);
            int? newUserStateId = null;
            if (userStateId.HasValue) {
                newUserStateId = UserProgressUtils.getNewUserStateId(userStateId.Value, MainMenuController.CurrentLessonNumber);
                if (newUserStateId.HasValue) {
                    UserProgressUtils.setUserState(MainMenuController.Username, newUserStateId.Value);
                }
            }

            string defaultCongratsText = $@"�� ������� {moduleScoreInPercent} �������� �� ������������� �����.";
            if (moduleScoreInPercent == 100) {
                defaultCongratsText = $@"�� ������� ������������ ����. ������ �� ��������, ��� �� ��������� �����������. ";
            } else if (moduleScoreInPercent >= 80) {
                defaultCongratsText += " �� ������ �����������. ";
            }

            var currentActualUserStateId = UserProgressUtils.getUserStateId(MainMenuController.Username);

            if (currentActualUserStateId.HasValue) {
                String nextDefaultLectureSummary = UserProgressUtils.getNextDefaultLectureSummary(currentActualUserStateId.Value);
                if (nextDefaultLectureSummary != null) {
                    defaultCongratsText += " ��������� ������� � �������� ������ ��� ��������� " + nextDefaultLectureSummary;
                }
                else {
                    defaultCongratsText += " ������ ���������� � �������� ��������� ������.";
                }
            } else {
                defaultCongratsText += " ������ ���������� � �������� ��������� ������.";
            }

            YandexSpeechKit.TextToSpeech(defaultCongratsText, YSKVoice.ERMIL, YSKEmotion.GOOD);
        }
        else {
            YandexSpeechKit.TextToSpeech("� ��������� �� �� ������� ����������� ���������� ������ ��� ����������� ������ ������."
                + " �� ��������������� � ���������� ��� ���.", YSKVoice.ERMIL, YSKEmotion.NEUTRAL);
        }

        Debug.LogError("moduleScoreInPercent = " + moduleScoreInPercent);

        if (moduleScoreInPercent > 0 && moduleScoreInPercent < 20) {
            Debug.LogError("test_0_20");
            moralSchema.makeIndependentAction("test_0_20");
        }
        else if (moduleScoreInPercent >= 20 && moduleScoreInPercent < 40) {
            Debug.LogError("test_20_40");
            moralSchema.makeIndependentAction("test_20_40");
        }
        else if (moduleScoreInPercent >= 40 && moduleScoreInPercent < 60) {
            Debug.LogError("test_40_60");
            moralSchema.makeIndependentAction("test_40_60");
        }
        else if (moduleScoreInPercent >= 60 && moduleScoreInPercent < 80) {
            Debug.LogError("test_60_80");
            moralSchema.makeIndependentAction("test_60_80");
        }
        else if (moduleScoreInPercent >= 80 && moduleScoreInPercent <= 100) {
            Debug.LogError("test_80_100");
            moralSchema.makeIndependentAction("test_80_100");
        }

        StartCoroutine(FinishCurrentLecture());
    }

    private IEnumerator FinishCurrentLecture() {
        
        // ������-�� getCurrentClipLength ����� �� ���������� ����������� ����� 
        yield return new WaitForSeconds(2.0f);

        yield return new WaitForSeconds(audioController.getCurrentClipLength() + 1.0f);
        // ���������� ��������� �������� ����� ������� �� �����
        PlayerState.setPlayerState(PlayerStateEnum.WALK);

        // ����� ����������
        lectureInterruptTime = 0.0f;
        CurrentSlideNumber = 1;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);

    }

    // Question number - �������� � ������ ������
    private void OnStudentAskQuestion(int questionNumber) {
        //Debug.LogError($"Student ask {questionNumber} question.");

        mouseLook.enabled = false;
        if (isLectureInProgress == false) {
            StopCoroutine("WaitingForQuestionsAfterLecture");
            StartCoroutine(OnStudentAskQuestionAfterLectureCoroutine(questionNumber));
        }
        else {
            StopCoroutine("askStudentForQuestionDuringLecture");
            StartCoroutine(OnStudentAskQuestionDuringLectureCoroutine(questionNumber));
        }
    }

    // Question number - �������� � ������ ������
    private IEnumerator OnStudentAskQuestionDuringLectureCoroutine(int questionNumber) {
        //uiController.OffQuestionButton();
        yield return new WaitForSeconds(0.5f);
        string pathToAnswer = $"Music/Lessons/{CurrentLessonNumber}/Answers/{questionNumber}";
        setClipToAudioControllerAndPlay(pathToAnswer);
        //audioController.playShortSound(pathToAnswer);
        uiController.HideQuestionButton();
        uiController.HideChatWithChatGPTButton();
        yield return new WaitForSeconds(audioController.getCurrentClipLength() + 0.5f);
        setClipToAudioControllerAndPlay($"Music/GeneralSounds/LetsContinue/lets_continue");
        yield return new WaitForSeconds(audioController.getCurrentClipLength() + 1.5f);
        playLectureCoroutine = StartCoroutine(PlayLectureFromCurrentSlideCoroutine());
        isStudentAskQuestion = false;
        //uiController.OnQuestionButton();
    }

    private void setClipToAudioControllerAndPlay(string pathToClip) {
        audioController.StopCurrentClip();
        audioController.setClipByPath(pathToClip);
        audioController.PlayCurrentClip();
    }

    private void setClipToAudioControllerAndPlay(AudioClip clip) {
        audioController.StopCurrentClip();
        audioController.setClip(clip);
        audioController.PlayCurrentClip();
    }

    private IEnumerator OnStudentAskQuestionAfterLectureCoroutine(int questionNumber) {
        yield return new WaitForSeconds(0.5f);
        string pathToAnswer = $"Music/Lessons/{CurrentLessonNumber}/Answers/{questionNumber}";
        audioController.playShortSound(pathToAnswer);
        uiController.HideQuestionButton();
        yield return new WaitForSeconds(AudioController.getClipLength(pathToAnswer));
        StartCoroutine("WaitingForQuestionsAfterLecture");
        uiController.ShowQuestionButton();
    }
}

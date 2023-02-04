using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Mono.Data.Sqlite;



public class MainMenuController : MonoBehaviour {
    [SerializeField] private InputField loginInputField;
    [SerializeField] private InputField passwordInputField;
    [SerializeField] private Text messageText;

    [SerializeField] private Button chooseLessonButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;


    public static bool IsUserAuthorized { get; private set; } = false;
    public static string Username { get; private set; } = "";

    private void Start() {
        // ���������� "�����������" ������, �.�. � ����� ������ �� �������
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        if (MainMenuController.IsUserAuthorized) {

            chooseLessonButton.interactable = true;
            loginButton.interactable = false;
            registerButton.interactable = false;

            loginInputField.interactable = false;
            passwordInputField.interactable = false;

            messageText.color = Color.green;
            messageText.text = "�� ��� ������������!";

            // for button in buttons { button.setActive...}
        }
        else {
            ConnectToDataBase();

        }
    }

    private void ConnectToDataBase() {
        using (var connection = new SqliteConnection(DBInfo.DataBaseName)) {
            connection.Open();

            using (var command = connection.CreateCommand()) {
                command.CommandText = "CREATE TABLE IF NOT EXISTS \"users\" ( \"id\" INTEGER NOT NULL UNIQUE, \"username\" TEXT NOT NULL UNIQUE, \"password\" TEXT NOT NULL, PRIMARY KEY(\"id\" AUTOINCREMENT))";
                command.ExecuteNonQuery();
            }
            connection.Close();
        }
    }

    public void TestButton() {
        Debug.LogError("Button pressed");
    }

    public void Login() {
        var username = loginInputField.text;
        var password = passwordInputField.text;

        messageText.color = Color.red;

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) {
            using (var connection = new SqliteConnection(DBInfo.DataBaseName)) {
                connection.Open();

                using (var command = connection.CreateCommand()) {
                    Debug.LogError(username);
                    command.CommandText = $"SELECT * FROM users WHERE username = '{username}'";
                    using (var reader = command.ExecuteReader()) {
                        if (reader.HasRows) {
                            if (reader.Read()) {
                                var correctPassword = reader["password"];
                                if (correctPassword.ToString() == password) {
                                    chooseLessonButton.interactable = true;
                                    loginButton.interactable = false;
                                    registerButton.interactable = false;

                                    loginInputField.interactable = false;
                                    passwordInputField.interactable = false;

                                    messageText.color = Color.green;
                                    messageText.text = "�� ������� ����� � �������!";

                                    MainMenuController.Username = username;
                                    MainMenuController.IsUserAuthorized = true;
                                }
                                else {
                                    messageText.text = "�������� ������";
                                }
                            }
                        }
                        else {
                            messageText.text = "�������� ��� ������������";
                        }
                        connection.Close();
                    }
                }
            }
        }
        else {
            messageText.text = "���������� ������ ��� ������������ � ������";
        }
    }

    public void RegisterAndLogin() {
        var username = loginInputField.text;
        var password = passwordInputField.text;

        messageText.color = Color.red;

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) {
            using (var connection = new SqliteConnection(DBInfo.DataBaseName)) {
                connection.Open();

                using (var command = connection.CreateCommand()) {
                    command.CommandText = $"SELECT * FROM users WHERE username = '{username}'";
                    using (var reader = command.ExecuteReader()) {
                        if (!reader.HasRows) {
                            using (var createUserCommand = connection.CreateCommand()) {
                                createUserCommand.CommandText = $"INSERT INTO users (username, password) VALUES ('{username}', '{password}')";
                                createUserCommand.ExecuteNonQuery();

                                chooseLessonButton.interactable = true;
                                loginButton.interactable = false;
                                registerButton.interactable = false;

                                loginInputField.interactable = false;
                                passwordInputField.interactable = false;

                                messageText.color = Color.green;
                                messageText.text = "�� ������� ����� � �������!";
                            }
                        }
                        else {
                            messageText.text = "��� ������������ ������";
                        }
                        connection.Close();
                    }
                }
            }
        }
        else {
            messageText.text = "���������� ������ ��� ������������ � ������";
        }
    }

    public void StartLesson() {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void ExitGame() {
        Debug.LogError("���� ���������.");
        Application.Quit();
    }
}

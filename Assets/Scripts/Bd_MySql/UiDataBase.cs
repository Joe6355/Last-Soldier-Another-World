using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UiDataBase : MonoBehaviour
{
    public MySQLConnector dbConnector; // Привяжите объект с MySQLConnector через Inspector
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;

    // Метод регистрации
    public void OnRegisterButtonClicked()
    {
        string username = usernameInput.text;
        string email = emailInput.text;
        string password = passwordInput.text;
        dbConnector.RegisterUser(username, email, password);
    }

    // Метод авторизации
    public void OnLoginButtonClicked()
    {
        string email = loginEmailInput.text;
        string password = loginPasswordInput.text;
        dbConnector.LoginUser(email, password);
    }
}

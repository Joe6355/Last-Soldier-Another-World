using UnityEngine;
using TMPro;

public class UserDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI userInfoText;

    void Start()
    {
        // Проверяем, сохранены ли данные
        if (UserData.username != null && userInfoText != null)
        {
            userInfoText.text = $"ID: {UserData.userId}\nИмя: {UserData.username}";
        }
        else
        {
            userInfoText.text = "Данные пользователя не найдены!";
        }
    }
}

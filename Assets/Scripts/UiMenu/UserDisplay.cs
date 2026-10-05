using TMPro;
using UnityEngine;
using YG;

public class UserDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI userInfoText;
    private void OnEnable() => GameProgress.Changed += Refresh;
    private void OnDisable() => GameProgress.Changed -= Refresh;
    private void Start() => Refresh();
    private void Refresh()
    {
        if (userInfoText == null) return;
        userInfoText.richText = false;
        userInfoText.text = YG2.player.auth ? "Игрок: " + YG2.player.name : "Гость";
    }
}

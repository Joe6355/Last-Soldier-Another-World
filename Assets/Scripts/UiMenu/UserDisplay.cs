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
        string name = YG2.player.auth ? YG2.player.name : "Гость";
        if (userInfoText != null) { userInfoText.richText = false; userInfoText.text = name; }
    }
}

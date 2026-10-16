using TMPro;
using UnityEngine;
using YG;

public class UserDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI userInfoText;
    [SerializeField] private TextMeshProUGUI profileNameText;
    private void Awake()
    {
        GameLocalization.Protect(userInfoText);
        GameLocalization.Protect(profileNameText);
    }
    private void OnEnable() { GameProgress.Changed += Refresh; GameLocalization.Changed += Refresh; Refresh(); }
    private void OnDisable() { GameProgress.Changed -= Refresh; GameLocalization.Changed -= Refresh; }
    private void Start() => Refresh();
    private void Refresh()
    {
        string name = YG2.player.auth ? YG2.player.name : GameLocalization.Text("Гость");
        if (userInfoText != null) { userInfoText.richText = false; userInfoText.text = name; }
        if (profileNameText != null) { profileNameText.richText = false; profileNameText.text = name; }
    }
}

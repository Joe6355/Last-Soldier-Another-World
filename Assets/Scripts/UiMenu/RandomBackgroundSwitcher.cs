using UnityEngine;
using UnityEngine.UI;

public class RandomBackgroundSwitcher : MonoBehaviour
{
    // Массив фоновых спрайтов, которые будут случайно выбираться.
    public Sprite[] backgrounds;

    // Ссылка на компонент Image панели.
    private Image panelImage;

    void Awake()
    {
        // Получаем компонент Image, предполагая, что скрипт висит на том же объекте, что и панель.
        panelImage = GetComponent<Image>();
        if (panelImage == null)
        {
            Debug.LogError("Компонент Image не найден на этом объекте!");
        }
    }

    // Метод, который вызывается каждый раз при активации панели.
    void OnEnable()
    {
        SwitchBackground();
    }

    // Метод для выбора случайного фона.
    public void SwitchBackground()
    {
        if (backgrounds == null || backgrounds.Length == 0)
        {
            Debug.LogWarning("Массив фонов пустой!");
            return;
        }

        int randomIndex = Random.Range(0, backgrounds.Length);
        panelImage.sprite = backgrounds[randomIndex];
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Kursorchik : MonoBehaviour
{
    [Header("Элементы управления")]
    [SerializeField] private Button enableCursorButton; // Кнопка включения курсора
    [SerializeField] private Button disableCursorButton; // Кнопка отключения курсора
    [SerializeField] private Slider cursorSlider; // Слайдер состояния курсора (0 - выключен, 1 - включен)
    [SerializeField] private GameObject menuPanel; // Панель меню

    private bool isCursorVisible = true; // Текущее состояние курсора
    private bool isMenuOpen = false; // Состояние меню (открыто/закрыто)

    private void Start()
    {
        // Привязываем кнопки к действиям
        enableCursorButton.onClick.AddListener(EnableCursor);
        disableCursorButton.onClick.AddListener(DisableCursor);

        // Устанавливаем начальное состояние курсора и слайдера
        cursorSlider.value = isCursorVisible ? 1f : 0f;
        cursorSlider.interactable = false; // Отключаем возможность взаимодействия слайдером вручную
        UpdateCursorState();
    }

    private void Update()
    {
        // Переключение видимости курсора на горячую клавишу N
        if (Input.GetKeyDown(KeyCode.N))
        {
            ToggleCursor();
        }

        // Если нажато Esc, переключаем меню
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isMenuOpen)
            {
                CloseMenu(); // Закрыть меню
            }
            else
            {
                OpenMenu(); // Открыть меню
            }
        }
    }

    private void ToggleCursor()
    {
        isCursorVisible = !isCursorVisible;
        cursorSlider.value = isCursorVisible ? 1f : 0f;
        UpdateCursorState();
    }

    private void EnableCursor()
    {
        // Включаем курсор
        isCursorVisible = true;
        cursorSlider.value = 1f; // Слайдер на 1
        UpdateCursorState();
    }

    private void DisableCursor()
    {
        // Выключаем курсор
        isCursorVisible = false;
        cursorSlider.value = 0f; // Слайдер на 0
        UpdateCursorState();
    }

    private void OpenMenu()
    {
        // Показываем меню
        menuPanel.SetActive(true);
        isMenuOpen = true;

        // Включаем курсор, так как меню требует его отображения
        isCursorVisible = true;
        cursorSlider.value = 1f;
        UpdateCursorState();

        // Останавливаем игру
        Time.timeScale = 0f;
    }

    private void CloseMenu()
    {
        // Скрываем меню
        menuPanel.SetActive(false);
        isMenuOpen = false;

        // Возвращаем курсор в состояние, соответствующее игре
        if (cursorSlider.value == 0f)
        {
            isCursorVisible = false;
        }

        UpdateCursorState();

        // Возобновляем игру
        Time.timeScale = 1f;
    }

    private void UpdateCursorState()
    {
        // Управляем видимостью курсора
        Cursor.visible = isCursorVisible;

        // Гарантируем, что курсор не будет заблокирован
        Cursor.lockState = CursorLockMode.None;

        Debug.Log($"Cursor State Updated: Visible={Cursor.visible}, MenuOpen={isMenuOpen}");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Сохраняем состояние курсора при возвращении в приложение
        if (!isMenuOpen)
        {
            Cursor.visible = isCursorVisible;
        }
    }

    private void OnApplicationQuit()
    {
        // Гарантируем, что курсор будет отключён при выходе из игры, если это нужно
        if (!isCursorVisible)
        {
            Cursor.visible = false;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

public class RandomActivator : MonoBehaviour
{
    // Список объектов, которые нужно случайно включить.
    // Заполните этот список через Inspector.
    public List<GameObject> objectsToActivate;

    void Start()
    {
        // Проверяем, что список не пустой.
        if (objectsToActivate != null && objectsToActivate.Count > 0)
        {
            // Генерируем случайный индекс.
            int randomIndex = Random.Range(0, objectsToActivate.Count);
            // Включаем выбранный объект.
            objectsToActivate[randomIndex].SetActive(true);
        }
        else
        {
            Debug.LogWarning("Список объектов пустой!");
        }
    }
}

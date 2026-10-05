using Cinemachine;
using System;
using System.Collections;
using UnityEngine;

public class CamController : MonoBehaviour
{
    public static Action<float, float, float> cameraShake;
    public static Action<float> changeCameraSizeEvent;
    public static Action<Transform> changeFollowTargetEvent;

    private CinemachineVirtualCamera cam;
    private CinemachineFramingTransposer transposer;
    private CinemachineBasicMultiChannelPerlin channelPerlin;

    [Header("Ссылка на игрока (PlayerController)")]
    [SerializeField] private PlayerController playerController;
    // Перетащите сюда в Inspector сам объект игрока с PlayerController

    [Header("Размер камеры при зажатой ПКМ")]
    [SerializeField] private float overrideCamSize = 6f;

    private bool isOverrideByMouse = false;
    private float normalCamSize;
    private float camSize;

    private Coroutine sizeRoutine;

    private void OnEnable()
    {
        cam = GetComponent<CinemachineVirtualCamera>();
        transposer = cam.GetCinemachineComponent<CinemachineFramingTransposer>();
        channelPerlin = cam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        cameraShake += shake;
        changeCameraSizeEvent += changeCameraSize;
        changeFollowTargetEvent += changeFollowTarget;

        // Запоминаем изначальный размер камеры
        normalCamSize = cam.m_Lens.OrthographicSize;
        camSize = normalCamSize;
    }

    private void OnDisable()
    {
        cameraShake -= shake;
        changeCameraSizeEvent -= changeCameraSize;
        changeFollowTargetEvent -= changeFollowTarget;
    }

    private void Update()
    {
        // При нажатии ПКМ
        if (Input.GetMouseButtonDown(1))
        {
            isOverrideByMouse = true;

            if (sizeRoutine != null) StopCoroutine(sizeRoutine);

            cam.m_Lens.OrthographicSize = overrideCamSize;
            camSize = overrideCamSize;

            // Вместо прямого изменения moveSpeed - ставим cameraMultiplier
            float ratio = normalCamSize / overrideCamSize;
            ratio = Mathf.Clamp(ratio, 0.5f, 1f);

            if (playerController != null)
            {
                playerController.cameraMultiplier = ratio;
            }
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isOverrideByMouse = false;
            // Плавно возвращаем к normalCamSize
            changeCameraSizeEvent?.Invoke(normalCamSize);
        }
    }

    private void changeCameraSize(float newSize)
    {
        normalCamSize = newSize;

        if (isOverrideByMouse) return;

        if (sizeRoutine != null) StopCoroutine(sizeRoutine);
        camSize = cam.m_Lens.OrthographicSize;
        sizeRoutine = StartCoroutine(ChangeSizeRoutine(newSize));
    }

    private void changeFollowTarget(Transform followObject)
    {
        if (followObject != null)
        {
            cam.m_Follow = followObject;
        }
    }

    private IEnumerator ChangeSizeRoutine(float newSize)
    {
        float startSize = camSize;
        float duration = 1f;

        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            if (isOverrideByMouse) yield break;

            float progress = t / duration;
            float eased = EaseInOut(progress);

            float currentSize = Mathf.Lerp(startSize, newSize, eased);

            cam.m_Lens.OrthographicSize = currentSize;
            camSize = currentSize;

            float ratio = normalCamSize / currentSize;
            ratio = Mathf.Clamp(ratio, 0.5f, 1f);

            if (playerController != null)
            {
                playerController.cameraMultiplier = ratio;
            }

            yield return null;
        }

        cam.m_Lens.OrthographicSize = newSize;
        camSize = newSize;

        float finalRatio = normalCamSize / newSize;
        finalRatio = Mathf.Clamp(finalRatio, 0.5f, 1f);

        if (playerController != null)
        {
            playerController.cameraMultiplier = finalRatio;
        }
    }

    private IEnumerator shakeCam(float strength, float time, float fadeTime)
    {
        float originStrength = strength;
        channelPerlin.m_AmplitudeGain = strength;

        yield return new WaitForSeconds(time);

        for (float i = 0; i < fadeTime; i += Time.deltaTime)
        {
            strength -= Time.deltaTime * originStrength / fadeTime;
            channelPerlin.m_AmplitudeGain = strength;
            yield return null;
        }
        channelPerlin.m_AmplitudeGain = 0;
    }

    private float EaseInOut(float x)
    {
        // Можно заменить на Mathf.SmoothStep
        return x < 0.5f ? x * x * 2 : 1 - (1 - x) * (1 - x) * 2;
    }

    private void shake(float strength, float time, float fadeTime)
    {
        StartCoroutine(shakeCam(strength, time, fadeTime));
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Sounds : MonoBehaviour
{
    public AudioClip[] sounds;
   

    private AudioSource cachedAudioSource;

    public AudioSource audioSrc
    {
        get
        {
            if (cachedAudioSource == null)
            {
                cachedAudioSource = GetComponent<AudioSource>();
                if (cachedAudioSource == null)
                {
                    cachedAudioSource = gameObject.AddComponent<AudioSource>();
                    cachedAudioSource.playOnAwake = false;
                }
            }
            return cachedAudioSource;
        }
    }

    public void PlaySound(AudioClip clip, float volume = 1f, bool destroyed = false)
    {
        if (clip == null) return;

        float finalVolume = volume * Ui.sfxVolume; // Умножаем на глобальную громкость эффектов

        if (destroyed)
            AudioSource.PlayClipAtPoint(clip, transform.position, finalVolume);
        else
            audioSrc.PlayOneShot(clip, finalVolume);
    }

}

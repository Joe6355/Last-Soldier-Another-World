using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sounds : MonoBehaviour
{
    public AudioClip[] sounds;
   

    public AudioSource audioSrc => GetComponent<AudioSource>();

    public void PlaySound(AudioClip clip, float volume = 1f, bool destroyed = false)
    {
        float finalVolume = volume * Ui.sfxVolume; // Умножаем на глобальную громкость эффектов

        if (destroyed)
            AudioSource.PlayClipAtPoint(clip, transform.position, finalVolume);
        else
            audioSrc.PlayOneShot(clip, finalVolume);
    }

}

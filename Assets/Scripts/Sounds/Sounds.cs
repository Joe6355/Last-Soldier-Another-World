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

    public void PlaySound(AudioClip clip, float volume = 1f, bool destroyed = false, bool spatial = true)
    {
        if (clip == null) return;

        float finalVolume = volume * Ui.sfxVolume; // Умножаем на глобальную громкость эффектов

        if (destroyed && !spatial)
        {
            // A defeated bomber disappears immediately; let its 2D burst finish.
            var soundObject = new GameObject("OneShot_" + clip.name);
            var source = soundObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = finalVolume;
            source.Play();
            Destroy(soundObject, clip.length + .1f);
        }
        else if (destroyed)
            AudioSource.PlayClipAtPoint(clip, transform.position, finalVolume);
        else
            audioSrc.PlayOneShot(clip, finalVolume);
    }

}

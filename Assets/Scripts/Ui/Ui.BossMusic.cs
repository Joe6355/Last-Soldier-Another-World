using System.Collections.Generic;
using UnityEngine;

public partial class Ui
{
    private AudioSource[] encounterMusic;
    private readonly float[] encounterWeights = new float[2];
    private readonly float[] encounterTargets = new float[2];
    private float encounterMusicVolume = .5f;
    private AudioClip encounterClip;

    // Background tracks keep their position while the encounter crossfades over them.
    public void SetBossMusic(AudioClip clip)
    {
        if (encounterClip == clip) return;
        encounterClip = clip;
        encounterTargets[0] = encounterTargets[1] = 0;
        if (clip == null) return;
        if (encounterMusic == null)
        {
            encounterMusic = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                encounterMusic[i] = gameObject.AddComponent<AudioSource>();
                encounterMusic[i].playOnAwake = false;
                encounterMusic[i].loop = true;
                encounterMusic[i].spatialBlend = 0;
                encounterMusic[i].volume = 0;
            }
        }
        int channel = encounterWeights[0] <= encounterWeights[1] ? 0 : 1;
        encounterMusic[channel].Stop(); encounterMusic[channel].clip = clip;
        encounterWeights[channel] = 0; encounterTargets[channel] = 1;
        encounterMusic[channel].Play();
    }
    private IEnumerable<AudioSource> MusicChannels()
    {
        if (gameMusicSources != null) foreach (var source in gameMusicSources) yield return source;
        if (encounterMusic != null) foreach (var source in encounterMusic) yield return source;
    }
    private void SetEncounterMusicVolume(float volume)
    { encounterMusicVolume = volume; ApplyEncounterVolumes(); }
    private void UpdateEncounterMusic()
    {
        if (encounterMusic == null || YG.YG2.isPauseGame) return;
        for (int i = 0; i < 2; i++)
        {
            encounterWeights[i] = Mathf.MoveTowards(encounterWeights[i], encounterTargets[i], Time.unscaledDeltaTime * 1.5f);
            if (encounterWeights[i] == 0 && encounterTargets[i] == 0 && encounterMusic[i].isPlaying)
                encounterMusic[i].Stop();
        }
        ApplyEncounterVolumes();
    }
    private void ApplyEncounterVolumes()
    {
        if (encounterMusic == null) return;
        float background = 1 - Mathf.Clamp01(encounterWeights[0] + encounterWeights[1]);
        if (gameMusicSources != null)
            foreach (var source in gameMusicSources) if (source != null) source.volume = background * encounterMusicVolume;
        for (int i = 0; i < 2; i++) encounterMusic[i].volume = encounterWeights[i] * encounterMusicVolume * .8f;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalAnim : Sounds
{
    // Cсылка на Animator, который висит на основном объекте портала (родитель).
    public Animator portalAnimator;


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Когда игрок заходит в круглый коллайдер - включаем анимацию (триггер «Open»)
            if (portalAnimator != null)
            {
                PlaySound(sounds[0], volume: 0.5f, destroyed: false);
                portalAnimator.SetTrigger("Open");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Когда игрок выходит из круга - можно закрыть анимацию
            if (portalAnimator != null)
            {
                portalAnimator.SetTrigger("Close");
            }
        }
    }
}

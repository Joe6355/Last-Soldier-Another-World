using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Anim : Sounds
{
    private void Zvyke()
    {
        PlaySound(sounds[0], volume: 1, destroyed: true);
    }
}

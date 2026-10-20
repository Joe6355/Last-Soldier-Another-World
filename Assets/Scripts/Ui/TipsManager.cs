using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using TMPro;
using UnityEngine;

public partial class TipsManager : MonoBehaviour
{
    public static Action<string> displayTipEvent;
    public static Action disabeTipEvent;

    [SerializeField] private TMP_Text messageText;

    private Animator anim;

    private int activeTips;

    private void Start()
    {
        anim = GetComponent<Animator>();
        StartGuided();
    }

    private void OnEnable()
    {
        EnableGuided();
        displayTipEvent += displayTip;
        disabeTipEvent += disableTip;
    }

    private void OnDisable()
    {
        displayTipEvent -= displayTip;
        disabeTipEvent -= disableTip;
        DisableGuided();
    }

    private void displayTip(string message)
    {
        if (guidedTutorial || GuideInstalled) return;
        messageText.text = message;
        anim.SetInteger("state", ++activeTips);
    }
    
    private void disableTip()
    {
        if (guidedTutorial || GuideInstalled) return;
        activeTips = Mathf.Max(0, activeTips - 1);
        anim.SetInteger("state", activeTips);
    }

    private void LateUpdate() => TickGuided();
    private void OnDestroy() => DestroyGuideView();
}

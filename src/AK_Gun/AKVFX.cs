using System;
using System.Reflection;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace AK_Gun;

public class AKVFX : ItemVFX
{
    public ParticleSystem akParticles;
    // public Animator animator;

    // [OriginalAttributes(MethodAttributes.Family)]
    public override void Start()
    {
        base.Start();
        OnEnable();
        // this.akParticles.Play();
        // this.animator.enabled = true;
    }

    public void OnEnable()
    {
        Subscribe();
    }

    public void OnDisable()
    {
        Unsubscribe();
    }

    public void Subscribe()
    {
        // this.item.OnPrimaryHeld += new Action(this.RunAction);
        this.item.GetComponent<Action_Gun>().OnShoot += new Action(this.RunAction);
    }

    public void RunAction()
    {
        this.akParticles.Play();
    }

    public void Unsubscribe()
    {
        // this.item.OnPrimaryHeld -= new Action(this.RunAction);
        this.item.GetComponent<Action_Gun>().OnShoot -= new Action(this.RunAction);
    }
}
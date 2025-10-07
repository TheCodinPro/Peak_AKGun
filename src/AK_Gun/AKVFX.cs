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
        this.akParticles.Play();
        // this.animator.enabled = true;
    }
}
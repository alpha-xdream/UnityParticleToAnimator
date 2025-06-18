using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public partial class ParticleToAnimator
{
    /// <summary>
    /// 粒子有生命周期。消失后，会复用渲染器，而不是放着不用
    /// </summary>
    void ReuseRenderer()
    {
    }

    void RecycleParticleId(ParticleSystem ps, int num)
    {
        var particleData = particleDatas[ps];
        var curSeed = particleData.curSeeds;
        var prevSeeds = particleData.prevSeeds;
        curSeed.Clear();
        for (int i = 0; i < num; i++)
        {
            var particle = tempParticles[i];
            curSeed.Add(particle.randomSeed);
        }
        particleData.prevSeeds.ExceptWith(curSeed);
        foreach(var seed in particleData.prevSeeds)
        {
            particleData.recycleParticleSeed.Enqueue(seed);
        }

        prevSeeds.Clear();
        prevSeeds.UnionWith(curSeed);
    }
}
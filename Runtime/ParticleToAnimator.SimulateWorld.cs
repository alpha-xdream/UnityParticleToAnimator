using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public partial class ParticleToAnimator
{
    /// <summary>
    /// 用Local来模拟World
    /// </summary>
    bool SimulateWorld(ParticleSystem ps, ref ParticleSystem.Particle particle,
        ref Vector3 position, ref Quaternion rotation, ref Vector3 scale,
        bool isNew)
    {
        var data = particleDatas[ps];
        if (!data.isWorld) return false;

        if(!data.simulateWorld.TryGetValue(particle.randomSeed, out var world))
        {
            data.simulateWorld[particle.randomSeed] = world = new ParticleData.World();
        }

        var trans = ps.transform;

        if (isNew)
        {
            world.localPosition = position;
            world.localRotation = rotation;
            world.localScale = scale;
            world.position = trans.TransformPoint(position);
            world.rotation = trans.rotation * rotation;
            world.scale = Vector3.Scale(trans.lossyScale, scale);
        }

        var posDelta = trans.TransformVector(position - world.localPosition);
        //var rotationDelta = Quaternion.Inverse(world.rotation) * rotation; // TODO
        //var scaleDelta = scale - world.scale;


        position = world.position + posDelta;
        rotation = trans.rotation * rotation;
        scale = Vector3.Scale(trans.lossyScale, scale);

        return true;
    }
}
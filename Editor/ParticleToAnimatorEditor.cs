using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Runtime.InteropServices;
using System;

[CustomEditor(typeof(ParticleToAnimator))]
public class ParticleToAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("开始转换"))
        {
            (target as ParticleToAnimator).StartBaked();
        }
    }
}

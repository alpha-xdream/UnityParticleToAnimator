using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Runtime.InteropServices;
using System;

public class ParticleToAnimatorMenu
{
    [MenuItem("CONTEXT/ParticleSystem/转为Mesh Mode", true)]
    static bool CheckParticleSystemConvertMeshMode(MenuCommand cmd)
    {
        var ps = cmd.context as ParticleSystem;
        return canConvert(ps);
    }
    private static bool canConvert(ParticleSystem ps)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        var renderMode = renderer.renderMode;
        switch (renderMode)
        {
            case ParticleSystemRenderMode.Billboard:
                var alig = renderer.alignment;
                return alig == ParticleSystemRenderSpace.View;
            case ParticleSystemRenderMode.Stretch:
                return true;
        }
        return false;
    }
    [MenuItem("CONTEXT/ParticleSystem/转为Mesh Mode")]
    static void ParticleSystemConvertMeshMode(MenuCommand cmd)
    {
        var ps = cmd.context as ParticleSystem;
        doConvert(ps);
    }

    private static void doConvert(ParticleSystem ps)
    {
        Debug.Log($"开始转换:{ps.name}");
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        var renderMode = renderer.renderMode;
        renderer.mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.alphaxdream.particle2animator/Runtime/QuadMesh.asset");
        var main = ps.main;
        var mat = renderer.sharedMaterial;
        //var trailMat = renderer.trailMaterial;
        var newMat = GameObject.Instantiate<Material>(mat);
        var shaderName = "ParticleToAnimator/Billboard";
        newMat.shader = Shader.Find(shaderName);
        string matPath = "";
        switch (renderMode)
        {
            case ParticleSystemRenderMode.Stretch:
                var lengthScale = renderer.lengthScale;
                renderer.mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.alphaxdream.particle2animator/Runtime/StretchMesh.asset");
                matPath = AssetDatabase.GetAssetPath(mat).Replace($".mat", $"-Stretch.mat");
                if (main.startSize3D)
                {
                    main.startSizeYMultiplier *= lengthScale;
                }
                else
                {
                    var startSize = main.startSizeMultiplier;
                    main.startSize3D = true;
                    main.startSizeXMultiplier = startSize;
                    main.startSizeYMultiplier = startSize * lengthScale;
                    main.startSizeZMultiplier = startSize;
                }
                break;
            case ParticleSystemRenderMode.Billboard:
            case ParticleSystemRenderMode.HorizontalBillboard:
                matPath = AssetDatabase.GetAssetPath(mat).Replace($".mat", $"-Billboard.mat");
                break;
            case ParticleSystemRenderMode.VerticalBillboard:
                matPath = AssetDatabase.GetAssetPath(mat).Replace($".mat", $"-Billboard.mat");
                newMat.SetFloat("_VerticalBillboarding", 1f);
                break;
        }
        if (!string.IsNullOrEmpty(matPath) && renderer.alignment == ParticleSystemRenderSpace.View)
        {
            AssetDatabase.CreateAsset(newMat, matPath);
            AssetDatabase.Refresh();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            renderer.trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            renderer.alignment = ParticleSystemRenderSpace.Local;
        }
        renderer.renderMode = ParticleSystemRenderMode.Mesh;

        EditorUtility.SetDirty(renderer);
    }

    [MenuItem("ParticleToAnimator/ConvertAllParticleSystem")]
    static void ConvertAllParticleSystem()
    {
        var select = Selection.activeGameObject;
        if(select == null)
        {
            Debug.LogError("请选中一个物体");
            return;
        }
        foreach(var ps in select.GetComponentsInChildren<ParticleSystem>())
        {
            if (canConvert(ps)) doConvert(ps);
        }
    }
    [MenuItem("ParticleToAnimator/GenerateMesh")]
    static void GenerateMesh()
    {
        var newMesh = new Mesh();
        var vertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
        var triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        var uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        newMesh.vertices = vertices;
        newMesh.triangles = triangles;
        newMesh.uv = uv;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        newMesh.RecalculateNormals();

        var tempPath = "Assets/QuadMesh.asset";
        AssetDatabase.CreateAsset(newMesh, tempPath);
        File.Copy(tempPath, "Packages/ParticleToAnimator/Runtime/QuadMesh.asset", true);
        AssetDatabase.DeleteAsset(tempPath);
        AssetDatabase.Refresh();


        newMesh = new Mesh();
        vertices = new Vector3[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.5f, -1f, 0), new Vector3(-0.5f, -1f, 0) };
        triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        newMesh.vertices = vertices;
        newMesh.triangles = triangles;
        newMesh.uv = uv;
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        newMesh.RecalculateNormals();

        tempPath = "Assets/StretchMesh.asset";
        AssetDatabase.CreateAsset(newMesh, tempPath);
        File.Copy(tempPath, "Packages/ParticleToAnimator/Runtime/StretchMesh.asset", true);
        AssetDatabase.DeleteAsset(tempPath);
        AssetDatabase.Refresh();
    }
}

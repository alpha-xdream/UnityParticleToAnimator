using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

public partial class ParticleToAnimator : MonoBehaviour
{
    [Header("Settings")]
    public float frameRate = 30;        // 帧速率
    public float colorScale = 2f; // 颜色缩放
    public List<GameObject> excludes = new List<GameObject>();

    private const string OUTPUT = "Assets/ParticleToAnimatorOutput";

#if UNITY_EDITOR

    // 粒子数据存储结构
    private class ParticleData
    {
        // 记录粒子的唯一id。ps.GetParticles获取到的粒子是按生成顺序排序的。如果当前帧有2个粒子，下一帧变成1个粒子，那么index为0的粒子会是上一帧index为1的粒子。
        public int id;
        public int frame; // 设置id时的时间
        public string name;
        public ParticleSystem ps;
        public ParticleSystem.Particle particle;
        public List<RecordedData> recordedData = new List<RecordedData>();
    }
    private class RecordedData
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Color color;
        public Vector4 textureOffset;
    }

    private class ParticleIdData
    {
        internal class IdData
        {
            public int count; // 获取id的次数。 每轮重置
            public List<int> ids = new List<int>();
        }
        public int nextId;
        public Queue<int> recycleIds = new Queue<int>();
        public Dictionary<uint, int> data = new Dictionary<uint, int>();
        public HashSet<uint> prevSeeds = new HashSet<uint>();
    }

    private List<ParticleSystem> psList = new List<ParticleSystem>();
    private List<Animator> animatorList = new List<Animator>();
    private Dictionary<string, ParticleData> recordedData = new Dictionary<string, ParticleData>();
    private Dictionary<ParticleSystem, ParticleIdData> particleIds = new Dictionary<ParticleSystem, ParticleIdData>();
    private bool isRecording;
    private float startTime;
    private int startFrame;
    private Transform TempTrans;
    private Transform TempChildTrans;
    private Transform WorldSpaceTrans;
    private float deltaTime;

    ParticleSystem.Particle[] tempParticles = new ParticleSystem.Particle[100];

    public void StartBaked()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogError("请先启用ParticleToAnimator");
            return;
        }
        if (!Application.isPlaying)
        {
            Debug.LogError("请先运行游戏");
            return;
        }
        StartRecording();
    }

    void OnDestroy()
    {
        if (TempTrans != null)
        {
            GameObject.DestroyImmediate(TempTrans.gameObject);
            TempTrans = null;
        }
    }

    void Update()
    {
        //if (isRecording && Application.isPlaying)
        //{
        //    RecordParticleData();
        //}
    }


    void ShowProgressBar(float progress)
    {
        //EditorUtility.DisplayProgressBar("正在转换动画", transform.name, progress);
    }

    // 开始记录数据
    void StartRecording()
    {
        ShowProgressBar(0f);
        recordedData.Clear();
        psList.Clear();
        animatorList.Clear();
        particleIds.Clear();
        WorldSpaceTrans = null;

        float duration = 0f;
        foreach(var exclude in excludes)
        {
            exclude.SetActive(false);
        }

        foreach(var animator in GetComponentsInChildren<Animator>())
        {
            animator.enabled = false;
            animatorList.Add(animator);
        }

        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            psList.Add(ps);
            ps.Stop(false);
            ps.Clear(false);
            ps.Play(false);
            ps.Pause(false);
            duration = Mathf.Max(duration, ps.main.duration + ps.main.startLifetime.constantMax); // 还要加上粒子的生存时间
        }

        Debug.LogError($"duration:{duration}");
        Debug.LogError($"Start :{Time.realtimeSinceStartup}");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        isRecording = true;
        startTime = 0f;
        startFrame = 0;
        deltaTime = 1f / frameRate;

        while(duration >= 0f)
        {
            duration -= deltaTime;
            RecordParticleData();
            //Debug.LogError($"Time.fixedDeltaTime:{Time.fixedDeltaTime}");
        }

        foreach (var animator in animatorList) animator.enabled = true;

        //var sw2 = System.Diagnostics.Stopwatch.StartNew();
        StopRecording();
        //Debug.LogError($"StopRecording Cost:{sw2.ElapsedMilliseconds} ms");
        Debug.LogError($"Finished :{Time.realtimeSinceStartup}, Cost:{sw.ElapsedMilliseconds} ms");
    }

    public static Matrix4x4 CalculateTransformationMatrix(
        Vector3 p0, Vector3 p1, Vector3 p2,
        Vector3 p0_t, Vector3 p1_t, Vector3 p2_t)
    {
        // 计算原基向量
        Vector3 v1 = p1 - p0;
        Vector3 v2 = p2 - p0;
        Vector3 v3 = Vector3.Cross(v1, v2);

        // 检查三点是否共线
        if (v3.sqrMagnitude < 1e-12f)
            throw new System.ArgumentException("Original points are colinear.");

        // 计算目标基向量
        Vector3 v1_t = p1_t - p0_t;
        Vector3 v2_t = p2_t - p0_t;
        Vector3 v3_t = Vector3.Cross(v1_t, v2_t);

        // 构造原基矩阵
        Matrix4x4 matrixB = new Matrix4x4();
        matrixB.SetColumn(0, new Vector4(v1.x, v1.y, v1.z, 0));
        matrixB.SetColumn(1, new Vector4(v2.x, v2.y, v2.z, 0));
        matrixB.SetColumn(2, new Vector4(v3.x, v3.y, v3.z, 0));
        matrixB.SetColumn(3, new Vector4(0, 0, 0, 1));

        // 构造目标基矩阵
        Matrix4x4 matrixBPrime = new Matrix4x4();
        matrixBPrime.SetColumn(0, new Vector4(v1_t.x, v1_t.y, v1_t.z, 0));
        matrixBPrime.SetColumn(1, new Vector4(v2_t.x, v2_t.y, v2_t.z, 0));
        matrixBPrime.SetColumn(2, new Vector4(v3_t.x, v3_t.y, v3_t.z, 0));
        matrixBPrime.SetColumn(3, new Vector4(0, 0, 0, 1));

        // 计算原矩阵的逆
        Matrix4x4 invB = matrixB.inverse;

        // 计算线性变换部分
        Matrix4x4 M_linear = matrixBPrime * invB;

        // 计算平移向量
        Vector3 T = p0_t - M_linear.MultiplyPoint(p0);

        // 构造最终变换矩阵
        Matrix4x4 transformationMatrix = M_linear;
        transformationMatrix.SetColumn(3, new Vector4(T.x, T.y, T.z, 1));

        return transformationMatrix;
    }

    public static string GetRelativePath(Transform parent, Transform child, string separator = "/")
    {
        if (parent == child) return "";
        string path = child.name;
        Transform t = child.parent;
        while (t != null && t != parent)
        {
            path = t.name + separator + path;
            t = t.parent;
        }
        return path;
    }

    string GetParticleName(string name, int index)
    {
        var number = string.Format("{0:00}", index);
        return $"{name}-{number}";
    }

    Color GetMainColor(Material material, out string propertyName)
    {
        var checkList = new string[] { "_Color", "_TintColor", "_MainTexColor" };
        foreach(var name in checkList)
        {
            if (material.HasProperty(name))
            {
                propertyName = name;
                return material.GetColor(name);
            }
        }
        propertyName = "";
        return Color.white;
    }

    void ResetParticleCount()
    {
        //foreach(var p in particleIds)
        //{
        //    foreach(var id in p.Value.data)
        //    {
        //        id.Value.count = 0;
        //    }
        //}
    }
    int GetParticleId(ParticleSystem ps, ref ParticleSystem.Particle particle)
    {
        var remainingLifetime = particle.remainingLifetime;
        var liveingFrameCnt = Mathf.FloorToInt(-0.001f + (particle.startLifetime - remainingLifetime) / deltaTime);
        int spawnInFrame = startFrame - liveingFrameCnt;
        var seed = particle.randomSeed;
        if (!particleIds.TryGetValue(ps, out var p))
        {
            particleIds[ps] = p = new ParticleIdData();
        }
        //Debug.Log($"ps:{GetRelativePath(transform, ps.transform)}, frame:{spawnInFrame}:{startFrame}, {particle.startLifetime}:{remainingLifetime}, liveF:{liveingFrameCnt}:{(particle.startLifetime - remainingLifetime) / deltaTime}, delta:{particle.startLifetime - remainingLifetime}:{deltaTime}, seed:{seed}");
        if (!p.prevSeeds.Contains(seed))
        {
            p.prevSeeds.Add(seed);
            particle.remainingLifetime = particle.startLifetime;
            
            if(!p.data.TryGetValue(seed, out var id))
            {
                p.data[seed] = id = p.data.Count;
            }


            //Debug.LogError($"new id:{id}");
            return id;
        }

        return particleIds[ps].data[seed];
    }

    void ResetTransform(Transform transform, Transform parent = null, Transform copy = null)
    {
        if(parent != null) transform.SetParent(parent);
        if(copy == null)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
        else
        {
            transform.localPosition = copy.localPosition;
            transform.localRotation = copy.localRotation;
            transform.localScale = copy.localScale;
        }
    }


    // 记录单帧数据
    void RecordParticleData()
    {
        if (!isRecording) return;

        ResetParticleCount();

        foreach(var animtor in animatorList)
        {
            AnimatorStateInfo state = animtor.GetCurrentAnimatorStateInfo(0);
            AnimationClip clip = animtor.GetCurrentAnimatorClipInfo(0)[0].clip;

            float normalizedTime = Mathf.Min(1f, startTime / clip.length);
            animtor.Play(state.fullPathHash, 0, normalizedTime);
            animtor.Update(0f);
        }

        foreach (var ps in psList)
        {
            var path = $"{GetRelativePath(transform, ps.transform)}";
            var isWorldSpace = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
            if (isWorldSpace && WorldSpaceTrans == null)
            {
                WorldSpaceTrans = new GameObject("World").transform;
            }
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            var originColor = GetMainColor(psRenderer.sharedMaterial, out _);
            ps.Simulate(deltaTime, false, false, false);

            int num = ps.GetParticles(tempParticles);
            //Debug.Log($"num:{num}");
            for (int i = 0; i < num; i++)
            {
                if (TempTrans == null)
                {
                    TempTrans = new GameObject("TempTrans").transform;
                    TempTrans.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    TempChildTrans = new GameObject("TempChild").transform;
                    TempChildTrans.SetParent(TempTrans, false);
                }
                var particle = tempParticles[i];
                int id = GetParticleId(ps, ref particle);
                tempParticles[i] = particle;
                //Debug.Log($"index:{i}, id:{id}");

                var originMesh = psRenderer.mesh;
                Vector3 pivotOffset = psRenderer.pivot; // 获取归一化Pivot偏移
                TempTrans.SetParent(isWorldSpace ? null : ps.transform);
                TempTrans.localPosition = particle.position;
                TempTrans.localEulerAngles = ps.main.startRotation3D ? particle.rotation3D : new Vector3(0, particle.rotation, 0);
                TempTrans.localScale = ps.main.startSize3D ? particle.GetCurrentSize3D(ps) : Vector3.one * particle.GetCurrentSize(ps);

                TempChildTrans.localRotation = Quaternion.identity;
                TempChildTrans.localScale = Vector3.one;
                if (psRenderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    pivotOffset.Scale(originMesh.bounds.size);
                    pivotOffset.z = -pivotOffset.z; // 测试发现，z轴是反的，所以要取反

                    switch (psRenderer.alignment)
                    {
                        case ParticleSystemRenderSpace.Local:
                            TempTrans.localEulerAngles = particle.rotation3D;
                            break;
                        case ParticleSystemRenderSpace.World:
                            TempTrans.rotation = Quaternion.identity;
                            break;
                        case ParticleSystemRenderSpace.Velocity:
                            TempTrans.localRotation *= Quaternion.LookRotation(particle.velocity.normalized);
                            break;
                    }
                }
                else if (psRenderer.renderMode == ParticleSystemRenderMode.Billboard)
                {
                    // pivotOffset相对于视图坐标系的偏移
                    pivotOffset = Vector3.zero;
                }
                else if (psRenderer.renderMode == ParticleSystemRenderMode.Stretch)
                {
                    TempTrans.localRotation = Quaternion.FromToRotation(Vector3.up, particle.velocity);
                    TempTrans.localScale = Vector3.Scale(TempTrans.localScale, new Vector3(1, psRenderer.lengthScale, 1));
                    pivotOffset = Vector3.zero;
                }
                TempChildTrans.localPosition = pivotOffset;


                var position = isWorldSpace ? transform.InverseTransformPoint(TempChildTrans.position) : TempTrans.parent.InverseTransformPoint(TempChildTrans.position);
                var color = particle.GetCurrentColor(ps) * originColor;
                if(psRenderer.sharedMaterial.shader.name == "LayaAir3D/Particle/ShurikenParticle")
                {
                    color *= colorScale;
                }
                RecordedData data = new RecordedData
                {
                    time = startTime,
                    position = position,
                    rotation = TempTrans.localRotation * TempChildTrans.localRotation,
                    scale = Vector3.Scale(TempChildTrans.localScale, TempTrans.localScale),
                    color = color
                };

                // 获取Renderer材质属性
                Vector4 mainTex_ST = psRenderer.sharedMaterial.GetVector("_MainTex_ST");
                data.textureOffset = mainTex_ST;
                if (ps.textureSheetAnimation.enabled)
                {
                    var tsa = ps.textureSheetAnimation;
                    int cols = tsa.numTilesX;
                    int rows = tsa.numTilesY;
                    data.textureOffset = CalculateTilingOffset(tsa, particle, new Vector2Int(cols, rows));
                }
                var name = GetParticleName(ps.name, id);
                string animPath = path == "" ? name : path + "/" + name;
                if (isWorldSpace)
                {
                    name = animPath.Replace("/", "_");
                    animPath = $"{WorldSpaceTrans.name}/" + name;
                }
                if (recordedData.TryGetValue(animPath, out var particleData))
                {
                    particleData.recordedData.Add(data);
                }
                else
                {
                    recordedData.Add(animPath, new ParticleData()
                    {
                        id = id,
                        frame = startFrame,
                        name = name,
                        ps = ps,
                        particle = particle,
                        recordedData = new List<RecordedData>() { data }
                    });
                }
            }
            ps.SetParticles(tempParticles, num);
        }
        startTime += deltaTime;
        startFrame++;
    }
    Vector4 CalculateTilingOffset(ParticleSystem.TextureSheetAnimationModule tsa, ParticleSystem.Particle particle, Vector2Int tileCount)
    {

        float lifeProgress = 1f - (particle.remainingLifetime / particle.startLifetime);
        int totalFrames = (int)(tileCount.x * tileCount.y);
        float prog = 1f - (particle.remainingLifetime / particle.startLifetime);
        int currentFrame = Mathf.CeilToInt(totalFrames * tsa.frameOverTime.Evaluate(prog));
        //Debug.Log($"currentFrame:{currentFrame}, {tsa.frameOverTime.Evaluate(prog)}, {particle.remainingLifetime}/{particle.startLifetime}");

        float frameX = currentFrame % (int)tileCount.x;
        float frameY = Mathf.Floor(currentFrame / tileCount.y);

        Vector2 scaleTS = new Vector2(1f / tileCount.x, 1f / tileCount.y);

        return new Vector4(
            scaleTS.x, scaleTS.y,
            (frameX * scaleTS.x), 1 - (frameY * scaleTS.y) - scaleTS.y
        );
    }

    // 停止记录并生成动画
    void StopRecording()
    {
        isRecording = false;
        CancelInvoke();
        ReuseRenderer();
        GenerateAnimation();
        EditorUtility.ClearProgressBar();
    }

    // GameObject
    AnimationCurve active;

    // Transform
    AnimationCurve
        posX, posY, posZ,
        rotX, rotY, rotZ,
        scaleX, scaleY, scaleZ;

    // Material
    AnimationCurve
        colorR, colorG, colorB, colorA,
        texScaleX, texScaleY, texOffsetX, texOffsetY;

    // 生成动画资源
    void GenerateAnimation()
    {
        var savePath = $"{OUTPUT}/{gameObject.name}/";
        if(Directory.Exists(savePath)) Directory.Delete(savePath, true);
        Directory.CreateDirectory(savePath);
        AssetDatabase.Refresh();

        var newGo = new GameObject();
        newGo.name = $"{gameObject.name}-Baked";
        newGo.transform.position = transform.position;
        newGo.transform.localRotation = transform.localRotation;
        newGo.transform.localScale = transform.localScale;

        if (WorldSpaceTrans != null) ResetTransform(WorldSpaceTrans, newGo.transform);

        var animtor = GetComponent<Animator>();
        AnimationClip clip = animtor == null ? new AnimationClip() : Instantiate(animtor.runtimeAnimatorController.animationClips[0]);
        clip.name = gameObject.name + "-BakedAnimation";
        clip.frameRate = frameRate;

        #region 复制Renderer
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (renderer is SkinnedMeshRenderer) continue;

            if (renderer.transform != transform)
            {
                var newRenderGO = new GameObject(renderer.name);
                newRenderGO.AddComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                newRenderGO.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;

                var _path = GetRelativePath(transform, renderer.transform.parent);
                var t = newGo.transform;
                var originT = transform;
                foreach (var childName in _path.Split('/'))
                {
                    var child = t.Find(childName);
                    var originChild = originT.Find(childName);
                    if (child == null)
                    {
                        child = new GameObject(childName).transform;
                        ResetTransform(child, t, originChild);
                        if (originChild.GetComponent<Animator>())
                        {
                            child.gameObject.AddComponent<Animator>().runtimeAnimatorController = originChild.GetComponent<Animator>().runtimeAnimatorController;
                        }
                    }
                    t = child;
                    originT = originChild;
                }
                ResetTransform(newRenderGO.transform, newGo.transform.Find(_path), renderer.transform);
            }
            else
            {
                newGo.AddComponent<MeshFilter>().sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                newGo.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
            }
        }
        #endregion

        // 填充数据
        foreach (var p in recordedData)
        {
            // GameObject
            active = new AnimationCurve();
            // Transform
            posX = new AnimationCurve();
            posY = new AnimationCurve();
            posZ = new AnimationCurve();
            rotX = new AnimationCurve();
            rotY = new AnimationCurve();
            rotZ = new AnimationCurve();
            scaleX = new AnimationCurve();
            scaleY = new AnimationCurve();
            scaleZ = new AnimationCurve();

            // Material
            colorR = new AnimationCurve();
            colorG = new AnimationCurve();
            colorB = new AnimationCurve();
            colorA = new AnimationCurve();
            texScaleX = new AnimationCurve();
            texScaleY = new AnimationCurve();
            texOffsetX = new AnimationCurve();
            texOffsetY = new AnimationCurve();


            var path = p.Key;
            var matPath = p.Key;
            var datas = p.Value;
            var name = datas.name;
            var ps = datas.ps;
            var particle = datas.particle;

            #region 创建模拟粒子的节点
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            var test = new GameObject(name);
            //Debug.Log($"test new {name}, psRender:{psRenderer.name}");

            var newMesh = psRenderer.mesh;
            var newMaterial = psRenderer.sharedMaterial;
            Material tempMat;
            string matName = GetRelativePath(transform, ps.transform, "_");
            matName = matName == "" ? ps.name  : matName;
            var tempMatPath = savePath + matName + ".mat";
            if (psRenderer.renderMode == ParticleSystemRenderMode.Billboard)
            {
                newMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.alphaxdream.particle2animator/Runtime/QuadMesh.asset");
                if (AssetDatabase.LoadAssetAtPath<Material>(tempMatPath) == null)
                {
                    tempMat = new Material(Shader.Find("ParticleToAnimator/ViewBillboard"));
                    tempMat.name = matName;
                    var pivot = psRenderer.pivot;
                    pivot.z = -pivot.z;
                    tempMat.SetTexture("_MainTex", newMaterial.GetTexture("_MainTex"));
                    tempMat.SetInt("_SrcBlend", newMaterial.GetInt("_SrcBlend"));
                    tempMat.SetInt("_DstBlend", newMaterial.GetInt("_DstBlend"));
                    tempMat.SetVector("_Offset", pivot);
                    tempMat.renderQueue = newMaterial.renderQueue;
                    AssetDatabase.CreateAsset(tempMat, tempMatPath);
                    AssetDatabase.Refresh();
                }

                newMaterial = AssetDatabase.LoadAssetAtPath<Material>(tempMatPath);
            }
            else if(psRenderer.renderMode == ParticleSystemRenderMode.Stretch)
            {
                newMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.alphaxdream.particle2animator/Runtime/StretchMesh.asset");
                if (AssetDatabase.LoadAssetAtPath<Material>(tempMatPath) == null)
                {
                    tempMat = new Material(Shader.Find("ParticleToAnimator/StretchedBillboard"));
                    tempMat.name = matName;
                    var pivot = psRenderer.pivot;
                    pivot.z = 0;
                    tempMat.SetTexture("_MainTex", newMaterial.GetTexture("_MainTex"));
                    tempMat.SetInt("_SrcBlend", newMaterial.GetInt("_SrcBlend"));
                    tempMat.SetInt("_DstBlend", newMaterial.GetInt("_DstBlend"));
                    tempMat.renderQueue = newMaterial.renderQueue;
                    tempMat.SetVector("_Offset", pivot);
                    AssetDatabase.CreateAsset(tempMat, tempMatPath);
                    AssetDatabase.Refresh();
                }
                newMaterial = AssetDatabase.LoadAssetAtPath<Material>(tempMatPath);
            }

            test.AddComponent<MeshFilter>().sharedMesh = newMesh;
            test.AddComponent<MeshRenderer>().sharedMaterial = newMaterial;

            var isWorldSpace = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
            if (ps.transform != transform && !isWorldSpace)
            {
                var _path = GetRelativePath(transform, ps.transform);
                var t = newGo.transform; // 不跟着父节点走。有可能被录制了动画，重新挂一个父节点
                var originT = transform;
                foreach (var childName in _path.Split('/'))
                {
                    var child = t.Find(childName);
                    var originChild = originT.Find(childName);
                    if (child == null)
                    {
                        child = new GameObject(childName).transform;
                        ResetTransform(child, t, originChild);
                    }
                    t = child;
                    originT = originChild;
                }
                test.transform.SetParent(newGo.transform.Find(_path));
            }
            else
            {
                test.transform.SetParent(isWorldSpace ? WorldSpaceTrans : newGo.transform);
            }
            ResetTransform(test.transform);

            #endregion

            foreach (RecordedData data in datas.recordedData)
            {
                float time = data.time;

                posX.AddKey(time, data.position.x);
                posY.AddKey(time, data.position.y);
                posZ.AddKey(time, data.position.z);

                Vector3 euler = data.rotation.eulerAngles;
                rotX.AddKey(time, euler.x);
                rotY.AddKey(time, euler.y);
                rotZ.AddKey(time, euler.z);

                scaleX.AddKey(time, data.scale.x);
                scaleY.AddKey(time, data.scale.y);
                scaleZ.AddKey(time, data.scale.z);

                colorR.AddKey(time, data.color.r);
                colorG.AddKey(time, data.color.g);
                colorB.AddKey(time, data.color.b);
                colorA.AddKey(time, data.color.a);

                texScaleX.AddKey(time, data.textureOffset.x);
                texScaleY.AddKey(time, data.textureOffset.y);
                texOffsetX.AddKey(time, data.textureOffset.z);
                texOffsetY.AddKey(time, data.textureOffset.w);
            }

            #region Clean Curve
            var cleanList = new List<AnimationCurve>()
            {
                posX, posY, posZ,
                rotX, rotY, rotZ,
                scaleX, scaleY, scaleZ,
                colorR, colorG, colorB, colorA,
                texScaleX, texScaleY, texOffsetX, texOffsetY
            };
            foreach (var curve in cleanList)
            {
                for (int index = curve.length - 2; index >= 1; index--)
                {
                    var frame = curve[index];
                    var preframe = curve[index - 1];
                    var lastframe = curve[index + 1];
                    if (Mathf.Abs(frame.value - preframe.value) < 0.0001f && Mathf.Abs(frame.value - lastframe.value) < 0.0001f)
                    {
                        curve.RemoveKey(index); continue;
                    }
                }
            }
            #endregion

            // 为GameObject添加首尾帧显隐
            if(rotX[0].time == 0) active.AddKey(rotX[0].time, 1);
            else
            {
                active.AddKey(0, 0);
                active.AddKey(rotX[0].time, 1);
            }
            active.AddKey(rotX[rotX.length - 1].time, 0);

            var constantList = new List<AnimationCurve>()
            {
                active, texScaleX, texScaleY, texOffsetX, texOffsetY,
                rotX, rotY, rotZ,
            };
            foreach (var curve in constantList)
            {
                for(int index = curve.length - 1; index >= 0; index--)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Constant);
                }
            }

            

            // 绑定曲线到Clip
            clip.SetCurve(path, typeof(MeshRenderer), "m_Enabled", active);

            clip.SetCurve(path, typeof(Transform), "localPosition.x", posX);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", posY);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", posZ);

            clip.SetCurve(path, typeof(Transform), "localEulerAngles.x", rotX);
            clip.SetCurve(path, typeof(Transform), "localEulerAngles.y", rotY);
            clip.SetCurve(path, typeof(Transform), "localEulerAngles.z", rotZ);

            clip.SetCurve(path, typeof(Transform), "localScale.x", scaleX);
            clip.SetCurve(path, typeof(Transform), "localScale.y", scaleY);
            clip.SetCurve(path, typeof(Transform), "localScale.z", scaleZ);

            GetMainColor(psRenderer.sharedMaterial, out var colorKey);
            clip.SetCurve(matPath, typeof(MeshRenderer), $"material.{colorKey}.r", colorR);
            clip.SetCurve(matPath, typeof(MeshRenderer), $"material.{colorKey}.g", colorG);
            clip.SetCurve(matPath, typeof(MeshRenderer), $"material.{colorKey}.b", colorB);
            clip.SetCurve(matPath, typeof(MeshRenderer), $"material.{colorKey}.a", colorA);

            clip.SetCurve(matPath, typeof(MeshRenderer), "material._MainTex_ST.x", texScaleX);
            clip.SetCurve(matPath, typeof(MeshRenderer), "material._MainTex_ST.y", texScaleY);
            clip.SetCurve(matPath, typeof(MeshRenderer), "material._MainTex_ST.z", texOffsetX);
            clip.SetCurve(matPath, typeof(MeshRenderer), "material._MainTex_ST.w", texOffsetY);

        }

        var clipPath = $"{savePath}{clip.name}.anim";// AssetDatabase.GenerateUniqueAssetPath($"Assets/{clip.name}.anim");
        AssetDatabase.CreateAsset(clip, clipPath);
        AssetDatabase.Refresh();

        // 创建Animator Controller
        string controllerPath = $"{savePath}{transform.name}.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddMotion(clip);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        newGo.AddComponent<Animator>().runtimeAnimatorController = controller;
        //PrefabUtility.SaveAsPrefabAsset(newGo, $"{savePath}{transform.name}.prefab");

        Debug.Log("Animation baked successfully!");
    }
#endif
}


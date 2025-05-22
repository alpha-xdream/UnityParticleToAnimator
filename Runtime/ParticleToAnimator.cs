using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.IO;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

public class ParticleToAnimator : MonoBehaviour
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

    private List<ParticleSystem> psList = new List<ParticleSystem>();
    private Dictionary<string, ParticleData> recordedData = new Dictionary<string, ParticleData>();
    private bool isRecording;
    private float startTime;
    private Transform TempTrans;
    private Transform TempChildTrans;
    private float deltaTime;

    ParticleSystem.Particle[] tempParticles = new ParticleSystem.Particle[100];


    void OnEnable()
    {
        StartRecording();
    }

    //void OnDisable()
    //{
    //
    //}

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
        if (isRecording && Application.isPlaying)
        {
            //foreach (var ps in psList)
            //{
            //    ps.Simulate(Time.deltaTime, true, false);
            //}
            RecordParticleData();
        }
    }

    //Transform GetParent(Transform root, Transform child)
    //{

    //}

    bool CanBaked(Transform check)
    {
        while (true)
        {
            if (check == transform) return true;
            if (check.GetComponent<Animator>() != null) return false;

            check = check.parent;
            if (check.parent == null) break;
        }
        return false;
    }

    // 开始记录数据
    void StartRecording()
    {
        recordedData.Clear();
        psList.Clear();

        float duration = 0f;
        foreach(var exclude in excludes)
        {
            exclude.SetActive(false);
        }

        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            var path = $"{GetRelativePath(transform, ps.transform)}";
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            //if (psRenderer.renderMode != ParticleSystemRenderMode.Mesh)
            //{
            //    Debug.LogError($"{path}是非Mesh模式。需要手动将转换后的MeshRenderer换成Billboard材质！");
            //}
            psList.Add(ps);
            ps.Clear(false);
            ps.Play(false);
            ps.Pause(false);
            duration = Mathf.Max(duration, ps.main.duration);
        }

        isRecording = true;
        startTime = 0f;
        deltaTime = 1f / frameRate;
        //InvokeRepeating("RecordParticleData", 0f, deltaTime);
        StartCoroutine(StartStopRecording(duration));
    }

    IEnumerator StartStopRecording(float t)
    {
        yield return new WaitForSeconds(t);
        //foreach (var ps in psList)
        //{
        //    ps.Simulate(deltaTime, true, false);
        //}
        RecordParticleData();
        StopRecording();
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

    public static string GetRelativePath(Transform parent, Transform child)
    {
        if (parent == child) return "";
        string path = child.name;
        Transform t = child.parent;
        while (t != null && t != parent)
        {
            path = t.name + "/" + path;
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
        var checkList = new string[] { "_Color", "_TintColor" };
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

    // 记录单帧数据
    void RecordParticleData()
    {
        if (!isRecording) return;

        foreach (var ps in psList)
        {
            var path = $"{GetRelativePath(transform, ps.transform)}";
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            var originColor = GetMainColor(psRenderer.sharedMaterial, out _);
            ps.Simulate(deltaTime, true, false);
            int num = ps.GetParticles(tempParticles);
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
                var originMesh = psRenderer.mesh;
                Vector3 pivotOffset = psRenderer.pivot; // 获取归一化Pivot偏移
                TempTrans.SetParent(ps.transform);
                TempTrans.localPosition = particle.position;
                TempTrans.localEulerAngles = ps.main.startRotation3D ? particle.rotation3D : new Vector3(0, particle.rotation, 0);
                TempTrans.localScale = ps.main.startSize3D ? particle.GetCurrentSize3D(ps) : Vector3.one * particle.GetCurrentSize(ps);

                if (psRenderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    pivotOffset.Scale(originMesh.bounds.size);
                    pivotOffset.z = -pivotOffset.z; // 测试发现，z轴是反的，所以要取反
                }
                else if (psRenderer.renderMode == ParticleSystemRenderMode.Billboard)
                {
                    // pivotOffset相对于视图坐标系的偏移
                    //var cameraTrans = activeCamera.transform;
                    //var offsetPos = cameraTrans.InverseTransformPoint(TempTrans.position);
                    //pivotOffset.z = -pivotOffset.z;
                    //pivotOffset.Scale(TempTrans.lossyScale);
                    //offsetPos += pivotOffset;
                    //pivotOffset = TempTrans.InverseTransformPoint(cameraTrans.TransformPoint(offsetPos));
                    pivotOffset = Vector3.zero;
                }
                else if (psRenderer.renderMode == ParticleSystemRenderMode.Stretch)
                {
                    if(ps.shape.enabled && pivotOffset != Vector3.zero) Debug.LogError($"StretchedBillboard暂时不支持Shape和Pivot同时使用");
                    pivotOffset = Vector3.zero;
                }
                TempChildTrans.localPosition = pivotOffset;
                TempChildTrans.localRotation = Quaternion.identity; 
                TempChildTrans.localScale = Vector3.one;

                var position = TempTrans.parent.InverseTransformPoint(TempChildTrans.position);
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

                var name = GetParticleName(ps.name, i);
                string animPath = path == "" ? name : path + "/" + name;
                if (recordedData.TryGetValue(animPath, out var particleData))
                {
                    particleData.recordedData.Add(data);
                }
                else
                {
                    recordedData.Add(animPath, new ParticleData()
                    {
                        name = name,
                        ps = ps,
                        particle = particle,
                        recordedData = new List<RecordedData>() { data }
                    });
                }
            }
        }

        startTime += deltaTime;
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
        GenerateAnimation();
    }


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
        Directory.CreateDirectory(savePath);

        var newGo = new GameObject();
        newGo.name = $"{gameObject.name}-Baked";
        newGo.transform.position = transform.position;
        newGo.transform.localRotation = transform.localRotation;
        newGo.transform.localScale = transform.localScale;

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
                        child.SetParent(t);
                        child.localPosition = originChild.localPosition;
                        child.localRotation = originChild.localRotation;
                        child.localScale = originChild.localScale;
                        if (originChild.GetComponent<Animator>())
                        {
                            child.gameObject.AddComponent<Animator>().runtimeAnimatorController = originChild.GetComponent<Animator>().runtimeAnimatorController;
                        }
                    }
                    t = child;
                    originT = originChild;
                }
                newRenderGO.transform.SetParent(newGo.transform.Find(_path));
                newRenderGO.transform.localPosition = renderer.transform.localPosition;
                newRenderGO.transform.localRotation = renderer.transform.localRotation;
                newRenderGO.transform.localScale = renderer.transform.localScale;
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
            Debug.Log($"test new {name}, psRender:{psRenderer.name}");
            var newMesh = psRenderer.mesh;
            var newMaterial = psRenderer.sharedMaterial;
            if (psRenderer.renderMode == ParticleSystemRenderMode.Billboard)
            {
                newMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.alphaxdream.particle2animator/Runtime/QuadMesh.asset");
                var tempPath = savePath + ps.name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(tempPath) == null)
                {
                    var temp = new Material(Shader.Find("ParticleToAnimator/ViewBillboard"));
                    temp.name = $"{transform.name}_{ps.name}";
                    AssetDatabase.CreateAsset(temp, tempPath);
                    AssetDatabase.Refresh();
                    temp.SetTexture("_MainTex", newMaterial.GetTexture("_MainTex"));
                    temp.SetInt("_SrcBlend", newMaterial.GetInt("_SrcBlend"));
                    temp.SetInt("_DstBlend", newMaterial.GetInt("_DstBlend"));
                    temp.renderQueue = newMaterial.renderQueue;
                }
                newMaterial = AssetDatabase.LoadAssetAtPath<Material>(tempPath);
                newMaterial.shader = Shader.Find("ParticleToAnimator/ViewBillboard");
                var pivot = psRenderer.pivot;
                pivot.z = -pivot.z;
                newMaterial.SetVector("_Offset", pivot);
            }
            else if(psRenderer.renderMode == ParticleSystemRenderMode.Stretch)
            {
                newMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Packages/com.alphaxdream.particle2animator/Runtime/StretchMesh.asset");
                var tempPath = savePath + ps.name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(tempPath) == null)
                {
                    var temp = new Material(Shader.Find("ParticleToAnimator/StretchedBillboard"));
                    temp.name = $"{transform.name}_{ps.name}";
                    temp.SetTexture("_MainTex", newMaterial.GetTexture("_MainTex"));
                    temp.SetInt("_SrcBlend", newMaterial.GetInt("_SrcBlend"));
                    temp.SetInt("_DstBlend", newMaterial.GetInt("_DstBlend"));
                    temp.renderQueue = newMaterial.renderQueue;
                    AssetDatabase.CreateAsset(temp, tempPath);
                    AssetDatabase.Refresh();
                }
                newMaterial = AssetDatabase.LoadAssetAtPath<Material>(tempPath);
                newMaterial.shader = Shader.Find("ParticleToAnimator/StretchedBillboard");
                var pivot = psRenderer.pivot;
                pivot.z = 0;
                newMaterial.SetVector("_Offset", pivot);
            }

            test.AddComponent<MeshFilter>().sharedMesh = newMesh;
            test.AddComponent<MeshRenderer>().sharedMaterial = newMaterial;
            if (ps.transform != transform)
            {
                var _path = GetRelativePath(transform, ps.transform);
                var t = newGo.transform;
                var originT = transform;
                foreach (var childName in _path.Split('/'))
                {
                    var child = t.Find(childName);
                    var originChild = originT.Find(childName);
                    if (child == null)
                    {
                        child = new GameObject(childName).transform;
                        child.SetParent(t);
                        child.localPosition = originChild.localPosition;
                        child.localRotation = originChild.localRotation;
                        child.localScale = originChild.localScale;
                    }
                    t = child;
                    originT = originChild;
                }
                test.transform.SetParent(newGo.transform.Find(_path));
            }
            else
            {
                test.transform.SetParent(newGo.transform);
            }
            test.transform.localPosition = Vector3.zero;
            test.transform.localRotation = Quaternion.identity;
            test.transform.localScale = Vector3.one;

            if (psRenderer.renderMode == ParticleSystemRenderMode.Stretch)
            {
                var newParent = new GameObject(test.name);
                newParent.transform.SetParent(test.transform.parent);
                newParent.transform.localPosition = Vector3.zero;
                newParent.transform.localRotation = Quaternion.identity;
                newParent.transform.localScale = Vector3.one;
                matPath = matPath + "/" + test.name;
                test.transform.SetParent(newParent.transform);
                test.transform.localPosition = Vector3.zero;
                test.transform.localEulerAngles = new Vector3(90, 0, 0); // 90度旋转适配StretchedBillboard.shader
                test.transform.localScale = new Vector3(1, psRenderer.lengthScale, 1);
            }

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

            var constantList = new List<AnimationCurve>()
            {
                texScaleX, texScaleY, texOffsetX, texOffsetY
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
        AssetDatabase.Refresh();

        newGo.AddComponent<Animator>().runtimeAnimatorController = controller;
        PrefabUtility.SaveAsPrefabAsset(newGo, $"{savePath}{transform.name}.prefab");

        Debug.Log("Animation baked successfully!");
    }


    #region Gizmos

    void OnDrawGizmos()
    {
        //if (!Application.isPlaying || isRecording) return;

        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            var num = ps.GetParticles(tempParticles);
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            for (int i = 0; i < num; i++)
            {
                Gizmos.color = Color.green;
                if (TempTrans == null)
                {
                    TempTrans = new GameObject("TempTrans").transform;
                    //TempTrans.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    TempChildTrans = new GameObject("TempChild").transform;
                    TempChildTrans.SetParent(TempTrans, false);
                }
                var particle = tempParticles[i];
                var originMesh = psRenderer.mesh;
                Vector3 pivotOffset = psRenderer.pivot; // 获取归一化Pivot偏移
                TempTrans.SetParent(ps.transform);
                TempTrans.localPosition = particle.position;
                TempTrans.localEulerAngles = ps.main.startRotation3D ? particle.rotation3D : new Vector3(0, particle.rotation, 0);
                TempTrans.localScale = ps.main.startSize3D ? particle.GetCurrentSize3D(ps) : Vector3.one * particle.GetCurrentSize(ps);


                if (psRenderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    pivotOffset.Scale(originMesh.bounds.size);
                    pivotOffset.z = -pivotOffset.z; // 测试发现，z轴是反的，所以要取反
                }
                else if(psRenderer.renderMode == ParticleSystemRenderMode.Billboard)
                {
                    // pivotOffset相对于视图坐标系的偏移
                    var cameraTrans = Camera.current.transform;
                    var offsetPos = cameraTrans.InverseTransformPoint(TempTrans.position);
                    pivotOffset.z = -pivotOffset.z;
                    pivotOffset.Scale(TempTrans.lossyScale);
                    offsetPos += pivotOffset;
                    pivotOffset = TempTrans.InverseTransformPoint(cameraTrans.TransformPoint(offsetPos));
                }
                else if (psRenderer.renderMode == ParticleSystemRenderMode.Stretch)
                {
                    // z值没有作用
                    pivotOffset.z = pivotOffset.y * 2;
                    pivotOffset.y = 0;

                    pivotOffset = Vector3.zero; // test
                }
                TempChildTrans.localPosition = pivotOffset;
                TempChildTrans.localRotation = Quaternion.identity;
                TempChildTrans.localScale = Vector3.one;

                var realPos = TempChildTrans.position - TempTrans.position + TempTrans.localPosition;
                Gizmos.DrawWireSphere(TempChildTrans.position, 0.1f);
                Gizmos.DrawRay(TempChildTrans.position, particle.axisOfRotation);

                //DrawStretchedGizmos(ps, particle);
            }
        }
    }

    static Mesh stretchedMeshGizmos;
    void DrawStretchedGizmos(ParticleSystem ps, ParticleSystem.Particle p)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Vector3 pos = p.position;
        Vector3 vel = p.velocity;
        float w = p.size;

        // 1. 方向与基准
        Vector3 dir = vel.magnitude > 1e-6f ? vel.normalized : Vector3.forward;
        Vector3 camF = SceneView.currentDrawingSceneView.camera.transform.forward;
        Vector3 camV = SceneView.currentDrawingSceneView.camera.velocity;

        // 2. 计算三重拉伸
        float L_cam = renderer.cameraVelocityScale * Vector3.Dot(camV, dir);
        float L_vel = renderer.velocityScale * vel.magnitude;
        float L_base = renderer.lengthScale * w;
        float length = L_base + L_vel + L_cam;

        // 3. 自由拉伸修正
        //if (!renderer.freeformStretching)
        //{
        //    float align = Mathf.Abs(Vector3.Dot(dir, camF));
        //    length *= (1 - align);  // 面向时变细
        //}
        float align = Mathf.Abs(Vector3.Dot(dir, camF));
        length *= (1 - align);  // 面向时变细

        // 4. 半幅向量
        Vector3 halfLen = dir * (length * 0.5f);
        Vector3 right = Vector3.Cross(camF, dir).normalized;
        Vector3 halfWid = right * (w * 0.5f);

        // 5. 顶点位置
        Vector3[] quad = new Vector3[4];
        quad[0] = halfLen + halfWid;
        quad[1] = halfLen - halfWid;
        quad[2] = -halfLen - halfWid;
        quad[3] = -halfLen + halfWid;

        // 6. 可选：绕 dir 轴额外旋转
        //if (renderer.rotateWithStretchDirection)
        //{
        //    Quaternion rot = Quaternion.FromToRotation(Vector3.forward, dir);
        //    for (int i = 0; i < 4; ++i)
        //        quad[i] = pos + rot * (quad[i] - pos);
        //}

        Gizmos.color = Color.red;
        foreach (var q in quad)
        {
            Gizmos.DrawWireSphere(q + pos, 0.1f);
        }
    }

    #endregion

#endif
}


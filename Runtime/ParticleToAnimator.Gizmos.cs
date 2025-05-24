#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class ParticleToAnimator
{
    #region Gizmos
    void OnDrawGizmos()
    {
        if (isRecording) return;
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
                    TempTrans.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    TempChildTrans = new GameObject("TempChild").transform;
                    TempChildTrans.SetParent(TempTrans, false);
                }
                var particle = tempParticles[i];
                var originMesh = psRenderer.mesh;
                Vector3 pivotOffset = psRenderer.pivot; // 获取归一化Pivot偏移
                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                    TempTrans.SetParent(ps.transform);
                else if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
                    TempTrans.SetParent(null);
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
                    var cameraTrans = Camera.current.transform;
                    var offsetPos = cameraTrans.InverseTransformPoint(TempTrans.position);
                    pivotOffset.z = -pivotOffset.z;
                    pivotOffset.Scale(TempTrans.lossyScale);
                    offsetPos += pivotOffset;
                    pivotOffset = TempTrans.InverseTransformPoint(cameraTrans.TransformPoint(offsetPos));
                }
                else if (psRenderer.renderMode == ParticleSystemRenderMode.Stretch)
                {
                    TempTrans.localRotation = Quaternion.FromToRotation(Vector3.up, particle.velocity);
                    TempTrans.localScale = new Vector3(1, psRenderer.lengthScale, 1);
                    pivotOffset = Vector3.zero; // test

                }
                //Debug.Log($"seed :{particle.randomSeed}");
                TempChildTrans.localPosition = pivotOffset;
                TempChildTrans.localRotation = Quaternion.identity;
                TempChildTrans.localScale = Vector3.one;

                var realPos = TempChildTrans.position - TempTrans.position + TempTrans.localPosition;
                Gizmos.DrawWireSphere(TempChildTrans.position, 0.1f);
                Gizmos.DrawRay(TempChildTrans.position, particle.axisOfRotation);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(TempChildTrans.position, particle.velocity);

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

}

#endif
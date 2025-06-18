#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public partial class ParticleToAnimator
{
    #region Gizmos
    void OnDrawGizmos()
    {
        if (isRecording) return;
        if (MeshGizmos == null) MeshGizmos = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/RawResources/Effect/Prefab/UI/Mesh/E_ui_tanshetexiao_01.FBX");
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            var num = ps.GetParticles(tempParticles);
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            var isWorldSpace = ps.main.simulationSpace == ParticleSystemSimulationSpace.World;
            for (int i = 0; i < num; i++)
            {
                Gizmos.color = Color.green;
                if (TempTrans == null)
                {
                    TempTrans = new GameObject("TempTrans").transform;
                    TempTrans.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    //TempTrans.gameObject.hideFlags = HideFlags.DontSave;
                    TempChildTrans = new GameObject("TempChild").transform;
                    TempChildTrans.SetParent(TempTrans, false);
                }
                var particle = tempParticles[i];
                var originMesh = psRenderer.mesh;
                Vector3 pivotOffset = psRenderer.pivot; // 获取归一化Pivot偏移
                TempTrans.SetParent(isWorldSpace ? null : ps.transform);
                TempTrans.localPosition = particle.position;
                TempTrans.localEulerAngles = ps.main.startRotation3D ? particle.rotation3D : new Vector3(0, particle.rotation, 0);
                TempTrans.localScale = ps.main.startSize3D ? particle.GetCurrentSize3D(ps) : Vector3.one * particle.GetCurrentSize(ps);


                if (psRenderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    TempTrans.localScale = particle.GetCurrentSize3D(ps);
                    if (isWorldSpace) TempTrans.localScale = Vector3.Scale(TempTrans.localScale, ps.transform.lossyScale);
                    pivotOffset.Scale(originMesh.bounds.size);
                    pivotOffset.z = -pivotOffset.z; // 测试发现，z轴是反的，所以要取反

                    switch (psRenderer.alignment)
                    {
                        case ParticleSystemRenderSpace.Local:
                            TempTrans.localRotation = ps.transform.rotation * TempTrans.localRotation;
                            break;
                        case ParticleSystemRenderSpace.World:
                            TempTrans.rotation = Quaternion.identity;
                            break;
                        case ParticleSystemRenderSpace.Velocity:
                            if (isWorldSpace) TempTrans.localRotation = Quaternion.LookRotation(particle.velocity.normalized);
                            else TempTrans.localRotation = Quaternion.LookRotation(TempTrans.InverseTransformDirection(particle.velocity.normalized)) * TempTrans.localRotation;
                            break;
                    }
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
                Gizmos.DrawRay(TempChildTrans.position + Vector3.one * 0.01f, particle.velocity.normalized);


            }
        }
    }

    static Mesh MeshGizmos;
    #endregion

}

#endif
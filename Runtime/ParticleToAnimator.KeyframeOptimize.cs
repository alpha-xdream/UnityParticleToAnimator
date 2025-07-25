using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public partial class ParticleToAnimator
{
    /// <summary>
    /// 精简关键帧
    /// </summary>
    void OptimizeBoolKeyframe(AnimationCurve curve)
    {
    }

    /// <summary>
    /// 标准化旋转曲线，处理角度跳跃
    /// </summary>
    private AnimationCurve NormalizeRotationCurve(AnimationCurve curve)
    {
        if (curve.keys.Length <= 1) return curve;

        List<Keyframe> normalizedKeys = new List<Keyframe>();
        normalizedKeys.Add(curve.keys[0]);

        float cumulativeOffset = 0f;
        float previousAngle = curve.keys[0].value;

        for (int i = 1; i < curve.keys.Length; i++)
        {
            float currentAngle = curve.keys[i].value;
            float angleDiff = currentAngle - previousAngle;

            // 处理角度跳跃（如从350度跳到10度）
            if (angleDiff > 180f)
            {
                cumulativeOffset -= 360f;
            }
            else if (angleDiff < -180f)
            {
                cumulativeOffset += 360f;
            }

            Keyframe normalizedKey = curve.keys[i];
            normalizedKey.value = currentAngle + cumulativeOffset;
            normalizedKeys.Add(normalizedKey);

            previousAngle = currentAngle;
        }

        return new AnimationCurve(normalizedKeys.ToArray());
    }

    #region Linear Interpolation
    public static void ReduceKeyframesLinear(AnimationCurve curve)
    {
        if (curve.length <= 2) return;

        var removeList = new List<int>();
        Keyframe previous = curve.keys[0];
        for (int i = 1; i < curve.length - 1; i++)
        {
            Keyframe current = curve[i];
            Keyframe last = curve[i+1];

            //var timeRatio = (current.time - previous.time) / (last.time - previous.time);
            //var valueRatio = (current.value - previous.value) / (last.value - previous.value);
            var timeRatio = (current.time - previous.time) * (last.value - previous.value);
            var valueRatio = (current.value - previous.value) * (last.time - previous.time);
            // 如果值变化低于阈值，则删除
            if (Mathf.Abs(timeRatio - valueRatio) < 0.001f)
            {
                removeList.Add(i);
            }
            else
            {
                previous = current;
            }
        }
        removeList.Reverse();
        foreach(var i in removeList)
        {
            curve.RemoveKey(i);
        }
        if (curve.length == 2 && Mathf.Abs(curve[0].value - curve[1].value) < 0.0001f) curve.RemoveKey(1);

        // 保留最后一个关键帧
        //reducedKeys.Add(curve.keys[curve.keys.Length - 1]);

        //return new AnimationCurve(reducedKeys.ToArray());
    }
    #endregion

    #region DouglasPeucker
    public static List<Keyframe> DouglasPeucker(Keyframe[] points, float epsilon)
    {
        if (points.Length <= 2)
            return new List<Keyframe>(points);

        // 找到距离线段最远的点
        float maxDistance = 0f;
        int maxIndex = 0;

        for (int i = 1; i < points.Length - 1; i++)
        {
            float distance = PerpendicularDistance(points[i], points[0], points[points.Length - 1]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        if (maxDistance > epsilon)
        {
            // 递归简化
            Keyframe[] firstPart = new Keyframe[maxIndex + 1];
            System.Array.Copy(points, 0, firstPart, 0, maxIndex + 1);

            Keyframe[] secondPart = new Keyframe[points.Length - maxIndex];
            System.Array.Copy(points, maxIndex, secondPart, 0, points.Length - maxIndex);

            List<Keyframe> result1 = DouglasPeucker(firstPart, epsilon);
            List<Keyframe> result2 = DouglasPeucker(secondPart, epsilon);

            // 合并结果（移除重复点）
            result1.RemoveAt(result1.Count - 1);
            result1.AddRange(result2);

            return result1;
        }
        else
        {
            return new List<Keyframe> { points[0], points[points.Length - 1] };
        }
    }

    private static float PerpendicularDistance(Keyframe point, Keyframe lineStart, Keyframe lineEnd)
    {
        float A = lineEnd.value - lineStart.value;
        float B = lineStart.time - lineEnd.time;
        float C = lineEnd.time * lineStart.value - lineStart.time * lineEnd.value;

        return Mathf.Abs(A * point.time + B * point.value + C) / Mathf.Sqrt(A * A + B * B);
    }
    #endregion
}
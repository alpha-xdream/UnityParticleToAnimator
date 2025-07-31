using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class ParticleToAnimatorExtend
{
    public static float GetMaxValue(this ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.Curve:
                return Mathf.Max(curve.curve.Evaluate(0f), curve.curve.Evaluate(1f));
            case ParticleSystemCurveMode.TwoCurves:
                return Mathf.Max(
                    Mathf.Max(curve.curveMax.Evaluate(0f), curve.curveMax.Evaluate(1f)),
                    Mathf.Max(curve.curveMin.Evaluate(0f), curve.curveMin.Evaluate(1f))
                    );
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(curve.constantMin, curve.constantMax);
        }
        return 0;
    }
}

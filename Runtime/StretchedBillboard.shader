
Shader "ParticleToAnimator/StretchedBillboard" 
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}		
		_TintColor ("Tint Color", Color) = (1,1,1,1)
		_Offset ("Offset", Vector) = (0.0, 0.0, 0.0, 0.0)

		[HideInInspector] _Cull ("__cull", Float) = 0.0
		[HideInInspector] _Mode ("__mode", Float) = 1.0
		[HideInInspector] _SrcBlend ("__src", Float) = 5.0
		[HideInInspector] _DstBlend ("__dst", Float) = 10.0
		[HideInInspector] _ZWrite("__zw", Float) = 0.0
		[HideInInspector] _ZTest("__zt", Float) = 4.0
		[HideInInspector] _RenderQueue("__rq", Float) = 3000.0
	}	

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
		// Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest" "DisableBatching" = "True" }
		
		Cull Off
		Lighting Off 
		ZWrite Off 
		AlphaTest Off
		Blend [_SrcBlend] [_DstBlend]

		Pass
		{		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest

			#pragma multi_compile _ YMGAME_QUALITY_PERFECT YMGAME_QUALITY_HIGH

			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			
			struct appdata_t
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 uv : TEXCOORD0;
			};	
			
			sampler2D _MainTex;				
			half4 _MainTex_ST;	
			half4 _TintColor;
			float3 _Offset;
			
			v2f vert (appdata_t v)
			{
				v2f o;
				float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
				v.vertex.xyz *= scale;
				_Offset.y *= 2;
				v.vertex.xyz += _Offset;
				// 实现代码参考 https://zhuanlan.zhihu.com/p/65607800

				float3 center = float3(0, -0.5, 0);
				float3 viewer = mul(unity_WorldToObject,float4(_WorldSpaceCameraPos, 1));

				float3 normalDir = (viewer - center);
				float  distance = length(normalDir);
				normalDir.y = 0;
				normalDir = normalize(normalDir);
				float3 upDir = abs(normalDir.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
				float3 rightDir = normalize(cross(normalDir, upDir));
				upDir = normalize(cross(rightDir, normalDir));
					
				float3 centerOffs = v.vertex.xyz - center; 

				float3 localPos = center + rightDir * centerOffs.x + upDir * centerOffs.y + normalDir * centerOffs.z;

				float4x4 localToWorld = unity_ObjectToWorld;
				localToWorld._m00_m10_m20 /= scale.x;
				localToWorld._m01_m11_m21 /= scale.y;
				localToWorld._m02_m12_m22 /= scale.z;
				float3 worldPos = mul(localToWorld, float4(localPos, 1)).xyz;

				o.vertex = UnityWorldToClipPos(float4(worldPos, 1));

				o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			half4 frag (v2f i) : COLOR
			{
				half4 finalColor = tex2D(_MainTex, i.uv.xy);
				finalColor *= _TintColor;
				return finalColor;
			}
			ENDCG 
		}
	} 	

	FallBack "Game/Scene/SimpleTexture"
}


Shader "ParticleToAnimator/StretchedBillboard" 
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}		
		_TintColor ("Tint Color", Color) = (1,1,1,1)

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
			
			v2f vert (appdata_t v)
			{
				v2f o;
				
				// 实现代码参考 https://zhuanlan.zhihu.com/p/65607800

				float3 center = float3(0, -0.5, 0);
				float3 viewer = mul(unity_WorldToObject,float4(_WorldSpaceCameraPos, 1));

				float3 normalDir = (viewer - center);
				normalDir.y = 0;
				normalDir = normalize(normalDir);
				float3 upDir = abs(normalDir.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
				float3 rightDir = normalize(cross(normalDir, upDir));
				upDir = normalize(cross(rightDir, normalDir));
					
				float3 centerOffs = v.vertex.xyz - center; 

				float3 localPos = center + rightDir * centerOffs.x + upDir * centerOffs.y + normalDir * centerOffs.z;
						
				o.vertex = UnityObjectToClipPos(float4(localPos, 1));

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

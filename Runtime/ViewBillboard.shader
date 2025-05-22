
Shader "ParticleToAnimator/ViewBillboard"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}		
		_TintColor ("Tint Color", Color) = (1,1,1,1)
		_Offset ("Offset", Vector) = (0.0, 0.0, 0.0, 0.0)

		//UnityEngine.Rendering.BlendMode
		[HideInInspector] _SrcBlend ("__src", Int) = 5.0
		[HideInInspector] _DstBlend ("__dst", Int) = 10.0
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
				
				// Billboard类型介绍 https://zhuanlan.zhihu.com/p/65607800
				// 实现代码参考 https://zhuanlan.zhihu.com/p/630128375
                float3 center = float3(0, 0, 0); // 锚点位置

				//1.构建视口空间下的旋转矩阵 2.模式切换
				//UNITY_MATRIX_V[1].xyz == world space camera Up unit vector
				float3 upCamVec = normalize(UNITY_MATRIX_V._m10_m11_m12);
				//UNITY_MATRIX_V[2].xyz == -1 * world space camera Forward unit vector
				float3 forwardCamVec = -normalize(UNITY_MATRIX_V._m20_m21_m22);
				//UNITY_MATRIX_V[0].xyz == world space camera Right unit vector
				float3 rightCamVec = normalize(UNITY_MATRIX_V._m00_m01_m02);

				float4x4 rotationCamMatrix = float4x4(rightCamVec, 0, upCamVec, 0, forwardCamVec, 0, 0, 0, 0, 1);
				//转换法线和切线
				// v.normalOS = normalize(mul(float4(v.normalOS, 0), rotationCamMatrix)).xyz;
				// v.tangentOS.xyz = normalize(mul(float4(v.tangentOS.xyz, 0), rotationCamMatrix)).xyz;
				//求出缩放值,前三行三列的每一列的向量长度分别对应X,Y,Z三个轴向的缩放值
				v.vertex.x *= length(UNITY_MATRIX_M._m00_m10_m20);
				v.vertex.y *= length(UNITY_MATRIX_M._m01_m11_m21);
				v.vertex.z *= length(UNITY_MATRIX_M._m02_m12_m22);

				v.vertex.xyz += _Offset;
				//在固定坐标系下面，我们用左乘的方法；在非固定的坐标系下面，用右乘。
				// v.vertex = mul(v.vertex, rotationCamMatrix);
				// v.vertex = v.vertex.x * rotationCamMatrix._m00_m01_m02_m03 + v.vertex.y * rotationCamMatrix._m10_m11_m12_m13
				// + v.vertex.z * rotationCamMatrix._m20_m21_m22_m23 + v.vertex.w * rotationCamMatrix._m30_m31_m32_m33;
				v.vertex = v.vertex.x * float4(rightCamVec, 0.0) + v.vertex.y * float4(upCamVec, 0.0) + v.vertex.z * float4(forwardCamVec, 0.0)
					+ v.vertex.w * float4(0, 0, 0, 1.0);
				//最后一列是模型中心的世界坐标,加上的是齐次坐标的偏移值,不加都会在原点
				v.vertex.xyz += UNITY_MATRIX_M._m03_m13_m23;

				// 根据原始相机位置偏移
				// v.vertex.x -= (_WorldSpaceCameraPos.x - _CameraOriginPos.x) * _OffsetScaleX;
				// v.vertex.y += (_WorldSpaceCameraPos.z - _CameraOriginPos.z) * _OffsetScaleY + _OffsetHeight;

				o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
				// o.uv.zw = ComputeFogDensity(v.vertex);

				o.vertex = UnityWorldToClipPos(v.vertex);
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

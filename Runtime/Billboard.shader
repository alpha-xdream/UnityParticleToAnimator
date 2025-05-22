Shader "ParticleToAnimator/Billboard" {
	Properties{
		_MainTex("Main Tex", 2D) = "white" {}
		[HDR] _Color("Color Tint", Color) = (1, 1, 1, 1)
		_VerticalBillboarding("Vertical Restraints", Range(0, 1)) = 0
		_ColorPow("Color Pow", Float) = 0
		[Toggle] _IsPlane("Is Plane", Float) = 0

		[HideInInspector] _SrcBlend ("__src", Int) = 5.0
		[HideInInspector] _DstBlend ("__dst", Int) = 10.0
	}
		SubShader{
			// Need to disable batching because of the vertex animation
			//Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
			Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "True"}

			Pass {
				Tags { "LightMode" = "ForwardBase" }

				ZWrite Off
				Blend [_SrcBlend] [_DstBlend]
				Cull Off

				CGPROGRAM
				#pragma multi_compile_instancing
				#pragma vertex vert
				#pragma fragment frag

				#include "Lighting.cginc"

				sampler2D _MainTex;
		/*float4 _MainTex_ST;*/
				fixed4 _Color;
				fixed _VerticalBillboarding;
				float _ColorPow;
				float _IsPlane;
				

				struct a2v {
					float4 vertex : POSITION;
					float4 texcoord : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f {
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};


				UNITY_INSTANCING_BUFFER_START(Props)
					//UNITY_DEFINE_INSTANCED_PROP(sampler2D, _MainTex)
					UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
				UNITY_INSTANCING_BUFFER_END(Props)


				v2f vert(a2v v) {
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_TRANSFER_INSTANCE_ID(v, o);
					float3 center = float3(0, 0, 0);
					float3 viewer = mul(unity_WorldToObject,float4(_WorldSpaceCameraPos, 1));

					float3 normalDir = (viewer - center);
					normalDir.y = normalDir.y * _VerticalBillboarding;
					normalDir = normalize(normalDir);
					float3 upDir = abs(normalDir.y) > 0.999 ? float3(0, 0, 1) : float3(0, 1, 0);
					float3 rightDir = normalize(cross(upDir, normalDir));
					upDir = normalize(cross(rightDir, normalDir));


					// 计算摄像机到面片中心的距离
					float distance = length(viewer - center);
						
					
					float3 centerOffs = v.vertex.xyz - center;
					// Quad
					//float3 localPos = center + rightDir * centerOffs.x + upDir * centerOffs.y + normalDir * centerOffs.z;
					// Plane
					//float3 localPos = center + rightDir * centerOffs.x + normalDir * centerOffs.y + upDir * centerOffs.z;


					float3 localPosQuad  = center + rightDir * centerOffs.x + upDir * centerOffs.y + normalDir * centerOffs.z;
					float3 localPosPlane = center + rightDir * centerOffs.x + normalDir * centerOffs.y + upDir * centerOffs.z;
					float isPlane = step(0.01, _IsPlane);
					float3 localPos = (localPosQuad) *(1 - isPlane) + localPosPlane * isPlane;
						
					o.pos = UnityObjectToClipPos(float4(localPos, 1));
					float4 st = UNITY_ACCESS_INSTANCED_PROP(Props, _MainTex_ST);
					o.uv = v.texcoord.xy * st.xy + st.zw;
					return o;
				}

				fixed4 frag(v2f i) : SV_Target{
					UNITY_SETUP_INSTANCE_ID(i);
					fixed4 c = tex2D(_MainTex, i.uv);
					c.rgb *= _Color.rgb;
					c.rgb *= pow(c.rgb, _ColorPow);
					c.a *= _Color.a;

					return c;
				}

				ENDCG
			}
		}
		FallBack "Transparent/VertexLit"
}

Shader "Custom RP/Lit"
{

    Properties{
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        [Toggle(_NORMAL_MAP)] _NormalMapToggle("Normal Map", Float) = 0
        [NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 1)) = 1
        [Toggle(_MASK_MAP)] _MaskMapToggle("Mask Map", Float) = 0
        [NoScaleOffset]_MaskMap("Mask(MODS)", 2D) = "white"{}   //r��Ϊ����ֵ�� g��Ϊ�ڵ��� b��Ϊϸ�ڣ� a��Ϊ�⻬��
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Fresnel("Fresnel", Range(0, 1)) = 1
        _Occlusion("Occlusion", Range(0, 1)) = 1
        _CutOff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst blend", Float) = 1
        [Enum(Off, 0, On, 1)]_ZWrite("Z write", Float) = 1
        [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("EmissionColor", Color) = (0.0, 0.0, 0.0, 0.0)
        _DetailMap("Details", 2D) = "linearGrey"{}
        [Toggle(_DETAIL_MAP)] _DetailMapToggle("Detail Maps", Float) = 0
        [NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {}
        _DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
        _DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
        _DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {} //���ڴ���������ͬ����ʱ֧��ʵ����
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)    //���ڴ���������ͬ����ʱ֧��ʵ����
    }

        SubShader{

            HLSLINCLUDE
            #include "../shaderLibrary/common.hlsl"
            #include "../shaderLibrary/LitInput.hlsl"
            ENDHLSL

            Pass{

                Tags{ "LightMode" = "CustomLit" }

                Blend[_SrcBlend][_DstBlend], One OneMinusSrcAlpha
                ZWrite[_ZWrite]

                HLSLPROGRAM

                #pragma target 3.5
                #pragma shader_feature _CLIPPING
                //#pragma shader_feature _PREMULTIPLY_ALPHA
                #pragma shader_feature _RECEIVE_SHADOWS
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
                #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
                #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma shader_feature _NORMAL_MAP
                #pragma shader_feature _MASK_MAP
                #pragma shader_feature _DETAIL_MAP
                #pragma multi_compile _ _LIGHTS_PER_OBJECT
                #pragma multi_compile_instancing
                #pragma vertex vert
                #pragma fragment frag
                #include "LitPass.hlsl"

                ENDHLSL

            }

            Pass{

                Tags{ "LightMode" = "ShadowCaster" }

                ColorMask 0
                Cull Front

                HLSLPROGRAM
                #pragma target 3.5
                #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma multi_compile_instancing
                #pragma vertex ShadowCasterPassVertex
                #pragma fragment ShadowCasterPassFragment
                #include "ShadowPass.hlsl"
                ENDHLSL

            }

            Pass{

                Tags{ "LightMode" = "Meta" }

                Cull Off

                HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex MetaPassVertex
                #pragma fragment MetaPassFragment
                #include "MetaPass.hlsl"
                ENDHLSL

            }

        }

        CustomEditor "CustomShaderGUI"

}
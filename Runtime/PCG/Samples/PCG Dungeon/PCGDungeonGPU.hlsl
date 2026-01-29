#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	StructuredBuffer<float> _Noise;
	StructuredBuffer<float3> _Positions, _Normals;
#endif

float4 _Config;

void ConfigureProcedural()
{
	#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
		unity_ObjectToWorld = 0.0;
		// Set world position using instanced position
		unity_ObjectToWorld._m03_m13_m23_m33 = float4(
			_Positions[unity_InstanceID],
			1.0
		);
		// Displace using normals and hash 4th byte
		unity_ObjectToWorld._m03_m13_m23 +=
			_Config.z * _Noise[unity_InstanceID] * _Normals[unity_InstanceID];
		unity_ObjectToWorld._m00_m11_m22 = _Config.y;
#endif
}

float3 GetNoiseColor()
{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    float v = saturate(_Noise[unity_InstanceID]);

    if (_UseThresholdPreview > 0.5)
    {
        float m = (_ThresholdGE > 0.5) ? step(_Threshold, v) : (v > _Threshold ? 1.0 : 0.0);
        return lerp(_MaskOffColor.rgb, _MaskOnColor.rgb, m);
    }

    // Normal path: v is already 0/1 for masks (or a gradient if you upload scalar without preview)
    return lerp(_MaskOffColor.rgb, _MaskOnColor.rgb, v);
#else
    return 1.0;
#endif
}


void ShaderGraphFunction_float(float3 In, out float3 Out, out float3 Color)
{
    Out = In;
    Color = GetNoiseColor();
}

void ShaderGraphFunction_half(half3 In, out half3 Out, out half3 Color)
{
    Out = In;
    Color = GetNoiseColor();
}


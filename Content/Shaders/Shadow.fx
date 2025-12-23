#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Shadow parameters
float ShadowSoftness = 0.5;
float ShadowOpacity = 0.85;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput ShadowVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = input.Position;
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

// Soft shadow pixel shader - uses vertex color alpha for soft falloff
float4 ShadowPS(VertexShaderOutput input) : COLOR0
{
    // Vertex color contains shadow intensity (darker near occluder, fades out)
    float shadowIntensity = input.Color.a * ShadowOpacity;

    // Output shadow as black with variable alpha
    return float4(0, 0, 0, shadowIntensity);
}

technique Shadow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ShadowVS();
        PixelShader = compile PS_SHADERMODEL ShadowPS();
    }
}

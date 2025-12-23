#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Textures
Texture2D SceneTexture;
Texture2D LightMapTexture;

sampler2D SceneSampler = sampler_state
{
    Texture = <SceneTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

sampler2D LightMapSampler = sampler_state
{
    Texture = <LightMapTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

// Parameters
float AmbientLight = 0.15;

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

// Combine scene with light map
float4 CombineLightingPS(VertexShaderOutput input) : COLOR0
{
    float4 sceneColor = tex2D(SceneSampler, input.TexCoord);
    float4 lightColor = tex2D(LightMapSampler, input.TexCoord);

    // Light map contains additive light, add ambient
    float3 finalLight = lightColor.rgb + AmbientLight;

    // Multiply scene by lighting
    float3 result = sceneColor.rgb * finalLight;

    return float4(result, sceneColor.a);
}

technique CombineLighting
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL CombineLightingPS();
    }
}

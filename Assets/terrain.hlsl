// [name] app/terrain

#include <stereokit>

///////////////////////////////////////////

cbuffer ParamBuffer : register(b2) {
	// [param] vector world_size {-1, -1, 1, 1}
	float4 world_size;
	// [param] vector color_size {-1, -1, 1, 1}
	float4 color_size;
	// [param] float world_height 1
	float  world_height;
};
struct vsIn {
	float4 pos  : SV_POSITION;
	float3 norm : NORMAL;
	float4 col  : COLOR;
	float2 uv   : TEXCOORD0;
};
struct psIn {
	float4 pos   : SV_POSITION;
	float4 color : COLOR0;
	float2 uv    : TEXCOORD0;
	uint view_id : SV_RenderTargetArrayIndex;
};

// [texture] world black
Texture2D    world         : register(t0);
SamplerState world_sampler : register(s0);

// [texture] world_color black
Texture2D    world_color         : register(t1);
SamplerState world_color_sampler : register(s1);

///////////////////////////////////////////

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn output;
	
	float4 world_pos = mul(input.pos, sk_inst[id].world);
	float2 world_uv  = (world_pos.xz - world_size.xy) / world_size.zw;
	output.uv        = (world_pos.xz - color_size.xy) / color_size.zw;
	world_pos.y     += world.SampleLevel(world_sampler, world_uv, 0).r * world_height;
	output.pos       = mul(world_pos, sk_viewproj[sk_inst[id].view_id]);

	float3 normal = normalize(mul(input.norm, (float3x3)sk_inst[id].world));

	output.view_id    = sk_inst[id].view_id;
	output.color      = input.col;
	output.color.rgb *= Lighting(normal);
	return output;
}

///////////////////////////////////////////

float4 ps(psIn input) : SV_TARGET {
	float4 col = world_color.Sample(world_color_sampler, input.uv);
	return col * input.color;
}
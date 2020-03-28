// [name] app/terrain

#include <stereokit>

///////////////////////////////////////////

cbuffer ParamBuffer : register(b2) {
	// [param] vector world_size {-1, -1, 1, 1}
	float4 world_size;
	// [param] vector color_size {-1, -1, 1, 1}
	float4 color_size;
	// [param] vector clip_vars {0, 0, 0, 0.5}
	float4 clip_vars;
	// [param] float world_height 1
	float world_height;
};
struct vsIn {
	float4 pos  : SV_POSITION;
};
struct psIn {
	float4 pos   : SV_POSITION;
	float2 uv    : TEXCOORD0;
	float3 world : TEXCOORD1;
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
	output.world     = world_pos.xyz;
	world_pos.y     += world.SampleLevel(world_sampler, world_uv, 0).r * world_height;
	output.pos       = mul(world_pos, sk_viewproj[sk_inst[id].view_id]);

	output.view_id    = sk_inst[id].view_id;
	return output;
}

///////////////////////////////////////////

float4 ps(psIn input) : SV_TARGET{
	float3 diff = input.world - clip_vars.xyz;
	float  dist_field = clip_vars.w-dot(diff, diff);
	clip( dist_field );

	float4 col  = world_color.Sample(world_color_sampler, input.uv);
	float  fade = 1-saturate(dist_field*200);
	col.rgb += fade;
	return col;
}
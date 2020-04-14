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

// [texture] world_color white
Texture2D    world_color         : register(t1);
SamplerState world_color_sampler : register(s1);

///////////////////////////////////////////

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn output;
	
	// Transform the vertex position into world space
	float4 world_pos = mul(input.pos, sk_inst[id].world);
	output.world     = world_pos.xyz;

	// Calculate terrain world UVs based on each texture's layout information
	float2 world_uv  = (world_pos.xz - world_size.xy) / world_size.zw;
	output.uv        = (world_pos.xz - color_size.xy) / color_size.zw;

	// Offset the vert's height by a sample from the heightmap
	world_pos.y += world.SampleLevel(world_sampler, world_uv, 0).r * world_height;

	// Get the vertex position on screen
	output.pos     = mul(world_pos, sk_viewproj[sk_inst[id].view_id]);
	output.view_id = sk_inst[id].view_id;
	return output;
}

///////////////////////////////////////////

float4 ps(psIn input) : SV_TARGET{
	// Find the distance from this point to the clip center, and discard the
	// pixel if it's too far.
	float2 diff       = input.world.xz - clip_vars.xz;
	float  dist_field = clip_vars.w - dot(diff, diff);
	clip( dist_field );

	// Sample the texture's color
	float4 color = world_color.Sample(world_color_sampler, input.uv);
	// Add a little highlight around the edge of the clip radius
	float  fade = 1-saturate(dist_field*300);
	color.rgb += fade;

	return color;
}
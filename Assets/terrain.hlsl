//--name = app/terrain

#include <stereokit.hlsli>

///////////////////////////////////////////

//--world_size = -1, -1, 1, 1
float4 world_size;
//--color_size = -1, -1, 1, 1
float4 color_size;
//--clip_vars = 0, 0, 0, 0.5
float4 clip_vars;
//--world_height = 1
float world_height;

struct vsIn {
	float4 pos  : SV_POSITION;
};
struct psIn {
	float4 pos   : SV_POSITION;
	float2 uv    : TEXCOORD0;
	float3 world : TEXCOORD1;
	uint view_id : SV_RenderTargetArrayIndex;
};

//--world = black
Texture2D    world   : register(t0);
SamplerState world_s : register(s0);

//--world_color = white
Texture2D    world_color   : register(t1);
SamplerState world_color_s : register(s1);

///////////////////////////////////////////

psIn vs(vsIn input, uint id : SV_InstanceID) {
	psIn o;
	o.view_id = id % sk_view_count;
	id        = id / sk_view_count;
	
	// Transform the vertex position into world space
	float4 world_pos = mul(input.pos, sk_inst[id].world);
	o.world          = world_pos.xyz;

	// Calculate terrain world UVs based on each texture's layout information
	float2 world_uv  = (world_pos.xz - world_size.xy) / world_size.zw;
	o.uv             = (world_pos.xz - color_size.xy) / color_size.zw;

	// Offset the vert's height by a sample from the heightmap
	world_pos.y += world.SampleLevel(world_s, world_uv, 0).r * world_height;

	// Get the vertex position on screen
	o.pos = mul(world_pos, sk_viewproj[o.view_id]);
	return o;
}

///////////////////////////////////////////

float4 ps(psIn input) : SV_TARGET{
	// Find the distance from this point to the clip center, and discard the
	// pixel if it's too far.
	float2 diff       = input.world.xz - clip_vars.xz;
	float  dist_field = clip_vars.w - dot(diff, diff);
	clip( dist_field );

	// Sample the texture's color
	float4 color = world_color.Sample(world_color_s, input.uv);
	// Add a little highlight around the edge of the clip radius
	float  fade = 1-saturate(dist_field*300);
	color.rgb += fade;

	return color;
}
using StereoKit;

class Terrain
{
	struct Chunk 
	{
		public Vec3   centerOffset;
		public Matrix transform;
	}

	///////////////////////////////////////////

	Chunk[]  chunks;
	Vec3     chunkCenter;
	float    chunkSize;

	Material material;
	Mesh     mesh;
	Vec3     localPosition;

	Vec2     heightmapStart;
	Vec3     heightmapSize;
	Vec2     colormapStart;
	Vec2     colormapSize;

	///////////////////////////////////////////

	public float clipRadius = 0.5f;

	public Vec3 LocalPosition { 
		get => localPosition; 
		set  { localPosition = value; UpdateChunks(); } }

	public Material Material => material;

	///////////////////////////////////////////

	public Terrain(int chunkDetail, float chunkSize, int chunkGrid)
	{
		this.chunkSize  = chunkSize;
		chunkCenter     = Vec3.Zero;
		localPosition   = Vec3.Zero;

		// Assets that the terrain needs to render! terrain.hlsl is an 
		// important part of this terrain code, which does not stand on its 
		// own without the shader. To completely undestand this code, you'll
		// also need to look at the shader!
		material = new Material(Shader.FromFile("terrain.hlsl"));
		mesh     = Mesh.GeneratePlane(Vec2.One * chunkSize, chunkDetail);

		//if (chunkGrid %2 != 1)
		//    chunkGrid += 1;

		chunks = new Chunk[chunkGrid * chunkGrid];
		float half = (int)(chunkGrid/2.0f);
		for (int y = 0; y < chunkGrid; y++) {
		for (int x = 0; x < chunkGrid; x++) {
			Vec3 pos = new Vec3(x - half, 0, y - half) * chunkSize;
			chunks[x+y*chunkGrid].centerOffset = pos;
		} }

		UpdateChunks();
	}

	///////////////////////////////////////////

	public void SetHeightmapData(Tex heightData, Vec3 heightDimensions, Vec2 heightCenter)
	{
		SetHeightmapDimensions(heightDimensions, heightCenter);
		material["world"] = heightData;
	}

	///////////////////////////////////////////

	public void SetHeightmapDimensions(Vec3 heightDimensions, Vec2 heightCenter)
	{
		heightmapStart = heightCenter - heightDimensions.XZ / 2;
		heightmapSize  = heightDimensions;
	}

	///////////////////////////////////////////

	public void SetColormapData(Tex colorData, Vec2 colorDimensions, Vec2 colorCenter)
	{
		SetColormapDimensions(colorDimensions, colorCenter);
		material["world_color"] = colorData;
	}

	///////////////////////////////////////////

	public void SetColormapDimensions(Vec2 colorDimensions, Vec2 colorCenter)
	{
		colormapStart = colorCenter - colorDimensions / 2;
		colormapSize  = colorDimensions;
	}

	///////////////////////////////////////////

	void UpdateChunks() 
	{
		for (int i = 0; i < chunks.Length; i++)
			chunks[i].transform = Matrix.T(chunks[i].centerOffset+chunkCenter+localPosition);
	}

	///////////////////////////////////////////

	public void Update()
	{
		// Ensure the terrain chunks are centered around the local origin, 
		// since this is where the radius clipping happens. Chunks are
		// located at chunkCenter + terrainPosition, so chunkCenter kinda 
		// behaves like a -terrainPosition that's snapped to grid intervals.
		Vec3 offset = chunkCenter + localPosition;
		bool update = false;
		if      (offset.x > chunkSize* 0.4f) { chunkCenter.x -= chunkSize*0.5f; update = true; }
		else if (offset.x < chunkSize*-0.4f) { chunkCenter.x += chunkSize*0.5f; update = true; }
		if      (offset.z > chunkSize* 0.4f) { chunkCenter.z -= chunkSize*0.5f; update = true; }
		else if (offset.z < chunkSize*-0.4f) { chunkCenter.z += chunkSize*0.5f; update = true; }
		if (update) UpdateChunks();

		// The shader uses world coordinates for the color and height map UV
		// lookups, but the shader parameters aren't affected by Hierarchy
		// transforms. So we need to do this ourselves to ensure that 
		// Hierarchy functionality still works. For simplicity, this terrain 
		// does not account for rotation, so if that's embedded in the
		// Hierachy, we'll get undefined behavior.
		Vec4 heightParams = new Vec4();
		heightParams.XY = Hierarchy.ToWorld         (localPosition + heightmapStart.X0Y).XZ;
		heightParams.ZW = Hierarchy.ToWorldDirection(heightmapSize.XZ.X0Y).XZ;
		material["world_size"] = heightParams;

		Vec4 colorParams = new Vec4();
		colorParams.XY = Hierarchy.ToWorld         (localPosition + colormapStart.X0Y).XZ;
		colorParams.ZW = Hierarchy.ToWorldDirection(colormapSize.X0Y).XZ;
		material["color_size"] = colorParams;

		// Do the same for the shader clip parameters! Note that clip radius 
		// is provided as squared clip radius, which is an easy optimization
		// to skip an extra multiply in the pixel shader.
		Vec3 sizes            = Hierarchy.ToWorldDirection(new Vec3(clipRadius, heightmapSize.y, 0));
		Vec3 clipCenterShader = Hierarchy.ToWorld(Vec3.Zero);
		material["clip_vars"] = new Vec4(
			clipCenterShader.x,
			clipCenterShader.y,
			clipCenterShader.z,
			sizes.x * sizes.x);
		material["world_height"] = sizes.y;

		// Draw each terrain chunk!
		for (int i = 0; i < chunks.Length; i++)
		{
			mesh.Draw(material, chunks[i].transform);
		}
	}
}
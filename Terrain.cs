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

    Material terrainMaterial;
    Mesh     terrainMesh;
    Vec3     terrainPosition;

    Vec2     heightmapStart;
    Vec3     heightmapSize;
    Vec2     colormapStart;
    Vec2     colormapSize;

    ///////////////////////////////////////////

    public float clipRadius = 0.5f;

    public Vec3 Position { 
        get => terrainPosition; 
        set  { terrainPosition = value; UpdateChunks(); } }

    public Material Material => terrainMaterial;

    ///////////////////////////////////////////
    
    public Terrain(int chunkDetail, float chunkSize, int chunkGrid)
    {
        this.chunkSize = chunkSize;
        chunkCenter = Vec3.Zero;
        terrainPosition = Vec3.Zero;

        terrainMaterial = new Material(Shader.FromFile(@"terrain.hlsl"));
        terrainMesh     = Mesh.GeneratePlane(Vec2.One * chunkSize, chunkDetail);

        if (chunkGrid %2 != 1)
            chunkGrid += 1;

        chunks = new Chunk[chunkGrid * chunkGrid];
        float half = (int)(chunkGrid/2.0f);
        for (int y = 0; y < chunkGrid; y++) {
        for (int x = 0; x < chunkGrid; x++)  {
            Vec3 pos = new Vec3(x - half, 0, y - half) * chunkSize;
            chunks[x+y*chunkGrid].centerOffset = pos;
        } }

        UpdateChunks();
    }

    ///////////////////////////////////////////

    public void SetHeightmapData(Tex heightData, Vec3 heightDimensions, Vec2 heightCenter)
    {
        SetHeightmapDimensions(heightDimensions, heightCenter);
        terrainMaterial["world"] = heightData;
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
        terrainMaterial["world_color"] = colorData;
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
            chunks[i].transform = Matrix.T(chunks[i].centerOffset+chunkCenter+terrainPosition);
    }

    ///////////////////////////////////////////

    public void Update()
    {
        Vec3 offset = terrainPosition - chunkCenter;
        bool update = false;
        if      (offset.x > chunkSize* 0.4f) { chunkCenter.x += chunkSize*0.5f; update = true; }
        else if (offset.x < chunkSize*-0.4f) { chunkCenter.x -= chunkSize*0.5f; update = true; }
        if      (offset.z > chunkSize* 0.4f) { chunkCenter.z += chunkSize*0.5f; update = true; }
        else if (offset.z < chunkSize*-0.4f) { chunkCenter.z -= chunkSize*0.5f; update = true; }
        if (update) UpdateChunks();

        Vec4 heightParams = new Vec4();
        heightParams.XY = Hierarchy.ToWorld         (terrainPosition + heightmapStart.X0Y).XZ;
        heightParams.ZW = Hierarchy.ToWorldDirection(heightmapSize.XZ.X0Y).XZ;
        terrainMaterial["world_size"] = heightParams;

        Vec4 colorParams = new Vec4();
        colorParams.XY = Hierarchy.ToWorld         (terrainPosition + colormapStart.X0Y).XZ;
        colorParams.ZW = Hierarchy.ToWorldDirection(colormapSize.X0Y).XZ;
        terrainMaterial["color_size"] = colorParams;

        Vec3 sizes            = Hierarchy.ToWorldDirection(new Vec3(clipRadius, heightmapSize.y, 0));
        Vec3 clipCenterShader = Hierarchy.ToWorld(Vec3.Zero);
        terrainMaterial["clip_vars"] = new Vec4(
            clipCenterShader.x,
            clipCenterShader.y,
            clipCenterShader.z,
            sizes.x * sizes.x);
        terrainMaterial["world_height"] = sizes.y;

        for (int i = 0; i < chunks.Length; i++)
        {
            terrainMesh.Draw(terrainMaterial, chunks[i].transform);
        }
    }
}
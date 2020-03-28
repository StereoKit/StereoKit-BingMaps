using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BingMapsRESTToolkit;
using StereoKit;

class Program
{
    // ConfigurationManager is in NuGet System.Configuration.ConfigurationManager
    // You can get a key, here: https://www.bingmapsportal.com/Application
    // Then add a [Project->Add New Item->Application Configuration File] and 
    // add this XML to the file:
    // <appSettings>
    //   <add key="BingMapsKey" value="[Your API key here!]"/>
    // </appSettings>
    static private string _ApiKey = ConfigurationManager.AppSettings.Get("BingMapsKey");

    static Tex         mapHeight = null;
    static Tex         mapColor  = null;
    static Terrain     terrain;
    static BoundingBox queryBounds = new BoundingBox(new double[] { 21.8, -159.9, 22.3, -159.1 }); // LatLon of Kauai
    static float       worldScale = 0.00001f;
    
    static Mesh cylinderMesh;

    static Vec3 justStart;
    static Vec3 justFinger;

    ///////////////////////////////////////////

    static void Main(string[] args)
    {
        StereoKitApp.settings.assetsFolder = "Assets";
        if (!StereoKitApp.Initialize("StereoKit_BingMaps", Runtime.MixedReality))
            Environment.Exit(1);

        Model cube = Model.FromMesh(
            Mesh.GenerateRoundedCube(Vec3.One, 0.2f),
            Default.Material);

        cylinderMesh = Mesh.GenerateCylinder(1,1,Vec3.Up, 32);

        terrain = new Terrain(32, 1, 7);
        terrain.Translation = Vec3.Up*-0.5f;
        terrain.ClipCenter = -terrain.Translation;
        RequestColor();
        RequestHeight();

        while (StereoKitApp.Step(() =>
        {
            Hand hand = Input.Hand(Handed.Right);
            if (hand.IsJustPinched) 
            {
                justStart  = terrain.Translation;
                justFinger = hand[FingerId.Index, JointId.Tip].position;
            }
            if (hand.IsPinched)
            {
                Vec3 newPos = justStart + (hand[FingerId.Index, JointId.Tip].position - justFinger);
                newPos.y = -0.5f;
                terrain.Translation = newPos;
                terrain.ClipCenter = -newPos;
                
            }
            terrain.Material.Wireframe = Input.Hand(Handed.Right).IsGripped;

            terrain.Update();
            cylinderMesh.Draw(Default.Material, Matrix.TS(Vec3.Up*-0.56f, new Vec3(terrain.ClipRadius*2, 0.1f, terrain.ClipRadius*2)));
        }));

        StereoKitApp.Shutdown();
    }

    ///////////////////////////////////////////

    static async Task RequestColor()
    {
        var request = new ImageryRequest() {
            MapArea     = queryBounds,
            MapWidth    = 1024,
            MapHeight   = 1024,
            ImagerySet  = ImageryType.Aerial,
            BingMapsKey = _ApiKey
        };
        Task<Response> metaTask  = ServiceManager.GetResponseAsync(request);
        Task<Stream>   colorTask = ServiceManager.GetImageAsync   (request);
        await Task.WhenAll(metaTask, colorTask);
        Response meta  = await metaTask;
        Stream   color = await colorTask;

        MemoryStream stream = null;
        if (color is MemoryStream) stream = color as MemoryStream;
        else color.CopyTo(stream);

        mapColor = Tex.FromMemory(stream.ToArray());
        mapColor.AddressMode = TexAddress.Clamp;

        BoundingBox bounds = new BoundingBox(meta.ResourceSets[0].Resources[0].BoundingBox);
        Geo.BoundsToWorld(queryBounds, bounds, worldScale, out Vec3 size, out Vec2 offset);
        terrain.SetColorData(mapColor,size.XZ, offset);

        if (mapColor == null)
            Log.Warn(Encoding.ASCII.GetString(stream.ToArray()));
    }

    ///////////////////////////////////////////

    static async Task RequestHeight()
    {
        var request = new ElevationRequest() {
            Bounds = queryBounds,
            Row  = 32,
            Col  = 32,
            BingMapsKey = _ApiKey
        };
        Response response = await request.Execute();

        ElevationData data = response.ResourceSets[0].Resources[0] as ElevationData;
        Color[] heights = new Color[32 * 32];
        for (int y = 0; y < 32; y++) {
        for (int x = 0; x < 32; x++) {
            // Mount everest is 8,848m tall
            heights[x + (31 - y) * 32] = Color.White * (data.Elevations[x + y * 32] / 9000.0f);
        }
        }
        mapHeight = new Tex(TexType.ImageNomips, TexFormat.Rgba128);
        mapHeight.SetColors(32, 32, heights);
        mapHeight.AddressMode = TexAddress.Clamp;

        BoundingBox bounds = queryBounds;
        Geo.BoundsToWorld(queryBounds, bounds, worldScale, out Vec3 size, out Vec2 offset);
        terrain.SetHeightData(mapHeight, size, offset);
    }

    ///////////////////////////////////////////
}

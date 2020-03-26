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
    static float       worldScale = 0.00002f;
    
    static float justHeight;
    static float justFinger;

    ///////////////////////////////////////////

    static void Main(string[] args)
    {
        StereoKitApp.settings.assetsFolder = "Assets";
        if (!StereoKitApp.Initialize("StereoKit_BingMaps", Runtime.MixedReality))
            Environment.Exit(1);

        Model cube = Model.FromMesh(
            Mesh.GenerateRoundedCube(Vec3.One, 0.2f),
            Default.Material);

        terrain = new Terrain(32, 1, 7);
        terrain.Center -= Vec3.Up * 0.5f;
        RequestColor();
        RequestHeight();

        while (StereoKitApp.Step(() =>
        {
            Hand hand = Input.Hand(Handed.Right);
            if (hand.IsJustPinched) 
            {
                justHeight = terrain.Height;
                justFinger = hand[FingerId.Index, JointId.Tip].position.y;
            }
            if (hand.IsPinched)
            {
                terrain.Height = justHeight + (hand[FingerId.Index, JointId.Tip].position.y - justFinger)*3;
            }
            terrain.Material.Wireframe = Input.Hand(Handed.Right).IsGripped;
            terrain.Update();
        })) ;

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
        Log.Info($"Color bounds: {bounds.WestLongitude}, {bounds.EastLongitude}, {bounds.NorthLatitude}, {bounds.SouthLatitude}");
        BoundsToWorld(queryBounds, bounds, worldScale, out Vec3 size, out Vec2 offset);
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
        Log.Info($"height bounds: {bounds.WestLongitude}, {bounds.EastLongitude}, {bounds.NorthLatitude}, {bounds.SouthLatitude}");
        BoundsToWorld(queryBounds, bounds, worldScale, out Vec3 size, out Vec2 offset);
        terrain.SetHeightData(mapHeight, size, offset);
    }

    ///////////////////////////////////////////

    const double EarthCircumference = 40075040.0;

    static double DistanceLatitude(double a, double b)
        => (EarthCircumference*(a-b)) / 360.0;

    static double DistanceLongitude(double a, double b, double latitude)
        => ((a-b)*EarthCircumference*Math.Cos(latitude*(Math.PI/180.0))) / 360.0;

    static Vec2 BoundsSize(BoundingBox bounds)
    {
        return new Vec2(
            (float)DistanceLongitude(bounds.EastLongitude, bounds.WestLongitude, (bounds.NorthLatitude + bounds.SouthLatitude) / 2),
            (float)DistanceLatitude (bounds.SouthLatitude, bounds.NorthLatitude));
    }

    static void BoundsToWorld(BoundingBox queryBox, BoundingBox givenBox, float scale, out Vec3 size, out Vec2 offset)
    {
        Vec2 queryCenter = new Vec2(
            (float)(queryBox.WestLongitude + queryBox.EastLongitude) / 2.0f,
            (float)(queryBox.NorthLatitude + queryBox.SouthLatitude) / 2.0f);
        Vec2 givenCenter = new Vec2(
            (float)(givenBox.WestLongitude + givenBox.EastLongitude) / 2.0f,
            (float)(givenBox.NorthLatitude + givenBox.SouthLatitude) / 2.0f);
        offset = new Vec2(
            (float)DistanceLongitude(givenCenter.x, queryCenter.x, queryCenter.y),
            (float)DistanceLatitude (givenCenter.y, queryCenter.y)) * scale;
        size = Vec3.Zero;
        size.XZ = BoundsSize(givenBox) * scale;
        size.y = 9000*scale * 4;

        Log.Info($"{size}, {offset}");
    }
}

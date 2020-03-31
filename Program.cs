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

    static BoundingBox[] locationQueries = new BoundingBox[] {
        Geo.LatLonBounds( 22,    -159.5, 20000), // LatLon of Kauai
        Geo.LatLonBounds( 36.3, -112.75, 10000), // LatLon of Grand Canyon
        Geo.LatLonBounds( 27.98,  86.92, 10000), // LatLon of Everest
        Geo.LatLonBounds(-13.16, -72.54, 10000), // LatLon of Machu Picchu
    };

    static Tex         mapHeight = null;
    static Vec2        mapHeightCenter;
    static Vec3        mapHeightSize;

    static Tex         mapColor  = null;
    static Vec2        mapColorCenter;
    static Vec3        mapColorSize;

    static Terrain     terrain;
    static float       worldScale   = 0.00002f;
    static float       uiWorldScale = 0.00002f;
    static Pose        terrainPose  = new Pose(0, 0, -0.5f, Quat.Identity);
    static int         locationId   = -1;
    
    static Mesh  cylinderMesh;
    static Model compassModel;

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

        cylinderMesh = Mesh.GenerateCylinder(1,1,Vec3.Up, 64);
        compassModel = Model.FromFile("Compass.glb");

        terrain = new Terrain(128, 1, 3);
        terrain.ClipRadius = 0.3f;
        
        LoadLocation(0);

        while (StereoKitApp.Step(() =>
        {   
            UI.AffordanceBegin("Terrain", ref terrainPose, new Bounds(new Vec3(terrain.ClipRadius*2, 0.1f, terrain.ClipRadius*2)));
            terrainPose.orientation = Quat.Identity;
            UI.AffordanceEnd();

            Hand hand = Input.Hand(Handed.Right);
            if (hand.IsJustPinched) 
            {
                justStart  = terrain.Translation;
                justFinger = hand[FingerId.Index, JointId.Tip].position;
            }
            if (hand.IsPinched)
            {
                Vec3 newPos = justStart + (hand[FingerId.Index, JointId.Tip].position - justFinger);
                newPos.y = 0;
                terrain.Translation = newPos;
                terrain.ClipCenter = -newPos;
                
            }
            terrain.Update();
            cylinderMesh.Draw(Default.Material, Matrix.TS(Vec3.Up*-0.04f, new Vec3(terrain.ClipRadius*2, 0.05f, terrain.ClipRadius*2)), Color.White*0.25f);

            Vec3 pos = Vec3.Zero;
            Vec3 dir = (Input.Head.position - pos);
            dir.y = 0;
            dir.Normalize();

            float angle = MathF.Atan2(dir.z, dir.x) * Units.rad2deg;
            if (angle < 0) angle = 360+angle;

            angle = (int)(angle / 60) * 60 + 30;
            dir = Vec3.AngleXZ(angle);
            Vec3 lookat = dir + Vec3.Up;
            Vec3 menuAt = pos + dir * (terrain.ClipRadius + 0.04f);
            compassModel.Draw(Matrix.TS(pos + dir * (terrain.ClipRadius + 0.01f) + Vec3.Up*0.02f, 0.4f));
            Pose uiPose = new Pose(menuAt, Quat.LookDir(lookat));

            UI.WindowBegin("TerrainOptions", ref uiPose, new Vec2(30,0) * Units.cm2m, false);

            // Show location buttons
            Vec2 btnSize = new Vec2(6, 3) * Units.cm2m;
            if (UI.Radio("Kauai", locationId == 0, btnSize))
                LoadLocation(0);

            UI.SameLine();
            if (UI.Radio("Grand Canyon", locationId == 1, btnSize))
                LoadLocation(1);

            UI.SameLine();
            if (UI.Radio("Mt. Everest", locationId == 2, btnSize))
                LoadLocation(2);

            UI.SameLine();
            if (UI.Radio("Machu Picchu", locationId == 3, btnSize))
                LoadLocation(3);

            // Scale slider to zoom in and out
            if (UI.HSlider("Scale", ref uiWorldScale, 0.00002f, 0.00004f, 0, 27*Units.cm2m))
                SetScale(uiWorldScale);

            UI.WindowEnd();
        }));

        StereoKitApp.Shutdown();
    }

    ///////////////////////////////////////////
    
    static void SetScale(float newScale)
    {
        // Set the terrain dimensions with the new scale
        terrain.SetHeightDimensions(mapHeightSize  *newScale, mapHeightCenter*newScale);
        terrain.SetColorDimensions (mapColorSize.XZ*newScale, mapColorCenter *newScale);

        // Bring out translation into geographical space, and then scale it
        // back down into the new scale
        Vec3 geoTranslation = terrain.Translation / worldScale;
        terrain.Translation = geoTranslation * newScale;
        terrain.ClipCenter = -terrain.Translation;

        worldScale = newScale;
    }

    ///////////////////////////////////////////
    
    static void LoadLocation(int id)
    {
        if (locationId == id)
            return;
        locationId = id;

        mapColor  = null;
        mapHeight = null;
        terrain.SetColorData (Default.Tex,      Vec2.Zero, Vec2.Zero);
        terrain.SetHeightData(Default.TexBlack, Vec3.Zero, Vec2.Zero);
        terrain.Translation = Vec3.Zero;
        terrain.ClipCenter  = Vec3.Zero;

        RequestColor (locationQueries[id]);
        RequestHeight(locationQueries[id]);
    }

    ///////////////////////////////////////////

    static async Task RequestColor(BoundingBox regionBounds)
    {
        var request = new ImageryRequest() {
            MapArea     = regionBounds,
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

        if (meta.StatusCode != 200)
        {
            Log.Warn("Bing Maps API error:\n" + string.Join('\n', meta.ErrorDetails));
            return;
        }

        MemoryStream stream = null;
        if (color is MemoryStream) stream = color as MemoryStream;
        else color.CopyTo(stream);

        mapColor = Tex.FromMemory(stream.ToArray());
        mapColor.AddressMode = TexAddress.Clamp;

        BoundingBox bounds = new BoundingBox(meta.ResourceSets[0].Resources[0].BoundingBox);
        Geo.BoundsToWorld(regionBounds, bounds, out mapColorSize, out mapColorCenter);
        terrain.SetColorData(mapColor, mapColorSize.XZ*worldScale, mapColorCenter * worldScale);

        if (mapColor == null)
            Log.Warn(Encoding.ASCII.GetString(stream.ToArray()));
    }

    ///////////////////////////////////////////

    static async Task RequestHeight(BoundingBox regionBounds)
    {
        var request = new ElevationRequest() {
            Bounds      = regionBounds,
            Row         = 32,
            Col         = 32,
            BingMapsKey = _ApiKey
        };
        Response response = await request.Execute();

        if (response.StatusCode != 200)
        {
            Log.Warn("Bing Maps API error:\n" + string.Join('\n', response.ErrorDetails));
            return;
        }

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

        Geo.BoundsToWorld(regionBounds, regionBounds, out mapHeightSize, out mapHeightCenter);
        terrain.SetHeightData(mapHeight, mapHeightSize * worldScale, mapHeightCenter * worldScale);
    }

    ///////////////////////////////////////////
}

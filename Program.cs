using System;
using System.Configuration;
using BingMapsRESTToolkit;
using StereoKit;

class Program
{
    // ConfigurationManager is in the NuGet package: 
    // System.Configuration.ConfigurationManager

    // You can get a Bing Maps API key here:
    // https://www.bingmapsportal.com/Application
    // Then in App.config, you can replace "[Your API key here]" with your 
    // own key!
    static private string ApiKey = ConfigurationManager.AppSettings.Get("BingMapsKey");

    static BoundingBox[] locationQueries = new BoundingBox[] {
        Geo.LatLonBounds( 22,    -159.5, 20000), // LatLon of Kauai
        Geo.LatLonBounds( 36.3, -112.75, 10000), // LatLon of Grand Canyon
        Geo.LatLonBounds( 27.98,  86.92, 10000), // LatLon of Everest
        Geo.LatLonBounds(-13.16, -72.54, 10000), // LatLon of Machu Picchu
    };

    static Vec2        mapHeightCenter;
    static Vec3        mapHeightSize;

    static Vec2        mapColorCenter;
    static Vec3        mapColorSize;

    static Terrain     terrain;
    static float       worldScale   = 0.00004f;
    static float       uiWorldScale = 0.00004f;
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
        if (!StereoKitApp.Initialize("StereoKit_BingMaps", Runtime.Flatscreen))
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
            if (UI.HSlider("Scale", ref uiWorldScale, 0.00003f, 0.00005f, 0, 27*Units.cm2m))
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

        terrain.SetColorData (Default.Tex,      Vec2.Zero, Vec2.Zero);
        terrain.SetHeightData(Default.TexBlack, Vec3.Zero, Vec2.Zero);
        terrain.Translation = Vec3.Zero;
        terrain.ClipCenter  = Vec3.Zero;

        BingMaps.RequestColor(ApiKey, ImageryType.Aerial, locationQueries[id], (tex, size, center) => {
            mapColorSize   = size;
            mapColorCenter = center;
            terrain.SetColorData(tex, size.XZ*worldScale, center*worldScale);
        }).ConfigureAwait(false);

        BingMaps.RequestHeight(ApiKey, locationQueries[id], (tex, size, center) => {
            mapHeightSize   = size;
            mapHeightCenter = center;
            terrain.SetHeightData(tex, size*worldScale, center*worldScale);
        }).ConfigureAwait(false);
    }
}

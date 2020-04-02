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
    static int locationId = -1;

    static Vec2        mapHeightCenter;
    static Vec3        mapHeightSize;

    static Vec2        mapColorCenter;
    static Vec3        mapColorSize;

    static Terrain     terrain;
    static float       terrainScale   = 0.00004f;
    static float       uiTerrainScale = 0.00004f;
    static Vec3        terrainDrag;
    static Pose        terrainPose    = new Pose(0, 0, -0.5f, Quat.Identity);
    
    static Model pedestalModel;
    static Model compassModel;
    static Model widgetModel;

    static Vec3 justStart;
    static Vec3 justFinger;
    static bool dragActive;

    ///////////////////////////////////////////

    static void Main(string[] args)
    {
        // Initialize the StereoKit application
        StereoKitApp.settings.assetsFolder = "Assets";
        if (!StereoKitApp.Initialize("StereoKit_BingMaps", Runtime.MixedReality))
            Environment.Exit(1);

        Initialize();
        
        while (StereoKitApp.Step(() =>
        {   
            float pedestalScale  = terrain.ClipRadius*2;
            UI.AffordanceBegin("Terrain", ref terrainPose, pedestalModel.Bounds*pedestalScale, false, UIMove.PosOnly);
            UI.AffordanceEnd();
            pedestalModel.Draw(Matrix.TS(terrainPose.position, pedestalScale), Color.White * 0.25f);

            Hand hand = Input.Hand(Handed.Right);
            Vec3 widgetPos = 
                hand[FingerId.Index, JointId.Tip].position * 0.5f + 
                hand[FingerId.Thumb, JointId.Tip].position * 0.5f;
            if (dragActive || Vec2.DistanceSq(widgetPos.XZ, terrainPose.position.XZ) < terrain.ClipRadius*terrain.ClipRadius && widgetPos.y > terrainPose.position.y) { 
                widgetModel.Draw(Matrix.TS(widgetPos, dragActive?1.5f:1), Color.White * (dragActive ?1.5f:1f));
                if (!UI.IsInteracting(Handed.Right) && hand.IsJustPinched) 
                {
                    justStart  = terrainDrag;
                    justFinger = hand[FingerId.Thumb, JointId.Tip].position;
                    dragActive = true;
                }
                if (dragActive && hand.IsPinched)
                {
                    Vec3 newPos = justStart + (hand[FingerId.Thumb, JointId.Tip].position - justFinger);
                    newPos.y = 0;
                    terrainDrag = newPos;
                }
                if (hand.IsJustUnpinched)
                    dragActive = false;
            }

            terrain.Translation = terrainPose.position + terrainDrag;
            terrain.ClipCenter = -terrainDrag;
            terrain.Update();

            Hierarchy.Push(terrainPose.ToMatrix());
            Vec3 pos = Vec3.Zero;
            Vec3 dir = (Input.Head.position.XZ - terrainPose.position.XZ).Normalized().X0Y;

            float angle = (int)(dir.XZ.Angle() / 60) * 60 + 30;
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
            if (UI.HSlider("Scale", ref uiTerrainScale, 0.00003f, 0.00005f, 0, 27*Units.cm2m))
                SetScale(uiTerrainScale);

            UI.WindowEnd();
            Hierarchy.Pop();
        }));

        StereoKitApp.Shutdown();
    }

    ///////////////////////////////////////////
    
    static void Initialize()
    {
        pedestalModel = Model.FromFile("Pedestal.glb", Default.ShaderUI);
        compassModel  = Model.FromFile("Compass.glb");
        widgetModel   = Model.FromFile("MoveWidget.glb");

        terrain = new Terrain(128, 1, 3);
        terrain.ClipRadius = 0.3f;
        
        LoadLocation(0);
    }

    ///////////////////////////////////////////
    
    static void SetScale(float newScale)
    {
        // Set the terrain dimensions with the new scale
        terrain.SetHeightDimensions(mapHeightSize  *newScale, mapHeightCenter*newScale);
        terrain.SetColorDimensions (mapColorSize.XZ*newScale, mapColorCenter *newScale);

        // Bring out translation into geographical space, and then scale it
        // back down into the new scale
        Vec3 geoTranslation = terrainDrag / terrainScale;
        terrainDrag = geoTranslation * newScale;
        terrain.Translation = terrainPose.position + terrainDrag;
        terrain.ClipCenter = -terrainDrag;

        terrainScale = newScale;
    }

    ///////////////////////////////////////////
    
    static void LoadLocation(int id)
    {
        if (locationId == id)
            return;
        locationId = id;

        terrain.SetColorData (Default.Tex,      Vec2.Zero, Vec2.Zero);
        terrain.SetHeightData(Default.TexBlack, Vec3.Zero, Vec2.Zero);
        terrain.Translation = terrainPose.position;
        terrain.ClipCenter  = Vec3.Zero;
        terrainDrag = Vec3.Zero;

        BingMaps.RequestColor(ApiKey, ImageryType.Aerial, locationQueries[id], (tex, size, center) => {
            mapColorSize   = size;
            mapColorCenter = center;
            terrain.SetColorData(tex, size.XZ*terrainScale, center*terrainScale);
        }).ConfigureAwait(false);

        BingMaps.RequestHeight(ApiKey, locationQueries[id], (tex, size, center) => {
            mapHeightSize   = size;
            mapHeightCenter = center;
            terrain.SetHeightData(tex, size*terrainScale, center*terrainScale);
        }).ConfigureAwait(false);
    }
}

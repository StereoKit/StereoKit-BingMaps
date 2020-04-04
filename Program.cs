using System;
using BingMapsRESTToolkit;
using StereoKit;

class Program
{
    // You can get a Bing Maps API key here:
    // https://www.bingmapsportal.com/Application
    static private string ApiKey = "[Your API Key Here]";

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
    static float       uiAngle        = 0;
    
    static Model pedestalModel;
    static Model compassModel;
    static Model widgetModel;

    static Vec3 justStart;
    static Vec3 justFinger;
    static bool dragActive;

    static Mesh     floorMesh;
    static Material floorMat;

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
            floorMesh?.Draw(floorMat, Matrix.T(0,-1.5f,0));

            ShowPedestalControls();
            
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

        // Add a floor if we're in VR, and hide the hands if we're in AR!
        if (StereoKitApp.System.displayType == Display.Opaque) 
        { 
            floorMesh = Mesh.GeneratePlane(new Vec2(10, 10));
            floorMat  = Default.Material.Copy();
            floorMat[MatParamName.DiffuseTex] = Tex.FromFile("floor.png");
            floorMat[MatParamName.TexScale  ] = 8;
        }
        else
        {
            Input.HandVisible(Handed.Max, false);
        }
        
        LoadLocation(0);
    }

    ///////////////////////////////////////////
    
    static Vec3 CalcPedestalUIDir()
    {
        // Get the angle from the center of the pedestal to the user's head,
        // flatten it on the Y axis, and normalize it for angle calculations.
        Vec3 dir = Input.Head.position - terrainPose.position;
        dir = dir.XZ.Normalized().X0Y;

        // Use a 'sticky' algorithm for updating the angle of the UI. We snap
        // to increments of 60 degrees, but only do it after we've traveled 
        // 20 degrees into the next increment. This prevents the UI from
        // moving back and forth when the user is wiggling around at the edge
        // of a snap increment.
        const float snapAngle    = 60;
        const float stickyAmount = 20;
        float angle = dir.XZ.Angle();
        if (SKMath.AngleDist(angle, uiAngle) > snapAngle/2 + stickyAmount)
            uiAngle = (int)(angle/snapAngle) * snapAngle + snapAngle/2;

        // Turn the angle back into a direction we can use to position the
        // pedestal
        return Vec3.AngleXZ(uiAngle);
    }

    ///////////////////////////////////////////
    
    static void ShowPedestalControls()
    {
        float pedestalScale = terrain.ClipRadius * 2;
        UI.AffordanceBegin("Terrain", ref terrainPose, pedestalModel.Bounds*pedestalScale, false, UIMove.PosOnly);
        pedestalModel.Draw(Matrix.TS(Vec3.Zero, pedestalScale));

        Vec3 uiDir  = CalcPedestalUIDir();
        Pose uiPose = new Pose(uiDir * (terrain.ClipRadius + 0.04f), Quat.LookDir(uiDir+Vec3.Up));
        compassModel.Draw(Matrix.TS(uiDir * (terrain.ClipRadius + 0.01f) + Vec3.Up * 0.02f, 0.4f));
        UI.WindowBegin("TerrainOptions", ref uiPose, new Vec2(30,0) * Units.cm2m, false);

        // Show location buttons
        Vec2 btnSize = new Vec2(6, 3) * Units.cm2m;
        if (UI.Radio("Kauai",        locationId == 0, btnSize)) LoadLocation(0);
        UI.SameLine();
        if (UI.Radio("Grand Canyon", locationId == 1, btnSize)) LoadLocation(1);
        UI.SameLine();
        if (UI.Radio("Mt. Everest",  locationId == 2, btnSize)) LoadLocation(2);
        UI.SameLine();
        if (UI.Radio("Machu Picchu", locationId == 3, btnSize)) LoadLocation(3);

        // Scale slider to zoom in and out
        if (UI.HSlider("Scale", ref uiTerrainScale, 0.00003f, 0.00005f, 0, 27*Units.cm2m))
            SetScale(uiTerrainScale);

        UI.WindowEnd();

        UI.AffordanceEnd();
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

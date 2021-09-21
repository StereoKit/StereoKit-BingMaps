// This is the application logic! Before you try to run this code, make sure
// you create and add your Bing Maps API key, check the top of the Program 
// class for a link, and where you need to add the key.
//
// The experience focuses around a terrain map placed on a circular pedestal!
// The pedestal itself can move around, and has a small UI attached to
// control the map a bit. The map can change locations, and pan/scale around.
//
// This file contains the code for the pedestal and associated UI and 
// interaction. The Terrain and Bing Maps portions are separated off into 
// their own files!

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

	static Terrain terrain;
	static float   terrainScale = 0.00004f;
	static Pose    terrainPose  = new Pose(0, 0, -0.5f, Quat.Identity);
	static Vec2    mapHeightCenter;
	static Vec3    mapHeightSize;
	static Vec2    mapColorCenter;
	static Vec3    mapColorSize;

	static Model pedestalModel;
	static Model compassModel;
	static Model widgetModel;
	static float uiAngle = 0;

	static Vec3 dragStart;
	static Vec3 dragWidgetStart;
	static bool dragActive;

	static Mesh     floorMesh;
	static Material floorMat;

	///////////////////////////////////////////

	static void Main(string[] args)
	{
		// Initialize the StereoKit application
		SKSettings settings = new SKSettings
		{
			appName      = "StereoKit_BingMaps",
			assetsFolder = "Assets",
		};
		if (!SK.Initialize(settings))
			Environment.Exit(1);

		Initialize();

		while (SK.Step(() =>
		{
			// If we're in AR, we don't initialize floorMesh, hence the '?'
			// operator! The real world should already have a floor :)
			floorMesh?.Draw(floorMat, Matrix.T(0,-1.5f,0));

			// Draw the terrain widget!
			ShowTerrainWidget();
		}));

		SK.Shutdown();
	}

	///////////////////////////////////////////

	static void Initialize()
	{
		// Art resources for the terrain widget
		pedestalModel = Model.FromFile("Pedestal.glb", Shader.UI);
		compassModel  = Model.FromFile("Compass.glb");
		widgetModel   = Model.FromFile("MoveWidget.glb");

		terrain = new Terrain(
			chunkDetail: 64, 
			chunkSize:   0.6f, 
			chunkGrid:   2);
		terrain.clipRadius = 0.3f;

		// Add a floor if we're in VR, and hide the hands if we're in AR!
		if (SK.System.displayType == Display.Opaque) 
		{ 
			floorMesh = Mesh.GeneratePlane(new Vec2(10, 10));
			floorMat  = Default.Material.Copy();
			floorMat[MatParamName.DiffuseTex] = Tex.FromFile("floor.png");
			floorMat[MatParamName.TexScale  ] = 8;
		}
		else
		{
			Default.MaterialHand[MatParamName.ColorTint] = Color.Black;
			//Input.HandVisible(Handed.Max, false);
		}

		// Load up an initial, default location
		LoadLocation(0);
	}

	///////////////////////////////////////////

	static void ShowTerrainWidget()
	{
		// Create an affordance for the pedestal that the terrain and UI will
		// rest on. The user can drag this around the environment, but it 
		// doesn't rotate at all. The pedestal model asset has a diameter of 
		// 1, or radius of 0.5, so the proper scale is radius * 2!
		float pedestalScale = terrain.clipRadius * 2;
		UI.HandleBegin("TerrainWidget", ref terrainPose, pedestalModel.Bounds*pedestalScale, false, UIMove.PosOnly);
		pedestalModel.Draw(Matrix.S(pedestalScale));

		// We've got a simple UI attached to the pedestal, just a list of 
		// places we can display, and a scale slider. It'll face towards the
		// user at fixed intervals, and won't slide around. This means it's
		// easy to access, but not hard to touch.
		Vec3 uiDir  = CalcPedestalUIDir();
		Pose uiPose = new Pose(uiDir * (terrain.clipRadius + 0.04f), Quat.LookDir(uiDir+Vec3.Up));
		compassModel.Draw(Matrix.TS(uiDir * (terrain.clipRadius + 0.01f) + Vec3.Up * 0.02f, 0.4f));
		UI.WindowBegin("TerrainOptions", ref uiPose, new Vec2(30,0) * Units.cm2m, UIWin.Empty);

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
		float uiScale = terrainScale;
		if (UI.HSlider("Scale", ref uiScale, 0.00003f, 0.00005f, 0, 27*Units.cm2m, UIConfirm.Pinch))
		{ 
			SetScale(uiScale);
		}

		UI.WindowEnd(); // End TerrainOptions

		// Now we'll display the terrain on top of the pedestal!
		ShowTerrain();

		UI.HandleEnd(); // End TerrainWidget
	}

	///////////////////////////////////////////

	static Vec3 CalcPedestalUIDir()
	{
		// Get the angle from the center of the pedestal to the user's head,
		// flatten it on the Y axis, and normalize it for angle calculations.
		Vec3 dir = Input.Head.position - terrainPose.position;
		dir = dir.XZ.Normalized.X0Y;

		// Use a 'sticky' algorithm for updating the angle of the UI. We snap
		// to increments of 60 degrees, but only do it after we've traveled 
		// 20 degrees into the next increment. This prevents the UI from
		// moving back and forth when the user is wiggling around at the edge
		// of a snap increment.
		const float snapAngle    = 60;
		const float stickyAmount = 20;
		float angle = dir.XZ.Angle();
		if (SKMath.AngleDist(angle, uiAngle) > snapAngle/2 + stickyAmount)
		{ 
			uiAngle = (int)(angle/snapAngle) * snapAngle + snapAngle/2;
		}

		// Turn the angle back into a direction we can use to position the
		// pedestal
		return Vec3.AngleXZ(uiAngle);
	}

	///////////////////////////////////////////

	static void ShowTerrain()
	{
		// The first part of this method is dragging the terrain itself around
		// on the pedestal! Then after that, we can draw it :)

		// Here we're getting hand information that we'll use to calculate
		// the user's hand drag action.
		Hand hand      = Input.Hand(Handed.Right);
		Vec3 widgetPos = Hierarchy.ToLocal(
			hand[FingerId.Index, JointId.Tip].position * 0.5f + 
			hand[FingerId.Thumb, JointId.Tip].position * 0.5f);
		bool handInVolume = widgetPos.y > 0
				&& widgetPos.XZ.Magnitude < terrain.clipRadius; // For speed, use MagnitudeSq and clipRadius^2

		if (dragActive || handInVolume) 
		{
			// Render a little compass widget between the fingers, as an 
			// indicator that users can grab/pinch it to move the map. We
			// also scale and brighten the widget when it's active.
			float activeMod = dragActive ? 1.5f : 1;
			widgetModel.Draw(Matrix.TS(widgetPos, activeMod), Color.White*activeMod);

			// UI.IsInteracting tells us if an existing UI element is active.
			// If so, we don't want to steal focus from it, and can ignore
			// this IsJustPinched.
			if (!UI.IsInteracting(Handed.Right) && hand.IsJustPinched) 
			{
				// Save the initial positions, so we can calculate the drag
				// vector relative to the start point.
				dragStart       = terrain.LocalPosition;
				dragWidgetStart = widgetPos;
				dragActive      = true;
			}

			if (dragActive && hand.IsPinched)
			{
				// Update the terrain based on the current drag amount.
				Vec3 newPos = dragStart + (widgetPos - dragWidgetStart);
				newPos.y = 0;
				terrain.LocalPosition = newPos;
			}

			// Done with dragging!
			if (hand.IsJustUnpinched)
				dragActive = false;
		}

		// Update and draw the terrain itself
		terrain.Update();
	}

	///////////////////////////////////////////

	static void SetScale(float newScale)
	{
		// Set the terrain dimensions with the new scale
		terrain.SetHeightmapDimensions(mapHeightSize  *newScale, mapHeightCenter*newScale);
		terrain.SetColormapDimensions (mapColorSize.XZ*newScale, mapColorCenter *newScale);

		// Bring out translation into geographical space, and then scale it
		// back down into the new scale
		Vec3 geoTranslation = terrain.LocalPosition / terrainScale;
		terrain.LocalPosition = geoTranslation * newScale;

		terrainScale = newScale;
	}

	///////////////////////////////////////////

	static void LoadLocation(int id)    
	{
		if (locationId == id) return;
		locationId = id;

		// Reset data first, set terrain data values back to default!
		terrain.SetColormapData (Default.Tex,      Vec2.Zero, Vec2.Zero);
		terrain.SetHeightmapData(Default.TexBlack, Vec3.Zero, Vec2.Zero);
		terrain.LocalPosition = Vec3.Zero;

		// Now request color and height data from the Bing Maps API, and when
		// it receives the results, store the values and setup the terrain!

		BingMaps.RequestColor(ApiKey, ImageryType.Aerial, locationQueries[id], (tex, size, center) => {
			mapColorSize   = size;
			mapColorCenter = center;
			terrain.SetColormapData(tex, size.XZ*terrainScale, center*terrainScale);
		}).ConfigureAwait(false);

		BingMaps.RequestHeight(ApiKey, locationQueries[id], (tex, size, center) => {
			mapHeightSize   = size;
			mapHeightCenter = center;
			terrain.SetHeightmapData(tex, size*terrainScale, center*terrainScale);
		}).ConfigureAwait(false);
	}
}

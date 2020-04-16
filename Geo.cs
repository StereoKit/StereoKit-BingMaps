// This file contains logic and math for working with latitude and longitude
// values. Bing Maps uses lat/lon for many of their parameters, and these 
// aren't always intuitive or simple to draw them on screen in 3D. So these
// functions are largely just converting values to and from 2D grids of 
// meters.

using BingMapsRESTToolkit;
using StereoKit;
using System;

static class Geo
{
	public const double EarthCircumference = 40075040.0;
	public const float  EarthTallest       = 8848.0f; // Mount everest is 8,848m tall

	///////////////////////////////////////////

	public static double DistLatitude(double a, double b)
		=> (EarthCircumference * (a-b)) / 360.0;

	///////////////////////////////////////////

	public static double DistLongitude(double a, double b, double latitudeY)
		=> ((a-b) * EarthCircumference * Math.Cos(latitudeY*Units.deg2rad)) / 360.0; 

	///////////////////////////////////////////

	public static BoundingBox LatLonBounds(double latitudeY, double longitudeX, double radiusM)
	{
		double radiusY = (radiusM * 360) / EarthCircumference;
		double radiusX = (radiusM * 360) / (EarthCircumference * Math.Cos(latitudeY * Units.deg2rad));

		// In order of South, West, North, East
		return new BoundingBox(new double[]{ 
			latitudeY -radiusY, 
			longitudeX-radiusX, 
			latitudeY +radiusY, 
			longitudeX+radiusX });
	}

	///////////////////////////////////////////

	public static Vec2 BoundsSize(BoundingBox bounds)
	{
		return new Vec2(
			(float)DistLongitude(bounds.EastLongitude, bounds.WestLongitude, (bounds.NorthLatitude + bounds.SouthLatitude) / 2),
			(float)DistLatitude (bounds.NorthLatitude, bounds.SouthLatitude));
	}

	///////////////////////////////////////////

	public static void BoundsToWorld(BoundingBox queryBox, BoundingBox givenBox, out Vec3 size, out Vec2 offset)
	{
		Vec2 queryCenter = new Vec2(
			(float)(queryBox.WestLongitude + queryBox.EastLongitude) / 2.0f,
			(float)(queryBox.NorthLatitude + queryBox.SouthLatitude) / 2.0f);
		Vec2 givenCenter = new Vec2(
			(float)(givenBox.WestLongitude + givenBox.EastLongitude) / 2.0f,
			(float)(givenBox.NorthLatitude + givenBox.SouthLatitude) / 2.0f);
		offset = new Vec2(
			(float)DistLongitude(givenCenter.x, queryCenter.x, queryCenter.y),
			(float)DistLatitude (givenCenter.y, queryCenter.y));
		size = Vec3.Zero;
		size.XZ = BoundsSize(givenBox);
		size.y  = EarthTallest;
	}
}

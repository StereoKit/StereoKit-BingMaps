using BingMapsRESTToolkit;
using StereoKit;
using System;

static class Geo
{
    const double EarthCircumference = 40075040.0;

    ///////////////////////////////////////////

    public static double DistLatitude(double a, double b)
        => (EarthCircumference * (a - b)) / 360.0;

    ///////////////////////////////////////////
    
    public static double DistLongitude(double a, double b, double latitude)
        => ((a - b) * EarthCircumference * Math.Cos(latitude * (Math.PI / 180.0))) / 360.0;

    ///////////////////////////////////////////

    public static Vec2 BoundsSize(BoundingBox bounds)
    {
        return new Vec2(
            (float)DistLongitude(bounds.EastLongitude, bounds.WestLongitude, (bounds.NorthLatitude + bounds.SouthLatitude) / 2),
            (float)DistLatitude(bounds.NorthLatitude, bounds.SouthLatitude));
    }

    ///////////////////////////////////////////
    
    public static void BoundsToWorld(BoundingBox queryBox, BoundingBox givenBox, float scale, out Vec3 size, out Vec2 offset)
    {
        Vec2 queryCenter = new Vec2(
            (float)(queryBox.WestLongitude + queryBox.EastLongitude) / 2.0f,
            (float)(queryBox.NorthLatitude + queryBox.SouthLatitude) / 2.0f);
        Vec2 givenCenter = new Vec2(
            (float)(givenBox.WestLongitude + givenBox.EastLongitude) / 2.0f,
            (float)(givenBox.NorthLatitude + givenBox.SouthLatitude) / 2.0f);
        offset = new Vec2(
            (float)DistLongitude(givenCenter.x, queryCenter.x, queryCenter.y),
            (float)DistLatitude (givenCenter.y, queryCenter.y)) * scale;
        size = Vec3.Zero;
        size.XZ = BoundsSize(givenBox) * scale;
        size.y  = 9000 * scale * 4;
    }
}

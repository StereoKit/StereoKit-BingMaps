using BingMapsRESTToolkit;
using StereoKit;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

static class BingMaps
{
    ///////////////////////////////////////////

    public static async Task RequestColor(string apiKey, ImageryType imageryType, BoundingBox regionBounds, Action<Tex, Vec3, Vec2> OnReceivedColor)
    {
        var request = new ImageryRequest() {
            MapArea     = regionBounds,
            MapWidth    = 1024,
            MapHeight   = 1024,
            ImagerySet  = imageryType,
            BingMapsKey = apiKey
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

        Tex mapColor = Tex.FromMemory(stream.ToArray());
        if (mapColor == null)
        { 
            Log.Warn(Encoding.ASCII.GetString(stream.ToArray()));
            return;
        }
        mapColor.AddressMode = TexAddress.Clamp;

        BoundingBox bounds = new BoundingBox(meta.ResourceSets[0].Resources[0].BoundingBox);
        Geo.BoundsToWorld(regionBounds, bounds, out Vec3 mapColorSize, out Vec2 mapColorCenter);
        OnReceivedColor(mapColor, mapColorSize, mapColorCenter);
    }

    ///////////////////////////////////////////

    public static async Task RequestHeight(string apiKey, BoundingBox regionBounds, Action<Tex, Vec3, Vec2> OnReceivedHeight)
    {
        var request = new ElevationRequest() {
            Bounds      = regionBounds,
            Row         = 32,
            Col         = 32,
            BingMapsKey = apiKey
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
            float height = data.Elevations[x+y*32] / Geo.EarthTallest;
            heights[x+(31-y)*32] = Color.White * height;
        }}

        Tex mapHeight = new Tex(TexType.ImageNomips, TexFormat.Rgba128);
        mapHeight.SetColors(32, 32, heights);
        mapHeight.AddressMode = TexAddress.Clamp;

        Geo.BoundsToWorld(regionBounds, regionBounds, out Vec3 size, out Vec2 center);
        OnReceivedHeight(mapHeight, size, center);
    }

    ///////////////////////////////////////////
}

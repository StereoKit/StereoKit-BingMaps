// This file contains a pair of Bing Maps API requests! One for color, and 
// one for elevation. It'll execute these asynchronously, so they don't block
// the application, and will notify a callback method with a texture 
// generated from the data, as well as the size of the data received.
//
// The data structures ImageryRequest and ElevationRequest have a whole pile
// of parameters packed away in them, so these are great places to start 
// messing around to see what additional information and configuration you 
// can get!

using BingMapsRESTToolkit;
using StereoKit;
using System;
using System.IO;
using System.Threading.Tasks;

static class BingMaps
{
	///////////////////////////////////////////

	public static async Task RequestColor(string apiKey, ImageryType imageryType, BoundingBox regionBounds, Action<Tex, Vec3, Vec2> OnReceivedColor)
	{
		// Request an image from the maps API! This is the request package
		// that gets sent to the server, details about the arguments can be
		// found here:
		// https://github.com/microsoft/BingMapsRESTToolkit/blob/master/Docs/API%20Reference.md#ImageryRequest
		ImageryRequest request = new ImageryRequest() {
			MapArea     = regionBounds,
			MapWidth    = 1024,
			MapHeight   = 1024,
			ImagerySet  = imageryType,
			BingMapsKey = apiKey
		};
		// We need the meta response as well as the image response, since the
		// image API doesn't care too much about what we actually request! 
		// The results it sends back can differ in size, bounds, image format,
		// so we need to know what we got!
		Task<Response> metaTask  = ServiceManager.GetResponseAsync(request);
		Task<Stream>   colorTask = ServiceManager.GetImageAsync   (request);
		await Task.WhenAll(metaTask, colorTask);
		Response meta   = await metaTask;
		Stream   stream = await colorTask;

		// StatusCode is a web response status code, where 200-299 means 
		// success. Details here:
		// https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
		if (meta.StatusCode < 200 || meta.StatusCode >= 300)
		{
			Log.Warn("Bing Maps API error:\n" + string.Join('\n', meta.ErrorDetails));
			return;
		}

		// We need the result as a MemoryStream so we can grab the result as
		// an array of bytes.
		MemoryStream memStream = null;
		if (stream is MemoryStream) memStream = (MemoryStream)stream;
		else stream.CopyTo(memStream);

		// Send the image over to StereoKit, and turn it into a texture!
		Tex texture = Tex.FromMemory(memStream.ToArray());
		texture.AddressMode = TexAddress.Mirror;

		// Convert the image's bounds from lat/lon information into our 
		// world's meters.
		BoundingBox bounds = new BoundingBox(meta.ResourceSets[0].Resources[0].BoundingBox);
		Geo.BoundsToWorld(regionBounds, bounds, out Vec3 size, out Vec2 center);

		// Done! Pass the results back.
		OnReceivedColor(texture, size, center);
	}

	///////////////////////////////////////////

	public static async Task RequestHeight(string apiKey, BoundingBox regionBounds, Action<Tex, Vec3, Vec2> OnReceivedHeight)
	{
		// Here's an elevation request! This doesn't provide an image, rather,
		// it gives us a grid of height values. It's limited to a maximum of
		// 1024 values per request, so we're only asking for a grid of 32x32
		// elevations.
		// However! This request does work exactly within the bounds provided,
		// so we're getting what we expect from the results of this request.
		// Details about the request can be found here:
		// https://github.com/microsoft/BingMapsRESTToolkit/blob/master/Docs/API%20Reference.md#ElevationRequest
		ElevationRequest request = new ElevationRequest() {
			Bounds      = regionBounds,
			Row         = 32,
			Col         = 32,
			BingMapsKey = apiKey
		};
		Response response = await ServiceManager.GetResponseAsync(request);

		// StatusCode is a web response status code, where 200-299 means 
		// success. Details here:
		// https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
		if (response.StatusCode < 200 || response.StatusCode >= 300)
		{
			Log.Warn("Bing Maps API error:\n" + string.Join('\n', response.ErrorDetails));
			return;
		}

		// Convert the elevation data we've received into a grayscale heightmap texture!
		ElevationData data    = response.ResourceSets[0].Resources[0] as ElevationData;
		Color[]       heights = new Color[32 * 32];
		for (int y = 0; y < 32; y++) {
		for (int x = 0; x < 32; x++) {
			float height = data.Elevations[x+y*32] / Geo.EarthTallest;
			// Height data is provided upside-down, so we're flipping it with
			// this index on the Y axis.
			heights[x+(31-y)*32] = Color.White * height;
		}}

		// Create a texture from the elevation data! We're storing it as 
		// Rgba128 to preserve floating point precision in the height values.
		Tex texture = new Tex(TexType.ImageNomips, TexFormat.Rgba128);
		texture.SetColors(32, 32, heights);
		texture.AddressMode = TexAddress.Clamp;

		// Our bounds should be correct, but we still need it in StereoKit
		// units, so convert!
		Geo.BoundsToWorld(regionBounds, regionBounds, out Vec3 size, out Vec2 center);

		// Done! Pass the results back.
		OnReceivedHeight(texture, size, center);
	}

	///////////////////////////////////////////
}

#if ENABLE_UI_UGUI
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hotfix.Manager;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace Hotfix.UI
{
    public sealed class DifferenceRemoteLevel : IDisposable
    {
        public DifferenceLevel Level;
        public string Error;
        UnityWebRequest request;
        DifferenceBatchCache batchCache;
        Texture2D originalTexture, changedTexture;
        bool loading, cancelled;

        public IEnumerator Load(string apiUrl, int expectedNumber = 1)
        {
            if (loading) throw new InvalidOperationException("Level Loading");
            ReleaseResources();
            loading = true; cancelled = false; Error = null;
            var urls = new string[4];
            string endpointJson = null;
            var gameSize = new Vector2Int();
            try
            {
                if (!Attempt(() => urls[0] = WebUrl(apiUrl))) yield break;
                for (var step = 0; step < urls.Length; step++)
                {
                    if (cancelled) { Error = "Loading was cancelled."; yield break; }
                    if (!Attempt(() =>
                    {
                        request = UnityWebRequest.Get(urls[step]);
                        request.timeout = 20;
                    })) yield break;
                    using (var current = request)
                    {
                        UnityWebRequestAsyncOperation operation = null;
                        if (!Attempt(() => operation = current.SendWebRequest())) yield break;
                        yield return operation;
                        if (cancelled) { Error = "Loading was cancelled."; yield break; }
                        if (current.result != UnityWebRequest.Result.Success)
                        { Error = "Level download failed: " + current.error; yield break; }
                        if (!Attempt(() =>
                        {
                            if (step == 0)
                            {
                                endpointJson = current.downloadHandler.text;
                                var endpoint = ParseEndpoint(endpointJson, expectedNumber);
                                urls[1] = AssetUrl(endpoint, "level-diffs.json");
                                urls[2] = AssetUrl(endpoint, "level-base.png");
                                urls[3] = AssetUrl(endpoint, "different.png");
                            }
                            else if (step == 1) Level = Parse(apiUrl, endpointJson, current.downloadHandler.text, out gameSize, expectedNumber);
                            else
                            {
                                var texture = DecodeTexture(current.downloadHandler.data, gameSize);
                                if (step == 2) originalTexture = texture;
                                else changedTexture = texture;
                                CheckTexture(texture, gameSize);
                            }
                        })) yield break;
                    }
                    request = null;
                }
                Attempt(() => CreateSprites(Level, originalTexture, changedTexture, gameSize));
            }
            finally
            {
                if (request != null) request.Dispose();
                request = null; loading = false;
                if (Error != null || cancelled) ReleaseResources();
            }
        }

        public IEnumerator LoadMainBatch(string apiUrl, int expectedNumber = 1)
        {
            if (loading) throw new InvalidOperationException("Level Loading");
            ReleaseResources();
            loading = true; cancelled = false; Error = null;
            try
            {
                yield return null;
                if (cancelled) { Error = "Loading was cancelled."; yield break; }
                if (!Attempt(() => { WebUrl(apiUrl); batchCache = new DifferenceBatchCache(); })) yield break;
                yield return batchCache.Ensure(expectedNumber);
                if (cancelled) { Error = "Loading was cancelled."; yield break; }
                if (batchCache.Error != null) { Error = batchCache.Error; yield break; }
                string endpointJson = null, diffsJson = null;
                byte[] original = null, changed = null;
                var cache = batchCache;
                Task<bool> reading = null;
                if (!Attempt(() => reading = Task.Run(() => cache.TryRead(expectedNumber, out endpointJson, out diffsJson, out original, out changed)))) yield break;
                while (!reading.IsCompleted)
                {
                    if (cancelled) { Error = "Loading was cancelled."; yield break; }
                    yield return null;
                }
                if (cancelled) { Error = "Loading was cancelled."; yield break; }
                var readable = false;
                if (!Attempt(() => readable = reading.GetAwaiter().GetResult())) yield break;
                if (!readable)
                { Error = "The level cache is incomplete. Please try again."; yield break; }
                var size = new Vector2Int();
                if (!Attempt(() => Level = Parse(apiUrl, endpointJson, diffsJson, out size, expectedNumber))) yield break;
                yield return null;
                if (cancelled) { Error = "Loading was cancelled."; yield break; }
                yield return DecodePicture(original, size, true);
                if (Error != null) yield break;
                yield return null;
                if (cancelled) { Error = "Loading was cancelled."; yield break; }
                yield return DecodePicture(changed, size, false);
                if (Error != null) yield break;
                yield return null;
                if (cancelled) { Error = "Loading was cancelled."; yield break; }
                Attempt(() => CreateSprites(Level, originalTexture, changedTexture, size));
            }
            finally
            {
                batchCache?.Dispose(); batchCache = null;
                loading = false;
                if (Error != null || cancelled) ReleaseResources();
            }
        }

        public void Cancel()
        {
            cancelled = true;
            batchCache?.Dispose();
            if (request != null)
            {
                request.Abort(); request.Dispose(); request = null;
            }
            if (loading) ReleaseResources();
        }

        internal IEnumerator DecodePicture(byte[] data, Vector2Int size, bool original)
        {
            Task<byte[]> decoding = null;
            if (IsWebP(data))
            {
                if (!Attempt(() =>
                {
                    WebP.Texture2DExt.GetWebPDimensions(data, out var width, out var height);
                    Require(width == size.x && height == size.y && width > 0 && height > 0 &&
                        width <= SystemInfo.maxTextureSize && height <= SystemInfo.maxTextureSize, "WebP image dimensions do not match the level data or exceed the device limit.");
                    // Decode pixels on a worker; texture creation and upload stay on Unity's main thread.
                    decoding = Task.Run(() =>
                    {
                        var pixels = WebP.Texture2DExt.LoadRGBAFromWebP(data, ref width, ref height, false, out var error);
                        Require(error == WebP.Error.Success, "Unable to decode the WebP image: " + error);
                        return pixels;
                    });
                })) yield break;
                while (!decoding.IsCompleted)
                {
                    if (cancelled) { Error = "Loading was cancelled."; yield break; }
                    yield return null;
                }
            }
            if (cancelled) { Error = "Loading was cancelled."; yield break; }
            Attempt(() =>
            {
                Texture2D texture;
                if (decoding == null) texture = DecodeTexture(data, size);
                else
                {
                    var pixels = decoding.GetAwaiter().GetResult();
                    texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                    if (original) originalTexture = texture; else changedTexture = texture;
                    texture.LoadRawTextureData(pixels);
                    texture.Apply(false, true);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.filterMode = FilterMode.Bilinear;
                }
                if (original) originalTexture = texture; else changedTexture = texture;
            });
        }

        static bool IsWebP(byte[] data) => data != null && data.Length >= 12 &&
            data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
            data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P';

        public static Texture2D DecodeTexture(byte[] data, Vector2Int expectedSize)
        {
            Require(data != null && data.Length >= 12, "Image data is empty or incomplete.");
            Texture2D texture = null;
            try
            {
                // Asset keys can still end in .png when the server returns WebP bytes.
                if (IsWebP(data))
                {
                    WebP.Texture2DExt.GetWebPDimensions(data, out var width, out var height);
                    Require(width == expectedSize.x && height == expectedSize.y &&
                        width > 0 && height > 0 && width <= SystemInfo.maxTextureSize && height <= SystemInfo.maxTextureSize,
                        "WebP image dimensions do not match the level data or exceed the device limit.");
                    texture = WebP.Texture2DExt.CreateTexture2DFromWebP(data, false, false, out var error);
                    Require(error == WebP.Error.Success && texture, "Unable to decode the WebP image: " + error);
                }
                else
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    Require(texture.LoadImage(data, true), "Unable to decode the image. Supported formats: WebP, PNG and JPG.");
                }
                CheckTexture(texture, expectedSize);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                return texture;
            }
            catch { Destroy(texture); throw; }
        }

        public void Dispose()
        {
            Cancel();
            ReleaseResources();
        }

        bool Attempt(Action action)
        {
            try { action(); return true; }
            catch (Exception exception) { Error = "Invalid level data: " + exception.Message; return false; }
        }

        void ReleaseResources()
        {
            ReleaseSprites(Level);
            Destroy(originalTexture); Destroy(changedTexture);
            originalTexture = changedTexture = null; Level = null;
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        static void ReleaseSprites(DifferenceLevel level)
        {
            if (level == null) return;
            Destroy(level.original); Destroy(level.changed);
            if (level.croppedPatches != null)
                foreach (var patch in level.croppedPatches) Destroy(patch);
            level.original = level.changed = null; level.croppedPatches = null;
        }

        public static DifferenceLevel Parse(string apiUrl, string endpointJson, string diffsJson, out Vector2Int gameSize, int expectedNumber = 1)
        {
            WebUrl(apiUrl);
            var endpoint = ParseEndpoint(endpointJson, expectedNumber);
            var data = JSON.Parse(diffsJson);
            Require(data != null && data.IsObject, "The differences JSON must be an object.");
            Require(Text(data["schema"], "schema") == "difference_desk/greyfun-level/v1", "The differences schema is not supported.");
            Require(Text(data["level_id"], "level_id") == endpoint["level_id"].Value, "The level ID does not match.");
            var sizeNode = data["game_size"];
            Require(sizeNode.IsArray && sizeNode.Count == 2, "game_size has an invalid length.");
            gameSize = new Vector2Int(Integer(sizeNode[0], "game_size"), Integer(sizeNode[1], "game_size"));
            Require(gameSize.x > 0 && gameSize.y > 0 && (long)gameSize.x * 2 == (long)gameSize.y * 3, "Level images must have a 3:2 aspect ratio.");
            var size = new Vector2(gameSize.x, gameSize.y);
            var items = data["items"];
            Require(items != null && items.IsArray && items.Count >= 1 && items.Count <= DifferenceRound.MaxSpots, "A level must contain 1 to " + DifferenceRound.MaxSpots + " differences.");
            Require(Integer(data["expected"], "expected") == items.Count, "expected does not match the number of differences.");
            var level = new DifferenceLevel
            {
                title = data["title"].IsString ? data["title"].Value : "Level " + expectedNumber,
                contentKey = apiUrl + "|" + endpoint["level_id"].Value + "|" + endpoint["revision"].Value + "|" + Hash128.Compute(diffsJson),
                analyticsPicId = endpoint["level_id"].Value,
                analyticsPicName = AnalyticsText(data["pic_name"]) ?? AnalyticsText(endpoint["pic_name"]) ??
                    System.IO.Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(new Uri(AssetUrl(endpoint, "level-base.png")).AbsolutePath)),
                analyticsDifficulty = AnalyticsDifficulty(data["difficulty"]) ?? AnalyticsDifficulty(endpoint["difficulty"]) ?? "unknown",
                analyticsParts = new string[items.Count],
                regions = new Vector4[items.Count], hitSpots = new DifferenceSpot[items.Count], changedOnTop = false
            };
            var hasMarkers = items[0].HasKey("cirPos") || items[0].HasKey("cir_size") || items[0].HasKey("marker_size");
            if (hasMarkers) level.markerRegions = new Vector4[items.Count];
            var nodes = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Require(item != null && item.IsObject, "Each difference must be an object.");
                Require(nodes.Add(Text(item["node"], "node")), "Duplicate difference node.");
                level.analyticsParts[i] = item["node"].Value;
                var position = Numbers(item["pos"], 2, "pos", true);
                var cropSize = Numbers(item["crop_size"], 2, "crop_size", true);
                var crop = new Rect(position[0], position[1], cropSize[0], cropSize[1]);
                CheckRect(crop, gameSize, "The crop region");
                var hit = Numbers(item["hit_bounds"], 4, "hit_bounds");
                var hitRect = new Rect(hit[0], hit[1], hit[2], hit[3]);
                CheckRect(hitRect, gameSize, "The hit region");
                Require(Text(item["hit_geometry"], "hit_geometry") == "bbox-rect", "Only rectangular hit regions are supported.");
                level.regions[i] = new Vector4(crop.center.x / size[0], crop.center.y / size[1], crop.width / size[0], crop.height / size[1]);
                var radius = Mathf.Min(.25f, new Vector2(hitRect.width / size[0], hitRect.height / size[0]).magnitude * .5f);
                level.hitSpots[i] = new DifferenceSpot(hitRect.x / size[0], hitRect.y / size[1], hitRect.width / size[0], hitRect.height / size[1], radius);
                Require(hasMarkers == (item.HasKey("cirPos") || item.HasKey("cir_size") || item.HasKey("marker_size")), "Circle metadata must be supplied for every difference.");
                if (!hasMarkers) continue;
                var circlePosition = Numbers(item["cirPos"], 2, "cirPos");
                var circleSize = (float)Number(item["cir_size"], "cir_size");
                Require(circleSize > 0 && circleSize <= size.x * 2, "cir_size is out of range.");
                var center = new Vector2(circlePosition[0] + circleSize * .5f, circlePosition[1] + circleSize * .5f);
                Require(center.x >= 0 && center.x <= size.x && center.y >= 0 && center.y <= size.y, "The circle center extends beyond the image.");
                var diameter = item.HasKey("marker_size") ? (float)Number(item["marker_size"], "marker_size") : circleSize;
                if (diameter <= 0) diameter = circleSize;
                Require(diameter <= size.x * 2, "marker_size is out of range.");
                level.markerRegions[i] = new Vector4(center.x, center.y, diameter, diameter);
            }
            if (hasMarkers)
            {
                // Match the webpage's 12-pixel gap without changing rectangular click targets.
                for (var i = 0; i < items.Count; i++)
                    for (var j = i + 1; j < items.Count; j++)
                    {
                        var left = level.markerRegions[i]; var right = level.markerRegions[j];
                        var dx = (double)left.x - right.x; var dy = (double)left.y - right.y;
                        var safeDiameter = (float)Math.Max(1, Math.Floor(Math.Sqrt(dx * dx + dy * dy) - 12));
                        left.z = Mathf.Min(left.z, safeDiameter); right.z = Mathf.Min(right.z, safeDiameter);
                        level.markerRegions[i] = left; level.markerRegions[j] = right;
                    }
                for (var i = 0; i < items.Count; i++)
                {
                    var marker = level.markerRegions[i];
                    level.markerRegions[i] = new Vector4(marker.x / size.x, marker.y / size.y, marker.z / size.x, marker.z / size.y);
                }
            }
            new DifferenceRound(level.hitSpots, (float)gameSize.x / gameSize.y);
            return level;
        }

        public static void CreateSprites(DifferenceLevel level, Texture2D original, Texture2D changed, Vector2Int gameSize)
        {
            Require(level != null && level.regions != null && level.regions.Length >= 1 && level.regions.Length <= DifferenceRound.MaxSpots, "The number of difference regions must be 1 to " + DifferenceRound.MaxSpots + ".");
            CheckTexture(original, gameSize); CheckTexture(changed, gameSize);
            Require(level.original == null && level.changed == null && level.croppedPatches == null, "Level images have already been created.");
            try
            {
                original.wrapMode = changed.wrapMode = TextureWrapMode.Clamp;
                level.original = CropSprite(original, new Rect(0, 0, gameSize.x, gameSize.y));
                level.changed = CropSprite(changed, new Rect(0, 0, gameSize.x, gameSize.y));
                level.croppedPatches = new Sprite[level.regions.Length];
                for (var i = 0; i < level.regions.Length; i++)
                {
                    var region = level.regions[i];
                    var crop = new Rect(Mathf.RoundToInt((region.x - region.z * .5f) * gameSize.x),
                        Mathf.RoundToInt((region.y - region.w * .5f) * gameSize.y),
                        Mathf.RoundToInt(region.z * gameSize.x), Mathf.RoundToInt(region.w * gameSize.y));
                    level.croppedPatches[i] = CropSprite(changed, crop);
                }
            }
            catch { ReleaseSprites(level); throw; }
        }

        public static Sprite CropSprite(Texture2D texture, Rect topLeftRect)
        {
            Require(texture != null, "The crop image is missing.");
            CheckRect(topLeftRect, new Vector2Int(texture.width, texture.height), "The crop region");
            var rect = new Rect(topLeftRect.x, texture.height - topLeftRect.y - topLeftRect.height, topLeftRect.width, topLeftRect.height);
            return Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }

        internal static JSONNode ParseEndpoint(string json, int expectedNumber)
        {
            Require(expectedNumber > 0, "The level number must be a positive integer.");
            var endpoint = JSON.Parse(json);
            Require(endpoint != null && endpoint.IsObject && endpoint["ok"].IsBoolean && endpoint["ok"].AsBool, "The server returned no valid level.");
            Require(Integer(endpoint["number"], "number") == expectedNumber, "The returned level number does not match the request.");
            Require(Integer(endpoint["revision"], "revision") >= 0, "revision is invalid.");
            Text(endpoint["level_id"], "level_id");
            Require(endpoint["assets"].IsObject, "Missing assets.");
            AssetUrl(endpoint, "different.png"); AssetUrl(endpoint, "level-base.png"); AssetUrl(endpoint, "level-diffs.json");
            return endpoint;
        }

        internal static string AssetUrl(JSONNode endpoint, string name) => WebUrl(Text(endpoint["assets"][name], name));
        static string AnalyticsText(JSONNode node) => node != null && node.IsString && !string.IsNullOrWhiteSpace(node.Value) ? node.Value : null;
        static string AnalyticsDifficulty(JSONNode node)
        {
            var text = AnalyticsText(node);
            if (text != null) return text;
            return node != null && node.IsNumber && !double.IsNaN(node.AsDouble) && !double.IsInfinity(node.AsDouble)
                ? node.AsDouble.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        }
        static string WebUrl(string value)
        {
            Require(Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp), "Asset URLs must use HTTP or HTTPS.");
            return value;
        }
        static string Text(JSONNode node, string name)
        {
            Require(node != null && node.IsString && !string.IsNullOrWhiteSpace(node.Value), "Missing " + name + ".");
            return node.Value;
        }
        static int Integer(JSONNode node, string name)
        {
            var value = Number(node, name);
            Require(value >= 0 && value <= int.MaxValue && value == Math.Truncate(value), name + " must be a nonnegative integer.");
            return (int)value;
        }
        static double Number(JSONNode node, string name)
        {
            Require(node != null && node.IsNumber, name + " must be a number.");
            var value = node.AsDouble;
            Require(!double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= int.MaxValue, name + " is out of range.");
            return value;
        }
        static float[] Numbers(JSONNode node, int count, string name, bool integer = false)
        {
            Require(node != null && node.IsArray && node.Count == count, name + " has an invalid length.");
            var values = new float[count];
            for (var i = 0; i < count; i++) values[i] = integer ? Integer(node[i], name) : (float)Number(node[i], name);
            return values;
        }
        static void CheckTexture(Texture2D texture, Vector2Int size)
        { Require(texture != null && texture.width == size.x && texture.height == size.y, "Image dimensions do not match game_size."); }
        static void CheckRect(Rect rect, Vector2Int size, string name)
        {
            Require(!float.IsNaN(rect.x) && !float.IsNaN(rect.y) && !float.IsNaN(rect.width) && !float.IsNaN(rect.height) &&
                !float.IsInfinity(rect.x) && !float.IsInfinity(rect.y) && !float.IsInfinity(rect.width) && !float.IsInfinity(rect.height) &&
                rect.x >= 0 && rect.y >= 0 && rect.width > 0 && rect.height > 0 &&
                rect.xMax <= size.x && rect.yMax <= size.y, name + " extends beyond the image.");
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new FormatException(message); }

        public static void SelfCheck()
        {
            const string api = "https://example.com/api/levels/1";
            const string endpoint = "{\"ok\":true,\"number\":1,\"revision\":1,\"level_id\":\"check\",\"assets\":{\"different.png\":\"https://example.com/d.png\",\"level-base.png\":\"https://example.com/b.png\",\"level-diffs.json\":\"https://example.com/d.json\"}}";
            const string diffs = "{\"schema\":\"difference_desk/greyfun-level/v1\",\"level_id\":\"check\",\"game_size\":[1500,1000],\"expected\":1,\"items\":[{\"node\":\"D01\",\"pos\":[969,60],\"crop_size\":[204,377],\"hit_bounds\":[975,65,192,366],\"hit_geometry\":\"bbox-rect\"}]}";
            var level = Parse(api, endpoint, diffs, out var size);
            Require(level.analyticsPicId == "check" && level.analyticsPicName == "b" && level.analyticsDifficulty == "unknown" &&
                level.analyticsParts[0] == "D01", "埋点必须使用实际图片 ID、资源名和差异 node，缺失难度不得编造");
            var annotated = Parse(api, endpoint, diffs.Replace("\"expected\":1", "\"pic_name\":\"winter-scene\",\"difficulty\":2,\"expected\":1"), out size);
            Require(annotated.analyticsPicName == "winter-scene" && annotated.analyticsDifficulty == "2", "埋点应保留明确配置的图片名和难度");
            Require(size == new Vector2Int(1500, 1000) && Mathf.Abs(level.regions[0].x - 1071f / 1500) < .00001f &&
                Mathf.Abs(level.regions[0].y - 248.5f / 1000) < .00001f && !level.changedOnTop, "坐标转换自检失败");
            Require(new DifferenceRound(level.hitSpots, 1.5f).Click(976f / 1500, 66f / 1000) == 0, "矩形点击自检失败");
            Require(new DifferenceRound(level.hitSpots, 1.5f).Click(970f / 1500, 61f / 1000) == DifferenceRound.Miss, "裁剪边缘不应算命中");
            Require(level.markerRegions == null, "没有圆圈配置的旧数据应保留原来的绘制方式");
            var markerDiffs = diffs.Replace("\"node\":\"D01\"", "\"node\":\"D01\",\"cirPos\":[-40,90],\"cir_size\":200,\"marker_size\":123");
            var marked = Parse(api, endpoint, markerDiffs, out size);
            Require(Vector4.Distance(marked.markerRegions[0], new Vector4(60f / 1500, .19f, 123f / 1500, .123f)) < .00001f &&
                marked.hitSpots[0].x == level.hitSpots[0].x && marked.hitSpots[0].y == level.hitSpots[0].y,
                "圆圈必须使用 cirPos 加 cir_size 半径作为圆心，使用 marker_size 绘制，并保留原点击区域");
            var fallbackMarker = Parse(api, endpoint, markerDiffs.Replace(",\"marker_size\":123", ""), out size);
            Require(Mathf.Abs(fallbackMarker.markerRegions[0].z - 200f / 1500) < .00001f, "缺少 marker_size 时应使用 cir_size");
            var adjacentData = JSON.Parse(markerDiffs);
            adjacentData["expected"] = 2;
            var adjacent = JSON.Parse(adjacentData["items"][0].ToString());
            adjacent["node"] = "D02"; adjacent["cirPos"][0] = 60;
            adjacentData["items"].Add(adjacent);
            var pair = Parse(api, endpoint, adjacentData.ToString(), out size);
            Require(Mathf.Abs(pair.markerRegions[0].z * 1500 - 88) < .001f && Mathf.Abs(pair.markerRegions[1].z * 1500 - 88) < .001f,
                "相邻圆圈必须与网页一样保留 12 源图像素间距");
            foreach (var invalid in new[] { markerDiffs.Replace("\"cir_size\":200", "\"cir_size\":0"),
                markerDiffs.Replace("[-40,90]", "[-400,90]"), markerDiffs.Replace("\"cir_size\":200,", "") })
            {
                var invalidMarkerRejected = false;
                try { Parse(api, endpoint, invalid, out size); }
                catch (FormatException) { invalidMarkerRejected = true; }
                Require(invalidMarkerRejected, "不完整或越界的圆圈配置必须被拒绝");
            }
            var rejected = false;
            try { Parse(api, endpoint, diffs.Replace("[969,60]", "[1490,60]"), out size); }
            catch (FormatException) { rejected = true; }
            Require(rejected, "越界裁剪必须被拒绝");
            Require(level.contentKey != Parse(api, endpoint.Replace("\"revision\":1", "\"revision\":2"), diffs, out size).contentKey, "版本变化必须更换存档标识");
            var secondEndpoint = endpoint.Replace("\"number\":1", "\"number\":2");
            var secondLevel = Parse("https://example.com/api/levels/2", secondEndpoint, diffs, out size, 2);
            Require(secondLevel.title == "Level 2" && secondLevel.contentKey != level.contentKey, "应能加载独立的第 2 关");
            rejected = false;
            try { Parse(api, secondEndpoint, diffs, out size); }
            catch (FormatException) { rejected = true; }
            Require(rejected, "响应关卡编号不匹配必须被拒绝");
            rejected = false;
            try { Parse(api, endpoint, diffs, out size, 0); }
            catch (FormatException) { rejected = true; }
            Require(rejected, "非正关卡编号必须被拒绝");
            var texture = new Texture2D(6, 4);
            Sprite sprite = null;
            try
            {
                sprite = CropSprite(texture, new Rect(1, 0, 2, 1));
                Require(sprite.texture == texture && sprite.rect == new Rect(1, 3, 2, 1), "裁剪必须共享纹理并翻转 Y 坐标");
            }
            finally { Destroy(sprite); Destroy(texture); }
        }
    }
}
#endif

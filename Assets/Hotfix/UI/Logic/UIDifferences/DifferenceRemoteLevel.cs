#if ENABLE_UI_UGUI
using System;
using System.Collections;
using System.Collections.Generic;
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
        Texture2D originalTexture, changedTexture;
        bool loading, cancelled;

        public IEnumerator Load(string apiUrl, int expectedNumber = 1)
        {
            if (loading) throw new InvalidOperationException("关卡正在加载");
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
                    if (cancelled) { Error = "加载已取消"; yield break; }
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
                        if (cancelled) { Error = "加载已取消"; yield break; }
                        if (current.result != UnityWebRequest.Result.Success)
                        { Error = "关卡下载失败：" + current.error; yield break; }
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

        public void Cancel()
        {
            cancelled = true;
            if (request != null)
            {
                request.Abort(); request.Dispose(); request = null;
            }
            if (loading) ReleaseResources();
        }

        public static Texture2D DecodeTexture(byte[] data, Vector2Int expectedSize)
        {
            Require(data != null && data.Length >= 12, "图片数据为空或不完整");
            Texture2D texture = null;
            try
            {
                // Asset keys can still end in .png when the server returns WebP bytes.
                var webp = data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
                    data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P';
                if (webp)
                {
                    WebP.Texture2DExt.GetWebPDimensions(data, out var width, out var height);
                    Require(width == expectedSize.x && height == expectedSize.y &&
                        width > 0 && height > 0 && width <= SystemInfo.maxTextureSize && height <= SystemInfo.maxTextureSize,
                        "WebP 图片尺寸与关卡数据不符或超出设备限制");
                    texture = WebP.Texture2DExt.CreateTexture2DFromWebP(data, false, false, out var error);
                    Require(error == WebP.Error.Success && texture, "WebP 图片解码失败：" + error);
                }
                else
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    Require(texture.LoadImage(data, true), "图片解码失败，仅支持 WebP、PNG、JPG");
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
            catch (Exception exception) { Error = "关卡数据无效：" + exception.Message; return false; }
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
            Require(data != null && data.IsObject, "差异 JSON 必须是对象");
            Require(Text(data["schema"], "schema") == "difference_desk/greyfun-level/v1", "不支持的差异 schema");
            Require(Text(data["level_id"], "level_id") == endpoint["level_id"].Value, "关卡 ID 不一致");
            var sizeNode = data["game_size"];
            Require(sizeNode.IsArray && sizeNode.Count == 2, "game_size 长度不正确");
            gameSize = new Vector2Int(Integer(sizeNode[0], "game_size"), Integer(sizeNode[1], "game_size"));
            Require(gameSize.x > 0 && gameSize.y > 0 && (long)gameSize.x * 2 == (long)gameSize.y * 3, "关卡图片必须为 3:2");
            var size = new Vector2(gameSize.x, gameSize.y);
            var items = data["items"];
            Require(items != null && items.IsArray && items.Count >= 1 && items.Count <= DifferenceRound.MaxSpots, "关卡需包含 1 至 " + DifferenceRound.MaxSpots + " 处差异");
            Require(Integer(data["expected"], "expected") == items.Count, "expected 与差异数量不一致");
            var level = new DifferenceLevel
            {
                title = data["title"].IsString ? data["title"].Value : "关卡 " + expectedNumber,
                contentKey = apiUrl + "|" + endpoint["level_id"].Value + "|" + endpoint["revision"].Value + "|" + Hash128.Compute(diffsJson),
                regions = new Vector4[items.Count], hitSpots = new DifferenceSpot[items.Count], changedOnTop = true
            };
            var nodes = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                Require(item != null && item.IsObject, "差异项必须是对象");
                Require(nodes.Add(Text(item["node"], "node")), "差异 node 重复");
                var position = Numbers(item["pos"], 2, "pos", true);
                var cropSize = Numbers(item["crop_size"], 2, "crop_size", true);
                var crop = new Rect(position[0], position[1], cropSize[0], cropSize[1]);
                CheckRect(crop, gameSize, "裁剪区域");
                var hit = Numbers(item["hit_bounds"], 4, "hit_bounds");
                var hitRect = new Rect(hit[0], hit[1], hit[2], hit[3]);
                CheckRect(hitRect, gameSize, "点击区域");
                Require(Text(item["hit_geometry"], "hit_geometry") == "bbox-rect", "仅支持矩形点击区域");
                level.regions[i] = new Vector4(crop.center.x / size[0], crop.center.y / size[1], crop.width / size[0], crop.height / size[1]);
                var radius = Mathf.Min(.25f, new Vector2(hitRect.width / size[0], hitRect.height / size[0]).magnitude * .5f);
                level.hitSpots[i] = new DifferenceSpot(hitRect.x / size[0], hitRect.y / size[1], hitRect.width / size[0], hitRect.height / size[1], radius);
            }
            new DifferenceRound(level.hitSpots, (float)gameSize.x / gameSize.y);
            return level;
        }

        public static void CreateSprites(DifferenceLevel level, Texture2D original, Texture2D changed, Vector2Int gameSize)
        {
            Require(level != null && level.regions != null && level.regions.Length >= 1 && level.regions.Length <= DifferenceRound.MaxSpots, "差异区域数量必须为 1 至 " + DifferenceRound.MaxSpots);
            CheckTexture(original, gameSize); CheckTexture(changed, gameSize);
            Require(level.original == null && level.changed == null && level.croppedPatches == null, "关卡图片已创建");
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
            Require(texture != null, "缺少裁剪图片");
            CheckRect(topLeftRect, new Vector2Int(texture.width, texture.height), "裁剪区域");
            var rect = new Rect(topLeftRect.x, texture.height - topLeftRect.y - topLeftRect.height, topLeftRect.width, topLeftRect.height);
            return Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
        }

        static JSONNode ParseEndpoint(string json, int expectedNumber)
        {
            Require(expectedNumber > 0, "关卡编号必须为正整数");
            var endpoint = JSON.Parse(json);
            Require(endpoint != null && endpoint.IsObject && endpoint["ok"].IsBoolean && endpoint["ok"].AsBool, "后台未返回有效关卡");
            Require(Integer(endpoint["number"], "number") == expectedNumber, "接口关卡编号与请求不一致");
            Require(Integer(endpoint["revision"], "revision") >= 0, "revision 不合法");
            Text(endpoint["level_id"], "level_id");
            Require(endpoint["assets"].IsObject, "缺少 assets");
            AssetUrl(endpoint, "different.png"); AssetUrl(endpoint, "level-base.png"); AssetUrl(endpoint, "level-diffs.json");
            return endpoint;
        }

        static string AssetUrl(JSONNode endpoint, string name) => WebUrl(Text(endpoint["assets"][name], name));
        static string WebUrl(string value)
        {
            Require(Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp), "资源地址必须为 HTTP(S)");
            return value;
        }
        static string Text(JSONNode node, string name)
        {
            Require(node != null && node.IsString && !string.IsNullOrWhiteSpace(node.Value), "缺少 " + name);
            return node.Value;
        }
        static int Integer(JSONNode node, string name)
        {
            var value = Number(node, name);
            Require(value >= 0 && value <= int.MaxValue && value == Math.Truncate(value), name + " 必须是非负整数");
            return (int)value;
        }
        static double Number(JSONNode node, string name)
        {
            Require(node != null && node.IsNumber, name + " 必须是数字");
            var value = node.AsDouble;
            Require(!double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= int.MaxValue, name + " 数字越界");
            return value;
        }
        static float[] Numbers(JSONNode node, int count, string name, bool integer = false)
        {
            Require(node != null && node.IsArray && node.Count == count, name + " 长度不正确");
            var values = new float[count];
            for (var i = 0; i < count; i++) values[i] = integer ? Integer(node[i], name) : (float)Number(node[i], name);
            return values;
        }
        static void CheckTexture(Texture2D texture, Vector2Int size)
        { Require(texture != null && texture.width == size.x && texture.height == size.y, "图片尺寸与 game_size 不一致"); }
        static void CheckRect(Rect rect, Vector2Int size, string name)
        {
            Require(!float.IsNaN(rect.x) && !float.IsNaN(rect.y) && !float.IsNaN(rect.width) && !float.IsNaN(rect.height) &&
                !float.IsInfinity(rect.x) && !float.IsInfinity(rect.y) && !float.IsInfinity(rect.width) && !float.IsInfinity(rect.height) &&
                rect.x >= 0 && rect.y >= 0 && rect.width > 0 && rect.height > 0 &&
                rect.xMax <= size.x && rect.yMax <= size.y, name + " 越出图片范围");
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new FormatException(message); }

        public static void SelfCheck()
        {
            const string api = "https://example.com/api/levels/1";
            const string endpoint = "{\"ok\":true,\"number\":1,\"revision\":1,\"level_id\":\"check\",\"assets\":{\"different.png\":\"https://example.com/d.png\",\"level-base.png\":\"https://example.com/b.png\",\"level-diffs.json\":\"https://example.com/d.json\"}}";
            const string diffs = "{\"schema\":\"difference_desk/greyfun-level/v1\",\"level_id\":\"check\",\"game_size\":[1500,1000],\"expected\":1,\"items\":[{\"node\":\"D01\",\"pos\":[969,60],\"crop_size\":[204,377],\"hit_bounds\":[975,65,192,366],\"hit_geometry\":\"bbox-rect\"}]}";
            var level = Parse(api, endpoint, diffs, out var size);
            Require(size == new Vector2Int(1500, 1000) && Mathf.Abs(level.regions[0].x - 1071f / 1500) < .00001f &&
                Mathf.Abs(level.regions[0].y - 248.5f / 1000) < .00001f && level.changedOnTop, "坐标转换自检失败");
            Require(new DifferenceRound(level.hitSpots, 1.5f).Click(976f / 1500, 66f / 1000) == 0, "矩形点击自检失败");
            Require(new DifferenceRound(level.hitSpots, 1.5f).Click(970f / 1500, 61f / 1000) == DifferenceRound.Miss, "裁剪边缘不应算命中");
            var rejected = false;
            try { Parse(api, endpoint, diffs.Replace("[969,60]", "[1490,60]"), out size); }
            catch (FormatException) { rejected = true; }
            Require(rejected, "越界裁剪必须被拒绝");
            Require(level.contentKey != Parse(api, endpoint.Replace("\"revision\":1", "\"revision\":2"), diffs, out size).contentKey, "版本变化必须更换存档标识");
            var secondEndpoint = endpoint.Replace("\"number\":1", "\"number\":2");
            var secondLevel = Parse("https://example.com/api/levels/2", secondEndpoint, diffs, out size, 2);
            Require(secondLevel.title == "关卡 2" && secondLevel.contentKey != level.contentKey, "应能加载独立的第 2 关");
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

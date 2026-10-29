#if ENABLE_UI_UGUI
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;

namespace Hotfix.UI
{
    public sealed class DifferenceBatchCache : IDisposable
    {
        public const string DefaultEndpoint = "https://diffapi.joystar.pro/batch/";
        public const float DefaultTimeout = 12f;
        const int BatchSize = 10;
        readonly string directory, endpoint;
        readonly List<Download> active = new List<Download>();
        volatile bool disposed;
        bool ensuring;
        Stopwatch clock;
        float timeout;
        public string Error { get; private set; }

        sealed class Download
        {
            public string Url, Path;
            public UnityWebRequest Request;
        }

        public DifferenceBatchCache(string directory = null, string endpoint = DefaultEndpoint)
        {
            this.endpoint = (endpoint ?? "").TrimEnd('/') + "/";
            this.directory = Path.Combine(directory ?? Path.Combine(Application.persistentDataPath, "DifferenceMainBatches"),
                Hash128.Compute(this.endpoint).ToString());
        }

        public IEnumerator Ensure(int number, float timeout = DefaultTimeout)
        {
            Error = null;
            if (disposed) { Error = "The level cache is closed."; yield break; }
            if (ensuring) { Error = "Levels are already being prepared."; yield break; }
            ensuring = true;
            this.timeout = timeout;
            clock = Stopwatch.StartNew();
            try
            {
                if (!Attempt(() =>
                {
                    Require(number > 0, "The level number must be a positive integer.");
                    Require(timeout > 0 && !float.IsNaN(timeout) && !float.IsInfinity(timeout), "The cache timeout must be positive.");
                    Require(Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps), "The batch URL must use HTTP or HTTPS.");
                })) yield break;

                Task<bool> cached = null;
                if (!Attempt(() => cached = Task.Run(() =>
                {
                    if (!TryManifest(number, out var manifest)) return false;
                    for (var i = 0; i < manifest["levels"].Count; i++)
                        if (disposed || !TryReadLevel(manifest, Int(manifest["start_number"]) + i, out _, out _, out _, out _)) return false;
                    return true;
                }))) yield break;
                while (!cached.IsCompleted)
                {
                    if (!Continue()) yield break;
                    yield return null;
                }
                var valid = false;
                if (!Attempt(() => valid = cached.GetAwaiter().GetResult())) yield break;
                if (valid) { Continue(); yield break; }

                if (!Continue()) yield break;
                Download metadata = null;
                if (!Attempt(() => metadata = Start(endpoint + Batch(number).ToString(CultureInfo.InvariantCulture)))) yield break;
                while (true)
                {
                    if (!Continue()) yield break;
                    if (metadata.Request.isDone) break;
                    yield return null;
                }
                JSONNode batch = null;
                if (!Attempt(() =>
                {
                    CheckDownload(metadata);
                    batch = ParseBatch(metadata.Request.downloadHandler.text, number);
                    batch["cache_version"] = 1;
                    batch["cache_hashes"] = new JSONObject();
                    Directory.CreateDirectory(Path.Combine(directory, "assets"));
                })) yield break;
                Finish(metadata);

                var downloads = new List<Download>();
                if (!Attempt(() =>
                {
                    foreach (var level in batch["levels"].Children)
                        foreach (var name in new[] { "level-diffs.json", "level-base.png", "different.png" })
                        {
                            var url = DifferenceRemoteLevel.AssetUrl(level, name);
                            downloads.Add(new Download { Url = url, Path = AssetPath(level, url) });
                        }
                })) yield break;
                var next = 0;
                while (next < downloads.Count || active.Count > 0)
                {
                    if (!Continue()) yield break;
                    while (next < downloads.Count && active.Count < 4)
                    {
                        var download = downloads[next++];
                        if (!Attempt(() => Start(download))) yield break;
                    }
                    for (var i = active.Count - 1; i >= 0; i--)
                    {
                        var download = active[i];
                        if (!download.Request.isDone) continue;
                        if (!Attempt(() =>
                        {
                            CheckDownload(download);
                            var bytes = download.Request.downloadHandler.data;
                            Require(bytes != null && bytes.Length > 0, "An asset is empty.");
                            WriteAtomic(download.Path, bytes);
                            batch["cache_hashes"][Path.GetFileName(download.Path)] = Hash(bytes);
                        })) yield break;
                        Finish(download);
                    }
                    yield return null;
                }
                for (var i = 0; i < batch["levels"].Count; i++)
                {
                    if (!Continue()) yield break;
                    var levelNumber = Int(batch["start_number"]) + i;
                    yield return ValidateDownloadedLevel(batch, levelNumber);
                    if (Error != null) yield break;
                    yield return null;
                }
                if (!Continue()) yield break;
                Attempt(() => WriteAtomic(ManifestPath(number), Encoding.UTF8.GetBytes(batch.ToString())));
            }
            finally
            {
                AbortAll();
                ensuring = false;
            }
        }

        public bool TryRead(int number, out string endpointJson, out string diffsJson, out byte[] original, out byte[] changed)
        {
            endpointJson = diffsJson = null; original = changed = null;
            return number > 0 && TryManifest(number, out var batch) &&
                TryReadLevel(batch, number, out endpointJson, out diffsJson, out original, out changed);
        }

        bool TryManifest(int number, out JSONNode batch)
        {
            batch = null;
            try
            {
                batch = ParseBatch(File.ReadAllText(ManifestPath(number), Encoding.UTF8), number);
                Require(Int(batch["cache_version"]) == 1 && batch["cache_hashes"].IsObject, "The level cache is incomplete.");
                return true;
            }
            catch { batch = null; return false; }
        }

        bool TryReadLevel(JSONNode batch, int number, out string endpointJson, out string diffsJson, out byte[] original, out byte[] changed)
        {
            endpointJson = diffsJson = null; original = changed = null;
            try { ReadLevel(batch, number, out endpointJson, out diffsJson, out original, out changed); return true; }
            catch { endpointJson = diffsJson = null; original = changed = null; return false; }
        }

        void ReadLevel(JSONNode batch, int number, out string endpointJson, out string diffsJson, out byte[] original, out byte[] changed)
        {
            var level = batch["levels"][number - Int(batch["start_number"])];
            endpointJson = level.ToString();
            diffsJson = Encoding.UTF8.GetString(ReadAsset(batch, level, "level-diffs.json"));
            original = ReadAsset(batch, level, "level-base.png");
            changed = ReadAsset(batch, level, "different.png");
            // Committed images were decoded before saving; hashes detect changes without decoding all ten levels again.
            DifferenceRemoteLevel.Parse(endpoint + number.ToString(CultureInfo.InvariantCulture), endpointJson, diffsJson, out _, number);
        }

        IEnumerator ValidateDownloadedLevel(JSONNode batch, int number)
        {
            string endpointJson = null, diffsJson = null;
            byte[] original = null, changed = null;
            Task reading = null;
            if (!Attempt(() => reading = Task.Run(() => ReadLevel(batch, number, out endpointJson, out diffsJson, out original, out changed)))) yield break;
            while (!reading.IsCompleted)
            {
                if (!Continue()) yield break;
                yield return null;
            }
            if (!Attempt(() => reading.GetAwaiter().GetResult())) yield break;
            var size = new Vector2Int();
            if (!Attempt(() => DifferenceRemoteLevel.Parse(endpoint + number.ToString(CultureInfo.InvariantCulture),
                endpointJson, diffsJson, out size, number))) yield break;
            using (var validation = new DifferenceRemoteLevel())
            {
                for (var i = 0; i < 2; i++)
                {
                    var decode = validation.DecodePicture(i == 0 ? original : changed, size, i == 0);
                    try
                    {
                        while (true)
                        {
                            if (!Continue()) yield break;
                            if (!decode.MoveNext()) break;
                            yield return decode.Current;
                        }
                    }
                    finally { (decode as IDisposable)?.Dispose(); }
                    if (validation.Error != null) { Error = validation.Error; yield break; }
                    yield return null;
                }
            }
        }

        byte[] ReadAsset(JSONNode batch, JSONNode level, string name)
        {
            var path = AssetPath(level, DifferenceRemoteLevel.AssetUrl(level, name));
            var hash = batch["cache_hashes"][Path.GetFileName(path)];
            Require(hash.IsString && hash.Value.Length == 64, "An asset is missing its integrity record.");
            var bytes = File.ReadAllBytes(path);
            Require(bytes.Length > 0 && Hash(bytes) == hash.Value, "A cached asset is damaged.");
            return bytes;
        }

        static JSONNode ParseBatch(string json, int number)
        {
            var batch = JSON.Parse(json);
            Require(batch != null && batch.IsObject && batch["ok"].IsBoolean && batch["ok"].AsBool, "The server returned no valid level batch.");
            Require(Int(batch["batch"]) == Batch(number) && Int(batch["batch_size"]) == BatchSize, "The batch number or size does not match.");
            var total = Int(batch["level_count"]);
            Require(total >= number, "The requested level exceeds the total level count.");
            var start = number - (number - 1) % BatchSize;
            var end = (int)Math.Min((long)start + BatchSize - 1, total);
            Require(Int(batch["start_number"]) == start && Int(batch["end_number"]) == end, "The batch range does not match.");
            Require(batch["missing_numbers"].IsArray && batch["missing_numbers"].Count == 0, "The batch is missing levels.");
            var levels = batch["levels"];
            Require(levels.IsArray && Int(batch["count"]) == end - start + 1 && levels.Count == end - start + 1, "The batch has an incomplete level count.");
            var ordered = new JSONNode[levels.Count];
            foreach (var level in levels.Children)
            {
                var index = Int(level["number"]);
                Require(index >= start && index <= end && ordered[index - start] == null, "The batch contains duplicate or out-of-range level numbers.");
                DifferenceRemoteLevel.ParseEndpoint(level.ToString(), index);
                ordered[index - start] = level;
            }
            var sorted = new JSONArray();
            foreach (var level in ordered) sorted.Add(level);
            batch["levels"] = sorted;
            return batch;
        }

        Download Start(string url)
        {
            var download = new Download { Url = url };
            Start(download);
            return download;
        }

        void Start(Download download)
        {
            download.Request = UnityWebRequest.Get(download.Url);
            active.Add(download);
            download.Request.timeout = Math.Max(1, (int)Math.Ceiling(timeout - clock.Elapsed.TotalSeconds));
            download.Request.SendWebRequest();
        }

        static void CheckDownload(Download download)
        {
            Require(download.Request.result == UnityWebRequest.Result.Success, "Level download failed: " + download.Request.error);
        }

        void Finish(Download download)
        {
            download.Request.Dispose();
            active.Remove(download);
        }

        bool Continue()
        {
            if (disposed) { Error = "Level preparation was cancelled."; return false; }
            if (clock.Elapsed.TotalSeconds >= timeout) { Error = "Level preparation timed out."; return false; }
            return true;
        }

        bool Attempt(Action action)
        {
            try { action(); return true; }
            catch (Exception exception) { Error = "Unable to prepare levels: " + exception.Message; return false; }
        }

        static int Batch(int number) => (number - 1) / BatchSize + 1;
        string ManifestPath(int number) => Path.Combine(directory, "batch-" + Batch(number).ToString(CultureInfo.InvariantCulture) + ".json");
        string AssetPath(JSONNode level, string url) => Path.Combine(directory, "assets", Hash128.Compute(level.ToString()) + "-" + Hash128.Compute(url) + ".bytes");

        static int Int(JSONNode node)
        {
            Require(node != null && node.IsNumber && !double.IsNaN(node.AsDouble) && node.AsDouble >= 0 &&
                node.AsDouble <= int.MaxValue && node.AsDouble == Math.Truncate(node.AsDouble), "Batch fields must be nonnegative integers.");
            return (int)node.AsDouble;
        }

        static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }

        static void WriteAtomic(string path, byte[] bytes)
        {
            var temporary = path + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public void Dispose()
        {
            disposed = true;
            if (ensuring) Error = "Level preparation was cancelled.";
            AbortAll();
        }

        void AbortAll()
        {
            foreach (var download in active)
            {
                try { download.Request.Abort(); }
                catch { }
                try { download.Request.Dispose(); }
                catch { }
            }
            active.Clear();
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new FormatException(message); }
    }
}
#endif

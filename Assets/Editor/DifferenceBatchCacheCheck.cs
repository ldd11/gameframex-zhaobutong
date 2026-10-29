#if ENABLE_UI_UGUI
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class DifferenceBatchCacheCheck
{
    static bool running;

    [MenuItem("Tools/Find Differences/Measure Cached Level Load Only")]
    public static async void MeasureCachedLoad()
    {
        if (running) return;
        running = true;
        var report = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/FindDifferences-cached-load-timing.txt"));
        var steps = new Stack<IEnumerator>();
        try
        {
            using (var cache = new DifferenceBatchCache())
                Check(cache.TryRead(1, out _, out _, out _, out _), "请先通过 Logo 页面缓存第 1 批关卡");
            using (var level = new DifferenceRemoteLevel())
            {
                steps.Push(level.LoadMainBatch("https://example.com/api/greyfun/levels/1", 1));
                var elapsed = Stopwatch.StartNew();
                double total = 0, longest = 0;
                var frames = 0;
                while (steps.Count > 0)
                {
                    var work = Stopwatch.StartNew();
                    while (steps.Count > 0)
                    {
                        var operation = steps.Peek();
                        if (!operation.MoveNext()) { steps.Pop(); (operation as IDisposable)?.Dispose(); continue; }
                        if (operation.Current is IEnumerator nested) { steps.Push(nested); continue; }
                        break;
                    }
                    var milliseconds = work.Elapsed.TotalMilliseconds;
                    total += milliseconds; longest = Math.Max(longest, milliseconds); frames++;
                    Check(elapsed.Elapsed.TotalSeconds < 15, "缓存加载耗时超出期限");
                    await Task.Delay(5);
                }
                Check(level.Error == null && level.Level != null && level.Level.original && level.Level.changed,
                    "缓存加载完整图片：" + level.Error);
                Directory.CreateDirectory(Path.GetDirectoryName(report));
                var result = DateTime.UtcNow.ToString("O") + " work=" + total.ToString("F2") + "ms; longest slice=" +
                    longest.ToString("F2") + "ms; slices=" + frames + "; wall=" + elapsed.Elapsed.TotalMilliseconds.ToString("F2") + "ms.\n";
                File.AppendAllText(report, result);
                Debug.Log("Cached level load timing: " + result);
            }
        }
        catch (Exception exception) { File.AppendAllText(report, "FAIL " + exception + "\n"); Debug.LogException(exception); }
        finally
        {
            while (steps.Count > 0) (steps.Pop() as IDisposable)?.Dispose();
            running = false;
        }
    }

    [MenuItem("Tools/Find Differences/Check Main Batch Cache Only")]
    public static async void Run()
    {
        if (running) return;
        running = true;
        var temporary = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
        var folder = Path.Combine(temporary, "DifferenceBatchCacheCheck-" + Guid.NewGuid().ToString("N"));
        var report = Path.Combine(temporary, "FindDifferences-batch-cache.txt");
        Directory.CreateDirectory(folder);
        File.WriteAllText(report, "RUNNING " + DateTime.UtcNow.ToString("O"));
        try
        {
            using (var server = new FixtureServer(Png(Color.red), Png(Color.green), Png(Color.blue, 3, 2)))
            {
                using (var cache = new DifferenceBatchCache(folder, server.Endpoint))
                {
                    await Drive(cache.Ensure(1));
                    Check(cache.Error == null, "首次整批缓存成功：" + cache.Error);
                    Check(server.Count("/batch/1") == 1 && server.Count("/batch/2") == 0, "第 1 关只请求一次批次 1");
                    Check(server.AssetRequests == 30, "差异 JSON 与双图各 10 份完整下载");
                    Read(cache, 1); Read(cache, 10);
                    var requests = server.Requests.Length;
                    await Drive(cache.Ensure(10));
                    Check(cache.Error == null && server.Requests.Length == requests, "同批第 10 关不重复请求");
                }

                var firstManifest = Manifests(folder).Single();
                var firstManifestText = File.ReadAllText(firstManifest);
                foreach (var fault in new[] { "http", "json", "wrong", "number", "duplicate", "missing", "diffs", "image" })
                {
                    server.Fault = fault;
                    using (var cache = new DifferenceBatchCache(folder, server.Endpoint))
                    {
                        await Drive(cache.Ensure(11, 3));
                        Check(!string.IsNullOrEmpty(cache.Error), fault + " 必须报告失败");
                        Check(!cache.TryRead(11, out _, out _, out _, out _), fault + " 不提交不完整批次");
                        Check(Manifests(folder).Length == 1 && File.ReadAllText(firstManifest) == firstManifestText,
                            fault + " 不覆盖已有完整 manifest");
                        Read(cache, 1);
                    }
                }

                server.Fault = null;
                server.DelayMilliseconds = 1000;
                var watch = Stopwatch.StartNew();
                using (var cache = new DifferenceBatchCache(folder, server.Endpoint))
                {
                    await Drive(cache.Ensure(11, .12f));
                    Check(!string.IsNullOrEmpty(cache.Error) && watch.Elapsed.TotalSeconds < 1,
                        "整批超时应及时结束并报告错误");
                }
                using (var cache = new DifferenceBatchCache(folder, server.Endpoint))
                {
                    var batches = server.Count("/batch/2");
                    var operation = cache.Ensure(11);
                    Check(operation.MoveNext(), "取消用例已启动缓存检查");
                    watch.Restart();
                    while (server.Count("/batch/2") == batches && watch.Elapsed.TotalSeconds < 1)
                    {
                        Check(operation.MoveNext(), "取消前请求仍在进行");
                        await Task.Delay(5);
                    }
                    Check(server.Count("/batch/2") == batches + 1, "取消用例的请求已到达服务端");
                    cache.Dispose();
                    watch.Restart();
                    await Drive(operation);
                    Check(!string.IsNullOrEmpty(cache.Error) && watch.Elapsed.TotalSeconds < .5,
                        "Dispose 取消后及时结束并报告错误");
                }
                Check(Manifests(folder).Length == 1, "超时和取消不提交 manifest");

                server.DelayMilliseconds = 0;
                using (var cache = new DifferenceBatchCache(folder, server.Endpoint))
                {
                    var batches = server.Count("/batch/2");
                    await Drive(cache.Ensure(11));
                    var after = server.Count("/batch/2");
                    Check(cache.Error == null && after == batches + 1,
                        "第 11 关申请批次 2；Error=" + (cache.Error ?? "<null>") + "，before=" + batches + "，after=" + after);
                    Read(cache, 11); Read(cache, 20);
                }
                Check(server.Requests.All(path => path.StartsWith("/batch/", StringComparison.Ordinal) ||
                    path.StartsWith("/assets/", StringComparison.Ordinal)), "缓存过程不调用逐关接口");

                server.Dispose();
                using (var cache = new DifferenceBatchCache(folder, server.Endpoint))
                {
                    foreach (var number in new[] { 1, 10, 11, 20 })
                    {
                        await Drive(cache.Ensure(number));
                        Check(cache.Error == null, "重建实例后离线命中第 " + number + " 关：" + cache.Error);
                        Read(cache, number);
                    }

                    var manifests = Manifests(folder).ToDictionary(path => path, File.ReadAllText);
                    var asset = Directory.GetFiles(folder, "*.bytes", SearchOption.AllDirectories).First();
                    var original = File.ReadAllBytes(asset);
                    try
                    {
                        var corrupt = (byte[])original.Clone(); corrupt[0] ^= 0xff;
                        File.WriteAllBytes(asset, corrupt);
                        var number = Enumerable.Range(1, 20).First(n => !cache.TryRead(n, out _, out _, out _, out _));
                        await Drive(cache.Ensure(number, .3f));
                        Check(!string.IsNullOrEmpty(cache.Error), "损坏缓存不能当作完整离线缓存");
                        var other = number % 10 == 0 ? number - 1 : number + 1;
                        Check(cache.TryRead(other, out _, out _, out _, out _), "同批另一关自身资源完好");
                        await Drive(cache.Ensure(other, .3f));
                        Check(!string.IsNullOrEmpty(cache.Error), "同批存在损坏资源时，离线准备完好关仍须拒绝整批缓存");
                        File.Delete(asset);
                        Check(!cache.TryRead(number, out _, out _, out _, out _), "缺失资源不能读取");
                        await Drive(cache.Ensure(number, .3f));
                        Check(!string.IsNullOrEmpty(cache.Error), "资源缺失且离线时及时失败");
                        Check(manifests.All(pair => File.ReadAllText(pair.Key) == pair.Value), "修复失败不覆盖原 manifest");
                    }
                    finally { File.WriteAllBytes(asset, original); }
                    foreach (var number in new[] { 1, 10, 11, 20 }) Read(cache, number);
                }
            }
            Check(!Directory.GetFiles(folder, "*.tmp", SearchOption.AllDirectories).Any(), "不遗留原子写入临时文件");
            File.WriteAllText(report, "PASS " + DateTime.UtcNow.ToString("O") +
                "\n10-level batch download; 1/10 and 11/20 boundaries; restart + offline reads; original diffs + two images decode/create sprites; no per-level endpoint; HTTP/JSON/range/duplicate/missing/diffs/image failures; deadline + cancellation; corrupt/missing asset rejection; existing manifests preserved. Production cache and save data untouched.");
            Debug.Log("Find Differences batch cache checks passed; production cache and saves untouched.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(report, "FAIL " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            // This root is generated above; never delete outside this check's Temp child directory.
            var full = Path.GetFullPath(folder);
            if (full.StartsWith(temporary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(full).StartsWith("DifferenceBatchCacheCheck-", StringComparison.Ordinal) && Directory.Exists(full))
                Directory.Delete(full, true);
            running = false;
        }
    }

    static async Task Drive(IEnumerator operation)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            while (operation.MoveNext())
            {
                Check(watch.Elapsed.TotalSeconds < DifferenceBatchCache.DefaultTimeout + 2, "缓存操作未能在测试期限内结束");
                if (operation.Current is IEnumerator nested) await Drive(nested);
                else await Task.Delay(5);
            }
        }
        finally { (operation as IDisposable)?.Dispose(); }
    }

    static void Read(DifferenceBatchCache cache, int number)
    {
        Check(cache.TryRead(number, out var endpoint, out var diffs, out var original, out var changed), "读取完整第 " + number + " 关");
        var api = "https://example.com/api/greyfun/levels/" + number.ToString(CultureInfo.InvariantCulture);
        var level = DifferenceRemoteLevel.Parse(api, endpoint, diffs, out var size, number);
        Check(diffs == FixtureServer.Diffs(number) && level.contentKey.StartsWith(api + "|", StringComparison.Ordinal),
            "缓存保留差异原文和逐关 contentKey");
        Texture2D a = null, b = null;
        try
        {
            a = DifferenceRemoteLevel.DecodeTexture(original, size);
            b = DifferenceRemoteLevel.DecodeTexture(changed, size);
            DifferenceRemoteLevel.CreateSprites(level, a, b, size);
            Check(level.original && level.changed && level.croppedPatches.Length == 1 && level.croppedPatches[0],
                "缓存数据可直接创建玩法图片和差异裁块");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(level.original);
            UnityEngine.Object.DestroyImmediate(level.changed);
            if (level.croppedPatches != null) foreach (var patch in level.croppedPatches) UnityEngine.Object.DestroyImmediate(patch);
            UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b);
        }
    }

    static byte[] Png(Color color, int width = 6, int height = 4)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try { texture.SetPixels(Enumerable.Repeat(color, width * height).ToArray()); texture.Apply(); return texture.EncodeToPNG(); }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    static string[] Manifests(string folder) => Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories);
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("批量缓存检查失败：" + message); }

    sealed class FixtureServer : IDisposable
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly List<string> requests = new List<string>();
        readonly byte[] original, changed, wrongSize;
        public volatile string Fault;
        public volatile int DelayMilliseconds;
        public string Endpoint { get; }
        public string[] Requests { get { lock (requests) return requests.ToArray(); } }
        public int AssetRequests => Requests.Count(path => path.StartsWith("/assets/", StringComparison.Ordinal));
        public int Count(string path) => Requests.Count(value => value == path);

        public FixtureServer(byte[] original, byte[] changed, byte[] wrongSize)
        {
            this.original = original; this.changed = changed; this.wrongSize = wrongSize;
            listener.Start();
            Endpoint = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/batch/";
            _ = Accept();
        }

        async Task Accept()
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Respond(client);
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        async Task Respond(TcpClient client)
        {
            using (client)
            try
            {
                var stream = client.GetStream();
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) return;
                    var path = line.Split(' ')[1];
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync().ConfigureAwait(false))) { }
                    lock (requests) requests.Add(path);
                    var fault = Fault;
                    if (DelayMilliseconds > 0) await Task.Delay(DelayMilliseconds, cancel.Token).ConfigureAwait(false);
                    byte[] body;
                    var status = fault == "http" ? "503 Service Unavailable" : "200 OK";
                    if (path.StartsWith("/batch/", StringComparison.Ordinal))
                    {
                        var batch = int.Parse(path.Substring("/batch/".Length), CultureInfo.InvariantCulture);
                        body = Encoding.UTF8.GetBytes(fault == "json" ? "{ broken" : Batch(batch, fault));
                    }
                    else
                    {
                        var parts = path.Split('/');
                        var number = int.Parse(parts[2], CultureInfo.InvariantCulture);
                        body = parts[3] == "level-diffs.json" ? Encoding.UTF8.GetBytes(fault == "diffs" ? "{}" : Diffs(number)) :
                            fault == "image" ? wrongSize : parts[3] == "level-base.png" ? original : changed;
                    }
                    var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Length: " + body.Length +
                        "\r\nConnection: close\r\nContent-Type: application/octet-stream\r\n\r\n");
                    await stream.WriteAsync(header, 0, header.Length, cancel.Token).ConfigureAwait(false);
                    await stream.WriteAsync(body, 0, body.Length, cancel.Token).ConfigureAwait(false);
                }
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        string Batch(int batch, string fault)
        {
            var start = (batch - 1) * 10 + 1;
            var items = new List<string>();
            for (var number = start; number < start + 10; number++)
            {
                if (fault == "missing" && number == start + 9) continue;
                var value = number != start + 9 ? number : fault == "duplicate" ? start : fault == "number" ? start + 10 : number;
                var prefix = Endpoint.Substring(0, Endpoint.Length - "batch/".Length) + "assets/" + value + "/";
                items.Add("{\"ok\":true,\"number\":" + value + ",\"revision\":1,\"level_id\":\"fixture-" + value +
                    "\",\"assets\":{\"level-diffs.json\":\"" + prefix + "level-diffs.json\",\"level-base.png\":\"" + prefix +
                    "level-base.png\",\"different.png\":\"" + prefix + "different.png\"}}");
            }
            return "{\"ok\":true,\"batch\":" + (fault == "wrong" ? batch + 1 : batch) +
                ",\"batch_size\":10,\"level_count\":20,\"start_number\":" + start + ",\"end_number\":" + (start + 9) +
                ",\"count\":10,\"missing_numbers\":[],\"levels\":[" + string.Join(",", items) + "]}";
        }

        public static string Diffs(int number) => "{\"schema\":\"difference_desk/greyfun-level/v1\",\"level_id\":\"fixture-" + number +
            "\",\"game_size\":[6,4],\"expected\":1,\"items\":[{\"node\":\"D01\",\"pos\":[1,1],\"crop_size\":[2,2],\"hit_bounds\":[1,1,2,2],\"hit_geometry\":\"bbox-rect\"}]}";

        public void Dispose()
        {
            if (cancel.IsCancellationRequested) return;
            cancel.Cancel(); listener.Stop();
        }
    }
}
#endif

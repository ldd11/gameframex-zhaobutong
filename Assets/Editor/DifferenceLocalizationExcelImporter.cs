#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class DifferenceLocalizationExcelImporter
{
    const string ConfigPath = "Assets/Bundles/Config/local_tblocalization.json";
    const string SourcePath = "GameFrameX/Config/Excels/Local/L-Localization-找不同.xlsx";
    const string GeneratePath = "GameFrameX/Config/generate-client-json.ps1";
    static readonly string[] Headers = { "英文", "简体中文", "繁体中文", "法语", "德语", "印尼语", "日语", "韩语", "葡萄牙语（巴西）", "葡萄牙语（葡萄牙）", "俄语", "西班牙语（拉美）", "西班牙语（西班牙）", "泰语", "越南语", "印地语" };
    static readonly string[] Fields = { "English", "ChineseSimplified", "ChineseTraditional", "French", "German", "Indonesian", "Japanese", "Korean", "PortugueseBrazil", "PortuguesePortugal", "Russian", "SpanishLatinAmerica", "SpanishSpain", "Thai", "Vietnamese", "Hindi" };
    static readonly Regex Placeholder = new Regex(@"\{\d+(?:[^{}]*)\}", RegexOptions.Compiled);
    static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    [MenuItem("Tools/Find Differences/导入多语言 Excel")]
    public static void ImportMenu()
    {
        var path = EditorUtility.OpenFilePanel("选择游戏多语言翻译表", Path.GetFullPath("多语言表"), "xlsx");
        if (string.IsNullOrEmpty(path)) return;
        try { var count = Import(path); EditorUtility.DisplayDialog("导入成功", $"已导入 {count} 条游戏文字到热更配置。启动页面文案在代码中维护。", "确定"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("导入失败", e.Message, "确定"); }
    }

    public static int Import(string path)
    {
        var rows = ReadWorksheet(path);
        if (rows.Count < 2) throw new InvalidDataException("工作表没有翻译数据。");
        var header = rows[0];
        var columns = Headers.Select(h => Array.IndexOf(header, h)).ToArray();
        var keyColumn = Array.IndexOf(header, "文字键");
        if (keyColumn < 0 || columns.Any(c => c < 0)) throw new InvalidDataException("工作表缺少文字键或语言列，请使用游戏多语言表的原始表头。");
        var existing = File.Exists(ConfigPath) ? JArray.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8)) : new JArray();
        var byKey = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (var token in existing)
        {
            if (!(token is JObject obj)) throw new InvalidDataException("原配置包含无效条目。");
            var key = (string)obj["key"];
            if (string.IsNullOrWhiteSpace(key) || byKey.ContainsKey(key)) throw new InvalidDataException("原配置存在空键或重复键：" + key);
            byKey.Add(key, obj);
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sourceRows = new List<JObject>();
        var descriptionColumn = Array.IndexOf(header, "字段描述");
        var count = 0;
        foreach (var row in rows.Skip(1))
        {
            string Cell(int column) => column < row.Length ? row[column] : "";
            var key = Cell(keyColumn).Trim();
            if (string.IsNullOrEmpty(key))
            {
                if (row.Any(v => !string.IsNullOrWhiteSpace(v))) throw new InvalidDataException($"第 {count + 2} 行缺少文字键。");
                continue;
            }
            if (!seen.Add(key)) throw new InvalidDataException("Excel 中有重复文字键：" + key);
            var english = Cell(columns[0]);
            if (string.IsNullOrWhiteSpace(english)) throw new InvalidDataException(key + " 缺少英文。");
            if (!byKey.TryGetValue(key, out var item)) { item = new JObject { ["key"] = key }; existing.Add(item); byKey.Add(key, item); }
            var required = Tokens(english);
            for (var i = 0; i < Fields.Length; i++)
            {
                var value = Cell(columns[i]);
                if (!string.IsNullOrWhiteSpace(value) && !required.SequenceEqual(Tokens(value)))
                    throw new InvalidDataException($"{key} 的 {Headers[i]} 占位符与英文不一致。");
                item[Fields[i]] = value;
            }
            // Existing generated Luban readers require these fields, including Italian.
            if (item["Italian"] == null) item["Italian"] = english;
            item["Spanish"] = Cell(columns[12]);
            item["#Description"] = descriptionColumn >= 0 ? Cell(descriptionColumn) : "";
            sourceRows.Add(item);
            count++;
        }
        if (count == 0) throw new InvalidDataException("没有可导入的文字。");
        if (!File.Exists(SourcePath) || !File.Exists(GeneratePath)) throw new FileNotFoundException("找不到 Luban 源表或生成脚本。");
        var originalSource = File.ReadAllBytes(SourcePath);
        var updatedSource = WriteLubanRows(originalSource, sourceRows);
        try
        {
            File.WriteAllBytes(SourcePath, updatedSource);
            var start = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + Path.GetFullPath(GeneratePath) + "\"")
            {
                WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("无法启动 Luban 生成器。");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidDataException("Luban 生成失败：\n" + output.Result + "\n" + error.Result);
            }
        }
        catch
        {
            File.WriteAllBytes(SourcePath, originalSource);
            throw;
        }
        AssetDatabase.Refresh();
        return count;
    }

    static byte[] WriteLubanRows(byte[] bytes, List<JObject> rows)
    {
        using (var memory = new MemoryStream())
        {
            memory.Write(bytes, 0, bytes.Length);
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Update, true))
            {
                var entry = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidDataException("Luban 源表没有工作表。");
                XDocument document;
                using (var stream = entry.Open()) document = XDocument.Load(stream);
                var data = document.Root?.Element(Main + "sheetData") ?? throw new InvalidDataException("Luban 源表格式无效。");
                var head = data.Elements(Main + "row").FirstOrDefault(r => (string)r.Attribute("r") == "1") ?? throw new InvalidDataException("Luban 源表缺少表头。");
                var columns = head.Elements(Main + "c").Select(c => c.Element(Main + "v")?.Value ?? "").ToArray();
                if (columns.Length != 21 || columns[0] != "##var" || columns[2] != "key" || !Fields.All(columns.Contains)) throw new InvalidDataException("Luban 源表字段已变化，请检查表头。");
                var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
                string[] shared;
                if (sharedEntry == null) shared = Array.Empty<string>();
                else using (var stream = sharedEntry.Open()) shared = XDocument.Load(stream).Descendants(Main + "si").Select(s => string.Concat(s.Descendants(Main + "t").Select(t => t.Value))).ToArray();
                string CellValue(XElement row, string column)
                {
                    var cell = row.Elements(Main + "c").FirstOrDefault(c => ((string)c.Attribute("r") ?? "").TakeWhile(char.IsLetter).SequenceEqual(column));
                    var value = cell?.Element(Main + "v")?.Value ?? "";
                    if ((string)cell?.Attribute("t") == "s") return shared[int.Parse(value)];
                    if ((string)cell?.Attribute("t") == "inlineStr") return string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
                    return value;
                }
                var incoming = rows.ToDictionary(r => (string)r["key"], StringComparer.Ordinal);
                var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
                var next = 7;
                foreach (var old in data.Elements(Main + "row").Where(r => (int?)r.Attribute("r") > 6).ToArray())
                {
                    var key = CellValue(old, "C");
                    if (!string.IsNullOrEmpty(key) && !sourceKeys.Add(key)) throw new InvalidDataException("Luban 源表存在重复文字键：" + key);
                    if (incoming.TryGetValue(key, out var incomingRow))
                    {
                        incomingRow["Italian"] = CellValue(old, "N");
                        old.Remove();
                        continue;
                    }
                    old.SetAttributeValue("r", next);
                    foreach (var c in old.Elements(Main + "c"))
                    {
                        var letters = new string(((string)c.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray());
                        c.SetAttributeValue("r", letters + next);
                    }
                    next++;
                }
                for (var i = 0; i < rows.Count; i++)
                {
                    var number = next + i;
                    var row = new XElement(Main + "row", new XAttribute("r", number));
                    for (var col = 0; col < columns.Length; col++)
                    {
                        var value = col == 0 ? "" : (string)rows[i][columns[col]] ?? "";
                        row.Add(new XElement(Main + "c", new XAttribute("r", ((char)('A' + col)).ToString() + number),
                            new XAttribute("t", "str"), new XElement(Main + "v", value)));
                    }
                    data.Add(row);
                }
                entry.Delete();
                using (var stream = archive.CreateEntry("xl/worksheets/sheet1.xml").Open()) document.Save(stream);
            }
            return memory.ToArray();
        }
    }

    static string[] Tokens(string value) => Placeholder.Matches(value).Cast<Match>().Select(m => m.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    static List<string[]> ReadWorksheet(string path)
    {
        using (var zip = ZipFile.OpenRead(path))
        {
            XDocument Read(string name)
            {
                var entry = zip.GetEntry(name) ?? throw new InvalidDataException("Excel 缺少文件：" + name);
                using (var stream = entry.Open()) return XDocument.Load(stream);
            }
            var workbook = Read("xl/workbook.xml");
            var sheet = workbook.Descendants(Main + "sheet").FirstOrDefault(s => (string)s.Attribute("name") == "游戏多语言")
                ?? throw new InvalidDataException("Excel 缺少“游戏多语言”工作表。");
            var id = (string)sheet.Attribute(Rel + "id");
            var relationship = Read("xl/_rels/workbook.xml.rels").Descendants(PackageRel + "Relationship").FirstOrDefault(x => (string)x.Attribute("Id") == id)
                ?? throw new InvalidDataException("Excel 工作表引用无效。");
            var target = ((string)relationship.Attribute("Target")).Replace('\\', '/');
            var sheetPath = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
            var shared = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() : Read("xl/sharedStrings.xml").Descendants(Main + "si").Select(x => string.Concat(x.Descendants(Main + "t").Select(t => t.Value))).ToArray();
            var result = new List<string[]>();
            foreach (var row in Read(sheetPath).Descendants(Main + "sheetData").Elements(Main + "row"))
            {
                var cells = new string[19];
                Array.Fill(cells, "");
                foreach (var cell in row.Elements(Main + "c"))
                {
                    var address = (string)cell.Attribute("r") ?? "";
                    var column = 0;
                    foreach (var ch in address.TakeWhile(char.IsLetter)) column = column * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
                    if (column < 1 || column > cells.Length) continue;
                    var type = (string)cell.Attribute("t");
                    var value = cell.Element(Main + "v")?.Value ?? "";
                    if (type == "s") value = shared[int.Parse(value)];
                    else if (type == "inlineStr") value = string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
                    cells[column - 1] = value;
                }
                result.Add(cells);
            }
            return result;
        }
    }
}
#endif

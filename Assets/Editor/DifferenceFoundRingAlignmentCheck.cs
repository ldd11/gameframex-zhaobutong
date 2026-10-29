#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Reflection;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DifferenceFoundRingAlignmentCheck
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Tools/Find Differences/Check Found Ring Alignment Only")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before checking ring alignment.");
        const string report = "Temp/FindDifferences-found-ring-alignment.txt";
        Directory.CreateDirectory("Temp");
        var diagnosis = "Fixture setup has not finished.";
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            DifferenceRemoteLevel.SelfCheck();
            var ui = root.GetComponent<UIDifferences>();
            ui.play.SetActive(true);
            ui.play.GetComponent<DifferencePlayLayout>().ApplyLayout();
            ui.upperImage.rectTransform.ForceUpdateRectTransforms();
            ui.lowerImage.rectTransform.ForceUpdateRectTransforms();
            typeof(UIDifferences).GetField("useSpineFoundRing", Private).SetValue(ui, false);
            var show = typeof(UIDifferences).GetMethod("ShowFoundRing", Private);
            var region = typeof(UIDifferences).GetMethod("SetBoardRegion", BindingFlags.NonPublic | BindingFlags.Static);
            ui.foundRingStyle.minDiameter = 10;
            ui.foundRingStyle.maxDiameter = 160;
            ui.foundRingStyle.radiusOffset = 0;
            ui.foundRingStyle.glowIntensity = 0;
            var mask = ui.differencePatches[0].GetComponent<DifferenceFoundMask>() ?? ui.differencePatches[0].AddComponent<DifferenceFoundMask>();
            var play = ui.play.transform;
            var imageWidth = ui.lowerImage.rectTransform.rect.width * play.InverseTransformVector(
                ui.lowerImage.transform.parent.TransformVector(Vector3.right)).magnitude;
            foreach (var zoom in new[] { 1f, 2.5f })
            {
                ui.upperImage.GetComponent<DifferenceBoard>().SetZoom(zoom);
                foreach (var diameter in new[] { 2f, 125f, 300f })
                    for (var i = 0; i < ui.topRings.Length; i++)
                    {
                        diagnosis = $"zoom={zoom:R}, diameter={diameter:R}, index={i}, referenceWidth={imageWidth:R}";
                        var point = new Vector2(.1f + i * .05f, .2f + i % 3 * .25f);
                        var bounds = new Vector4(point.x, point.y, diameter / imageWidth, diameter / imageWidth * UIDifferences.ImageAspect);
                        var measured = new Vector2[2];
                        for (var side = 0; side < 2; side++)
                        {
                            var marker = (side == 0 ? ui.topRings : ui.bottomRings)[i];
                            var board = side == 0 ? ui.upperImage.rectTransform : ui.lowerImage.rectTransform;
                            marker.gameObject.SetActive(true);
                            region.Invoke(null, new object[] { marker.rectTransform, bounds });
                            show.Invoke(ui, new object[] { marker, false });
                            var ring = marker.GetComponentInChildren<DifferenceFoundRing>(true);
                            ring.sourceImageWidth = 0;
                            ring.rectTransform.ForceUpdateRectTransforms();
                            var center = board.InverseTransformPoint(ring.rectTransform.TransformPoint(ring.rectTransform.rect.center));
                            var normalized = new Vector2((center.x - board.rect.xMin) / board.rect.width, 1 - (center.y - board.rect.yMin) / board.rect.height);
                            measured[side] = Measure(ring, play);
                            var viewport = (RectTransform)board.parent;
                            diagnosis += $"\nside={side}, active={marker.gameObject.activeInHierarchy}, measured(radius,stroke)={measured[side].ToString("F6")}, radius={ring.Radius:R}, innerRadius={ring.InnerRadius:R}, normalizedCenter={normalized.ToString("F6")}, boardSize={board.rect.size.ToString("F6")}, boardScale={board.localScale.ToString("F6")}, viewportSize={viewport.rect.size.ToString("F6")}, viewportScale={viewport.localScale.ToString("F6")}, ringSize={ring.rectTransform.rect.size.ToString("F6")}, ringScale={ring.transform.localScale.ToString("F6")}, layoutUnit={typeof(DifferenceFoundRing).GetProperty("LayoutUnit", Private).GetValue(ring)}";
                            Check(Vector2.Distance(normalized, point) < .00001f, "The drawn circle moved away from its shared hit point.");
                            var inner = play.InverseTransformVector(ring.transform.TransformVector(Vector3.right * ring.InnerRadius)).magnitude;
                            Check(Mathf.Abs(inner - (measured[side].x - measured[side].y)) < .001f, "The flash mask inner radius no longer matches the drawn ring.");
                            mask.ring = ring;
                            using (var mesh = new VertexHelper())
                            {
                                typeof(DifferenceFoundMask).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(mask, new object[] { mesh });
                                var edge = new UIVertex(); mesh.PopulateUIVertex(ref edge, 1);
                                var expected = ring.transform.TransformPoint((Vector3)ring.rectTransform.rect.center + Vector3.right * ring.InnerRadius);
                                Check(Vector3.Distance(mask.transform.TransformPoint(edge.position), expected) < .001f, "The crop-space flash mask does not follow the corrected circle boundary.");
                            }
                        }
                        var expectedRadius = Mathf.Clamp(diameter + ui.foundRingStyle.radiusOffset * 2,
                            ui.foundRingStyle.minDiameter, ui.foundRingStyle.maxDiameter) * .5f * zoom;
                        var expectedStroke = Mathf.Clamp(ui.foundRingStyle.lineWidth * zoom, .1f * zoom, expectedRadius - .5f * zoom);
                        Check(Vector2.Distance(measured[0], measured[1]) < .001f, "Upper/lower circle radii or strokes differ under authored viewport scaling.");
                        Check(Mathf.Abs(measured[0].x - expectedRadius) < .001f && Mathf.Abs(measured[0].y - expectedStroke) < .001f,
                            "Circle limits and strokes must use layout units while retaining board zoom.");
                    }
            }
            var upperViewport = ui.upperImage.transform.parent;
            var lowerViewport = ui.lowerImage.transform.parent;
            foreach (var scales in new[] { Vector2.one, new Vector2(.65f, 1.4f) })
            {
                ui.upperImage.GetComponent<DifferenceBoard>().SetZoom(1);
                upperViewport.localScale = new Vector3(scales.x, scales.x, 1);
                lowerViewport.localScale = new Vector3(scales.y, scales.y, 1);
                ui.play.GetComponent<DifferencePlayLayout>().ApplyLayout();
                foreach (var zoom in new[] { 1f, 2.5f })
                {
                    ui.upperImage.GetComponent<DifferenceBoard>().SetZoom(zoom);
                    foreach (var sourceDiameter in new[] { 6f, 123f, 900f })
                    {
                        var measured = new Vector2[2];
                        for (var side = 0; side < 2; side++)
                        {
                            var board = side == 0 ? ui.upperImage.rectTransform : ui.lowerImage.rectTransform;
                            var marker = (side == 0 ? ui.topRings : ui.bottomRings)[0];
                            var point = new Vector2(.4f, .5f);
                            region.Invoke(null, new object[] { marker.rectTransform,
                                new Vector4(point.x, point.y, sourceDiameter / 1500, sourceDiameter / 1000) });
                            show.Invoke(ui, new object[] { marker, false });
                            var ring = marker.GetComponentInChildren<DifferenceFoundRing>(true);
                            ring.sourceImageWidth = 1500;
                            ring.Play(false);
                            ring.rectTransform.ForceUpdateRectTransforms();
                            var pictureWidth = play.InverseTransformVector(board.TransformVector(Vector3.right * board.rect.width)).magnitude;
                            var expectedRadius = Mathf.Clamp(sourceDiameter / 1500 * pictureWidth / zoom * ui.foundRingStyle.sizeMultiplier +
                                ui.foundRingStyle.radiusOffset * 2, ui.foundRingStyle.minDiameter, ui.foundRingStyle.maxDiameter) * .5f * zoom;
                            var pixelUnit = pictureWidth / 1500;
                            var expectedStroke = Mathf.Clamp(ui.foundRingStyle.lineWidth * pixelUnit, .1f * pixelUnit, expectedRadius - .5f * pixelUnit);
                            measured[side] = Measure(ring, play);
                            diagnosis = $"authored sourceDiameter={sourceDiameter:R}, viewportScales={scales}, zoom={zoom:R}, side={side}, pictureWidth={pictureWidth:R}, measured={measured[side]}, expectedRadius={expectedRadius:R}, expectedStroke={expectedStroke:R}";
                            Check(Mathf.Abs(measured[side].x - expectedRadius) < .001f,
                                "Authored circles must apply layout-unit minimum and maximum diameters before board zoom.");
                            Check(Mathf.Abs(measured[side].y - expectedStroke) < .001f && measured[side].x > measured[side].y,
                                "Authored strokes must follow source-image scaling and leave the small circle's center transparent.");
                            var center = board.InverseTransformPoint(ring.rectTransform.TransformPoint(ring.rectTransform.rect.center));
                            var normalized = new Vector2((center.x - board.rect.xMin) / board.rect.width, 1 - (center.y - board.rect.yMin) / board.rect.height);
                            Check(Vector2.Distance(normalized, point) < .00001f, "Authored circle centers must remain normalized to their own board.");
                            var inner = play.InverseTransformVector(ring.transform.TransformVector(Vector3.right * ring.InnerRadius)).magnitude;
                            Check(Mathf.Abs(inner - (measured[side].x - measured[side].y)) < .001f,
                                "Authored flash masks must use the same source-relative stroke as the visible circle.");
                        }
                        Check(Vector2.Distance(measured[0], measured[1]) < .001f,
                            "Authored circles and strokes must match across differently scaled viewports.");
                    }
                }
            }
            var localLevel = ui.levels[0];
            var savedMarkers = localLevel.markerRegions;
            try
            {
                var expectedHitX = localLevel.hitSpots != null ? localLevel.hitSpots[0].x : localLevel.regions[0].x;
                var expectedHitY = localLevel.hitSpots != null ? localLevel.hitSpots[0].y : localLevel.regions[0].y;
                localLevel.markerRegions = (Vector4[])localLevel.regions.Clone();
                var authored = localLevel.markerRegions[0] = new Vector4(.78f, .542f, 123f / 1500, .123f);
                typeof(UIDifferences).GetMethod("ConfigureLevel", Private).Invoke(ui, null);
                Check(Mathf.Abs(ui.spots[0].x - expectedHitX) < .00001f && Mathf.Abs(ui.spots[0].y - expectedHitY) < .00001f,
                    "Configuring authored markers must preserve the original gameplay hit center.");
                foreach (var marker in new[] { ui.topRings[0], ui.bottomRings[0] })
                {
                    show.Invoke(ui, new object[] { marker, false });
                    var board = (RectTransform)marker.transform.parent;
                    marker.rectTransform.ForceUpdateRectTransforms();
                    var center = board.InverseTransformPoint(marker.rectTransform.TransformPoint(marker.rectTransform.rect.center));
                    var normalized = new Vector2((center.x - board.rect.xMin) / board.rect.width, 1 - (center.y - board.rect.yMin) / board.rect.height);
                    Check(Vector2.Distance(normalized, new Vector2(authored.x, authored.y)) < .00001f &&
                        Mathf.Abs(marker.rectTransform.rect.width / board.rect.width - authored.z) < .00001f &&
                        Mathf.Abs(marker.rectTransform.rect.height / board.rect.height - authored.w) < .00001f &&
                        marker.GetComponentInChildren<DifferenceFoundRing>(true).sourceImageWidth == ui.upperImage.sprite.rect.width,
                        "ConfigureLevel and ShowFoundRing must apply authored center, diameter and source-relative stroke to both boards.");
                }
            }
            finally { localLevel.markerRegions = savedMarkers; }
            File.WriteAllText(report, "PASS: remote parser self-checks; all 15 fallback pairs share normalized centers, radii, strokes and mask boundaries at min/intermediate/max sizes and zoom 1/2.5; authored 6/123/900-pixel circles apply min 10/max 160 in layout units, retain source-relative strokes with transparent small-circle centers under equal/different viewport scales and zoom 1/2.5; ConfigureLevel and ShowFoundRing apply authored geometry to both boards without moving hit centers.\n" + diagnosis);
        }
        catch (Exception error)
        {
            File.WriteAllText(report, "FAIL: " + error + "\n" + diagnosis);
            throw;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Vector2 Measure(DifferenceFoundRing ring, Transform play)
    {
        using (var mesh = new VertexHelper())
        {
            typeof(DifferenceFoundRing).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly).Invoke(ring, new object[] { mesh });
            var min = float.MaxValue; var max = 0f; var vertex = new UIVertex();
            for (var i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                var radius = play.InverseTransformVector(ring.transform.TransformVector(vertex.position - (Vector3)ring.rectTransform.rect.center)).magnitude;
                min = Mathf.Min(min, radius); max = Mathf.Max(max, radius);
            }
            Check(mesh.currentVertCount > 0, "The found circle mesh is empty.");
            return new Vector2(max, max - min);
        }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif

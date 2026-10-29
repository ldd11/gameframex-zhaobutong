#if ENABLE_UI_UGUI
using System;
using System.IO;
using System.Reflection;
using Hotfix.UI;
using UnityEditor;
using UnityEngine;

public static class DifferenceProfilePriceCheck
{
    [MenuItem("Tools/Find Differences/Check Profile Price Layout Only")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before checking Profile prices.");
        const string report = "Temp/FindDifferences-profile-price-layout.txt";
        var root = PrefabUtility.LoadPrefabContents("Assets/Bundles/UI/UIDifferences/UIDifferences.prefab");
        try
        {
            var ui = root.GetComponent<UIDifferences>();
            var choices = ui.profile.transform.Find("Card/ChoicesScroll/Viewport/Choices");
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
            void Set(string name, object value) => typeof(UIDifferences).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, value);
            void Refresh() => typeof(UIDifferences).GetMethod("RefreshProfile", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            RectTransform Price(int index) => (RectTransform)choices.Find("Choice" + index + "/Price");
            void Geometry(int index)
            {
                var price = Price(index); var coin = (RectTransform)price.Find("Coin");
                var paid = index >= 4 && index <= 7;
                Check(price.anchorMin == Vector2.one * .5f && price.anchorMax == price.anchorMin &&
                    price.pivot == new Vector2(.5f, paid ? .5f : 1) &&
                    Vector3.Distance(price.anchoredPosition3D, paid ? new Vector3(11.3f, -83.69f, 0) : new Vector3(.5f, -66, 0)) < .001f &&
                    Vector2.Distance(price.sizeDelta, new Vector2(paid ? 59.33f : 108.2f, 39.8f)) < .001f,
                    "Price geometry differs from its unlock type: " + index);
                Check(coin.anchorMin == new Vector2(0, .5f) && coin.anchorMax == coin.anchorMin && coin.pivot == new Vector2(1, .5f) &&
                    Vector3.Distance(coin.anchoredPosition3D, new Vector3(0, -1.900024f, 0)) < .001f && coin.sizeDelta == new Vector2(32, 32) &&
                    coin.localRotation == Quaternion.identity && coin.localScale == Vector3.one,
                    "Coin geometry differs from the screenshot: " + index);
                var text = price.GetComponent<UnityEngine.UI.Text>();
                Check(text.fontSize == 28 && text.resizeTextForBestFit && text.resizeTextMinSize == 10 && text.resizeTextMaxSize == 28,
                    "Price font settings must remain unchanged: " + index);
            }
            Check(choices.childCount == 38, "Expected all 38 Profile choices.");
            for (var i = 0; i < choices.childCount; i++) Geometry(i);
            Set("avatar", 0); Set("frame", 0); Set("draftAvatar", 0); Set("draftFrame", 0);
            Set("ownedFrames", 15); Set("coins", 10000); Set("completed", 0); Set("journeyCompleted", 0);
            ui.modal.SetActive(false); ui.profile.SetActive(true);
            for (var cycle = 0; cycle < 2; cycle++)
            {
                Set("frameTab", false); Refresh();
                for (var i = 0; i < 38; i++) Check(!Price(i).gameObject.activeSelf, "Free avatars must not display a price.");
                Set("frameTab", true); Refresh();
                var labels = new[] { "288", "888", "1588", "2588", "Level 30", "Journey 20", "Journey 50", "Level 100" };
                for (var i = 4; i < 12; i++)
                {
                    Geometry(i);
                    Check(Price(i).gameObject.activeSelf && Price(i).GetComponent<UnityEngine.UI.Text>().text == labels[i - 4] &&
                        Price(i).Find("Coin").gameObject.activeSelf == (i < 8), "Unlock text or coin visibility changed: " + i);
                }
                Price(4).sizeDelta = Price(8).sizeDelta = Vector2.one;
                Price(4).anchoredPosition = Price(8).anchoredPosition = Vector2.zero;
                ((RectTransform)Price(4).Find("Coin")).anchorMin = Vector2.one;
            }
            Set("ownedFrames", 15 | 1 << 4); Set("completed", 30); Set("journeyCompleted", 20); Refresh();
            Check(!Price(4).gameObject.activeSelf && !Price(8).gameObject.activeSelf && !Price(9).gameObject.activeSelf && Price(10).gameObject.activeSelf,
                "Purchased and progress-unlocked frames must hide their conditions independently.");
            Directory.CreateDirectory("Temp");
            File.WriteAllText(report, "PASS: all 38 saved Price/Coin geometries; coin and level conditions after repeated Avatar/Frame switches; automatic layout restoration; unchanged costs/level requirements; hidden conditions after unlock.");
        }
        catch (Exception error) { File.WriteAllText(report, "FAIL: " + error); throw; }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif

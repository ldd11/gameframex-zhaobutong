"""Run with Python 3 to verify the optional mobile SDK vendor payload."""
from pathlib import Path
import json
import re
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "Assets/ThirdParty/DifferenceSdk"
NATIVE = ROOT / "SdkDependencies/native/Android"
MACRO = "ENABLE_DIFFERENCE_SDK"


def check():
    required = {
        "Adjust/Scripts/AdjustSdk.Scripts.asmdef",
        "Max/Scripts/MaxSdk.Scripts.asmdef",
        "Firebase/Plugins/Firebase.App.dll",
        "Firebase/Plugins/iOS/Firebase.App.dll",
        "Firebase/Native/iOS/libFirebaseCppApp.a",
        "GoogleMobileAds/GoogleMobileAds.dll",
        "GoogleMobileAds/Native/iOS/unity-plugin-library.xcframework/Info.plist",
    }
    assert all((ASSETS / name).is_file() for name in required), "Missing mobile SDK payload"
    for sdk in ("Adjust", "Max", "Runtime"):
        directory = ASSETS / sdk if sdk == "Runtime" else ASSETS / sdk / "Scripts"
        asmdef = next(directory.glob("*.asmdef"))
        data = json.loads(asmdef.read_text(encoding="utf-8-sig"))
        assert data["defineConstraints"] == [MACRO], asmdef
        assert set(data["includePlatforms"]) == {"Android", "iOS", "Editor"}, asmdef
    for path in ASSETS.rglob("*.meta"):
        text = path.read_text(encoding="utf-8-sig")
        if "PluginImporter:" in text:
            assert re.search(r"defineConstraints:\s*\n\s*- " + MACRO, text), path
            assert "enabled: 1" in text, path
        assert "GoogleService-Info" not in path.name and "google-services" not in path.name, path
    assert not list(ASSETS.rglob("*Dependencies.xml")), "Unconditional SDK dependency resolver"
    assert not list(ASSETS.rglob("*.aar")), "Android payload must stay outside Assets"
    for path in ASSETS.rglob("link.xml"):
        assert all('ignoreIfMissing="1"' in line for line in path.read_text().splitlines() if "<assembly" in line), path
    android_archives = list(NATIVE.rglob("*.aar"))
    assert len(android_archives) == 8, "Three bridge AARs and five Firebase Maven AARs required"
    for path in android_archives:
        with zipfile.ZipFile(path) as archive:
            assert archive.testzip() is None, path
            assert "AndroidManifest.xml" in archive.namelist(), path
    poms = list((NATIVE / "m2repository").rglob("*.pom"))
    assert len(poms) == 5, "Five Firebase Maven POMs required"
    for path in poms:
        packaging = ET.parse(path).getroot().find("{http://maven.apache.org/POM/4.0.0}packaging")
        assert packaging is not None and packaging.text == "aar", path
        assert path.with_suffix(".aar").is_file(), path
    for name in ("sdk.gradle", "compatibility.gradle", "repositories.gradle", "sdk-proguard.pro", "MessagingUnityPlayerActivity.java"):
        assert (ROOT / "SdkDependencies/Android" / name).is_file(), name
    assert (NATIVE / "java/com/difference/sdk/DifferenceBannerAnalytics.java").is_file(), "Missing Banner native request listener"
    gradle = (ROOT / "SdkDependencies/Android/sdk.gradle").read_text(encoding="utf-8-sig")
    assert "sourceSets.main.java.srcDir 'difference-sdk/java'" in gradle, "Banner request bridge missing from SDK build"
    ids = [line.strip() for line in (ROOT / "SdkDependencies/iOS/SKAdNetworkIds.txt").read_text().splitlines() if line.strip() and not line.startswith("#")]
    assert len(ids) >= 100 and len(ids) == len(set(ids)), "Incomplete or duplicate iOS SKAdNetwork list"
    assert all(re.fullmatch(r"[a-z0-9]{10}\.skadnetwork", item) for item in ids), "Invalid iOS SKAdNetwork ID"
    guids = {}
    for base in (ROOT / "Assets", ROOT / "Packages"):
        for path in base.rglob("*.meta"):
            match = re.search(r"^guid: (\w+)", path.read_text(encoding="utf-8-sig", errors="replace"), re.M)
            if match:
                guids.setdefault(match[1], []).append(path)
    assert not [paths for paths in guids.values() if len(paths) > 1 and any(ASSETS in p.parents for p in paths)], "Duplicate SDK asset GUID"
    files = [p for base in (ASSETS, NATIVE) for p in base.rglob("*") if p.is_file() and p.suffix != ".meta"]
    for path in files:
        with path.open("rb") as file:
            assert not file.read(50).startswith(b"version https://git-lfs.github.com/spec/v1"), path
    print(f"SDK vendor check PASS: {len(files)} files, {sum(p.stat().st_size for p in files):,} bytes; 8 Android AARs; {len(ids)} iOS SKAdNetwork IDs; constrained mobile plugins; unique GUIDs.")


if __name__ == "__main__":
    check()

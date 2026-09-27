# Optional mobile SDK dependencies

安卓安装测试：退出 Unity 运行模式，打开 `GameFrameX > Build > Mobile Build (SDK)`，勾选“包含 SDK（两个平台）”和“Adjust 测试环境”，等待编译完成后点击“打 Android APK”。AAB 用于提交商店。若刚切到 Android 平台，等待切换和编译完成后再点击一次。

“Adjust 测试环境”只控制 Adjust，不会自动开启 MAX 的测试广告。Event Token 留空不阻止构建，只影响对应的 Adjust 事件上报。Firebase 配置保存在 `LocalSdkConfig/`，相对路径以项目根目录解析；文件缺失时可在窗口点击“选择”重新指定。`SdkDependencies/` 是打包所需依赖，需要保留在工程中。

Use `GameFrameX > Build > Mobile Build (SDK)` to enable `ENABLE_DIFFERENCE_SDK` for Android and iOS. SDK builds include MAX, Adjust, Firebase and the Google Mobile Ads/UMP integration. Ordinary builds omit their managed assemblies, native libraries and platform dependency configuration. Application keys and Firebase configuration belong in `DifferenceSdkSettings`; no application credentials are included here.

Android SDK builds require API 24 or later. The build hook copies `Android/` and `native/Android/` into the generated `unityLibrary/difference-sdk` directory and removes that directory when SDK support is disabled. APK and AAB use the same optional payload. Android Java source in this directory is compiled only by the SDK Gradle script.

iOS SDK exports require iOS 15.0 or later. `DifferenceSdkIosBuild.cs` supplies the CocoaPods dependencies, project capabilities and application configuration; `iOS/SKAdNetworkIds.txt` supplies the attribution identifiers. On a Mac, run `pod install` in the exported directory and open the `.xcworkspace` to compile and sign. Native plugin importers require the SDK macro. Ordinary iOS exports must use a clean output directory so prior SDK Pod/framework files are not reused.

## Source versions

The runtime payload was imported from `jigsawpuzzle`, branch `christmas2026/SDK`, commit `a1443b435711efec15e6f1599a0e7abcd700e455`, and its exact cached MAX package.

| Component | Unity version | Source |
| --- | --- | --- |
| Adjust | 5.3.0 | `Puzzle/Assets/Adjust` |
| AppLovin MAX | 8.6.5 | `com.applovin.mediation.ads@8.6.5` |
| Firebase | 12.8.0 | `Puzzle/Assets/Firebase` and `Puzzle/Assets/Plugins/iOS/Firebase` |
| Google Mobile Ads / UMP bridge | 11.4.0 | `Puzzle/Assets/GoogleMobileAds` and `Puzzle/Assets/Plugins` |

Firebase includes Analytics, Crashlytics, Messaging and Remote Config. Native Maven/CocoaPods version pins are in `Android/sdk.gradle` and `Assets/Editor/DifferenceSdkIosBuild.cs`.

## Layout and vendor changes

- `Assets/ThirdParty/DifferenceSdk`: managed runtime and mobile native plugins, retaining original GUIDs and gated by `ENABLE_DIFFERENCE_SDK`. Vendor editor dependency resolvers, sample prefabs and desktop Firebase native libraries are excluded.
- `native/Android/libs`: three unmodified MAX, GMA and Firebase Messaging bridge AARs, outside `Assets` to prevent inclusion in ordinary packages.
- `native/Android/m2repository`: five Firebase Unity Maven packages. Original `.srcaar` files are renamed `.aar`, and POM packaging is changed to `aar`; their binary contents are unchanged. The build hook resolves these five modules exclusively from this repository.
- `native/Android/java`: the Banner request listener installs before the first native load and preserves MAX's existing ad/revenue listeners. It depends on MAX Unity 8.6.5's private `retrieveAdView` signature; recheck it on SDK upgrades. Hook failure logs a warning and preserves advertising. The provider suppresses Banner batch results until an actual request callback is observed, while valid revenue callbacks remain independent.
- MAX's iOS `MAUnityAdManager.m` adds a separate Banner request delegate without replacing its ad/revenue delegates. Current MAX Unity has no Banner display-failure callback; this metric must not be inferred from a load failure.
- `link.xml` preserves optional SDK callbacks for IL2CPP and ignores SDK assemblies absent from ordinary builds.

Licenses are in `licenses/`. Adjust and Firebase license texts are from their exact release tags: [Adjust v5.3.0](https://github.com/adjust/unity_sdk/blob/v5.3.0/LICENSE) and [Firebase v12.8.0](https://github.com/firebase/firebase-unity-sdk/blob/v12.8.0/LICENSE). Native dependencies retain their own upstream notices.

Run `python SdkDependencies/check_vendor.py` to verify payload presence, AAR integrity, Maven packaging, mobile SDK macro constraints, attribution IDs and duplicate GUIDs. This does not initialize an SDK or upload analytics.

## Recovery provenance

On 2026-09-27 this missing directory was restored from the previously compiled `Temp/sdk-android-native-probe/on/unityLibrary/difference-sdk` snapshot. All eight restored AARs were compared by SHA-256 with the original imported SDK files. The redundant `maven.google.com` repository was removed again, retaining the earlier connectivity fix. The 152 iOS SKAdNetwork identifiers were recovered from two matching previously validated Xcode export fixtures. Existing `Assets` runtime scripts and user configuration were not replaced.

using System.Net;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameFrameX;
using GameFrameX.Event.Runtime;
#if ENABLE_UI_FAIRYGUI
using GameFrameX.UI.FairyGUI.Runtime;
#endif
using GameFrameX.Network.Runtime;
using Hotfix.Proto;
using SimpleJSON;
using UnityEngine;
using GameFrameX.Runtime;
using GameFrameX.Startup.Application;
using GameFrameX.UI.Runtime;
using Hotfix.Config;
using Hotfix.Config.Local;
using Hotfix.Config.Tables;
using Hotfix.UI;
#if ENABLE_BINARY_CONFIG
using LuBan.Runtime;
#endif

namespace Hotfix
{
    public static class HotfixLauncher
    {
        public static Task Main()
        {
#if ENABLE_UI_UGUI
            GameApp.Base.FrameRate = 60;
            return LaunchDifferences();
#else
            Log.Info("Hello World HybridCLR");
            ProtoMessageIdHandler.Init(HotfixProtoHandler.CurrentAssembly);
            LoadConfig();
            LoadUI();
            return Task.CompletedTask;
#endif
        }

#if ENABLE_UI_UGUI
        private static async Task LaunchDifferences()
        {
            var home = await GameApp.UI.OpenFullScreenAsync<UIDifferences>(userData: UIDifferences.StartupDataMarker);
            DifferenceStartupAnalytics.PrepareHome(home);
            await home.PrepareStartupDataAsync();
            // Keep the launcher visible through the home's first layout/update frame.
            await UniTask.NextFrame();
            Canvas.ForceUpdateCanvases();
        }
#endif

        private static async void LoadUI()
        {
#if ENABLE_UI_FAIRYGUI
            GameApp.FUIPackage.AddPackageAsync(Utility.Asset.Path.GetUIPackagePath(FUIPackage.UICommon));
            GameApp.FUIPackage.AddPackageAsync(Utility.Asset.Path.GetUIPackagePath(FUIPackage.UICommonAvatar));
            GameApp.FUIPackage.AddPackageAsync(Utility.Asset.Path.GetUIPackagePath(FUIPackage.UIRoom));
#endif
            await GameApp.UI.OpenAsync<UILogin>();
            var item = GameApp.Config.GetConfig<TbSoundsConfig>().FirstOrDefault;
            Log.Info(item);
        }

        static async void LoadConfig()
        {
            _tablesComponent= new TablesComponent();
            _tablesComponent.Init(GameApp.Config);
#if ENABLE_BINARY_CONFIG
            // 使用二进制配置表
            await _tablesComponent.LoadAsync(ConfigBufferLoader);
#else
            // 使用JSON配置表
            await _tablesComponent.LoadAsync(ConfigLoader);
#endif
            TranslateText();
        }
        static TablesComponent _tablesComponent;

        /// <summary>
        /// 翻译本地化
        /// </summary>
        private static void TranslateText()
        {
            // 1. 删除源数据
            GameApp.Localization.RemoveAllRawStrings();
            // 2. 从配置表中加载数据
            var tbLocalization = GameApp.Config.GetConfig<TbLocalization>();
            // 3. 装填数据信息
            tbLocalization.ForEach(localization =>
            {
                if (localization.Key.IsNullOrWhiteSpace())
                {
                    return;
                }

                GameApp.Localization.AddRawString(localization.Key, localization.English);
            });

            // 刷新Luban配置表中的本地化内容
            _tablesComponent.SetTranslateText((key, y) =>
            {
                if (key.IsNullOrWhiteSpace())
                {
                    return string.Empty;
                }

                return GameApp.Localization.GetString(key);
            });
        }


#if ENABLE_BINARY_CONFIG
        /// <summary>
        /// 加载二进制配置表
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private static async Task<ByteBuf> ConfigBufferLoader(string file)
        {
            var assetHandle = await GameApp.Asset.LoadAssetAsync<TextAsset>(Utility.Asset.Path.GetConfigPath(file, Utility.Const.FileNameSuffix.Binary));
            return ByteBuf.Wrap(assetHandle.GetAssetObject<TextAsset>().bytes);
        }
#else
        /// <summary>
        /// 加载json配置表
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private static async Task<JSONNode> ConfigLoader(string file)
        {
            var assetHandle = await GameApp.Asset.LoadAssetAsync<TextAsset>(Utility.Asset.Path.GetConfigPath(file, Utility.Const.FileNameSuffix.Json));
            return JSON.Parse(assetHandle.GetAssetObject<TextAsset>().text);
        }
#endif
    }
}

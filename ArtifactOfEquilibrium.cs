using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ShareChest
{
    internal class ArtifactOfEquilibrium : ArtifactBase
    {
        // 内部标识：纯英文，用于日志、配置键名等
        public override string ArtifactName => "Artifact of Equilibrium";
        public override string ArtifactLangTokenName => "EQUILIBRIUM";

        // 游戏内显示的名称：中文（通过 LanguageAPI 添加）
        public override string DisplayName => "Artifact of Equilibrium - 均衡神器";

        public override string ArtifactDescription => "开启后，宝箱会掉落多倍物品（根据玩家数量动态调整或固定倍率，需前往配置文件修改配置）。\n启用此神器以激活共享宝箱Mod的多倍掉落。\n\nOnce activated, the treasure chest will drop items in multiple times (the multiplier is dynamically adjusted according to the number of players or can be set as a fixed rate. The configuration needs to be modified in the configuration file). \nEnable this artifact to activate the multiple-dropping feature of the Shared Treasure Chest Mod.";

        public override Sprite ArtifactEnabledIcon => GetIcon("ArtifactOfEquilibrium_On");
        public override Sprite ArtifactDisabledIcon => GetIcon("ArtifactOfEquilibrium_Off");

        private Sprite GetIcon(string iconName)
        {
            if (ShareChestPlugin.MainAssets != null)
            {
                var sprite = ShareChestPlugin.MainAssets.LoadAsset<Sprite>(iconName);
                if (sprite != null)
                    return sprite;
                ShareChestPlugin.LogWarning($"未找到图标 {iconName}，将使用默认占位图标");
            }
            else
            {
                ShareChestPlugin.LogWarning("AssetBundle 未加载，使用默认占位图标");
            }

            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels32(new Color32[4] { Color.clear, Color.clear, Color.clear, Color.clear });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        public override void Init(ConfigFile config)
        {
            CreateLang();
            bool added = CreateArtifact();
            if (added)
                ShareChestPlugin.LogInfo($"神器 {ArtifactName} 已成功注册到游戏。");
            else
                ShareChestPlugin.LogError($"神器 {ArtifactName} 注册失败。");
            Hooks();
        }

        public override void Hooks()
        {
            Run.onRunStartGlobal += OnRunStart;
        }

        private void OnRunStart(Run run)
        {
            if (!NetworkServer.active) return;
            ShareChestPlugin.IsArtifactEnabled = ArtifactEnabled;
            ShareChestPlugin.LogInfo($"神器均衡宝箱状态: {(ShareChestPlugin.IsArtifactEnabled ? "启用" : "禁用")}");
        }
    }
}
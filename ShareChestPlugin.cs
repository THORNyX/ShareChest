using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Runtime.CompilerServices;

namespace ShareChest
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(R2API.ItemAPI.PluginGUID)]
    [BepInDependency(R2API.LanguageAPI.PluginGUID)]
    public class ShareChestPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.Muskmelovon.ShareChest";
        public const string PluginName = "共享宝箱";
        public const string PluginVersion = "1.0.0";

        private static ManualLogSource Log;
        private static Harmony harmony;

        // 配置项
        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<bool> EnableDynamicMultiplier;
        public static ConfigEntry<int> FixedMultiplier;
        public static ConfigEntry<bool> EnableDebugLogs;
        public static ConfigEntry<string> AffectedChestTypes;
        public static ConfigEntry<float> DropDistanceClamp;

        private static readonly string ChestListSeparator = ",";
        private static HashSet<string> affectedChestTypeSet = new HashSet<string>();
        public static bool IsInitialized { get; private set; } = false;

        // 神器相关
        public static AssetBundle MainAssets;
        private static List<ArtifactBase> artifacts = new List<ArtifactBase>();
        public static bool IsArtifactEnabled = false;

        public void Awake()
        {
            Log = Logger;

            try
            {
                Log.LogInfo($"========== {PluginName} v{PluginVersion} 初始化开始 ==========");

                // 加载 AssetBundle（自动查找嵌入资源）
                LoadAssetBundle();

                InitializePlayerCountManager();
                InitializeConfiguration();
                InitializeArtifacts();
                InitializeHarmonyPatches();

                IsInitialized = true;

                Log.LogInfo($"插件初始化成功！");
                Log.LogInfo($"当前配置：插件总开关={ModEnabled.Value}, 动态倍率={EnableDynamicMultiplier.Value}");
                Log.LogInfo($"掉落距离设置：{DropDistanceClamp.Value}（原版为6）");
                Log.LogInfo($"应用倍率的宝箱类型：{(affectedChestTypeSet.Count > 0 ? string.Join(", ", affectedChestTypeSet) : "所有宝箱")}");
                Log.LogInfo($"配置位置：BepInEx/config/{PluginGUID}.cfg");
                Log.LogInfo($"调试命令：按 F5 显示调试信息，按 F6 强制重新计算玩家数量");
                Log.LogInfo($"==================================================");
            }
            catch (Exception ex)
            {
                Log.LogError($"插件初始化失败！");
                Log.LogError($"错误信息：{ex.Message}");
                Log.LogError($"堆栈跟踪：{ex.StackTrace}");
                if (ex.InnerException != null)
                    Log.LogError($"内部异常：{ex.InnerException.Message}");
                try { harmony?.UnpatchSelf(); } catch { }
                throw;
            }
        }

        private void LoadAssetBundle()
        {
            string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            string bundleResourceName = resourceNames.FirstOrDefault(r => r.EndsWith(".assets", StringComparison.OrdinalIgnoreCase));

            if (bundleResourceName == null)
            {
                Log.LogWarning("未找到任何 .assets 嵌入资源，神器图标将使用默认占位图。");
                return;
            }

            Log.LogInfo($"找到嵌入资源: {bundleResourceName}");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(bundleResourceName))
            {
                if (stream == null)
                {
                    Log.LogError($"无法获取资源流: {bundleResourceName}");
                    return;
                }

                MainAssets = AssetBundle.LoadFromStream(stream);
                if (MainAssets == null)
                {
                    Log.LogError("AssetBundle.LoadFromStream 返回 null，请检查资源文件是否有效。");
                }
                else
                {
                    Log.LogInfo($"AssetBundle 加载成功，包含的资源: {string.Join(", ", MainAssets.GetAllAssetNames())}");
                }
            }
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                ShowDebugInfo();
            if (Input.GetKeyDown(KeyCode.F6))
                ForceRecalculate();
        }

        private void InitializePlayerCountManager()
        {
            Log.LogInfo("正在初始化 PlayerCountManager...");
            if (PlayerCountManager.Instance != null)
            {
                Log.LogWarning("PlayerCountManager 已存在，跳过创建");
                return;
            }
            GameObject managerObj = new GameObject("ShareChest_PlayerCountManager");
            managerObj.AddComponent<PlayerCountManager>();
            DontDestroyOnLoad(managerObj);
            Log.LogInfo("PlayerCountManager 初始化完成");
        }

        private void InitializeConfiguration()
        {
            Log.LogInfo("正在初始化配置系统...");

            ModEnabled = Config.Bind("通用设置", "启用插件", true,
                new ConfigDescription("插件总开关。注意：同时需要神器“共享宝箱”启用才能生效。", null,
                new ConfigurationManagerAttributes { Order = 10, IsAdvanced = false }));

            EnableDynamicMultiplier = Config.Bind("掉落设置", "启用动态倍率", false,
                new ConfigDescription("是否根据玩家人数动态调整掉落倍率\n• true = 启用动态倍率（倍率 = 玩家人数）\n• false = 使用固定倍率", null,
                new ConfigurationManagerAttributes { Order = 15, IsAdvanced = false }));

            FixedMultiplier = Config.Bind("掉落设置", "固定掉落倍率", 5,
                new ConfigDescription("当'启用动态倍率'为false时使用的固定掉落倍率\n" +
                    "• 1 = 正常掉落（原版行为）\n" +
                    "• 5 = 掉落5个相同物品（推荐）\n" +
                    "• 单人固定倍率推荐1，多人推荐动态倍率\n" +
                    "• 注意：过高的值可能影响游戏性能", new AcceptableValueRange<int>(1, 50),
                new ConfigurationManagerAttributes { Order = 20, IsAdvanced = false }));

            DropDistanceClamp = Config.Bind("掉落设置", "掉落距离限制", 6f,
                new ConfigDescription("控制物品掉落时的最小距离限制\n" +
                    "• 默认值为6,不推荐更改\n" +
                    "• 较低的值使物品掉落更集中\n" +
                    "• 较高的值使物品掉落更分散\n" +
                    "• 注意：过高的值可能导致物品飞出太远", new AcceptableValueRange<float>(1f, 15f),
                new ConfigurationManagerAttributes { Order = 25, IsAdvanced = false }));

            AffectedChestTypes = Config.Bind("宝箱类型设置", "应用倍率的宝箱类型",
                "Chest1(Clone),Chest2(Clone),GoldChest,LunarChest(Clone),VoidChest(Clone),CategoryChestUtility(Clone),CategoryChest2Utility Variant(Clone),CategoryChestHealing(Clone),CategoryChest2Healing Variant(Clone),CategoryChestDamage(Clone),CategoryChest2Damage Variant(Clone)",
                new ConfigDescription("指定哪些宝箱类型应用掉落倍率\n" +
                    "• 用逗号分隔多个宝箱类型\n" +
                    "• 常见宝箱类型：\n" +
                    "  - Chest1(Clone): 普通宝箱\n" +
                    "  - Chest2(Clone): 大宝箱\n" +
                    "  - GoldChest: 传奇宝箱\n" +
                    "  - LunarChest(Clone): 月球舱\n" +
                    "  - VoidChest(Clone): 虚空摇篮\n" +
                    "  - EquipmentBarrel(Clone): 装备箱(默认关闭)\n" +
                    "  - Lockbox(Clone): 生锈带锁箱(默认关闭)\n" +
                    "  - CategoryChestUtility(Clone): 辅助箱\n" +
                    "  - CategoryChest2Utility Variant(Clone): 大辅助箱\n" +
                    "  - CategoryChestHealing(Clone): 治疗箱\n" +
                    "  - CategoryChest2Healing Variant(Clone): 大治疗箱\n" +
                    "  - CategoryChestDamage(Clone): 伤害箱\n" +
                    "  - CategoryChest2Damage Variant(Clone): 大伤害箱\n" +
                    "• 默认关闭倍率宝箱类型：\n" +
                    "  - EquipmentBarrel(Clone): 装备箱\n" +
                    "  - Lockbox(Clone): 生锈带锁箱\n" +
                    "• 如需启用请在默认值中添加\n" +
                    "• 留空表示所有宝箱都应用倍率\n" +
                    "• 注意：名字必须完全匹配，包括大小写和括号", null,
                new ConfigurationManagerAttributes { Order = 80, IsAdvanced = false }));

            EnableDebugLogs = Config.Bind("调试设置", "启用调试日志", true,
                new ConfigDescription("启用详细的调试日志输出", null,
                new ConfigurationManagerAttributes { Order = 1000, IsAdvanced = true }));

            UpdateAffectedChestTypes();

            ModEnabled.SettingChanged += (s, e) => LogInfo($"插件总开关已变更：{(ModEnabled.Value ? "启用" : "禁用")}");
            EnableDynamicMultiplier.SettingChanged += (s, e) => LogInfo($"倍率模式已变更：{(EnableDynamicMultiplier.Value ? "动态" : "固定")}");
            FixedMultiplier.SettingChanged += (s, e) => LogInfo($"固定倍率已变更：{FixedMultiplier.Value}x");
            DropDistanceClamp.SettingChanged += (s, e) => LogInfo($"掉落距离限制已变更：{DropDistanceClamp.Value}");
            AffectedChestTypes.SettingChanged += (s, e) => UpdateAffectedChestTypes();
            EnableDebugLogs.SettingChanged += (s, e) => LogInfo($"调试日志已{(EnableDebugLogs.Value ? "启用" : "禁用")}");
        }

        private void InitializeArtifacts()
        {
            Log.LogInfo("正在初始化神器系统...");
            var artifactTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ArtifactBase)));
            foreach (var type in artifactTypes)
            {
                var artifact = (ArtifactBase)Activator.CreateInstance(type);
                var enabled = Config.Bind("神器设置", artifact.ArtifactName + "_启用", true, "是否在神器选择界面显示该神器").Value;
                if (enabled)
                {
                    artifact.Init(Config);
                    artifacts.Add(artifact);
                    Log.LogInfo($"已加载神器: {artifact.ArtifactName}");
                }
            }
            Log.LogInfo($"神器系统初始化完成，共加载 {artifacts.Count} 个神器");
        }

        private void InitializeHarmonyPatches()
        {
            Log.LogInfo("正在应用 Harmony 补丁...");
            harmony = new Harmony(PluginGUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            var patches = harmony.GetPatchedMethods();
            Log.LogInfo($"成功应用 {patches.Count()} 个 Harmony 补丁");
        }

        private static void ShowDebugInfo()
        {
            int count = GetCurrentPlayerCount();
            int multiplier = CalculateMultiplier(count);
            LogInfo($"=== 共享宝箱调试信息 ===");
            LogInfo($"插件总开关: {ModEnabled.Value}");
            LogInfo($"神器启用状态: {IsArtifactEnabled}");
            LogInfo($"当前玩家数量: {count}");
            LogInfo($"动态倍率: {EnableDynamicMultiplier.Value}");
            LogInfo($"固定倍率: {FixedMultiplier.Value}");
            LogInfo($"掉落距离限制: {DropDistanceClamp.Value}");
            LogInfo($"计算后的倍率: {multiplier}x");
            LogInfo($"PlayerCountManager 实例: {(PlayerCountManager.Instance != null ? "存在" : "不存在")}");
            if (PlayerCountManager.Instance != null)
                LogInfo($"PlayerCountManager 计数: {PlayerCountManager.Instance.CurrentPlayerCount}");
            LogInfo($"宝箱类型白名单: {affectedChestTypeSet.Count}种");
            if (affectedChestTypeSet.Count > 0)
                LogInfo($"白名单内容: {string.Join(", ", affectedChestTypeSet)}");
            LogInfo($"========================");
        }

        private static void ForceRecalculate()
        {
            if (PlayerCountManager.Instance != null)
            {
                PlayerCountManager.Instance.RecalculatePlayerCount();
                LogInfo($"已强制重新计算玩家数量: {PlayerCountManager.Instance.CurrentPlayerCount}人");
            }
            else
            {
                LogError("PlayerCountManager 实例不存在，无法重新计算");
            }
        }

        public static int CalculateMultiplier(int currentPlayerCount)
        {
            if (!EnableDynamicMultiplier.Value)
                return Math.Max(1, FixedMultiplier.Value);
            return Math.Max(1, currentPlayerCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCurrentPlayerCount()
        {
            if (PlayerCountManager.Instance != null)
                return PlayerCountManager.Instance.CurrentPlayerCount;
            try
            {
                if (NetworkUser.readOnlyInstancesList != null)
                    return NetworkUser.readOnlyInstancesList.Count;
                if (NetworkServer.active)
                {
                    int count = 0;
                    foreach (var player in PlayerCharacterMasterController.instances)
                        if (player != null && player.isActiveAndEnabled) count++;
                    return Math.Max(1, count);
                }
                return 1;
            }
            catch { return 1; }
        }

        public static string GetMultiplierInfo(int playerCount)
        {
            if (!EnableDynamicMultiplier.Value)
                return $"固定倍率: {FixedMultiplier.Value}x";
            return $"动态倍率: {CalculateMultiplier(playerCount)}x (玩家数: {playerCount}人)";
        }

        private static void UpdateAffectedChestTypes()
        {
            affectedChestTypeSet.Clear();
            string configValue = AffectedChestTypes.Value;
            if (string.IsNullOrWhiteSpace(configValue)) return;
            var types = configValue.Split(new[] { ChestListSeparator }, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(s => s.Trim())
                                  .Where(s => !string.IsNullOrEmpty(s));
            foreach (var type in types)
                affectedChestTypeSet.Add(type);
            LogDebug($"已加载 {affectedChestTypeSet.Count} 种宝箱类型");
        }

        public static bool ShouldApplyToChest(string chestName)
        {
            if (affectedChestTypeSet.Count == 0) return true;
            return affectedChestTypeSet.Contains(chestName);
        }

        public static bool ShouldApplyMultiplier(ChestBehavior chest)
        {
            if (!ModEnabled.Value || !IsArtifactEnabled)
            {
                LogDebug($"多倍掉落未生效：插件总开关={ModEnabled.Value}, 神器启用={IsArtifactEnabled}");
                return false;
            }
            if (chest == null || chest.gameObject == null) return false;
            string chestName = chest.gameObject.name;
            if (!ShouldApplyToChest(chestName))
            {
                LogDebug($"宝箱 {chestName} 不在允许列表中，不应用倍率");
                return false;
            }
            var traverse = Traverse.Create(chest);
            bool isOpened = traverse.Field("isChestOpened").GetValue<bool>();
            if (isOpened) return false;
            if (chest.isCommandChest)
            {
                LogDebug($"命令宝箱不应用倍率");
                return false;
            }
            int playerCount = GetCurrentPlayerCount();
            int multiplier = CalculateMultiplier(playerCount);
            LogDebug($"将对宝箱 {chestName} 应用 {multiplier}x 倍率");
            return true;
        }

        public static void LogDebug(string message)
        {
            if (EnableDebugLogs.Value && Log != null)
                Log.LogInfo($"[调试] {message}");
        }

        public static void LogWarning(string message) => Log?.LogWarning($"[警告] {message}");
        public static void LogError(string message) => Log?.LogError($"[错误] {message}");
        public static void LogInfo(string message) => Log?.LogInfo($"[信息] {message}");
    }

    internal class ConfigurationManagerAttributes
    {
        public int? Order = null;
        public bool? IsAdvanced = null;
        public string Category = null;
        public Action<ConfigEntryBase> CustomDrawer = null;
    }
}
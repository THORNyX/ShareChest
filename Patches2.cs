using HarmonyLib;
using RoR2;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace ShareChest.Patches
{
    [HarmonyPatch]
    public static class ChestBehaviorPatches
    {
        private static readonly HashSet<uint> processedChests = new HashSet<uint>();
        private static int totalChestsGenerated = 0;
        private static int chestsOpenedCount = 0;
        private static float runStartTime = 0f;

        private static uint GetChestUniqueId(ChestBehavior chest)
        {
            if (chest == null) return 0;
            int instanceId = chest.GetInstanceID();
            Vector3 position = chest.transform.position;
            int positionHash = position.GetHashCode();
            return (uint)((instanceId ^ positionHash) & 0x7FFFFFFF);
        }

        [HarmonyPatch(typeof(ChestBehavior), "BaseItemDrop")]
        [HarmonyPrefix]
        private static bool BaseItemDropPrefix(ChestBehavior __instance)
        {
            try
            {
                uint chestId = GetChestUniqueId(__instance);
                if (!NetworkServer.active)
                {
                    ShareChestPlugin.LogDebug($"[{chestId}] 非服务器端，跳过处理");
                    return true;
                }
                if (processedChests.Contains(chestId))
                {
                    ShareChestPlugin.LogDebug($"[{chestId}] 宝箱已处理过，防止重复");
                    return false;
                }
                if (!ShareChestPlugin.ShouldApplyMultiplier(__instance))
                {
                    return true;
                }
                if (__instance == null || __instance.gameObject == null)
                    return true;
                var traverse = Traverse.Create(__instance);
                bool isAlreadyOpened = traverse.Field("isChestOpened").GetValue<bool>();
                if (isAlreadyOpened)
                    return true;
                if (!__instance.currentPickup.isValid)
                {
                    __instance.Roll();
                    if (!__instance.currentPickup.isValid)
                    {
                        ShareChestPlugin.LogWarning($"[{chestId}] 无法生成有效掉落物");
                        return true;
                    }
                }
                processedChests.Add(chestId);
                chestsOpenedCount++;
                int playerCount = ShareChestPlugin.GetCurrentPlayerCount();
                int multiplier = ShareChestPlugin.CalculateMultiplier(playerCount);
                int originalDropCount = __instance.dropCount;
                int finalDropCount = originalDropCount * multiplier;
                if (finalDropCount <= 0)
                {
                    ShareChestPlugin.LogWarning($"[{chestId}] 最终掉落数量异常: {finalDropCount}");
                    return false;
                }
                UniquePickup basePickup = __instance.currentPickup;
                Transform dropTransform = __instance.dropTransform ?? __instance.transform;
                ExecuteMultiDrop(__instance, dropTransform, basePickup, finalDropCount);
                traverse.Property("currentPickup").SetValue(UniquePickup.none);
                traverse.Field("isChestOpened").SetValue(true);
                __instance.NetworkisChestOpened = true;
                Util.PlaySound("Play_ui_item_world_pickup", __instance.gameObject);
                ShareChestPlugin.LogDebug($"[{chestId}] 成功生成 {finalDropCount} 个掉落物 (原始:{originalDropCount} × {multiplier})");
                return false;
            }
            catch (Exception ex)
            {
                ShareChestPlugin.LogError($"BaseItemDropPrefix 错误: {ex.Message}\n{ex.StackTrace}");
                return true;
            }
        }

        [HarmonyPatch(typeof(ChestBehavior), "Open")]
        [HarmonyPostfix]
        private static void OpenPostfix(ChestBehavior __instance)
        {
            if (!ShareChestPlugin.ModEnabled.Value) return;
            uint chestId = GetChestUniqueId(__instance);
            bool alreadyProcessed = processedChests.Contains(chestId);
            var traverse = Traverse.Create(__instance);
            bool isOpenedState = traverse.Field("isChestOpened").GetValue<bool>();
            ShareChestPlugin.LogInfo($"[{chestId}] Open() 被调用 - 已处理:{alreadyProcessed}, 状态:{isOpenedState}");
        }

        [HarmonyPatch(typeof(ChestBehavior), "Start")]
        [HarmonyPostfix]
        private static void StartPostfix(ChestBehavior __instance)
        {
            if (!ShareChestPlugin.ModEnabled.Value) return;
            if (runStartTime == 0f)
            {
                runStartTime = Time.time;
                ShareChestPlugin.LogDebug($"新对局开始于: {runStartTime}");
            }
            totalChestsGenerated++;
            uint chestId = GetChestUniqueId(__instance);
            ShareChestPlugin.LogInfo($"[{chestId}] 宝箱生成 - 总计: {totalChestsGenerated}个, 类型: {__instance.gameObject.name}");
            LogChestType(__instance);
        }

        [HarmonyPatch(typeof(ChestBehavior), "OnDisable")]
        [HarmonyPostfix]
        private static void OnDisablePostfix(ChestBehavior __instance)
        {
            uint chestId = GetChestUniqueId(__instance);
            processedChests.Remove(chestId);
        }

        [HarmonyPatch(typeof(Run), "Start")]
        [HarmonyPostfix]
        private static void RunStartPostfix()
        {
            processedChests.Clear();
            totalChestsGenerated = 0;
            chestsOpenedCount = 0;
            runStartTime = Time.time;
            if (PlayerCountManager.Instance != null)
                PlayerCountManager.Instance.Reset();
            ShareChestPlugin.LogInfo($"===== 新对局开始 =====");
        }

        [HarmonyPatch(typeof(Run), "OnDestroy")]
        [HarmonyPrefix]
        private static void RunOnDestroyPrefix()
        {
            if (totalChestsGenerated > 0)
            {
                float runDuration = Time.time - runStartTime;
                int finalPlayerCount = ShareChestPlugin.GetCurrentPlayerCount();
                ShareChestPlugin.LogInfo($"===== 对局结束统计 =====");
                ShareChestPlugin.LogInfo($"对局时长: {runDuration:F1}秒");
                ShareChestPlugin.LogInfo($"玩家数量: {finalPlayerCount}人");
                ShareChestPlugin.LogInfo($"生成宝箱总数: {totalChestsGenerated}个");
                ShareChestPlugin.LogInfo($"已打开宝箱数: {chestsOpenedCount}个");
                ShareChestPlugin.LogInfo($"宝箱打开率: {(chestsOpenedCount * 100f / totalChestsGenerated):F1}%");
                ShareChestPlugin.LogInfo($"==========================");
            }
        }

        private static void LogChestType(ChestBehavior chest)
        {
            string chestName = chest.gameObject.name.ToLower();
            string type = "未知";
            if (chestName.Contains("chest1")) type = "普通宝箱";
            else if (chestName.Contains("chest2")) type = "大型宝箱";
            else if (chestName.Contains("equipment")) type = "装备桶";
            else if (chestName.Contains("lunar")) type = "月球宝箱";
            else if (chestName.Contains("void")) type = "虚空宝箱";
            else if (chestName.Contains("category")) type = "分类宝箱";
            ShareChestPlugin.LogDebug($"宝箱类型识别: {chestName} → {type}");
        }

        private static void ExecuteMultiDrop(ChestBehavior chest, Transform dropTransform, UniquePickup basePickup, int dropCount)
        {
            try
            {
                float angleStep = 360f / dropCount;
                float clampValue = ShareChestPlugin.DropDistanceClamp.Value;
                float forwardStrength = Mathf.Clamp(chest.dropForwardVelocityStrength, clampValue, 7f);
                Vector3 baseVelocity = Vector3.up * chest.dropUpVelocityStrength + Vector3.forward * forwardStrength;
                Quaternion rotation = Quaternion.AngleAxis(angleStep, Vector3.up);
                Vector3 currentVelocity = baseVelocity;
                for (int i = 0; i < dropCount; i++)
                {
                    GenericPickupController.CreatePickupInfo pickupInfo = new GenericPickupController.CreatePickupInfo
                    {
                        pickup = basePickup,
                        position = dropTransform.position + Vector3.up * 1.5f,
                        chest = chest,
                        artifactFlag = chest.isCommandChest ? GenericPickupController.PickupArtifactFlag.COMMAND : GenericPickupController.PickupArtifactFlag.NONE
                    };
                    CreatePickupDropletSafe(pickupInfo, pickupInfo.position, currentVelocity);
                    currentVelocity = rotation * currentVelocity;
                }
            }
            catch (Exception ex)
            {
                ShareChestPlugin.LogError($"ExecuteMultiDrop 错误: {ex.Message}");
                throw;
            }
        }

        private static void CreatePickupDropletSafe(GenericPickupController.CreatePickupInfo pickupInfo, Vector3 position, Vector3 velocity)
        {
            try
            {
                MethodInfo methodWithPickupInfo = typeof(PickupDropletController).GetMethod(
                    "CreatePickupDroplet",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new Type[] { typeof(GenericPickupController.CreatePickupInfo), typeof(Vector3), typeof(Vector3) },
                    null
                );
                if (methodWithPickupInfo != null)
                {
                    methodWithPickupInfo.Invoke(null, new object[] { pickupInfo, position, velocity });
                    return;
                }
                PickupDropletController.CreatePickupDroplet(pickupInfo.pickup.pickupIndex, position, velocity);
            }
            catch (Exception ex)
            {
                ShareChestPlugin.LogError($"创建掉落物失败: {ex.Message}");
                try { PickupDropletController.CreatePickupDroplet(pickupInfo.pickup.pickupIndex, position, velocity); }
                catch { ShareChestPlugin.LogError($"备用创建方案也失败"); }
            }
        }
    }
}
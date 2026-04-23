using RoR2;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using System.Collections;
using System;

namespace ShareChest
{
    public class PlayerCountManager : MonoBehaviour
    {
        public static PlayerCountManager Instance { get; private set; }
        public int CurrentPlayerCount { get; private set; }
        public bool IsMultiplayer => CurrentPlayerCount > 1;

        private float lastRecalculateTime = 0f;
        private const float RECALCULATE_INTERVAL = 2f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                StartCoroutine(DelayedInitialize());
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator DelayedInitialize()
        {
            yield return null;
            try
            {
                NetworkUser.onPostNetworkUserStart += OnPlayerJoined;
                NetworkUser.onNetworkUserLost += OnPlayerLost;
                SceneManager.activeSceneChanged += OnSceneChanged;
                RecalculatePlayerCount();
                ShareChestPlugin.LogInfo($"[PlayerCountManager] 初始化完成，当前玩家数: {CurrentPlayerCount}");
                InvokeRepeating(nameof(PeriodicCheck), 5f, 5f);
            }
            catch (Exception ex)
            {
                ShareChestPlugin.LogError($"[PlayerCountManager] 初始化失败: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            try
            {
                NetworkUser.onPostNetworkUserStart -= OnPlayerJoined;
                NetworkUser.onNetworkUserLost -= OnPlayerLost;
                SceneManager.activeSceneChanged -= OnSceneChanged;
            }
            catch { }
            if (Instance == this) Instance = null;
            CancelInvoke(nameof(PeriodicCheck));
        }

        private void OnPlayerJoined(NetworkUser joinedUser)
        {
            StartCoroutine(UpdatePlayerCountDelayed(1.0f));
            ShareChestPlugin.LogInfo($"[PlayerCountManager] 玩家加入: {joinedUser.userName}");
        }

        private void OnPlayerLost(NetworkUser lostUser)
        {
            StartCoroutine(UpdatePlayerCountDelayed(1.0f));
            ShareChestPlugin.LogInfo($"[PlayerCountManager] 玩家离开: {lostUser?.userName}");
        }

        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            RecalculatePlayerCount();
            ShareChestPlugin.LogInfo($"[PlayerCountManager] 场景变化: {oldScene.name} -> {newScene.name}, 玩家数: {CurrentPlayerCount}");
        }

        private IEnumerator UpdatePlayerCountDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            RecalculatePlayerCount();
        }

        private void PeriodicCheck()
        {
            if (Time.time - lastRecalculateTime > RECALCULATE_INTERVAL)
                RecalculatePlayerCount();
        }

        public void RecalculatePlayerCount()
        {
            int oldCount = CurrentPlayerCount;
            try
            {
                if (NetworkUser.readOnlyInstancesList != null && NetworkUser.readOnlyInstancesList.Count >= 0)
                {
                    CurrentPlayerCount = NetworkUser.readOnlyInstancesList.Count;
                    ShareChestPlugin.LogInfo($"[PlayerCountManager] 通过NetworkUser获取: {CurrentPlayerCount}人");
                }
                else
                {
                    if (PlayerCharacterMasterController.instances != null && NetworkServer.active)
                    {
                        CurrentPlayerCount = 0;
                        foreach (var player in PlayerCharacterMasterController.instances)
                            if (player != null && player.isActiveAndEnabled) CurrentPlayerCount++;
                        ShareChestPlugin.LogDebug($"[PlayerCountManager] 通过PlayerController获取: {CurrentPlayerCount}人");
                    }
                    else
                    {
                        CurrentPlayerCount = 1;
                        ShareChestPlugin.LogDebug($"[PlayerCountManager] 单人游戏模式: 1人");
                    }
                }
                if (CurrentPlayerCount != oldCount)
                {
                    ShareChestPlugin.LogInfo($"[PlayerCountManager] 玩家数量变化: {oldCount} -> {CurrentPlayerCount}");
                    if (ShareChestPlugin.ModEnabled != null && ShareChestPlugin.ModEnabled.Value &&
                        ShareChestPlugin.EnableDynamicMultiplier != null && ShareChestPlugin.EnableDynamicMultiplier.Value)
                    {
                        ShareChestPlugin.LogInfo($"动态倍率更新: 当前{CurrentPlayerCount}人, 倍率{ShareChestPlugin.CalculateMultiplier(CurrentPlayerCount)}x");
                    }
                }
                lastRecalculateTime = Time.time;
            }
            catch (Exception ex)
            {
                ShareChestPlugin.LogError($"重新计算玩家数量失败: {ex.Message}");
                CurrentPlayerCount = Math.Max(1, oldCount);
            }
        }

        public void Reset()
        {
            CurrentPlayerCount = 0;
            RecalculatePlayerCount();
        }
    }
}
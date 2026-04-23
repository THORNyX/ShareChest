using BepInEx.Configuration;
using R2API;
using RoR2;
using UnityEngine;

namespace ShareChest
{
    public abstract class ArtifactBase
    {
        public abstract string ArtifactName { get; }
        public abstract string ArtifactLangTokenName { get; }
        public abstract string ArtifactDescription { get; }
        public abstract Sprite ArtifactEnabledIcon { get; }
        public abstract Sprite ArtifactDisabledIcon { get; }

        /// <summary>
        /// 游戏内显示的名称。默认返回 ArtifactName，子类可重写以返回中文名。
        /// </summary>
        public virtual string DisplayName => ArtifactName;

        public ArtifactDef ArtifactDef;
        public bool ArtifactEnabled => RunArtifactManager.instance?.IsArtifactEnabled(ArtifactDef) ?? false;

        public abstract void Init(ConfigFile config);

        protected void CreateLang()
        {
            // 使用 DisplayName 作为游戏内显示的名称（可以是中文）
            LanguageAPI.Add("ARTIFACT_" + ArtifactLangTokenName + "_NAME", DisplayName);
            LanguageAPI.Add("ARTIFACT_" + ArtifactLangTokenName + "_DESCRIPTION", ArtifactDescription);
        }

        protected bool CreateArtifact()
        {
            ArtifactDef = ScriptableObject.CreateInstance<ArtifactDef>();
            ArtifactDef.cachedName = "ARTIFACT_" + ArtifactLangTokenName;
            ArtifactDef.nameToken = "ARTIFACT_" + ArtifactLangTokenName + "_NAME";
            ArtifactDef.descriptionToken = "ARTIFACT_" + ArtifactLangTokenName + "_DESCRIPTION";
            ArtifactDef.smallIconSelectedSprite = ArtifactEnabledIcon;
            ArtifactDef.smallIconDeselectedSprite = ArtifactDisabledIcon;

            bool success = ContentAddition.AddArtifactDef(ArtifactDef);
            if (!success)
            {
                ShareChestPlugin.LogError($"神器 {ArtifactName} 添加失败！可能图标为null或重复。");
            }
            return success;
        }

        public abstract void Hooks();
    }
}
# ShareChest-共享宝箱

让宝箱掉落多倍物品的《雨中冒险2》模组。
在神器界面启用 **均衡神器（Artifact of Equilibrium）** 即可生效。

**在神器界面启用** 均衡神器来激活多倍掉落。
禁用即可恢复原版掉落。

## 影响游戏平衡

警告：使用过高的倍率（例如 10 倍以上）会显著增加物品掉落数量，可能影响游戏性能。

## 功能特性

- **动态倍率** = 当前玩家数量（单人 1 倍，双人 2 倍，以此类推）
- **固定倍率**（1–50，默认 5 倍）
- 自由选择哪些宝箱类型受倍率影响（白名单）
- 掉落距离配置（默认6）
- 实时追踪玩家数量（支持多人加入/离开）
- 调试命令：`F5` = 显示当前状态，`F6` = 强制刷新玩家数量
- 对局结束统计（生成宝箱数、打开宝箱数、打开率

## 配置说明

编辑配置文件 `BepInEx/config/com.Muskmelovon.ShareChest.cfg`：

| 配置项               | 默认值   | 说明                                             |
| :------------------- | :------- | :----------------------------------------------- |
| `启用插件`           | true     | 总开关（同时需要神器启用才生效）                 |
| `启用动态倍率`       | false    | `true` = 倍率 = 玩家数量，`false` = 使用固定倍率 |
| `固定掉落倍率`       | 5        | 动态倍率关闭时使用的倍率值                       |
| `掉落距离限制`       | 6        | 物品飞散范围（建议保持默认）                     |
| `应用倍率的宝箱类型` | *见下方* | 逗号分隔的宝箱名称。留空 = 所有宝箱              |
| `启用调试日志`       | true     | 输出详细日志，排错时开启                         |

• 常见宝箱类型：

- Chest1(Clone): 普通宝箱

- Chest2(Clone): 大宝箱

- GoldChest: 传奇宝箱

- LunarChest(Clone): 月球舱

- VoidChest(Clone): 虚空摇篮

- EquipmentBarrel(Clone): 装备箱(默认关闭)

- Lockbox(Clone): 生锈带锁箱(默认关闭)

- CategoryChestUtility(Clone): 辅助箱

- CategoryChest2Utility Variant(Clone): 大辅助箱

- CategoryChestHealing(Clone): 治疗箱

- CategoryChest2Healing Variant(Clone): 大治疗箱

- CategoryChestDamage(Clone): 伤害箱

- CategoryChest2Damage Variant(Clone): 大伤害箱

• 默认关闭倍率宝箱类型：

- EquipmentBarrel(Clone): 装备箱

- Lockbox(Clone): 生锈带锁箱

## 兼容性

- 依赖项：[BepInExPack](https://thunderstore.io/package/bbepis/BepInExPack/)和[R2API](https://thunderstore.io/package/tristanmcpherson/R2API/)
- 与大部分模组兼容性未知，尤其是修改 `ChestBehavior.BaseItemDrop` 的模组。

## 安装方法

1. 将 `ShareChest.dll` 放入 `BepInEx/plugins/` 文件夹。
2. 启动游戏，配置文件会自动生成。

## 有任何问题/建议或想做出贡献？

- 请将问题或建议提交至 GitHub 代码库
- 请将问题或建议提交至 GitHub 代码库[：https://github.com/THORNyX/ShareChest/issues](https://github.com/THORNyX/ShareChest/issues)

## 更新日志

- 1.0.0.0: 初始发布。
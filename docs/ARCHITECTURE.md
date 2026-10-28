# 技术架构与代码约定

## 1. 目标环境

- Unity 2022.3 LTS 起步。
- C#，优先使用 Unity 支持的语言特性，避免依赖较新的 .NET API。
- uGUI 用于第一阶段运行时界面。
- 游戏逻辑与 MonoBehaviour 展示层分离。
- 单机优先，数据先保存在本地。

## 2. 目录约定

    Assets/
      Editor/                 # Unity 编辑器工具
      Resources/
        Data/hero_catalog.json # 可扩展武将数据源
        Heroes/                # 导入后生成的 HeroDefinition 资源
      Scenes/
        Main.unity             # 主场景
      Scripts/
        App/                   # 游戏启动和 UI 组合
        Battle/                # 敌人、波次、战斗循环
        Data/                  # 静态数据模板
        Domain/                # 可测试的领域对象
        Persistence/           # 本地存档
        Services/              # 抽卡、合成、布阵等业务服务
    ProjectSettings/
    Packages/
    docs/

## 3. 数据与对象边界

- HeroDefinition：武将模板，不存储玩家拥有状态。
- HeroInstance：单份收藏副本，具有唯一 InstanceId、HeroId 和等级。
- HeroRoster：副本集合，负责查找、添加和移除。
- GachaService：根据三档卡池的等级命中率和目录中的 hero tier 产生副本；按等级分组归一化，避免同等级武将数量影响该等级总概率。
- HeroUpgradeService：校验材料并原子地消耗材料、升级目标。
- FormationService：管理固定点位与副本 ID 的映射。
- UI 层只负责展示状态、收集输入和调用服务，不直接绕过业务规则修改数据。
- 战斗中的单位状态与局外武将副本状态分开管理，避免敌人死亡、临时 Buff 等污染永久收藏。

## 4. 武将目录扩展

编辑 Assets/Resources/Data/hero_catalog.json 中的 heroes 数组，保证每个 heroId 唯一。数值字段：
- baseAttack：基础攻击力，至少为 1。
- baseHealth：基础生命值，至少为 0。
- attackInterval：攻击间隔秒数，至少为 0.1。
- attackRange：原型中的攻击距离。
- faction / rarity：必须匹配代码中的枚举名称。
- tier：武将抽卡等级，取值 Basic、Intermediate、Advanced；初级卡可抽初级/中级，中级卡可抽全部等级，高级卡可抽中级/高级。

在 Unity 中执行菜单 Sanguo/Import Hero Catalog，会生成或更新 Assets/Resources/Heroes/<heroId>.asset。导入工具不会删除目录中已存在但不在 JSON 中的资源。

## 5. 代码约定

- 稳定 ID 使用英文小写标识，不使用显示名称作为主键。
- 游戏逻辑应避免依赖场景对象或静态全局状态。
- 业务方法尽量返回明确错误信息，不以静默失败代替校验。
- 对外暴露只读集合，修改状态通过明确的方法完成。
- 修改数据格式时同步更新文档和存档版本。
- 新增依赖必须登记到 Packages/manifest.json。

## 6. 当前限制

当前仓库处于原型阶段。初始场景和运行时 UI 用于打通流程，不代表最终美术质量或平衡性。每次修改后需在 Unity Editor 中打开项目并检查 Console、脚本编译和实际运行效果。

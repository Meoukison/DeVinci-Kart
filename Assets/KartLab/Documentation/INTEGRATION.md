# 给赛道与 AI 负责人的接入说明

## 导入

优先统一使用 Unity 6000.6.3f1。把 `Deliverables/KartLab-Module.unitypackage` 导入项目，或复制 `Assets/KartLab` 与所引用的 `Assets/ThirdParty`，保留 `.meta` 文件。
依赖包版本见 `Packages/manifest.json`；需要 Input System，Player Settings 的 Active Input Handling 设为 Input System。
原型使用 Built-in Render Pipeline；如果正式项目使用 URP，需要升级导入材质，不能直接假设外观一致。

## 放置车辆

1. 把 `PlayerKart.prefab` 放在赛道路面上方约 0.02 米，车头沿本地 +Z。
2. 车辆根节点使用第 8 层；地面默认第 0 层。`KartMotor.groundMask` 必须包含赛道路面、排除车辆层。
3. 给道路配置静态 Collider，别设 Trigger。平坦道路可用 BoxCollider，模型道路可用 MeshCollider。
4. Prefab 自带 Camera 与 AudioListener，删除或关闭场景中重复的主相机。
5. 赛道单位为米；普通最高速度 18 m/s（VR 默认乘 0.72）。

## 赛道互动

- 放置 `MushroomBox`、`StarBox`、`GreenShellBox`、`BananaBox`、`IceFlowerBox`。把 `TrackZone.fixedItem` 改成 None 可作为随机箱。
- `BoostStrip.prefab` 是加速区。
- `Checkpoint.prefab` 的 Safe pose 子节点决定复位位置与朝向，放在不重叠墙面的路面上。
- 草地碰撞体加 `GroundSurface`，`speedFactor=0.6`；或创建 Trigger 并添加 Slow 类型 `TrackZone`。
- 出界区域添加 OutOfBounds 类型 `TrackZone`；低于 Y=-6 也会自动复位。
- 视觉模型与触发器分离，替换美术时保留 Collider 和脚本。

## AI 接口

现阶段为单机架构，所有效果在本地执行。

```csharp
// AI 向同一控制器提供输入，不需要重写车辆物理。
kartInput.useExternalInput = true;
kartInput.externalInput = new KartLab.DriveInput {
    throttle = 1f,
    steering = desiredSteering, // -1 到 +1
    brake = 0f,
    skill = holdDriftOrJump,
    use = pressUseItem
};

// 外部攻击、赛道和比赛系统可以使用：
motor.ApplyHit(0.6f, 1.2f);
motor.ApplyBoost(1f);
motor.ApplyStar(5f);
motor.ApplyFreeze();
motor.SetCheckpoint(safePosition, safeRotation);
motor.Feedback += message => { /* 音效、UI 或统计 */ };
```

AI 车辆需要 `KartMotor`、`KartInput`、Collider 和 Rigidbody；道具需要 `KartInventory`，并引用 GreenShell / BananaTrap / IceShot Prefab。
比赛系统自行维护圈数、排名和检查点顺序，复位事件不能用于增加赛程进度。若接多人联网，需增加服务端权威道具生成、拾取与状态同步；本模块不包含网络层。

## 两周验收顺序

先完成桌面基本操作、五种道具和组员赛道合并；第一周内确定头显。之后配置 OpenXR 目标平台和控制器 Profile，在真机验证油门、双手方向盘、追踪丢失、暂停与复位，再调速度、转向角和镜头舒适度。
测试和调参过程中优先修改 Inspector 的公开参数。测试赛道重建会覆盖自动生成的同名场景和 Prefab，请把正式场景另存。

粒子需要 Particle System 模块。KartEffects 引用 SoftGlow 材质，蘑菇使用自定义 Built-in Shader；迁移 URP 时两种 Shader 都需转换。新预制体保留原四种道具编号与 GUID，但源代码中的旧 ItemKind 枚举名称已经更换；请同步更新调用。

碰撞由 Rigidbody 解算，但旋转只由 KartMotor 控制。不要在其他脚本给根节点添加旋转冲量或更改旋转锁定。碰撞材质为零弹性/零接触摩擦，轮胎侧向抓地由 lateralGrip 实现；把碰撞体摩擦直接调大可能导致路缘绊住车辆。赛道碰撞网格应避免重叠尖角和重复墙体。

低路缘跨越由 KartMotor.maxStepHeight 控制，默认 0.32 米，只处理静态碰撞体。高度超过该值的墙体、护栏不会被跨越；不要把赛道的台阶能力用于陡坡或楼梯。接地使用中心及四个轮胎区域，当前采样位置对应 PlayerKart 的标准尺寸；改车型尺寸时需同步调整采样位置。

## 自动回到赛道中心

PlayerKart 现在带 KartRecovery。正式赛道需要一个 TrackRoute，在 points 中按行驶顺序设置本地空间中心线采样点（弯道应加密），closed 控制闭环，roadHalfWidth 为可用路面半宽。点的 Y 应是可停放车身根节点的高度（演示路面 0.15 米，节点为 0.18 米）。可显式给 KartRecovery.route 赋值；不赋值时启动寻找场景中的第一条 TrackRoute。多条赛道须显式分配，避免选错。

驶离中心线范围持续 1 秒自动回到最后在路面时记录的中心位置，并按中心线正向摆正。路缘附近持续踩油门但速度低于 0.7 m/s 达 1.5 秒也触发救援。手动 R 使用同一安全位置；没有 TrackRoute 时维持检查点复位。正式赛道合并后必须验证每个中心线节点都可停放车辆，且不能用救援事件增加圈数或赛程进度。

## 座舱、起步与战术道具接口

- `KartCockpit` 只负责视觉：绑定 `motor` / `view`、方向盘、仪表 UI、手部、轮胎枢轴和道具模型。不会施加物理力或修改 XR 头部。
- `KartRaceSkills.BeginCountdown()` 启动 3 秒起步流程并锁定车辆平移，`CancelCountdown()` 取消并解锁。比赛管理器应在发车点放置车辆后调用。组件禁用或复位自动解锁。
- `slipstreamEnabled` 可关闭尾流。默认从场景内 `KartMotor` 查找前车，未来 AI 复用同一组件即可；大型多人场景建议改为比赛管理器维护的参赛车辆列表。
- `KartInventory.Use(bool backward=false)` 保留原无参数调用兼容性；发射方向可由输入/AI 指定。`SetRacePosition(position,total)` 使用从 1 开始的排名并启用位置权重；没有外部调用就不会虚构排名。
- `KartMotor.assistedDriving` 控制自动入弯收速；`driveLocked` 由起步流程临时使用。不要在其他系统直接覆盖该标志而不协调锁定状态。
- `KartView.Recenter()` 重新记录 XR 头部基准和方向盘中位；`SavePreferences()` 保存本机驾驶偏好。编辑器自动验证不读取用户偏好。
- 导出包含素材原始许可证与音效署名。玩家采用 Keith CC0 整车、Grab3D 运动方向盘和仪表；不显示虚拟手，也不需要 XR Hands。

## 喇叭接入

玩家 Prefab 含 `KartHorn` 和已绑定的 CC0 音效。自定义输入设置 `DriveInput.horn`，组件支持点按、无冷却连按与长按持续音，并处理松开淡出与暂停/救援屏蔽；持续按住时须每帧保持 horn=true。`KartHorn.Sounding` 可供仪表查询。直接使用手柄追踪和按键，不依赖虚拟手。

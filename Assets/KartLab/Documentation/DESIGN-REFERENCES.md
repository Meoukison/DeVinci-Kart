# 官方参考与本项目改编

检索日期：2026-11-03。参考当前《Mario Kart World》，而非照搬旧作。

- [Nintendo：Mario Kart World 官方技巧](https://www.nintendo.com/us/whatsnew/get-a-head-start-on-mario-kart-world-with-a-few-tips/)：漂移蓄力、三级火花与蓄力跳跃，是本项目驾驶技能的机制依据。
- [Nintendo：Mario Kart World 官方游戏页](https://www.nintendo.com/en-gb/Games/Nintendo-Switch-2-games/Mario-Kart-World-2790000.html)：现有与新增道具说明；冰之花冻结对手，是本项目新增冰弹的玩法依据。
- [Nintendo：World Direct 介绍](https://www.nintendo.com/us/whatsnew/mario-kart-world-direct-revs-up-new-details-on-the-biggest-mario-kart-ever-coming-to-nintendo-switch-2-at-launch/)：整体风格、驾驶与道具功能背景。
- [Unity：Cinemachine Camera 文档](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineCamera.html)：镜头位置、旋转与镜头属性是独立可调的维度。当前项目用自身的 KartView 实现，不依赖 Cinemachine。

蘑菇、星星、龟壳、香蕉保留易识别的功能；冰之花参考 World 的新道具。道具时长、三发冰弹、三个反弹上限、等概率抽取、转弯半径、速度和镜头参数都是本项目设计，不能当作官方数值。冻结和旋转受击已改为较适合 VR 的减速反馈。

加速镜头采用平滑扩大视野与轻微后移来强化速度感。65° / 76° / 86° 是本项目实测调校值，并未声称是 Mario Kart 的真实内部镜头参数。VR 不复用桌面镜头动画。

## 素材与制作范围

使用 Kenney 官方 CC0 现成资源：Car Kit、Racing Kit、Interface Sounds，以及本轮新增 [Food Kit](https://kenney.nl/assets/food-kit) 与 [Platformer Kit](https://kenney.nl/assets/platformer-kit)。原始许可在 Assets/ThirdParty 各资源包目录，来源记录在 Evidence/asset-sources.json。

车辆与头盔驾驶员为现成模型。蘑菇基于现成蘑菇模型改红白材质；星星直接使用现成模型；龟壳由现成圆按钮与轮胎拼合；香蕉由三个现成香蕉模型组合；冰花由现成花朵和宝石组合。图标由这些模型渲染。树木、植物、岩石和赛道建筑使用现成模型。

本项目新增代码控制粒子、拖尾、材质发光、文字 UI 和相机动态；引擎声使用短合成循环，事件音使用现成 Interface Sounds。没有使用任天堂提取的角色、模型、贴图或音频。这是可替换的低多边形美术原型。

当前已安装技能目录中没有直接面向 Unity 街机赛车调校的技能，本轮依赖官方资料、源代码调校与真实 Unity 测试。

## 座舱与音效素材

- [Grab3D Sports Steering Wheel](https://grab3d.com/models/vehicles/sports-steering-wheel/)：CC0；下载 OBJ + 材质，适配大小和朝向。
- [Grab3D Instrument Cluster](https://grab3d.com/models/vehicles/instrument-cluster/)：CC0；仪表实体，覆盖项目动态数字读数与灯条。
- [Racing car engine sound loops — domasx2](https://opengameart.org/content/racing-car-engine-sound-loops)：CC0，使用三个原始循环，运行时调整音量/音高及交叉混合。
- [Car tire squeal skid loop — audible-edge / Tom Haigh](https://opengameart.org/content/car-tire-squeal-skid-loop)：[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)，qubodup 提交；原音频用于轮胎，低通/降调用于粗糙路面。署名随项目与游戏设置保留。
- [Crash Collision — qubodup](https://opengameart.org/content/crash-collision)：CC0，用于撞墙与命中音效。

- [Racing Kart — Keith at Fertile Soil Productions](https://opengameart.org/content/racing-kart)：免费 CC0，玩家整车复用完整底盘、座椅、脚踏、转向柱、车罩、四轮与前后护杠。原始 OBJ/MTL 随工程保留；适配轴向与尺寸、重命名部件、替换运动方向盘并重新配色。不显示虚拟手，双手柄输入逻辑保留。

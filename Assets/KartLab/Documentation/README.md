# APEX Driver Lab / A4 VR

Unity **6000.6.3f1**。驾驶与道具模块的独立演示项目，测试赛道可替换为组员制作的正式赛道。

![Mac 试玩版实际画面](Evidence/desktop-preview.png)

当前状态：驾驶逻辑基线 85 项 Play Mode 集成测试通过（清单见 Evidence/verification.txt）。完整整车素材替换另做驾驶舱运行检查、实际渲染检查和 Mac 独立程序试玩检查。VR 设备尚未确定，头显真机验证待完成。

## 开始试玩

**Mac 直接试玩：** 在 Finder 打开 `Builds`，双击 `APEX Driver Lab.app`；点击游戏窗口后按住 W 开车。无需先打开 Unity。更新版使用 Keith 的免费 CC0 完整车身、带橙色顶部标记的运动方向盘和嵌在车罩上的数字仪表。

1. 在 Unity Hub 中将本文件夹作为已有项目加入，使用 6000.6.3f1 打开。
2. 打开 `Assets/KartLab/Scenes/DriverLab.unity`，点击 Play。
3. 默认第一人称驾驶位。桌面按 **C** 可切换第三人称，再按一次回到驾驶位；沿右侧直道向前行驶即可拾取道具。

| 操作 | 桌面 | VR 默认输入 |
| --- | --- | --- |
| 方向 | A/D 或左右箭头 | 同时按住左右 Grip，双手模拟方向盘 |
| 油门 | W / 上箭头 | 右扳机，需双手握持 |
| 刹车、停稳后倒车 | S / 下箭头 | 左扳机 |
| 漂移 / 蓄力跳跃 | Space | 右手 Primary |
| 使用 / 向后发射道具 | E / Q | 左手 Primary；左摇杆向后同时按 Primary 后射 |
| 喇叭 | H，点按／连按／长按 | 右摇杆按下（需设备支持） |
| 起步练习 | Enter，3–2–1 最后阶段给油 | 比赛管理器调用 BeginCountdown |
| 回检查点 | 长按 R 1 秒 | 长按左手 Secondary 1 秒 |
| 暂停与驾驶设置 | Esc | 右手 Secondary |
| 切换镜头 | C | VR 固定驾驶位，头部自由观察 |

Primary/Secondary 是 OpenXR 语义按钮。Quest 通常分别为右 A/B、左 X/Y；最终映射以选定设备为准。没有这些按钮的手柄需要改输入适配器。

VR 握持规则：先把双手放在舒适的方向盘姿势，按住两个 Grip。首次握持自动把当前角度当作中位，左右转动双手连线控制转向。松开任意 Grip 或丢失任一手追踪，油门归零，转向平滑回正；重新握持重新校准。它是空中方向盘，不要求两只手准确碰到模型抓取点。

## 演示场景

- 右侧直道：蘑菇、星星、龟壳、香蕉固定箱与移动靶车。
- 顶部横道：冰之花固定箱；左侧含随机箱、冰弹测试区、跳跃障碍与加速带。
- 草地减速；可跨越低路缘。出界持续 1 秒或顶住路缘持续踩油门 1.5 秒，自动返回离场前的赛道中心并摆正车头。
- 碰撞不再带来额外旋转；斜撞沿墙滑行，低速顶墙可转向脱离；急弯自动收速。
- 三级漂移火花、加速尾焰、星星环绕、道具图标与动态速度镜头。
- 测试靶车不是完整比赛 AI；没有排名、圈数或网络同步。

## 模块与素材

运行时代码：`Assets/KartLab/Runtime/`。可复用 Prefab：`Assets/KartLab/Prefabs/`。
配置与规则见 [DESIGN.md](DESIGN.md)，组员接入见 [INTEGRATION.md](INTEGRATION.md)。

玩家整车采用 Keith 的免费 CC0 Racing Kart，保留底盘、座椅、脚踏、转向柱、护杠和车壳；测试靶车与头盔驾驶员、道路、树木、围栏、建筑和道具模型基于现成 Kenney 素材。运动方向盘和实体仪表来自 Grab3D；发动机、轮胎与碰撞使用有许可的现成音效。道具组合、动画、材质适配、粒子与界面由项目实现。具体改编和官方玩法出处见 [DESIGN-REFERENCES.md](DESIGN-REFERENCES.md)。

| 资源 | 官方来源 | 许可 |
| --- | --- | --- |
| Car Kit 3.1 | https://kenney.nl/assets/car-kit | CC0 |
| Racing Kit 1.0 | https://kenney.nl/assets/racing-kit | CC0 |
| Food Kit | https://kenney.nl/assets/food-kit | CC0 |
| Platformer Kit | https://kenney.nl/assets/platformer-kit | CC0 |
| Interface Sounds | https://kenney.nl/assets/interface-sounds | CC0 |
| Racing Kart — Keith | https://opengameart.org/content/racing-kart | CC0 |
| Sports steering wheel / Instrument cluster | https://grab3d.com/ | CC0（原始许可保留） |
| Klaxon #1 — Le tiroir du fond | https://lasonotheque.org/klaxon-1-s3504.html | CC0 |
| Engine loops / Crash Collision | https://opengameart.org/ | CC0，domasx2 / qubodup |
| Car tire squeal skid loop | https://opengameart.org/content/car-tire-squeal-skid-loop | CC BY 3.0，audible-edge / Tom Haigh |

原始许可在各 `Assets/ThirdParty/<pack>/License.txt` 中；下载来源记录在 `Evidence/asset-sources.json`。没有使用任天堂游戏中提取的模型或音频。

## VR 状态与后续

项目已安装 Input System、XR Management 和 OpenXR；输入、头部姿态、驾驶位 UI 与手柄振动代码已接入。设备尚未确定，**当前不能视为头显已验证的交付**。

选定设备后需要配置对应 XR loader、Interaction Profile、构建平台和真机性能。当前电脑只装有 Mac / WebGL 构建模块。Quest 独立运行需要 Android Build Support（SDK/NDK/OpenJDK）；Windows PCVR 需要对应 Windows 构建和运行环境。桌面模拟与自动测试不能证明 VR 舒适度、追踪或帧率合格。

## 重建和验证

关闭此项目的 Unity 编辑器、保持 Unity Hub 运行后，在项目目录执行：

```sh
python3 Tools/run_unity.py scene
python3 Tools/run_unity.py verify
python3 Tools/run_unity.py mac
python3 Tools/run_unity.py export
```

`scene` 重新生成测试场景和同名 Prefab，会覆盖它们的手动修改；组员正式赛道请使用单独场景。
`verify` 在真实 Play Mode 中执行集成测试，结果写入 `Evidence/verification.txt`，同时输出 3 张渲染图。
`mac` 输出 `Builds/APEX Driver Lab.app`；`export` 输出带依赖的 `Deliverables/KartLab-Module.unitypackage`。
Unity 菜单 `Kart Lab > Build Test Scene` 同样可以生成测试场景。

官网直接下载被网络阻止时，可通过浏览器下载原始三个官方 ZIP，再运行 `python3 Tools/fetch_assets.py /path/to/downloads` 导入。已有素材无需重新下载。

新增 Food Kit / Platformer Kit 可在浏览器下载后运行 `python3 Tools/import_polish_assets.py` 导入 Downloads 中的 ZIP。

回场专项可执行 `python3 Tools/run_unity.py edges`；自动救援专项执行 `python3 Tools/run_unity.py recovery`。正式赛道需配置 TrackRoute 中心线，详见接入说明。

## 新版驾驶舱操作

- 方向盘随转向运动，转向柱随方向盘距离调整；不显示虚拟手，VR 直接用左右手柄模拟握盘。轮胎会滚动，数字速度、漂移灯和前车罩右侧实体道具随状态更新。
- Space：地面转弯时按住漂移、直线按住蓄力跳；腾空后再按一次，落地获得加速。
- Enter：起步练习；看到最后的 `1` 后，在 GO 前约半秒开始按住 W。
- Esc：调整座椅、方向盘距离、灵敏度和辅助驾驶；点击 SAVE AND DRIVE 保存。标准模式需要自己提前刹车。
- 尾流跟随测试车可蓄力加速。完整 AI、圈数和排名仍由比赛模块提供。
- 坡道已按整条赛道宽度对齐，入口下沉 2mm 与原路面重叠，避免入口悬空和车道中的突起侧缘。

整车原始模型与许可在 `Assets/ThirdParty/keith-racing-kart/`。轴向/尺寸适配脚本为 `Tools/prepare_complete_kart.py`，从 `Research/KartCandidates/Keith-Racing-Kart/` 中保留的原始下载重建。重取方向盘与仪表素材可运行 `python3 Tools/import_cockpit_assets.py`；旧手套和裁切车壳不再用于驾驶舱。
坡道专项验证：`python3 Tools/run_unity.py ramp`。

喇叭：H / 右手柄摇杆按下。点按短鸣、快速连按连续短鸣、长按持续鸣笛，松开约 30ms 淡出；极短点按至少响约 75ms。无触发冷却，同一音源避免叠音，不消耗道具。暂停、救援时停止并屏蔽，恢复后需松开再按。持续音使用现有 CC0 录音的稳定片段制作无缝循环，原始录音保留。VR 按键与振动仍需真机验证。专项检查：`python3 Tools/run_unity.py horn`，12 项通过，见 `Evidence/horn-verification.txt`。重建循环音：`python3 Tools/prepare_horn_loop.py`（需 ffmpeg）。

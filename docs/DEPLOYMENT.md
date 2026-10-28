# 部署与运行指南

本文档说明如何在本地运行本项目，以及如何构建 Windows / WebGL / Android 发布版本。

## 1. 环境要求

| 项目 | 要求 |
| --- | --- |
| Unity 版本 | **2022.3 LTS**（以 `ProjectSettings/ProjectVersion.txt` 为准） |
| 操作系统 | Windows 10+ / macOS 12+ / 主流 Linux 发行版 |
| 磁盘空间 | ≥ 2 GB（含 Unity Editor） |
| 网络 | 首次打开工程时 Unity Package Manager 需要访问网络拉取包依赖 |

安装 Unity：通过 [Unity Hub](https://unity.com/cn/download) → Available Downloads → 安装 2022.3.x → 在 Add Modules 中按需勾选：

- Windows Build Support (Mono) —— 构建 PC 版
- Web Build Support —— 构建网页版
- Android Build Support（含 SDK & NDK Tools、OpenJDK）—— 构建安卓版

## 2. 获取并打开项目

```bash
git clone https://github.com/hjkl01/sanguo-tower-defense.git
cd sanguo-tower-defense
```

打开 Unity Hub → Open project → 选择仓库根目录（包含 `Assets/` 与 `ProjectSettings/` 的文件夹）。首次打开 Unity 会重新导入资源，耐心等待进度条完成。

> 若提示版本不匹配，可在弹窗中选择“以当前安装的兼容版本打开”，或按上表安装 2022.3 LTS。

## 3. 在编辑器中运行

1. 打开任意场景（或空场景均可）——本项目的 UI 与战斗场景全部由 `Assets/Scripts/App/GameBootstrap.cs` 在运行时用代码生成，无需手工搭建场景。
2. 确保场景中不存在其他冲突的启动脚本，直接点击 **Play**。
3. Game 视图分辨率建议选择 **1280×720**（与 CanvasScaler 参考分辨率一致），其他比例下 UI 会自动缩放。
4. 验证要点：
   - 界面中文正常显示（若出现方块乱码，见第 6 节 FAQ）；
   - 抽卡 / 十连抽、上阵、合成、开始战斗、暂停均无报错；
   - Console 窗口无红色错误。

## 4. 构建设施（Build Settings）

1. `File → Build Settings`（Ctrl/Cmd + B）。
2. 将当前场景加入 **Scenes In Build** 列表（若列表为空：先新建一个空场景 `Assets/Scenes/Main.unity` 保存后加入）。
3. 确认平台：左下角 `PC, Mac & Linux Standalone` / `Web` / `Android` 切换目标平台，必要时点 `Switch Platform`（切换后需等待重新导入）。

### 4.1 Windows / macOS 桌面版

- Target Platform: Windows，Architecture: x64。
- Output Path 例如 `Builds/Windows/SanguoTD.exe`，点击 **Build**。
- 分发方式：压缩整个输出目录发给玩家即可，无需安装 Unity。存档位于：
  - Windows: `%USERPROFILE%\AppData\LocalLow\<公司名>\sanguo_save.json`
  - macOS: `~/Library/Application Support/../../UserDefault` 下的 persistentDataPath

### 4.2 WebGL 网页版

- 切换到 Web 平台后点击 **Build**，输出到如 `Builds/WebGL`。
- **重要**：WebGL 产物不能直接双击 `index.html` 运行（浏览器安全策略限制），必须通过静态 HTTP 服务加载。任选其一：

```bash
# 方式 A：Python
cd Builds/WebGL && python3 -m http.server 8080

# 方式 B：Node serve
npx serve Builds/WebGL

# 方式 C：nginx / Apache / GitHub Pages / itch.io 等任意静态托管
```

- 浏览器访问 `http://localhost:8080` 验证。
- 上线：把 `Builds/WebGL` 整个目录上传到任意静态托管（GitHub Pages、Netlify、itch.io 均可）。注意 GitHub Pages 需在仓库 Settings → Pages 中启用，并把构建产物放到 `docs/` 或单独分支。
- WebGL 内存建议 ≥ 512 MB；Player Settings → Publishing Settings 可开启 Decompression Cache 加快二次加载。

### 4.3 Android 版

1. Player Settings 设置 **Package Name**（如 `com.hjkl01.sanguotd`）。
2. 配置 Keystore（仅发布 APK/AAB 到商店时需要；自测可用 debug keystore）。
3. Build Settings → **Build** 生成 `.apk`，或 **Build And Run** 直接安装到已连接的设备（需开启 USB 调试）。
4. 存档位置：应用私有目录 `persistentDataPath`（卸载应用即清除）。

## 5. CI 自动构建并发布 Release（已配置）

本仓库已内置工作流 **`.github/workflows/build-unity.yml`**，基于 [game-ci/unity-builder](https://game-ci.com)，使用 Unity 版本由 `ProjectSettings/ProjectVersion.txt`（2022.3 LTS）自动决定。触发方式：

- **推送 `v*` 标签**（如 `git tag v0.1.0 && git push origin v0.1.0`）→ 构建 WebGL → 自动创建 GitHub Release 并上传 `sanguo-webgl.zip`；
- **Actions 页面手动触发**（workflow_dispatch）→ 仅构建并产出 workflow artifact，不发 Release。

### 首次使用前需配置的 Secrets

GitHub 仓库 → Settings → Secrets and variables → Actions → New repository secret：

| Secret 名称 | 说明 |
| --- | --- |
| `UNITY_LICENSE` | Unity 许可证文件内容（见下） |
| `UNITY_EMAIL` | Unity 账号邮箱 |
| `UNITY_PASSWORD` | Unity 账号密码（若开启两步验证，请改用 `UNITY_LICENSE` + serial 方案或 App Password） |

个人版（Personal）许可证可用 [game-ci license-activate action](https://game-ci.com/docs/license) 在本地一键获取：

```bash
docker run --rm -e UNITY_EMAIL -e UNITY_PASSWORD -e UNITY_LICENSE \
  -v $HOME:/root ghcr.io/game-ci/unity-license-activate
```

执行后 `$HOME/.local/share/unity3d/License.ulf` 的内容即为要填入 `UNITY_LICENSE` 的值。注意：同一许可证最多同时在 2 台机器上激活，CI 每次运行会复用该 secret，请勿在多处并发构建。

### 产物说明

- `build/WebGL/` 打包为 `sanguo-webgl.zip`，解压后仍需通过 HTTP 服务访问（见 4.2），或直接上传到 itch.io / GitHub Pages。
- Release 标题与变更说明由 `generate_release_notes` 根据合并的 PR / commit 自动生成。

## 6. 常见问题（FAQ）

| 现象 | 处理 |
| --- | --- |
| 界面中文显示为方块 | 代码已内置系统中文字体回退（微软雅黑 / 苹方 / Noto Sans CJK 等）。若仍乱码，请在系统安装上述任一字体，或将 TTF 放入 `Assets/Resources/Fonts` 并在 `GameBootstrap.EnsureChineseFont()` 中优先加载。 |
| 打开工程报大量缺失引用 | 确认使用 2022.3 LTS；删除 `Library/` 目录后重新打开让 Unity 重导入。 |
| Play 后画面全黑 | 检查是否误加了第二个相机或旧场景对象；本工程只需要空场景 + GameBootstrap（挂在任意 GameObject 上，或由启动器自动创建）。 |
| WebGL 本地双击 index.html 白屏 | 必须走 HTTP 服务，见 4.2。 |
| 存档丢失 | 存档为 JSON 单文件，路径会在点击“存档”按钮后的状态栏显示；备份该文件即可迁移进度。损坏文件会被自动改名为 `*.corrupt-*` 并从新档开始。 |

## 7. 发布检查清单

- [ ] Unity Editor 中编译零错误、Console 无红色报错
- [ ] 全流程手测：新档 → 抽卡 → 上阵 → 战斗 → 胜利结算 → 关闭重启后进度恢复
- [ ] “重置存档”按钮可用且不影响正在进行的战斗（战斗中会被拒绝）
- [ ] 目标平台构建产物在本机实际运行通过
- [ ] WebGL：首屏加载 < 30s（视带宽），Chrome / Edge / Safari 各验证一次

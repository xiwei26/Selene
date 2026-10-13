# Windows 播放器功能补齐：Claude Code 实现交接

- 日期：2026-10-13
- 规划：Codex
- 实现负责人：Claude Code
- 指定模型：Opus 5.5，精确 ID `claude-opus-5-5`
- 状态：实现规格已完成；本文件不代表功能已经实现。

## 工作区和前置条件

- 工作区：`E:\learning\Selene\.worktrees\windows-player-controls`。
- 分支：`codex/windows-player-controls`，从 `origin/main` 建立。
- 前置 WinUI 3 提交：原提交 `f3295eb`，已拣选为本分支的 `c1a5d39`。
- 只修改 Windows 播放器、相关 Core 逻辑、测试和文档。其他平台不在本轮范围。
- 原工作区 `E:\learning\Selene` 有未提交的 Android、README 和 agents.md 改动；不要暂存、覆盖、清理或移动它们。
- 用户明确要求由 Claude Code 使用 Opus 5.5 实现。模型不可用或认证缺失时报告阻塞，不替换模型，不把规划当作实现成果。

## 当前问题与已有能力

`NativeVideoPlayerView.xaml.cs` 设置了 `AreTransportControlsEnabled = false`，关闭了原生播放控制。
`PlayerPage.xaml` 将暂停、重试、选集等按钮放在进度条之后的限高滚动区域，导致它们不容易被发现。

已有：加载和播放、暂停、拖动进度、播放时间、错误提示、重试、手动选集、集数显示倒序、续播和播放记录保存。

缺失或未接通：音量、静音、倍速、全屏、快捷键、上一集／下一集、自动下一集、可操作的多来源切换。
`BuildSourceRow()` 目前只有一个没有点击逻辑的来源按钮；`SwitchSource()` 没有调用方，来源集合基本只有当前资源。

## 本轮交付范围（P0，全部必须完成）

| 功能 | 交互与验收要求 |
| --- | --- |
| 完整播放控制 | 视频区原生控制提供播放／暂停、时间、进度、音量和静音；鼠标移入、触摸或键盘焦点能唤起控制。常用操作不依赖滚动下方影片资料。 |
| 快进／快退 | 按钮与快捷键默认跳转 ±10 秒；按真实可跳转能力启用并限制在合法范围。不可跳转的媒体不能显示虚假的可拖动状态。 |
| 音量／静音 | 音量 0–100%，对应引擎 0–1。静音不丢失用户原音量，取消静音恢复；原生控件、快捷键与显示值保持同步。 |
| 倍速 | 0.5、0.75、1、1.25、1.5、2 倍；默认 1 倍。原生速度菜单不能提供全部档位时，增加小型原生 Flyout。只展示已实现的档位，不支持时禁用并说明原因；同一观看会话换集／换源保留倍速。 |
| 真正全屏 | 入口按钮、F／F11、视频双击进入或退出；Esc 优先退出全屏。隐藏标题栏、导航、页面标题、影片资料和视频外边距；退出恢复原窗口状态和页面布局，播放不中断。 |
| 上一集／下一集 | 按后端原始集数索引导航；第一集／最后一集禁用越界按钮。倒序只改变显示，不改变集数身份和历史记录编号。 |
| 自动下一集 | 播放器菜单开关，默认开启并持久化。正常播放结束只触发一次下一集；错误、停止、手动 seek 不触发。最后一集显示播放完毕，不自动循环。 |
| 多来源切换 | 从同一作品的候选来源选择，而非只显示当前来源。来源详情按需加载；目标存在匹配集数才能切换，保留原位置与暂停状态。失败保持原来源，不偷偷退回第一集。 |
| 重试／续播 | 重试保留当前集数和位置；续播等待媒体真正可跳转再应用。媒体时长晚到或未知时，不提前清除待续播位置。 |
| 状态和记录 | 按引擎真实状态显示加载、缓冲、播放、暂停、结束、错误。换集、换源、离开页面、关闭窗口前保存旧资源的记录，再替换或停止；服务端失败时保留本地回退。 |

字幕／音轨下载、画中画、投屏、跳片头片尾和清晰度手动选择放入 P1，不作为本轮必做项。媒体自带字幕／音轨若已由原生控件正确支持可保留，但不要放置无效菜单。

## 页面布局和快捷键

- 保持 WinUI 3 / Fluent 风格及现有系统、浅色、深色主题。
- 页面分为视频区、固定的集数／来源操作工具栏、可滚动的影片资料区。
- 原生视频控制承担主要播放操作；移除资料区中重复的播放进度和暂停入口。
- 800×600 和 1024×640 窗口下，播放／暂停、静音、音量、全屏、倍速和选集入口均可达。低频功能可进入原生“更多”菜单；不能仅依靠原生控件在窄宽度自动隐藏按钮。
- 资料可以折叠或独立滚动；工具栏不跟随长评论、海报、推荐列表滚出视口。
- 每个图标都有中文 ToolTip 和辅助功能名称；Tab 可达，Enter／Space 可操作，焦点样式可见。

| 快捷键 | 行为 |
| --- | --- |
| Space | 播放／暂停 |
| Left / Right | 后退／前进 10 秒 |
| Up / Down | 音量 ±5% |
| M | 静音／取消静音 |
| F / F11 | 全屏切换 |
| Esc | 先关闭已打开的菜单／Flyout；没有弹出层时退出全屏。非全屏时不直接关闭播放器 |
| Ctrl+Left / Ctrl+Right | 上一集／下一集 |

快捷键只在播放器页活动时处理。TextBox、ComboBox、Slider、菜单、弹出层正在编辑或操作时，保留它们原有的键盘行为。
一次按键只能由一个处理器处理，防止原生 TransportControls 与页面快捷键重复跳转／暂停。

## 技术方案与代码边界

1. **Core 引擎契约**：扩展 `IMediaPlayer` 的真实播放状态、音量、静音、倍速、可跳转能力和必要变更通知；相应更新 FakeMediaPlayer。
   保持 Core 独立于 WinUI。对不支持的媒体能力给出真实结果，不能只依据 `TotalTime > 0` 推断可跳转。
2. **Windows 适配层**：`WindowsMediaPlayer` 对接 `MediaPlayer.Volume`、`IsMuted`、`PlaybackSession.PlaybackRate` 和实际 playback session 能力。
   引擎回调不是 UI 线程保证；UI／Observable 状态统一调度。原生 TransportControls 绕过 VM 直接操作引擎时，VM 也必须收到变更。
3. **原生控制**：优先使用 `Microsoft.UI.Xaml.Controls.MediaTransportControls`，通过公开属性配置常用功能。
   若需扩展选集／全屏请求，可采用小型派生类或独立原生 CommandBar；避免复制庞大的默认模板。不能禁用 MediaPlaybackCommandManager 后又依赖原生控件自动控制播放。
4. **播放协调**：`PlayerViewModel` 或一个单一的 Core 播放协调器负责播放请求、待 seek、换集、换源、重试、自动下一集和记录快照。
   不新增相互竞争的播放引擎实例，不让页面和播放器 ViewModel 分别拥有另一套当前集数。
5. **全屏**：`MainWindow` 拥有窗口 presenter 与外层布局切换，播放器页发出请求。
   使用 `AppWindow.SetPresenter(FullScreen)` 并保存原 presenter／窗口位置／最大化状态。
   `MediaPlayerElement.IsFullWindow` 仅扩展视频区域不足以作为桌面全屏验收。
   隐藏／禁用 TransportControls 自带 full-window 按钮；单一全屏命令由伴随工具栏、快捷键和双击调用，避免两套全屏状态竞争。
   全屏过程中保持原视频控件与原 MediaPlayer 的连接；避免重建、移出视觉树或触发 Unloaded 的暂停／保存逻辑。
6. **候选来源**：优先从聚合搜索和详情播放请求传递同一作品候选；历史续播可通过现有 `IContentProvider.SearchAsync` 查询候选，并用 DetailAsync 按需补齐。
   按 `source + id` 去重；已知有效 DoubanId、年份或媒体类型冲突时先排除，不能继续用标题回退。同一有效 DoubanId 优先，否则匹配规范化标题及可用的年份／类型信息；规范化保留季数和部数，排除重名、翻拍和不同季作品。不同来源集数排列不一致时优先匹配集名，确认合理才采用索引。
   不能把“同一关键词命中”全部视为同一作品。
7. **异步保护**：使用请求代次／取消令牌。快速点不同集数、来源，或者退出页面之后，过期请求不能覆盖最新选择或重新开始播放。
   保存旧记录时先快照旧 source、id、集数、时间，不在 await 之后读取已替换的可变 CurrentResult。
8. **设置**：本轮新增自动下一集设置须真正持久化并在重启后恢复；音量／倍速在同一播放会话内保留即可，不必新增整个设置页改版。

重点文件：

- `native-windows/src/SeleneNative/Views/PlayerPage.xaml` 和 `.xaml.cs`
- `native-windows/src/SeleneNative/Views/NativeVideoPlayerView.xaml` 和 `.xaml.cs`
- `native-windows/src/SeleneNative/Services/WindowsMediaPlayer.cs`
- `native-windows/src/SeleneNative/MainWindow.xaml.cs`
- `native-windows/src/SeleneNative.Core/Services/IMediaPlayer.cs`
- `native-windows/src/SeleneNative.Core/ViewModels/PlayerViewModel.cs`
- 聚合搜索、详情与播放请求中传递候选来源的最小必要代码
- `native-windows/tests/SeleneNative.Tests/Player/`

## 先修复现有状态问题

- VM 当前没有可观察的 PlaybackState。Playing → Paused／Ended 可能没有属性通知，暂停按钮无法可靠同步；增加真实状态通知。
- `OnMediaOpened` 当前在 `AutoPlay = false` 时也宣告 Playing；以实际 session 状态为准，区分“媒体已打开”和“正在播放”。
- `Retry()` 当前重新加载丢失位置；保存位置并在准备完成后 seek。
- `SwitchSource()` 当前先改 CurrentSource，再验证集数；应先验证候选，成功才提交切换，并恢复位置／暂停状态。
- PendingSeekTime 当前在时长未知、SeekTo 未执行时仍被清除；成功应用后才消费。
- 新播放请求须清理无关的待 seek／旧来源，避免上一部的进度应用到新片。
- 处理 Ended 事件与开关、边界、请求身份，防止重复事件导致连续跳集。
- 窗口退出保存须在引擎状态清空之前完成或得到可验证的可靠快照；不要依赖进程已经退出后的 fire-and-forget 写入。

## 验收与测试

### Core 行为测试

新增有意义的状态行为测试，不只检查源码中是否出现按钮名称：

- 音量范围、静音恢复、倍速合法档位与引擎变更同步。
- seek 上下界、不可 seek 媒体、时长晚到时续播只应用一次。
- 暂停／播放／结束状态通知；媒体准备完成与开始播放的抽象状态应分开。真实 MediaOpened 回调行为在 Windows 适配层／实测验证，不能要求只引用 Core 的现有测试项目调用 WinUI。
- 重试恢复位置；新作品不会继承旧待 seek。
- 上／下一集边界；显示倒序不影响真实集数；自动下一集开关和结束事件去重。
- 来源集数匹配、冲突 DoubanId／年份／不同季排除、目标集数缺失／详情失败保留原来源、暂停状态恢复。
- 快速连续选择的旧请求不能覆盖新请求。
- 替换前旧记录快照、退出保存、本地回退。

### 实际窗口验证

使用合法、可重复的本地短视频或确定可访问的测试流；写明输入来源，不能把远端链接失效记作通过。
至少检查：

1. 800×600、1024×640、最大化；主要控制无需滚动资料区。
2. 鼠标播放／暂停、拖动与 ±10 秒跳转、音量、静音、倍速；显示与实际效果一致。
3. 键盘与鼠标操作同一状态；文本／下拉框／滑块焦点时不抢键。
4. 全屏／Esc 多次往返不重载视频、不丢进度，返回正确窗口状态；播放和暂停各测试一次。
5. 手动下一集、第一／最后一集边界、正常结束自动下一集、关闭开关。
6. 两个测试来源切换同一集后恢复位置；缺集和网络错误不会错误换片。
7. 历史续播、重试、返回首页、窗口关闭后重新进入，记录正确。
8. 浅色／深色主题、Tab 焦点及菜单可达。

如果自动化工具或视频测试素材不可用，明确列出未完成的验证及原因；不能声称已经完成实测。

## 交付流程

在本工作区直接实施本轮 P0，不再重复建立规划文件，也不等额外确认。

1. 先核对工作区、分支、最新用户 AGENTS.md 约束和已有 diff。
2. 实现状态层，再接控制界面／全屏，再接来源与记录保存；每段保持可编译。
3. 更新 `native-windows/README.md`，列出实际完成的功能、快捷键及能力限制。
4. 运行 Core／回归测试和 Release 构建，通过后发布并生成安装包：

   ```powershell
   dotnet test .\native-windows\tests\SeleneNative.Tests\SeleneNative.Tests.csproj -c Release -p:Platform=x64
   .\native-windows\build-installer.ps1 -Platform x64
   ```

   `build-installer.ps1` 必須执行实际构建／测试／publish，不要仅重用原 UI 分支的旧安装包。
   构建前关闭自己启动的测试实例，保留用户其他进程和工作区改动。

5. 核对 `native-windows/publish/win-x64` 与新的
   `native-windows/dist/selene-1.1.0-windows-x64-setup.exe` 的生成时间。
6. 使用原生 `git add`、`git commit`、`git push` 提交和推送相关源码及文档。
   不使用 `gh`，不提交 bin、obj、publish、dist、日志或凭据，不擅自合并主分支。
7. 最终报告实现清单、测试结果、未覆盖的实测、提交／分支与安装包绝对路径。

预期本工作区安装包路径：
`E:\learning\Selene\.worktrees\windows-player-controls\native-windows\dist\selene-1.1.0-windows-x64-setup.exe`。

## P1（本轮完成后再评估）

画中画、媒体自带音轨／字幕选择、画面适配模式、片头片尾规则、周期性崩溃恢复记录、系统媒体快捷键／SMTC。
这些功能应先检查媒体和 Windows 版本能力，不能给每个来源放一个不能工作的开关。

## 技术参考

- [WinUI 原生 MediaTransportControls](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/custom-transport-controls)
- [AreTransportControlsEnabled 和自动控制绑定](https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.mediaplayerelement.aretransportcontrolsenabled)
- [Windows MediaPlayer 与播放 session](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/play-audio-and-video-with-mediaplayer)
- [Windows AppWindow](https://learn.microsoft.com/en-us/windows/apps/develop/ui/manage-app-windows)
- [Claude 模型 ID](https://platform.claude.com/docs/en/models/overview)

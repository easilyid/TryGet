# Adapter 契约偏离白名单（Adapter Contract Deviation Allowlist）

## Status

Accepted

## Context

V0.5 落地 5 个 Unity Adapter（UnityAudioModule / UnityInputModule / PlayerPrefsSaveModule / UGUIUIModule / UnitySceneModule），全部实现对应的 `IXxxModule` Core 接口。

期望：Memory 实现与 Unity Adapter 在公开接口契约上完全行为对齐（同一组操作两个实现输出一致状态），这样 Procedure / 业务代码切换 Memory ↔ Adapter 时不需要改业务代码。

实际 V0.5 关门评审（Plan agent）发现 3 处合理但**轻度偏离的契约**：

### 偏离 1：Adapter 注册前置（acceptable）

- `UnityAudioModule.Play(cue, category)` 要求先 `RegisterClip(cue, AudioClip)`，否则抛 `InvalidOperationException`。
- `MemoryAudioModule.Play(cue, category)` 无前置，任意 cue 字符串都成功。

**原因**：Unity AudioSource 必须有真实 AudioClip 资源才能播放，Memory 实现无渲染需求。
Unity Adapter 抛"未注册"错误是合理的早期失败（fail-fast）。

类似情况：
- `UGUIUIModule.Open(uiName)` 要求先 `RegisterPrefab`
- `MemoryUIModule.Open(uiName)` 无前置

### 偏离 2：诊断字段语义不同（acceptable）

- `MemorySaveModule.KeyCount`：反映**所有**通过本实例 Set 过的 key（含历史）。
- `PlayerPrefsSaveModule.KeyCount`：反映**本进程内**通过本 Adapter Set 过的 key，
  **不反映**跨进程已存在的 PlayerPrefs 历史 key。

**原因**：PlayerPrefs 原生不提供 key 枚举 API，Adapter 无法在 OnInit 时扫描全部历史 key。
KeyCount 在 Adapter 层退化为"本进程诊断"用途，与 Memory 实现"全量诊断"语义不完全等价。

### 偏离 3：隐式运行环境前置（acceptable）

- `MemorySceneModule.Load(sceneName)`：任意字符串都成功（纯状态机）。
- `UnitySceneModule.Load(sceneName)`：依赖 `SceneManager.LoadScene` 内部行为——
  场景必须在 BuildSettings 的 Scenes in Build 中注册才能真正加载（否则 Unity log error）。

**原因**：BuildSettings 是 Unity 工程级配置，Adapter 无法在 API 层做检查。Adapter 内部状态
（_ordered/_index）会更新，但真实场景加载是否成功取决于 BuildSettings。

## Decision

**接受以上 3 类偏离作为 Adapter 的合理 escape hatch**，但必须满足：

1. **接口 XML 注释明示**：Core `IXxxModule` 接口要在 XML doc 里加一句"Adapter 实现可附加注册前置 / 运行环境前置"。
2. **Adapter 实现自身 XML 注释列出**：每个 Adapter 类 XML doc 顶部要列出自身的"偏离点"。
3. **PlayMode 测试覆盖偏离边界**：每个 Adapter 测试要至少一个用例验证偏离行为（如 UnityAudioModule_Play_UnregisteredCue_Throws）。
4. **不允许的偏离**：
   - 状态机本身的语义偏离（如 IsOpen / IsPaused / IsPlaying 返回值含义不能变）
   - 顺序保持承诺（OpenedUIs / LoadedScenes 必须按插入顺序）
   - 异常行为（null/empty key 该抛该不抛必须一致）
5. **新增 Adapter 实施时**：发现新偏离点必须更新本 ADR 的白名单。

## Consequences

**好处：**
- 接受合理偏离避免 Adapter 实现臃肿（如 PlayerPrefsSaveModule 不需要在 OnInit 扫描全 PlayerPrefs key）
- 业务代码切换 Memory ↔ Adapter 时仍能 work（核心状态机一致）
- Adapter 可以做合理的 fail-fast（如 UnityAudio 要求 RegisterClip）

**风险：**
- 偏离白名单膨胀：每加一个 Adapter 都可能要加 1-2 条
- 业务代码假定 Memory 行为时（如假定 SaveModule.KeyCount 全量准确）在切 Adapter 后行为变
- 缓解：第 3 条强制 PlayMode 测试覆盖，发现偏离时业务测试会先爆

**违规处理：**
状态机语义 / 顺序保持 / 异常一致性的偏离 ≠ 合理偏离 = bug，必须修。

## V0.5 当前偏离白名单

| Adapter | 偏离点 | 类别 |
|---|---|---|
| UnityAudioModule | Play 要求 RegisterClip | 注册前置 |
| UGUIUIModule | Open 要求 RegisterPrefab | 注册前置 |
| UnityInputModule | Action 查询要求 RegisterButton/Axis/Axis2D | 注册前置 |
| PlayerPrefsSaveModule | KeyCount 仅反映本进程内 Set | 诊断字段语义 |
| UnitySceneModule | Load 隐式依赖 BuildSettings | 运行环境前置 |

V0.6+ 新增 Adapter 在此表追加。

## 关联

- ADR-0011 ModuleHost + IModule 契约：Adapter 必须实现 Core 接口
- ADR-0012 Shadow csproj：Adapter 不参与跨端编译，Core 必须
- design.md §13 关键纪律集第 11 条：Memory 实现纪律（Write 严格 / Read 容错 / Shutdown 回归）

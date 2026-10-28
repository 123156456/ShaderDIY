# ShaderDIY

面向美术的 Unity URP Shader 拼装工具。用下拉换光照模块、调参数、实时预览，按当前组合编译成一份真正的 URP Shader。

## 环境

- 以 **Unity 2021.3 + URP 12** 为主
- 第 1 区可切换 Unity 2022 / Unity 6 宏（未作为正式环境验证）

## 打开

`Window → ShaderDIY → 材质工坊`

把 `Assets/ShaderDIY` 拷进工程即可。保留 `.meta`。

## 能做什么

- 大类：无光照 / PBR / 卡渲 / 大气 / 自定义
- PBR：D、G、F、F0、直接/环境项可换；可叠加经验 SSS、视差、折射；光向量可换
- 卡渲：身体 / 脸 / 头发
- 大气：透射率（路径积分 / 查表）与单次 / 多次散射
- 贴图区、渲染状态、性能估算、公式、时间轴、Ctrl+Z 撤回

## 给会写 Shader 的人

用 `.sdiy`（骨架）和 `.sdiym`（模块）扩展槽位。插件只负责拼接和界面，不改你写进模块里的公式。说明见 `Assets/ShaderDIY/Documentation/SdiyFormat.txt`。

不要手改 `Assets/ShaderDIY/Generated`。

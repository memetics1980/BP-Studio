# 桌面版分发组件

本应用采用 WPF、Microsoft .NET 8、Microsoft WebView2 SDK/Runtime、NAudio、React、React DOM、Lucide React。许可副本保存在发布目录 `licenses`，各组件仍适用其原始许可。内置语音组件及模型的来源、版本和许可另见 `THIRD_PARTY_SPEECH.md` 及 `speech` 目录内的许可文件。

随包的 Microsoft WebView2 Fixed Version Runtime 为 Windows x64，版本 `156.0.4314.58`，来自微软官方分发接口 `https://developer.microsoft.com/microsoft-edge/api/webview2`。下载 CAB 的 SHA-256 为 `857dcfd6f7035508a29645fa2978c3a6a841f220df77cc366d318894e19f4792`，构建时验证主程序的微软 Authenticode 签名，逐文件校验清单在 `webview-runtime/bundle.json`。原始运行环境及其附带许可完整保留。

运行时优先使用本包的 `webview-runtime`，不用修改或安装系统 WebView2。微软的 Fixed Version 部署文档：https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution 。固定运行环境随本应用的新版本一同更新；维护者发布新版前应检查微软的稳定运行环境更新并重新验证。

分发程序只访问本地界面资源；AI/API 请求由用户配置后主动发起。分发包不包含正式资料目录、凭据、测试资料或个人缓存。

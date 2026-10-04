# Smart-Agent Windows brand assets

`SmartAgentMark.png` is the canonical Smartaryn ecosystem mark vendored from the Smart-Agent core repository at `ui/public/brand/smartaryn-ecosystem-mark.png`.

Canonical SHA-256:

`2B9150449DC27ED3D39BCBB76ACEF770DA4A48DE2B4D482C2C4B8C8A6315BACB`

The Windows tile, splash, Store, lock-screen, target-size, and `smart-agent.ico` assets are transparent/unplated derivatives of this exact mark. They intentionally add no downstream-only symbol, background, or color treatment.

The standard MSIX filenames (`Square44x44Logo.png`, `StoreLogo.png`, and related files) are retained to minimize upstream manifest churn. The upstream OpenClaw mascot/icon source files are intentionally removed from the Smart-Agent downstream tree because WinUI/PRI can package unreferenced asset files. The project exclusions remain as a fail-closed guard in case a future upstream merge reintroduces them.
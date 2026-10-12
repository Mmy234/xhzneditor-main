# 编辑端 V1 开发约定

给做 **Vue3 编辑器 + Runtime** 的人。展示端只吃你发布出来的包，不读草稿。配套文档：`Doc/player-v1.md`。

V1 目标：无编程人员能排图/字/视频/按钮、跳页、保存草稿、发布包。浏览器打开就能编。

---

## 1. 职责

| 做 | 不做 |
|---|---|
| 编排界面、画布、属性、资源库、基础设置 | kiosk 全屏、藏任务栏、禁 Win 键 |
| 草稿 `.xhzn` 的打开/保存 | 绑机、到期拦截、解压缓存 |
| 发布 `.xhznpack`（检查 + 加密 + 授权字段 + 版本 +1） | UDP/串口、多屏窗、安装包 |
| **Runtime**（页面渲染、跳转、翻页、视频、闲置） | 改播放器壳 |

播放器把 `/runtime` 当静态资源拷走。页面逻辑只改 Runtime，不要让展示端分叉。

建议目录：

```
/editor      Vue3
/runtime     index.html + js/css（本端维护）
/schema      与包格式相关的示例
/samples     手写 golden 包（先检入，给展示端联调）
```

---

## 2. 产品形态

- 纯浏览器。无账号、无协同。预留以后 SaaS（保存/打开做成可替换接口：先本地文件，后云）。
- 用户无编程经验。文案全中文。不要出现 JSON、hotspot、channel 这类词。
- 一台电脑一个人编一个工程。

现场流程：

```
新建工程（默认 1920×1080）
  → 加页、拖组件、改属性
  → 保存草稿 .xhzn
  → 发布 .xhznpack（另选路径，禁止默认覆盖草稿）
```

---

## 3. 两个文件

| | 保存 = 草稿 | 发布 = 导出 |
|---|---|---|
| 扩展名 | `.xhzn` | `.xhznpack` |
| 谁打开 | 仅编辑器 | 仅播放器 |
| 版本号 | 不动 | 每次发布自动 +1 |
| 检查 | 不挡保存 | 不通过不准发 |
| 工程密码 | 可设，打开草稿要密 | 包加密；播放器不对观众弹此密码 |
| 授权字段 | 不必给播放器校验 | 写入 `manifest.license` |

内部都是 zip。浏览器用 OPFS 做工作副本；保存时打回一个 `.xhzn`。不要让用户看到一堆散文件。

素材一律拷进工程；同名覆盖。不要引用盘上原路径。

---

## 4. 发布包格式（编辑器写出，播放器只读）

```
xxx.xhznpack
  manifest.json
  project.json
  pages/{pageId}.json
  assets/**
```

- UTF-8，JSON 无注释。
- 路径正斜杠，素材 `assets/...`。
- `schemaVersion: 1`。更高版本播放器会拒收，不要私自加未约定字段当必填。
- 坐标系：左上原点，Y 向下，单位 px，相对设计分辨率。
- 颜色 `#RRGGBB`。时间秒。
- 包体积按不超过 **2G** 检查。

资源 URL 在 Runtime 里用（播放器会映射虚拟主机）：

- 工程素材：`https://project.xhzn/assets/...`
- 编辑器预览可用 Vite 等价映射，保持相对路径一致。

不认识的字段：读取时忽略，不要崩。自己新加可选字段先写进约定再发。

### 4.1 `manifest.json`

```json
{
  "schemaVersion": 1,
  "publishVersion": 1,
  "id": "demo-001",
  "name": "样例查询",
  "design": { "width": 1920, "height": 1080 },
  "pages": ["home", "p2"],
  "entryPageId": "home",
  "license": {
    "expireDate": "2026-12-31",
    "trialDays": 7
  },
  "player": {
    "autoStart": false,
    "cursorVisible": false,
    "exitPassword": ""
  }
}
```

| 字段 | 规则 |
|---|---|
| `publishVersion` | 每次**发布** +1，保存草稿不动 |
| `pages` | 页顺；列表可拖拽改序后按此写出 |
| `entryPageId` | 钉的首页；未钉则 `pages[0]` |
| `license` | 基础设置填写。整段不写 = 播放器当试用并打「试用」大字 |
| `expireDate` | 可空。到日播放器锁死 |
| `trialDays` | 可空。从该包在该机首次打开起算 |
| `player.autoStart` | 播放器加载此包时登记/取消开机自启 |
| `player.cursorVisible` | 默认 `false`（不显示鼠标） |
| `player.exitPassword` | 播放器左上角连点 5 下用。可空（调试可直接退） |

工程密码只用于打开草稿，不要当作播放器观众密码。

### 4.2 `project.json`

```json
{
  "fonts": [
    { "family": "SourceHanSans", "src": "assets/fonts/SourceHanSans.otf" }
  ]
}
```

### 4.3 `pages/{id}.json`

```json
{
  "id": "home",
  "name": "首页",
  "background": { "type": "color", "value": "#000000" },
  "idle": { "seconds": 60, "pageId": "home" },
  "defaultTransition": { "type": "fade", "dur": 0.3 },
  "components": []
}
```

**背景** `type`：

- `color`：`value` 色值
- `image`：`src`、`fit`（同图片组件）
- `video`：同视频组件的循环/静音/自动播等，作全页背景

**闲置：** `seconds === 0` 或不写 = 关。每页单独配。任意点击/触摸重置（Runtime 做）。目标页必须存在，发布时检查。

**默认翻页：** `none` | `fade` | `slideLeft` | `slideRight` | `slideUp`。跳转没写 `transition` 时用目标页这个值。

---

## 5. 组件

V1 只允许：`image` | `text` | `video` | `button`。其它 type 不要写进包。

### 5.1 共用属性（每种都有）

```json
{
  "id": "c1",
  "name": "按钮1",
  "x": 100,
  "y": 200,
  "w": 280,
  "h": 80,
  "rotation": 0,
  "z": 1,
  "visible": true,
  "onClick": {
    "action": "gotoPage",
    "pageId": "p2",
    "transition": { "type": "slideLeft", "dur": 0.3 }
  }
}
```

- `id` 自动生成，工程内唯一；`name` 人可改。
- 组件不得拖出设计画布，到边吸住。
- `rotation` 单位度（静态摆放，不是入场动画）。
- `visible` 组件显隐。
- `onClick` 可 `null`。图片、按钮、视频都能点。
- `action` 仅两种：`gotoPage`（必填 `pageId`）| `gotoHome`。
- `transition` 可省 → 用目标页 `defaultTransition`。

复制组件 / 复制整页：深拷贝，新 `id`，名字加「副本」。

### 5.2 image

仅 **jpg / png**。

```json
"type": "image",
"props": {
  "src": "assets/a.png",
  "fit": "cover"
}
```

`fit`：`contain` | `cover` | `stretch` | `none`（原尺寸，超出裁切）。

### 5.3 text

整框一种样式。V1 **不做富文本、不做竖排**。溢出裁切。

```json
"type": "text",
"props": {
  "text": "欢迎查询",
  "fontFamily": "Microsoft YaHei",
  "fontSize": 48,
  "color": "#FFFFFF",
  "fontWeight": 400,
  "italic": false,
  "align": "left",
  "valign": "middle",
  "lineHeight": 1.4,
  "letterSpacing": 0
}
```

字体 = 预设系统名 + 工程导入字体文件。浏览器枚举不了本机全部字体。预设：微软雅黑、黑体、宋体、楷体、Arial、Times New Roman。导入的字体写入 `project.json` 并拷到 `assets/fonts/`。

改设计分辨率时：x/y/w/h 与 **字号** 一起等比缩放，先弹确认。

### 5.4 video

导入时检查常见格式（至少 **mp4 / mov**）。系统播放条不做，默认没有。

```json
"type": "video",
"props": {
  "src": ["assets/a.mp4"],
  "loopMode": "single",
  "intervalSeconds": 0,
  "muted": false,
  "autoplay": true
}
```

| `loopMode` | 含义 |
|---|---|
| `once` | 播一次 |
| `single` | 单曲循环 |
| `list` | 列表循环（`src` 多条才有意义） |
| `interval` | 播完停 `intervalSeconds` 秒再播 |

`src` 可多条。一条时列表循环按单曲处理即可。

`autoplay: true` 且 `muted: false`：播放器壳会允许带声自动播。编辑器 Chrome 预览可能要点一下才出声，接受。

`once` 播完可挂 `onEnded`，动作表与 `onClick` 相同，可空：

```json
"onEnded": { "action": "gotoPage", "pageId": "p2" }
```

切页时上页视频必须停并卸载（Runtime）。同一页多个视频允许同时播。

### 5.5 button

```json
"type": "button",
"props": {
  "srcUp": "assets/btn.png",
  "srcDown": "assets/btn-down.png"
}
```

缺图用内置默认皮。按下 `srcDown`，抬起 `srcUp`。单击走共用 `onClick`。

---

## 6. 翻页动画（V1 唯一动画）

五种：`none` | `fade` | `slideLeft` | `slideRight` | `slideUp`。

- 配在这一次跳转的 `onClick.transition` / `onEnded.transition`。
- 省略则用目标页 `defaultTransition`。
- 画布预览直接播。不要时间轴轨道。
- 不做页内渐隐/位移/缩放动画。

实现（Runtime）：CSS `transform` / `opacity`，不要用改 `left` 做位移动画。

---

## 7. 界面

```
┌ 菜单：新建 打开 保存 另存 发布 预览 基础设置 ─┐
├ 页列表 │  画布（Runtime + 选框） │ 属性 │
│         │  标尺 / 网格 / 参考线    │      │
│ 资源库  │  缩放 + 滚动条          │      │
└─────────┴─────────────────────────┴──────┘
```

贴近 Unity 布局：左树、中画布、右属性。

### 7.1 基础设置

- 工程名
- 设计宽高（默认 1920×1080，可直接改；改则整体等比并确认）
- 工程密码（打开草稿）
- 授权到期日、试用天数（写入发布包）
- 开机自启、鼠标显隐（写入 `manifest.player`）
- 退出密码
- 钉首页（页列表上有标；不钉 = 第一页）

### 7.2 画布

- 按设计分辨率逻辑像素编辑；窗口里缩小显示，超宽可滚动。
- 滚轮缩放；空格或 Q + 拖平移。
- 标尺、参考线、网格（默认 20px，可改）。
- 吸附：网格 + 对象边缘。
- 对齐：左/中/右/顶/底/水平分布/垂直分布。多选即可对齐，**不成组**。
- 可旋转。锁定/隐藏图层不做。
- 撤销 / 重做 10 步。

### 7.3 页

- 上限按 100 页内考虑。
- 增删改、复制整页、拖拽改序。不分章节。
- 每页名称、背景、闲置、默认翻页。

### 7.4 资源库

- 按类型分类、搜索、缩略图。
- 从库拖到画布；也支持从资源管理器把文件拖进画布。
- 图片 jpg/png；视频检查格式。

### 7.5 属性面板

先共用，后类型私有。双击文本进编辑。

### 7.6 快捷键（贴近 Unity，小白也能点工具栏）

| 键 | 作用 |
|---|---|
| Ctrl+S | 保存草稿 |
| Ctrl+Z / Ctrl+Y | 撤销 / 重做 |
| Ctrl+C / V / D | 复制、粘贴、再制 |
| Delete | 删 |
| 方向键 / Shift+方向 | 微移 1px / 10px |
| Q 或 空格+拖 | 平移 |
| E | 旋转 |
| 滚轮 | 缩放 |
| F | 对准选中 |
| Ctrl+A | 全选 |
| Esc | 预览中回到编辑 |

### 7.7 预览

- 同一 Runtime，画布内缩小播，可点可跳。
- 预览时藏选框。Esc 回编辑。
- 不做「推送到播放器」。
- 自动带声在浏览器里可能被拦，属预期。

---

## 8. Runtime（本端维护，展示端原样用）

- 读 JSON → 建页 → 点击/闲置/视频结束 → 跳转 + 五种翻页。
- 只加载当前页素材，预载邻页。禁止把 100 页大图一次解码。
- 编辑态：Runtime 上面叠选框和拖拽手柄，不要用 Konva 再画一套图文。
- V1 不需要 `host.send`（无通讯）。

编辑器预览映射工程目录，使 `assets/` 路径与发布后一致。

---

## 9. 发布检查（不通过不能出包）

- 至少一页
- `entryPageId` 存在（或可回落到第一页）
- 图/视频/字体文件都在工程内
- `onClick` / `onEnded` / `idle` 指向的页存在
- 视频扩展名在白名单
- 打包后将超过 2G

写出 `publishVersion`（上次发布 +1，首次为 1）。

---

## 10. V1 明确不做

音频；网页；PPT；PDF；滚动视窗；单选/多选；通讯；局域网推送；拓展屏；复制屏；页内 PPT 入场动画；时间轴；成组/锁层；富文本；竖排；账号；自动保存；最近工程列表。

「返回上一页 / 滑动翻页 / 倒计时」以后当独立组件做，V1 只提供 `gotoPage` 和 `gotoHome`。

---

## 11. 本端验收

1. 新建 1920×1080，放图、字、视频、按钮，保存草稿，关掉再打开还在。
2. 改设计分辨率，组件与字号等比，且不出画布。
3. 复制页、复制组件，id 不冲突。
4. 预览能点跳、五种翻页、闲置跳转、视频能播。
5. 发布出 `.xhznpack`，版本 +1；缺素材时拦住。
6. 手写或导出的包，展示端不改 JSON 就能加载（字段与本文一致）。

联调夹具：`samples` 里先放一份 golden 包（1 页图+字+按钮跳第 2 页+1 视频+fade），编辑器未完成时展示端也能吃。

(() => {
  const params = new URLSearchParams(location.search);
  const projectBase = (params.get("projectBase") || "https://project.xhzn").replace(/\/$/, "");
  const placeholder = params.get("placeholder") || "https://runtime.xhzn/placeholder.png";
  const trial = params.get("trial") === "1";
  const cursorVisible = params.get("cursor") === "1";

  document.body.style.cursor = cursorVisible ? "auto" : "none";

  const stage = document.getElementById("stage");
  const boot = document.getElementById("boot");
  const pageCache = new Map();
  const fontFaces = [];
  let manifest = null;
  let project = null;
  let currentId = null;
  let currentEl = null;
  let busy = false;
  let idleTimer = null;
  let design = { width: 1920, height: 1080 };
  let entryPageId = "";

  const notify = (payload) => {
    try {
      if (window.chrome && chrome.webview && chrome.webview.postMessage)
        chrome.webview.postMessage(typeof payload === "string" ? payload : JSON.stringify(payload));
    } catch (_) { /* 编辑器预览没有 webview 桥 */ }
  };

  const assetUrl = (src) => {
    if (!src) return "";
    if (/^https?:\/\//i.test(src)) return src;
    return projectBase + "/" + String(src).replace(/^\//, "");
  };

  const fetchJson = async (path) => {
    const res = await fetch(projectBase + "/" + path.replace(/^\//, ""));
    if (!res.ok) throw new Error("读取失败 " + path);
    return res.json();
  };

  const layout = () => {
    if (!manifest) return;
    const sx = window.innerWidth / design.width;
    const sy = window.innerHeight / design.height;
    stage.style.width = design.width + "px";
    stage.style.height = design.height + "px";
    stage.style.transform = `scale(${sx}, ${sy})`;
  };

  const resetIdle = () => {
    clearTimeout(idleTimer);
    idleTimer = null;
    if (!currentId) return;
    const page = pageCache.get(currentId);
    if (!page || !page.idle || !page.idle.seconds) return;
    const sec = Number(page.idle.seconds) || 0;
    if (sec <= 0 || !page.idle.pageId) return;
    idleTimer = setTimeout(() => {
      gotoPage(page.idle.pageId, page.idle.transition);
    }, sec * 1000);
  };

  const stopVideos = (root) => {
    if (!root) return;
    root.querySelectorAll("video").forEach((v) => {
      try {
        v.pause();
        v.removeAttribute("src");
        v.querySelectorAll("source").forEach((s) => s.remove());
        v.load();
      } catch (_) { /* ignore */ }
    });
  };

  const lockMedia = (el) => {
    el.draggable = false;
    el.setAttribute("draggable", "false");
  };

  const missingBox = () => {
    const wrap = document.createElement("div");
    wrap.className = "missing";
    const img = document.createElement("img");
    lockMedia(img);
    img.src = placeholder;
    img.alt = "";
    const label = document.createElement("div");
    label.className = "label";
    label.textContent = "缺资源";
    wrap.append(img, label);
    return wrap;
  };

  const applyCommon = (el, comp) => {
    el.classList.add("comp");
    el.style.left = (comp.x || 0) + "px";
    el.style.top = (comp.y || 0) + "px";
    el.style.width = (comp.w || 0) + "px";
    el.style.height = (comp.h || 0) + "px";
    el.style.zIndex = String(comp.z ?? 1);
    el.style.visibility = comp.visible === false ? "hidden" : "visible";
    const rot = Number(comp.rotation) || 0;
    if (rot) el.style.transform = `rotate(${rot}deg)`;
    if (comp.onClick && (comp.onClick.action === "gotoPage" || comp.onClick.action === "gotoHome")) {
      el.style.cursor = cursorVisible ? "pointer" : "none";
      el.addEventListener("click", (ev) => {
        ev.stopPropagation();
        runAction(comp.onClick);
      });
    }
  };

  const runAction = (action) => {
    if (!action) return;
    if (action.action === "gotoHome") {
      gotoPage(entryPageId, action.transition);
      return;
    }
    if (action.action === "gotoPage" && action.pageId)
      gotoPage(action.pageId, action.transition);
  };

  const fitStyle = (fit) => {
    if (fit === "contain") return "contain";
    if (fit === "cover") return "cover";
    if (fit === "none") return "none";
    return "fill";
  };

  const wireVideo = (video, props, onEndedAction) => {
    const list = Array.isArray(props.src) ? props.src.filter(Boolean) : (props.src ? [props.src] : []);
    const urls = list.map(assetUrl);
    let index = 0;
    const loopMode = props.loopMode || "single";
    const interval = Number(props.intervalSeconds) || 0;
    video.muted = !!props.muted;
    video.autoplay = props.autoplay !== false;
    video.playsInline = true;
    video.controls = false;
    video.setAttribute("playsinline", "");
    video.style.objectFit = "fill";
    video.style.width = "100%";
    video.style.height = "100%";

    const playAt = (i) => {
      if (!urls.length) return;
      index = ((i % urls.length) + urls.length) % urls.length;
      video.src = urls[index];
      const p = video.play();
      if (p && p.catch) p.catch(() => {});
    };

    video.addEventListener("error", () => {
      notify({ type: "missing-asset", src: urls[index] || "" });
      const ph = missingBox();
      video.replaceWith(ph);
    });

    video.addEventListener("ended", () => {
      if (loopMode === "once") {
        runAction(onEndedAction || props.onEnded);
        return;
      }
      if (loopMode === "single") {
        playAt(index);
        return;
      }
      if (loopMode === "list") {
        playAt(index + 1);
        return;
      }
      if (loopMode === "interval") {
        setTimeout(() => playAt(index), Math.max(0, interval) * 1000);
      }
    });

    if (urls.length) playAt(0);
    else {
      notify({ type: "missing-asset", src: "" });
      return missingBox();
    }
    return video;
  };

  const renderBackground = (pageEl, bg) => {
    if (!bg || !bg.type) {
      pageEl.style.background = "#000";
      return;
    }
    if (bg.type === "color") {
      pageEl.style.background = bg.value || "#000";
      return;
    }
    if (bg.type === "image") {
      const img = document.createElement("img");
      lockMedia(img);
      img.src = assetUrl(bg.src);
      img.alt = "";
      img.style.cssText = "position:absolute;inset:0;width:100%;height:100%;";
      img.style.objectFit = fitStyle(bg.fit);
      img.onerror = () => {
        notify({ type: "missing-asset", src: bg.src });
        img.replaceWith(missingBox());
      };
      pageEl.appendChild(img);
      return;
    }
    if (bg.type === "video") {
      const video = document.createElement("video");
      video.style.cssText = "position:absolute;inset:0;width:100%;height:100%;pointer-events:none;";
      const node = wireVideo(video, bg, null);
      pageEl.appendChild(node);
    }
  };

  const renderComponent = (comp) => {
    if (!comp || !comp.type) return null;
    const el = document.createElement("div");
    applyCommon(el, comp);
    const props = comp.props || {};

    if (comp.type === "image") {
      const img = document.createElement("img");
      lockMedia(img);
      img.alt = "";
      img.src = assetUrl(props.src);
      img.style.objectFit = fitStyle(props.fit);
      img.onerror = () => {
        notify({ type: "missing-asset", src: props.src });
        img.replaceWith(missingBox());
      };
      el.appendChild(img);
      return el;
    }

    if (comp.type === "text") {
      el.classList.add("text");
      el.textContent = props.text || "";
      el.style.fontFamily = props.fontFamily || "Microsoft YaHei";
      el.style.fontSize = (props.fontSize || 16) + "px";
      el.style.color = props.color || "#FFFFFF";
      el.style.fontWeight = String(props.fontWeight || 400);
      el.style.fontStyle = props.italic ? "italic" : "normal";
      el.style.textAlign = props.align || "left";
      el.style.lineHeight = String(props.lineHeight || 1.4);
      el.style.letterSpacing = (props.letterSpacing || 0) + "px";
      const valign = props.valign || "middle";
      el.style.display = "flex";
      el.style.flexDirection = "column";
      el.style.justifyContent = valign === "top" ? "flex-start" : valign === "bottom" ? "flex-end" : "center";
      return el;
    }

    if (comp.type === "video") {
      const video = document.createElement("video");
      const node = wireVideo(video, props, comp.onEnded);
      el.appendChild(node);
      return el;
    }

    if (comp.type === "button") {
      const btn = document.createElement("div");
      btn.className = "btn";
      const up = assetUrl(props.srcUp) || placeholder;
      const down = assetUrl(props.srcDown) || up;
      btn.style.backgroundImage = `url("${up}")`;
      btn.addEventListener("pointerdown", () => { btn.style.backgroundImage = `url("${down}")`; });
      btn.addEventListener("pointerup", () => { btn.style.backgroundImage = `url("${up}")`; });
      btn.addEventListener("pointerleave", () => { btn.style.backgroundImage = `url("${up}")`; });
      const probe = new Image();
      probe.onerror = () => {
        notify({ type: "missing-asset", src: props.srcUp });
        btn.style.backgroundImage = `url("${placeholder}")`;
      };
      probe.src = up;
      el.appendChild(btn);
      return el;
    }

    return null;
  };

  const buildPage = async (pageId) => {
    let page = pageCache.get(pageId);
    if (!page) {
      page = await fetchJson("pages/" + pageId + ".json");
      pageCache.set(pageId, page);
    }
    const el = document.createElement("div");
    el.className = "page";
    el.dataset.pageId = pageId;
    renderBackground(el, page.background);
    const comps = Array.isArray(page.components) ? page.components : [];
    comps
      .slice()
      .sort((a, b) => (a.z || 0) - (b.z || 0))
      .forEach((c) => {
        try {
          const node = renderComponent(c);
          if (node) el.appendChild(node);
        } catch (err) {
          console.warn("忽略组件", c && c.type, err);
        }
      });
    return { page, el };
  };

  const preloadNeighbor = async (pageId) => {
    const pages = manifest.pages || [];
    const i = pages.indexOf(pageId);
    const ids = [pages[i - 1], pages[i + 1]].filter(Boolean);
    for (const id of ids) {
      if (pageCache.has(id)) continue;
      try {
        const p = await fetchJson("pages/" + id + ".json");
        pageCache.set(id, p);
        const comps = p.components || [];
        comps.forEach((c) => {
          const src = c.props && (c.props.src || c.props.srcUp);
          const first = Array.isArray(src) ? src[0] : src;
          if (c.type === "image" && first) {
            const img = new Image();
            img.src = assetUrl(first);
          }
        });
      } catch (err) {
        console.warn("预载失败", id, err);
      }
    }
  };

  const waitFirstPaint = (el) => new Promise((resolve) => {
    const imgs = [...el.querySelectorAll("img")];
    const videos = [...el.querySelectorAll("video")];
    if (!imgs.length && !videos.length) {
      resolve();
      return;
    }
    let left = imgs.length + videos.length;
    const done = () => { if (--left <= 0) resolve(); };
    const timer = setTimeout(resolve, 8000);
    imgs.forEach((img) => {
      if (img.complete) done();
      else {
        img.addEventListener("load", done, { once: true });
        img.addEventListener("error", done, { once: true });
      }
    });
    videos.forEach((v) => {
      if (v.readyState >= 2) done();
      else {
        v.addEventListener("loadeddata", done, { once: true });
        v.addEventListener("error", done, { once: true });
      }
    });
    Promise.resolve().then(() => {
      if (left <= 0) clearTimeout(timer);
    });
  });

  const transitionOf = (requested, page) => {
    const t = requested || (page && page.defaultTransition) || { type: "none", dur: 0 };
    return { type: t.type || "none", dur: Number(t.dur) || 0 };
  };

  const animate = (fromEl, toEl, type, durSec) => new Promise((resolve) => {
    const ms = Math.max(0, durSec) * 1000;
    if (!fromEl || type === "none" || ms === 0) {
      resolve();
      return;
    }
    toEl.style.transition = "none";
    fromEl.style.transition = "none";
    if (type === "fade") {
      toEl.style.opacity = "0";
    } else if (type === "slideLeft") {
      toEl.style.transform = "translateX(100%)";
    } else if (type === "slideRight") {
      toEl.style.transform = "translateX(-100%)";
    } else if (type === "slideUp") {
      toEl.style.transform = "translateY(100%)";
    }
    void toEl.offsetWidth;
    const ease = `transform ${ms}ms ease, opacity ${ms}ms ease`;
    toEl.style.transition = ease;
    fromEl.style.transition = ease;
    toEl.style.opacity = "1";
    toEl.style.transform = "translate(0,0)";
    if (type === "fade") fromEl.style.opacity = "0";
    if (type === "slideLeft") fromEl.style.transform = "translateX(-100%)";
    if (type === "slideRight") fromEl.style.transform = "translateX(100%)";
    if (type === "slideUp") fromEl.style.transform = "translateY(-100%)";
    setTimeout(resolve, ms + 16);
  });

  async function gotoPage(pageId, transition) {
    if (!pageId || busy) return;
    if (!manifest.pages || !manifest.pages.includes(pageId)) return;
    if (pageId === currentId && currentEl) return;
    busy = true;
    try {
      const built = await buildPage(pageId);
      const t = transitionOf(transition, built.page);
      stage.appendChild(built.el);
      if (currentEl) await animate(currentEl, built.el, t.type, t.dur);
      if (currentEl) {
        stopVideos(currentEl);
        currentEl.remove();
      }
      currentEl = built.el;
      currentId = pageId;
      resetIdle();
      preloadNeighbor(pageId);
    } catch (err) {
      console.error(err);
    } finally {
      busy = false;
    }
  }

  const loadFonts = async () => {
    const fonts = (project && project.fonts) || [];
    for (const f of fonts) {
      if (!f || !f.family || !f.src) continue;
      try {
        const face = new FontFace(f.family, `url(${assetUrl(f.src)})`);
        await face.load();
        document.fonts.add(face);
        fontFaces.push(face);
      } catch (err) {
        console.warn("字体失败", f.family, err);
      }
    }
  };

  const bootApp = async () => {
    try {
      manifest = await fetchJson("manifest.json");
      try { project = await fetchJson("project.json"); }
      catch { project = { fonts: [] }; }
      design = {
        width: (manifest.design && manifest.design.width) || 1920,
        height: (manifest.design && manifest.design.height) || 1080
      };
      const pages = manifest.pages || [];
      entryPageId = manifest.entryPageId && pages.includes(manifest.entryPageId)
        ? manifest.entryPageId
        : (pages[0] || "");
      layout();
      await loadFonts();
      if (!entryPageId) throw new Error("没有页面");
      const first = await buildPage(entryPageId);
      await waitFirstPaint(first.el);
      stage.appendChild(first.el);
      currentEl = first.el;
      currentId = entryPageId;
      resetIdle();
      preloadNeighbor(entryPageId);
      boot.classList.add("hidden");
      notify("ready");
    } catch (err) {
      console.error(err);
      boot.textContent = "工程无法打开";
      notify({ type: "error", message: String(err) });
    }
  };

  window.addEventListener("resize", layout);
  window.addEventListener("pointerdown", resetIdle, true);
  window.addEventListener("touchstart", resetIdle, { capture: true, passive: true });
  document.addEventListener("contextmenu", (e) => e.preventDefault());
  document.addEventListener("dragstart", (e) => e.preventDefault(), true);

  if (trial) {
    const mark = document.createElement("div");
    mark.textContent = "试用";
    mark.style.cssText = "position:fixed;right:24px;top:24px;z-index:10000;background:#b00000;color:#fff;font-size:48px;font-weight:700;padding:8px 28px;pointer-events:none;";
    document.body.appendChild(mark);
  }

  bootApp();
})();

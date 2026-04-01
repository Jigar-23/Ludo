(function () {
  const container = document.getElementById("unity-container");
  const canvas = document.getElementById("unity-canvas");
  const loadingOverlay = document.getElementById("unity-loading");
  const progressBar = document.getElementById("progress-bar");
  const statusText = document.getElementById("loading-status");
  const errorBanner = document.getElementById("unity-error");
  const fullscreenButton = document.getElementById("fullscreen-button");

  if (!container || !canvas) {
    return;
  }

  const ua = navigator.userAgent || "";
  const isIos =
    /iPad|iPhone|iPod/.test(ua) ||
    (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
  const isSafari =
    /Safari/i.test(ua) && !/CriOS|FxiOS|EdgiOS|Chrome|Android/i.test(ua);
  const isTouchDevice =
    "ontouchstart" in window || navigator.maxTouchPoints > 0 || navigator.msMaxTouchPoints > 0;
  const isIosSafari = isIos && isSafari;
  const isStandalone =
    window.matchMedia("(display-mode: standalone)").matches || window.navigator.standalone === true;

  if (isIosSafari && !isStandalone) {
    document.body.classList.add("ios-browser");
  }

  let targetProgress = 0;
  let displayedProgress = 0;
  let rafId = 0;
  let bannerTimeout = 0;

  function updateViewportCssVars() {
    const viewport = window.visualViewport;
    const width = viewport ? viewport.width : window.innerWidth;
    const height = viewport ? viewport.height : window.innerHeight;
    document.documentElement.style.setProperty("--app-width", `${Math.round(width)}px`);
    document.documentElement.style.setProperty("--app-height", `${Math.round(height)}px`);
  }

  updateViewportCssVars();
  window.addEventListener("resize", updateViewportCssVars);
  if (window.visualViewport) {
    window.visualViewport.addEventListener("resize", updateViewportCssVars);
    window.visualViewport.addEventListener("scroll", updateViewportCssVars);
  }

  function setStatus(message) {
    if (statusText) {
      statusText.textContent = message;
    }
  }

  function showBanner(message, kind) {
    if (!errorBanner) {
      return;
    }

    if (bannerTimeout) {
      window.clearTimeout(bannerTimeout);
      bannerTimeout = 0;
    }

    if (kind) {
      errorBanner.dataset.kind = kind;
    } else {
      delete errorBanner.dataset.kind;
    }

    errorBanner.hidden = false;
    errorBanner.textContent = message;
  }

  function setError(message) {
    showBanner(message, "error");
  }

  function setInfo(message) {
    showBanner(message, "info");
    bannerTimeout = window.setTimeout(() => {
      if (errorBanner) {
        errorBanner.hidden = true;
      }
    }, 5000);
  }

  function hideLoading() {
    if (loadingOverlay) {
      loadingOverlay.style.display = "none";
    }
  }

  function animateProgress() {
    displayedProgress += (targetProgress - displayedProgress) * 0.16;
    if (Math.abs(displayedProgress - targetProgress) < 0.002) {
      displayedProgress = targetProgress;
    }

    if (progressBar) {
      progressBar.style.width = `${Math.max(0, Math.min(100, displayedProgress * 100))}%`;
    }

    if (displayedProgress < 0.999) {
      rafId = window.requestAnimationFrame(animateProgress);
    } else if (progressBar) {
      progressBar.style.width = "100%";
    }
  }

  function updateProgress(value) {
    targetProgress = Math.max(targetProgress, Math.min(1, value || 0));
    if (!rafId) {
      rafId = window.requestAnimationFrame(animateProgress);
    }
  }

  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = src;
      script.async = true;
      script.onload = resolve;
      script.onerror = () => reject(new Error(`Failed to load ${src}`));
      document.body.appendChild(script);
    });
  }

  function getDevicePixelRatio() {
    const dpr = window.devicePixelRatio || 1;
    if (isIosSafari) {
      return Math.min(3, Math.max(2, dpr));
    }

    if (isTouchDevice) {
      return Math.min(2, dpr);
    }

    return Math.min(2, dpr);
  }

  function getConfig(manifest) {
    const base = "/ludo/unity/";
    const streamingAssetsPath =
      manifest.streamingAssetsUrl === undefined || manifest.streamingAssetsUrl === null
        ? "StreamingAssets"
        : manifest.streamingAssetsUrl;

    return {
      dataUrl: base + manifest.dataUrl,
      frameworkUrl: base + manifest.frameworkUrl,
      codeUrl: base + manifest.codeUrl,
      streamingAssetsUrl: base + streamingAssetsPath,
      companyName: manifest.companyName || "Jigar",
      productName: manifest.productName || "Premium Ludo",
      productVersion: manifest.productVersion || "1.0.0",
      devicePixelRatio: getDevicePixelRatio(),
      matchWebGLToCanvasSize: true,
      cacheControl(url) {
        if (/\.data|\.bundle|\.wasm|\.br|\.gz$/i.test(url)) {
          return "immutable";
        }

        return "no-store";
      },
    };
  }

  function setupFullscreenButton(instance) {
    if (!fullscreenButton) {
      return;
    }

    const canUseNativeFullscreen =
      typeof container.requestFullscreen === "function" &&
      document.fullscreenEnabled === true &&
      !isIosSafari;

    if (isStandalone) {
      fullscreenButton.hidden = true;
      return;
    }

    if (canUseNativeFullscreen) {
      fullscreenButton.textContent = "Fullscreen";
      fullscreenButton.addEventListener("click", () => {
        instance.SetFullscreen(1);
      });
      return;
    }

    if (isIosSafari) {
      fullscreenButton.textContent = "Add to Home";
      fullscreenButton.addEventListener("click", () => {
        setInfo(
          "iPhone Safari does not reliably allow game fullscreen. Use Share -> Add to Home Screen for edge-to-edge play."
        );
      });
      return;
    }

    fullscreenButton.hidden = true;
  }

  async function boot() {
    try {
      setStatus("Loading build manifest...");
      const response = await fetch("/ludo/unity/build-manifest.json", { cache: "no-store" });
      if (!response.ok) {
        throw new Error("Unity WebGL build files are missing. Export the WebGL build first.");
      }

      const manifest = await response.json();
      if (!manifest.loaderUrl || !manifest.dataUrl || !manifest.frameworkUrl || !manifest.codeUrl) {
        throw new Error("Unity build manifest is incomplete.");
      }

      setStatus("Loading Unity engine...");
      await loadScript("/ludo/unity/" + manifest.loaderUrl);

      if (typeof createUnityInstance !== "function") {
        throw new Error("Unity loader did not initialize correctly.");
      }

      setStatus("Starting game...");
      const instance = await createUnityInstance(canvas, getConfig(manifest), (progress) => {
        updateProgress(progress * 0.9);
        if (progress < 0.2) {
          setStatus("Preparing files...");
        } else if (progress < 0.6) {
          setStatus("Loading game data...");
        } else if (progress < 0.95) {
          setStatus("Warming up the board...");
        } else {
          setStatus("Almost ready...");
        }
      });

      updateProgress(1);
      setTimeout(hideLoading, 120);

      setupFullscreenButton(instance);

      canvas.addEventListener(
        "webglcontextlost",
        (event) => {
          event.preventDefault();
          setError("The WebGL context was lost. Refresh the page to restart the game.");
        },
        false
      );
    } catch (error) {
      setError(error && error.message ? error.message : "Failed to load the game.");
      setStatus("Load failed");
    }
  }

  boot();
})();

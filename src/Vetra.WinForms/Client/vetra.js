(() => {
  if (globalThis.vetra && globalThis.vetra.__vetraOwned) {
    return;
  }

  const pending = new Map();
  const listeners = new Map();
  let requestCounter = 0;

  function nextId() {
    requestCounter += 1;
    return `vetra-${Date.now()}-${requestCounter}`;
  }

  function ensureWebView() {
    if (!window.chrome || !window.chrome.webview) {
      throw createError("not_initialized", "Vetra WebView messaging is not available.");
    }
  }

  function createError(code, message, details) {
    const error = new Error(message || code || "Vetra error");
    error.code = code || "command_failed";
    error.details = details;
    return error;
  }

  function post(message) {
    ensureWebView();
    window.chrome.webview.postMessage(message);
  }

  function invoke(name, ...args) {
    let options = null;
    if (
      args.length > 0 &&
      args[args.length - 1] &&
      typeof args[args.length - 1] === "object" &&
      Object.prototype.hasOwnProperty.call(args[args.length - 1], "__vetraOptions")
    ) {
      options = args.pop().__vetraOptions;
    }

    const timeoutMs =
      options && typeof options.timeoutMs === "number" ? options.timeoutMs : undefined;

    return new Promise((resolve, reject) => {
      ensureWebView();
      const id = nextId();
      let timer = null;

      const settle = (fn, value) => {
        if (timer) {
          clearTimeout(timer);
        }
        pending.delete(id);
        fn(value);
      };

      pending.set(id, {
        resolve: (value) => settle(resolve, value),
        reject: (error) => settle(reject, error)
      });

      if (typeof timeoutMs === "number" && timeoutMs > 0) {
        timer = setTimeout(() => {
          settle(reject, createError("timeout", `Invoke '${name}' timed out after ${timeoutMs}ms.`));
        }, timeoutMs);
      }

      post({
        v: 1,
        type: "invoke",
        id,
        name,
        args
      });
    });
  }

  function on(eventName, handler) {
    if (typeof eventName !== "string" || !eventName) {
      throw createError("invalid_arguments", "Event name must be a non-empty string.");
    }
    if (typeof handler !== "function") {
      throw createError("invalid_arguments", "Event handler must be a function.");
    }

    let set = listeners.get(eventName);
    if (!set) {
      set = new Set();
      listeners.set(eventName, set);
    }
    set.add(handler);

    return () => {
      const current = listeners.get(eventName);
      if (!current) {
        return;
      }
      current.delete(handler);
      if (current.size === 0) {
        listeners.delete(eventName);
      }
    };
  }

  function off(eventName, handler) {
    const set = listeners.get(eventName);
    if (!set) {
      return;
    }
    if (handler) {
      set.delete(handler);
      if (set.size === 0) {
        listeners.delete(eventName);
      }
    } else {
      listeners.delete(eventName);
    }
  }

  function handleHostMessage(event) {
    const message = event && event.data;
    if (!message || typeof message !== "object") {
      return;
    }

    if (message.type === "result" || message.type === "error") {
      const entry = pending.get(message.id);
      if (!entry) {
        return;
      }
      if (message.type === "result") {
        entry.resolve(message.data);
      } else {
        const err = (message.error) || {};
        entry.reject(createError(err.code, err.message, err.details));
      }
      return;
    }

    if (message.type === "event" && message.name) {
      const set = listeners.get(message.name);
      if (!set) {
        return;
      }
      for (const handler of Array.from(set)) {
        try {
          handler(message.data);
        } catch (err) {
          console.error("Vetra event handler failed:", err);
        }
      }
    }
  }

  ensureWebView();
  window.chrome.webview.addEventListener("message", handleHostMessage);

  const api = {
    __vetraOwned: true,
    invoke,
    on,
    off,
    window: {
      minimize: () => invoke("vetra.window.minimize"),
      maximize: () => invoke("vetra.window.maximize"),
      restore: () => invoke("vetra.window.restore"),
      close: () => invoke("vetra.window.close"),
      getState: () => invoke("vetra.window.getState"),
      getTitle: () => invoke("vetra.window.getTitle"),
      setTitle: (title) => invoke("vetra.window.setTitle", title),
      setAlwaysOnTop: (enabled) => invoke("vetra.window.setAlwaysOnTop", !!enabled)
    },
    dialogs: {
      openFolder: () => invoke("vetra.dialogs.openFolder"),
      openFile: (options) => invoke("vetra.dialogs.openFile", options || {}),
      openFiles: (options) => invoke("vetra.dialogs.openFiles", options || {}),
      saveFile: (options) => invoke("vetra.dialogs.saveFile", options || {}),
      showMessage: (options) => invoke("vetra.dialogs.showMessage", options || {})
    },
    storage: {
      get: (key, defaultValue) => invoke("vetra.storage.get", key, defaultValue === undefined ? null : defaultValue),
      set: (key, value) => invoke("vetra.storage.set", key, value),
      remove: (key) => invoke("vetra.storage.remove", key),
      clear: () => invoke("vetra.storage.clear")
    },
    app: {
      getInfo: () => invoke("vetra.app.getInfo"),
      getCapabilities: () => invoke("vetra.app.getCapabilities")
    },
    notifications: {
      show: (options) => invoke("vetra.notifications.show", options || {})
    },
    tray: {
      configure: (options) => invoke("vetra.tray.configure", options || {}),
      setVisible: (visible) => invoke("vetra.tray.setVisible", !!visible),
      setTooltip: (text) => invoke("vetra.tray.setTooltip", text || ""),
      dispose: () => invoke("vetra.tray.dispose")
    }
  };

  Object.defineProperty(globalThis, "vetra", {
    value: api,
    writable: false,
    configurable: false,
    enumerable: true
  });
})();

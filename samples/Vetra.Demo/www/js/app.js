(() => {
  const output = document.getElementById("output");
  const eventLog = document.getElementById("eventLog");

  function show(value, isError) {
    output.className = "log " + (isError ? "err" : "ok");
    output.textContent = typeof value === "string" ? value : JSON.stringify(value, null, 2);
  }

  async function run(label, action) {
    try {
      if (!window.vetra) {
        throw new Error("window.vetra is not available. The host injects it automatically.");
      }
      const result = await action();
      show({ label, result }, false);
    } catch (err) {
      show({
        label,
        error: {
          code: err.code || "error",
          message: err.message || String(err),
          details: err.details || null
        }
      }, true);
    }
  }

  document.getElementById("btnGreeting").addEventListener("click", () =>
    run("greeting", () => vetra.invoke("demo.getGreeting", document.getElementById("greetingName").value)));

  document.getElementById("btnTime").addEventListener("click", () =>
    run("time", () => vetra.invoke("demo.getTime")));

  document.querySelectorAll("[data-window]").forEach((button) => {
    button.addEventListener("click", () => {
      const action = button.getAttribute("data-window");
      run("window." + action, () => vetra.window[action]());
    });
  });

  document.getElementById("btnWindowState").addEventListener("click", () =>
    run("window.state", () => vetra.window.getState()));

  document.getElementById("btnFolder").addEventListener("click", () =>
    run("dialogs.openFolder", () => vetra.dialogs.openFolder()));

  document.getElementById("btnFile").addEventListener("click", () =>
    run("dialogs.openFile", () => vetra.dialogs.openFile({
      filters: [{ name: "Images", extensions: ["png", "jpg", "jpeg"] }]
    })));

  document.getElementById("btnSave").addEventListener("click", () =>
    run("dialogs.saveFile", () => vetra.dialogs.saveFile({
      filters: [{ name: "Text", extensions: ["txt"] }],
      defaultFileName: "notes.txt"
    })));

  document.getElementById("btnMessage").addEventListener("click", () =>
    run("dialogs.showMessage", () => vetra.dialogs.showMessage({
      title: "Vetra",
      message: "Native message dialog from JavaScript.",
      buttons: "ok",
      icon: "information"
    })));

  document.getElementById("btnStorageSet").addEventListener("click", () =>
    run("storage.set", () => vetra.storage.set("theme", document.getElementById("themeValue").value)));

  document.getElementById("btnStorageGet").addEventListener("click", () =>
    run("storage.get", () => vetra.storage.get("theme", "light")));

  document.getElementById("btnStorageRemove").addEventListener("click", () =>
    run("storage.remove", () => vetra.storage.remove("theme")));

  document.getElementById("btnInfo").addEventListener("click", () =>
    run("app.getInfo", () => vetra.app.getInfo()));

  document.getElementById("btnCaps").addEventListener("click", () =>
    run("app.getCapabilities", () => vetra.app.getCapabilities()));

  document.getElementById("btnNotify").addEventListener("click", () =>
    run("notifications.show", () => vetra.notifications.show({
      title: "Task Complete",
      message: "Your Vetra demo notification finished."
    })));

  document.getElementById("btnTray").addEventListener("click", () =>
    run("tray.configure", () => vetra.tray.configure({
      tooltip: "Vetra Demo",
      visible: true,
      includeShowHide: true,
      menuItems: [
        { id: "about", text: "About" },
        { id: "-", separator: true },
        { id: "quit", text: "Quit hint" }
      ]
    })));

  function waitForVetra() {
    if (!window.vetra) {
      setTimeout(waitForVetra, 50);
      return;
    }

    vetra.on("demo.heartbeat", (data) => {
      eventLog.textContent = JSON.stringify(data, null, 2);
    });

    vetra.on("window.stateChanged", (data) => {
      eventLog.textContent = "window.stateChanged\n" + JSON.stringify(data, null, 2);
    });

    vetra.on("tray.menuClicked", (data) => {
      show({ event: "tray.menuClicked", data }, false);
    });

    run("startup.capabilities", () => vetra.app.getCapabilities());
  }

  waitForVetra();
})();

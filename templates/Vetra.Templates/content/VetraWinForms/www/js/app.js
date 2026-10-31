(() => {
  const out = document.getElementById("out");

  function show(value) {
    out.textContent = typeof value === "string" ? value : JSON.stringify(value, null, 2);
  }

  async function whenReady(fn) {
    if (!window.vetra) {
      setTimeout(() => whenReady(fn), 40);
      return;
    }
    fn();
  }

  whenReady(() => {
    document.getElementById("greet").addEventListener("click", async () => {
      try {
        const name = document.getElementById("name").value;
        show(await vetra.invoke("app.getGreeting", name));
      } catch (err) {
        show({ error: err.message, code: err.code });
      }
    });

    document.getElementById("folder").addEventListener("click", async () => {
      try {
        show({ folder: await vetra.dialogs.openFolder() });
      } catch (err) {
        show({ error: err.message, code: err.code });
      }
    });
  });
})();

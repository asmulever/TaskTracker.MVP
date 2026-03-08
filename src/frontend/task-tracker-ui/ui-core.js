window.TaskTrackerUi = window.TaskTrackerUi || {};

(() => {
  const STATUS = ["Todo", "Doing", "Done"];
  const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  const STATUS_ALIASES = {
    Created: "Todo",
    Planned: "Todo",
    InProgress: "Doing",
    Blocked: "Doing",
    Done: "Done",
    Archived: "Done",
    Todo: "Todo",
    Doing: "Doing",
    Completed: "Done"
  };
  const STATUS_LABELS = {
    Todo: "Todo",
    Doing: "Doing",
    Done: "Done"
  };
  const STATUS_NOTES = {
    Todo: "Pendiente de arranque o definicion",
    Doing: "Trabajo activo en curso",
    Done: "Cierre y validacion final"
  };
  const PRIORITY = ["Low", "Medium", "High", "Critical"];
  const ALLOWED_TRANSITIONS = {
    Todo: ["Doing"],
    Doing: ["Done"],
    Done: []
  };
  const STATUS_ORDER = {
    Todo: 0,
    Doing: 1,
    Done: 2
  };
  const THEME_COOKIE = "task_theme";
  const MAX_COMMENT_IMAGE_SIZE = 2 * 1024 * 1024;

  function getCookieValue(name) {
    const regex = new RegExp(`(?:^|; )${name}=([^;]*)`);
    const match = document.cookie.match(regex);
    return match ? decodeURIComponent(match[1]) : null;
  }

  function setCookie(name, value, days = 365) {
    const maxAge = days * 24 * 60 * 60;
    document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAge}; samesite=lax`;
  }

  function getInitialTheme() {
    const saved = getCookieValue(THEME_COOKIE);
    if (saved === "light" || saved === "dark") return saved;
    const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    return prefersDark ? "dark" : "light";
  }

  function toast(message, isOk = true) {
    Toastify({
      text: message,
      duration: 2600,
      gravity: "top",
      position: "right",
      close: true,
      style: { background: isOk ? "#0f766e" : "#b91c1c", color: "#ffffff" }
    }).showToast();
  }

  function formatDate(dateValue) {
    if (!dateValue) return "Sin fecha";
    const date = new Date(dateValue);
    if (Number.isNaN(date.getTime())) return "Sin fecha";
    return date.toLocaleDateString("es-CL");
  }

  function formatDateTime(dateValue) {
    if (!dateValue) return "Sin fecha";
    const date = new Date(dateValue);
    if (Number.isNaN(date.getTime())) return "Sin fecha";
    return date.toLocaleString("es-CL");
  }

  function normalizeStatus(status) {
    if (typeof status !== "string" || status.length === 0) return "Todo";
    const aliased = STATUS_ALIASES[status] || status;
    if (STATUS.includes(aliased)) return aliased;
    if (STATUS.includes(status)) return status;
    if (status === "Created" || status === "Planned") return "Todo";
    if (status === "InProgress" || status === "Blocked") return "Doing";
    if (status === "Archived") return "Done";
    return "Todo";
  }

  function parseLabels(labelsText) {
    if (!labelsText) return [];
    return labelsText
      .split(",")
      .map((label) => label.trim())
      .filter((label) => label.length > 0);
  }

  function getTodayInputDate() {
    const now = new Date();
    const offsetMs = now.getTimezoneOffset() * 60 * 1000;
    return new Date(now.getTime() - offsetMs).toISOString().slice(0, 10);
  }

  function parseDateValue(dateValue) {
    if (!dateValue) return null;
    const date = new Date(dateValue);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  function startOfDay(date) {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }

  function endOfDay(date) {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999);
  }

  function emptyForm() {
    return {
      id: null,
      title: "",
      description: "",
      priority: "Medium",
      targetStartDate: getTodayInputDate(),
      targetDueDate: "",
      labelsText: "",
      status: "Todo"
    };
  }

  function canMoveTo(current, next) {
    if (current === next) return true;
    return (ALLOWED_TRANSITIONS[current] || []).includes(next);
  }

  function isBackwardTransition(current, next) {
    if (!(current in STATUS_ORDER) || !(next in STATUS_ORDER)) return false;
    return STATUS_ORDER[next] < STATUS_ORDER[current];
  }

  function getBackwardTransitionWarning(current, next) {
    if (!isBackwardTransition(current, next)) return null;
    return "Esta accion no es recomendada por consistencia de datos y puede romper el historial de tareas.";
  }

  function normalizeId(id) {
    return typeof id === "string" ? id.trim() : String(id ?? "").trim();
  }

  function isValidTaskId(id) {
    return UUID_REGEX.test(normalizeId(id));
  }

  function nextStatus(current) {
    const next = ALLOWED_TRANSITIONS[current] || [];
    return next.length > 0 ? next[0] : null;
  }

  function runCardAction(event, action) {
    event.preventDefault();
    event.stopPropagation();
    action();
  }

  function readFileAsDataUrl(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(typeof reader.result === "string" ? reader.result : "");
      reader.onerror = () => reject(new Error("No se pudo leer la imagen seleccionada."));
      reader.readAsDataURL(file);
    });
  }

  function formatFileSize(bytes) {
    if (!Number.isFinite(bytes) || bytes <= 0) return "0 KB";
    if (bytes < 1024 * 1024) {
      return `${Math.max(1, Math.round(bytes / 1024))} KB`;
    }

    const mb = bytes / (1024 * 1024);
    const formatted = mb >= 10 ? mb.toFixed(0) : mb.toFixed(1).replace(/\.0$/, "");
    return `${formatted} MB`;
  }

  const COMMENT_IMAGE_SIZE_LABEL = formatFileSize(MAX_COMMENT_IMAGE_SIZE);

  function getCommentImageFileName(file) {
    if (file && typeof file.name === "string" && file.name.trim().length > 0) {
      return file.name.trim();
    }

    const extension = (file?.type || "image/png").split("/")[1] || "png";
    return `imagen-${Date.now()}.${extension}`;
  }

  Object.assign(window.TaskTrackerUi, {
    STATUS,
    STATUS_LABELS,
    STATUS_NOTES,
    PRIORITY,
    THEME_COOKIE,
    MAX_COMMENT_IMAGE_SIZE,
    COMMENT_IMAGE_SIZE_LABEL,
    getCookieValue,
    setCookie,
    getInitialTheme,
    toast,
    formatDate,
    formatDateTime,
    formatFileSize,
    normalizeStatus,
    parseLabels,
    getTodayInputDate,
    parseDateValue,
    startOfDay,
    endOfDay,
    emptyForm,
    canMoveTo,
    isBackwardTransition,
    getBackwardTransitionWarning,
    normalizeId,
    isValidTaskId,
    nextStatus,
    runCardAction,
    readFileAsDataUrl,
    getCommentImageFileName
  });
})();

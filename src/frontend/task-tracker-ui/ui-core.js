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

  /**
   * Recupera el valor de una cookie por nombre.
   * @param {string} name Nombre de la cookie a consultar.
   * @returns {string|null} El valor decodificado o null cuando no existe.
   */
  function getCookieValue(name) {
    const regex = new RegExp(`(?:^|; )${name}=([^;]*)`);
    const match = document.cookie.match(regex);
    return match ? decodeURIComponent(match[1]) : null;
  }

  /**
   * Persiste una cookie simple con expiración en días.
   * @param {string} name Nombre de la cookie.
   * @param {string} value Valor a almacenar.
   * @param {number} [days=365] Cantidad de días de vigencia.
   * @returns {void}
   */
  function setCookie(name, value, days = 365) {
    const maxAge = days * 24 * 60 * 60;
    document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAge}; samesite=lax`;
  }

  /**
   * Determina el tema inicial leyendo la cookie o la preferencia del sistema.
   * @returns {"light"|"dark"} El tema inicial que debe aplicarse.
   */
  function getInitialTheme() {
    const saved = getCookieValue(THEME_COOKIE);
    if (saved === "light" || saved === "dark") return saved;
    const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
    return prefersDark ? "dark" : "light";
  }

  /**
   * Muestra una notificación breve en pantalla.
   * @param {string} message Mensaje a mostrar.
   * @param {boolean} [isOk=true] Indica si el toast representa éxito o error.
   * @returns {void}
   */
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

  /**
   * Formatea una fecha para mostrarla en el tablero.
   * @param {string|Date|null|undefined} dateValue Fecha a formatear.
   * @returns {string} La fecha formateada o un texto de fallback.
   */
  function formatDate(dateValue) {
    if (!dateValue) return "Sin fecha";
    const date = new Date(dateValue);
    if (Number.isNaN(date.getTime())) return "Sin fecha";
    return date.toLocaleDateString("es-CL");
  }

  /**
   * Formatea una fecha con hora para los feeds de actividad y comentarios.
   * @param {string|Date|null|undefined} dateValue Fecha a formatear.
   * @returns {string} La fecha y hora formateadas o un texto de fallback.
   */
  function formatDateTime(dateValue) {
    if (!dateValue) return "Sin fecha";
    const date = new Date(dateValue);
    if (Number.isNaN(date.getTime())) return "Sin fecha";
    return date.toLocaleString("es-CL");
  }

  /**
   * Normaliza estados provenientes del backend a los tres estados visibles del tablero.
   * @param {string} status Estado original recibido.
   * @returns {string} El estado normalizado para la UI.
   */
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

  /**
   * Convierte un texto separado por comas en una colección de etiquetas limpias.
   * @param {string} labelsText Texto de etiquetas ingresado por el usuario.
   * @returns {string[]} La lista de etiquetas sin vacíos.
   */
  function parseLabels(labelsText) {
    if (!labelsText) return [];
    return labelsText
      .split(",")
      .map((label) => label.trim())
      .filter((label) => label.length > 0);
  }

  /**
   * Obtiene la fecha actual en formato apto para inputs type="date".
   * @returns {string} Fecha local actual en formato YYYY-MM-DD.
   */
  function getTodayInputDate() {
    const now = new Date();
    const offsetMs = now.getTimezoneOffset() * 60 * 1000;
    return new Date(now.getTime() - offsetMs).toISOString().slice(0, 10);
  }

  /**
   * Intenta convertir un valor a una instancia válida de Date.
   * @param {string|Date|null|undefined} dateValue Valor de fecha a interpretar.
   * @returns {Date|null} La fecha parseada o null si no es válida.
   */
  function parseDateValue(dateValue) {
    if (!dateValue) return null;
    const date = new Date(dateValue);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  /**
   * Lleva una fecha al inicio de su día local.
   * @param {Date} date Fecha base.
   * @returns {Date} La fecha ajustada al inicio del día.
   */
  function startOfDay(date) {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }

  /**
   * Lleva una fecha al final de su día local.
   * @param {Date} date Fecha base.
   * @returns {Date} La fecha ajustada al final del día.
   */
  function endOfDay(date) {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59, 999);
  }

  /**
   * Crea el estado inicial del formulario de tareas.
   * @returns {object} El modelo base para alta o edición de tareas.
   */
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

  /**
   * Determina si una transición directa entre estados está permitida.
   * @param {string} current Estado actual.
   * @param {string} next Estado de destino.
   * @returns {boolean} True cuando la transición es válida.
   */
  function canMoveTo(current, next) {
    if (current === next) return true;
    return (ALLOWED_TRANSITIONS[current] || []).includes(next);
  }

  /**
   * Indica si un cambio de estado implica un retroceso en el flujo.
   * @param {string} current Estado actual.
   * @param {string} next Estado de destino.
   * @returns {boolean} True cuando el cambio es hacia atrás.
   */
  function isBackwardTransition(current, next) {
    if (!(current in STATUS_ORDER) || !(next in STATUS_ORDER)) return false;
    return STATUS_ORDER[next] < STATUS_ORDER[current];
  }

  /**
   * Devuelve un mensaje de advertencia para transiciones hacia atrás.
   * @param {string} current Estado actual.
   * @param {string} next Estado de destino.
   * @returns {string|null} El mensaje de advertencia o null si no aplica.
   */
  function getBackwardTransitionWarning(current, next) {
    if (!isBackwardTransition(current, next)) return null;
    return "Esta accion no es recomendada por consistencia de datos y puede romper el historial de tareas.";
  }

  /**
   * Convierte un identificador potencial en un string normalizado.
   * @param {unknown} id Valor de identificador a normalizar.
   * @returns {string} El identificador convertido y recortado.
   */
  function normalizeId(id) {
    return typeof id === "string" ? id.trim() : String(id ?? "").trim();
  }

  /**
   * Valida que un identificador cumpla el formato UUID esperado.
   * @param {unknown} id Valor a validar.
   * @returns {boolean} True cuando el identificador es válido.
   */
  function isValidTaskId(id) {
    return UUID_REGEX.test(normalizeId(id));
  }

  /**
   * Obtiene el siguiente estado natural del flujo para una tarea.
   * @param {string} current Estado actual.
   * @returns {string|null} El siguiente estado o null si no existe.
   */
  function nextStatus(current) {
    const next = ALLOWED_TRANSITIONS[current] || [];
    return next.length > 0 ? next[0] : null;
  }

  /**
   * Ejecuta una acción de tarjeta evitando que el click burbujee al contenedor.
   * @param {Event} event Evento de interacción del usuario.
   * @param {Function} action Acción a ejecutar.
   * @returns {void}
   */
  function runCardAction(event, action) {
    event.preventDefault();
    event.stopPropagation();
    action();
  }

  /**
   * Lee un archivo local y lo convierte a data URL.
   * @param {File} file Archivo a leer.
   * @returns {Promise<string>} La imagen codificada como data URL.
   */
  function readFileAsDataUrl(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(typeof reader.result === "string" ? reader.result : "");
      reader.onerror = () => reject(new Error("No se pudo leer la imagen seleccionada."));
      reader.readAsDataURL(file);
    });
  }

  /**
   * Formatea un tamaño en bytes para mostrarlo en la UI.
   * @param {number} bytes Tamaño a convertir.
   * @returns {string} El tamaño expresado en KB o MB.
   */
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

  /**
   * Determina el nombre que se mostrará para una imagen adjunta.
   * @param {File|null|undefined} file Archivo de imagen opcional.
   * @returns {string} El nombre del archivo o uno generado automáticamente.
   */
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

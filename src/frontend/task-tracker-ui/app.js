const { useEffect, useMemo, useRef, useState } = React;

const {
  STATUS,
  STATUS_LABELS,
  STATUS_NOTES,
  PRIORITY,
  THEME_COOKIE,
  MAX_COMMENT_IMAGE_SIZE,
  COMMENT_IMAGE_SIZE_LABEL,
  getInitialTheme,
  setCookie,
  toast,
  formatDate,
  normalizeStatus,
  parseLabels,
  emptyForm,
  canMoveTo,
  isBackwardTransition,
  getBackwardTransitionWarning,
  normalizeId,
  isValidTaskId,
  nextStatus,
  runCardAction,
  readFileAsDataUrl,
  getCommentImageFileName,
  createApiClient,
  buildDashboardMetrics,
  getActivityFeedFromUtc,
  MetricsSection,
  MetricsHelpModal,
  ContextModal,
  TaskFormModal,
  NoticeModal
} = window.TaskTrackerUi;

/**
 * Orquesta el estado principal del tablero y coordina la interacción con la API.
 * @returns {JSX.Element} La aplicación completa del task tracker.
 */
function App() {
  const apiBase = (window.TASK_API_URL || window.location.origin).replace(/\/$/, "");
  const request = useMemo(() => createApiClient(apiBase), [apiBase]);

  const [theme, setTheme] = useState(getInitialTheme);
  const [tasks, setTasks] = useState([]);
  const [recentActivityFeed, setRecentActivityFeed] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [filter, setFilter] = useState("all");
  const [search, setSearch] = useState("");
  const [form, setForm] = useState(emptyForm);
  const [dragTaskId, setDragTaskId] = useState(null);
  const [dragOverStatus, setDragOverStatus] = useState(null);
  const [selectedTaskId, setSelectedTaskId] = useState(null);
  const [comments, setComments] = useState([]);
  const [activity, setActivity] = useState([]);
  const [commentText, setCommentText] = useState("");
  const [commentImage, setCommentImage] = useState(null);
  const [expandedCommentImages, setExpandedCommentImages] = useState({});
  const [isContextLoading, setIsContextLoading] = useState(false);
  const [contextTab, setContextTab] = useState("comments");
  const [isContextOpen, setIsContextOpen] = useState(false);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isMetricsHelpOpen, setIsMetricsHelpOpen] = useState(false);
  const [noticeModal, setNoticeModal] = useState(null);

  const commentFileInputRef = useRef(null);
  const titleInputRef = useRef(null);
  const descriptionInputRef = useRef(null);

  const isEditing = form.id !== null;

  /**
   * Limpia el input de archivo del comentario para permitir seleccionar el mismo archivo nuevamente.
   * @returns {void}
   */
  function resetCommentFileInput() {
    if (commentFileInputRef.current) {
      commentFileInputRef.current.value = "";
    }
  }

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    document.body.setAttribute("data-theme", theme);
    setCookie(THEME_COOKIE, theme);
  }, [theme]);

  useEffect(() => {
    loadTasks();
  }, [request]);

  useEffect(() => {
    if (!selectedTaskId) return;
    loadTaskContext(selectedTaskId);
  }, [selectedTaskId]);

  useEffect(() => {
    if (!isFormOpen) return;
    window.requestAnimationFrame(() => {
      titleInputRef.current?.focus();
    });
  }, [isFormOpen]);

  const dashboardMetrics = useMemo(
    () => buildDashboardMetrics(tasks, recentActivityFeed),
    [tasks, recentActivityFeed]
  );

  const filteredTasks = useMemo(() => {
    const term = search.trim().toLowerCase();
    return tasks.filter((task) => {
      if (filter !== "all" && task.status !== filter) return false;
      if (!term) return true;
      const labelsText = (task.labels || []).join(" ").toLowerCase();
      const text = `${task.title || ""} ${task.description || ""} ${labelsText}`.toLowerCase();
      return text.includes(term);
    });
  }, [tasks, search, filter]);

  const board = useMemo(() => {
    const initial = Object.fromEntries(STATUS.map((status) => [status, []]));
    filteredTasks.forEach((task) => {
      initial[task.status]?.push(task);
    });

    STATUS.forEach((status) => {
      initial[status].sort((a, b) => {
        const aDate = a.targetDueDate || a.dueDate;
        const bDate = b.targetDueDate || b.dueDate;
        const aTs = aDate ? new Date(aDate).getTime() : Number.MAX_SAFE_INTEGER;
        const bTs = bDate ? new Date(bDate).getTime() : Number.MAX_SAFE_INTEGER;
        return aTs - bTs;
      });
    });

    return initial;
  }, [filteredTasks]);

  const selectedTask = useMemo(
    () => tasks.find((task) => task.id === selectedTaskId) || null,
    [tasks, selectedTaskId]
  );

  /**
   * Abre un modal de confirmación y resuelve la decisión del usuario mediante una promesa.
   * @param {string} title Título del aviso.
   * @param {string} message Mensaje a mostrar.
   * @returns {Promise<boolean>} True cuando el usuario acepta la acción.
   */
  function askConfirmation(title, message) {
    return new Promise((resolve) => {
      setNoticeModal({
        title,
        message,
        resolve
      });
    });
  }

  /**
   * Cierra el modal de aviso rechazando implícitamente la acción pendiente.
   * @returns {void}
   */
  function closeNoticeModal() {
    if (!noticeModal) return;
    if (typeof noticeModal.resolve === "function") {
      noticeModal.resolve(false);
    }
    setNoticeModal(null);
  }

  /**
   * Resuelve el modal de confirmación con la decisión elegida por el usuario.
   * @param {boolean} accepted Indica si la acción fue aceptada.
   * @returns {void}
   */
  function onNoticeDecision(accepted) {
    if (typeof noticeModal?.resolve === "function") {
      noticeModal.resolve(accepted);
    }
    setNoticeModal(null);
  }

  /**
   * Carga las tareas y el feed reciente desde la API y normaliza sus datos para la UI.
   * @returns {Promise<void>}
   */
  async function loadTasks() {
    setIsLoading(true);
    try {
      const fromUtc = encodeURIComponent(getActivityFeedFromUtc());
      const [data, activityFeed] = await Promise.all([
        request("/tasks", { method: "GET", headers: {} }),
        request(`/tasks/activity-feed?fromUtc=${fromUtc}`, { method: "GET", headers: {} }).catch(() => [])
      ]);

      const normalized = Array.isArray(data)
        ? data.map((task) => ({
            ...task,
            id: normalizeId(task.id),
            status: normalizeStatus(task.status),
            labels: Array.isArray(task.labels) ? task.labels : [],
            priority: PRIORITY.includes(task.priority) ? task.priority : "Medium"
          }))
        : [];

      setTasks(normalized);
      setRecentActivityFeed(Array.isArray(activityFeed) ? activityFeed : []);
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsLoading(false);
    }
  }

  /**
   * Carga comentarios y actividad de la tarea seleccionada.
   * @param {string} taskId Identificador de la tarea cuyo contexto se consultará.
   * @returns {Promise<void>}
   */
  async function loadTaskContext(taskId) {
    if (!isValidTaskId(taskId)) {
      setComments([]);
      setActivity([]);
      toast("La tarea seleccionada tiene un ID inválido para consultar contexto.", false);
      return;
    }

    setIsContextLoading(true);
    try {
      const [commentData, activityData] = await Promise.all([
        request(`/tasks/${taskId}/comments`, { method: "GET", headers: {} }),
        request(`/tasks/${taskId}/activity`, { method: "GET", headers: {} })
      ]);

      setComments(Array.isArray(commentData) ? commentData : []);
      setActivity(Array.isArray(activityData) ? activityData : []);
    } catch (error) {
      toast(error.message, false);
      setComments([]);
      setActivity([]);
    } finally {
      setIsContextLoading(false);
    }
  }

  /**
   * Actualiza un campo puntual del formulario de tareas.
   * @param {string} field Nombre del campo a modificar.
   * @param {string} value Valor nuevo para el campo.
   * @returns {void}
   */
  function onFormField(field, value) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  /**
   * Restaura el formulario al estado inicial.
   * @returns {void}
   */
  function resetForm() {
    setForm(emptyForm());
  }

  /**
   * Prepara el modal para crear una nueva tarea.
   * @returns {void}
   */
  function openCreateForm() {
    resetForm();
    setIsFormOpen(true);
  }

  /**
   * Cierra el modal del formulario y limpia su estado.
   * @returns {void}
   */
  function closeFormModal() {
    setIsFormOpen(false);
    resetForm();
  }

  /**
   * Abre el modal de contexto para una tarea específica.
   * @param {string} taskId Identificador de la tarea seleccionada.
   * @returns {void}
   */
  function openContextModal(taskId) {
    setCommentText("");
    setCommentImage(null);
    setExpandedCommentImages({});
    setSelectedTaskId(normalizeId(taskId));
    setContextTab("comments");
    setIsContextOpen(true);
  }

  /**
   * Cierra el modal de contexto y reinicia su estado asociado.
   * @returns {void}
   */
  function closeContextModal() {
    setCommentText("");
    setCommentImage(null);
    setExpandedCommentImages({});
    setContextTab("comments");
    setComments([]);
    setActivity([]);
    setSelectedTaskId(null);
    setIsContextOpen(false);
    resetCommentFileInput();
  }

  /**
   * Carga una tarea en el formulario para editarla.
   * @param {object} task Tarea seleccionada para edición.
   * @returns {void}
   */
  function startEdit(task) {
    setSelectedTaskId(normalizeId(task.id));
    setContextTab("comments");
    setIsContextOpen(false);
    setIsFormOpen(true);
    setForm({
      id: task.id,
      title: task.title || "",
      description: task.description || "",
      priority: task.priority || "Medium",
      targetStartDate: task.targetStartDate ? new Date(task.targetStartDate).toISOString().slice(0, 10) : "",
      targetDueDate: (task.targetDueDate || task.dueDate) ? new Date(task.targetDueDate || task.dueDate).toISOString().slice(0, 10) : "",
      labelsText: (task.labels || []).join(", "),
      status: task.status || "Todo"
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  /**
   * Persiste una tarea nueva o actualiza una existente según el estado actual del formulario.
   * @param {SubmitEvent} event Evento submit del formulario.
   * @returns {Promise<void>}
   */
  async function saveTask(event) {
    event.preventDefault();

    const title = form.title.trim();
    const description = form.description.trim();

    if (!title) {
      toast("El título es obligatorio", false);
      return;
    }

    const payload = {
      title,
      description,
      priority: form.priority,
      targetStartDate: form.targetStartDate || null,
      targetDueDate: form.targetDueDate || null,
      dueDate: form.targetDueDate || null,
      labels: parseLabels(form.labelsText)
    };

    setIsBusy(true);
    try {
      if (isEditing) {
        const previousTask = tasks.find((task) => task.id === form.id);

        await request(`/tasks/${form.id}`, {
          method: "PUT",
          body: JSON.stringify(payload)
        });

        if (previousTask && previousTask.status !== form.status) {
          const warning = getBackwardTransitionWarning(previousTask.status, form.status);
          if (warning && !(await askConfirmation("Cambio de estado", warning))) {
            setIsBusy(false);
            return;
          }

          await request(`/tasks/${form.id}/status`, {
            method: "PATCH",
            body: JSON.stringify({ status: form.status })
          });
        }

        toast("Tarea actualizada");
      } else {
        await request("/tasks", {
          method: "POST",
          body: JSON.stringify(payload)
        });
        toast("Tarea creada");
      }

      resetForm();
      setIsFormOpen(false);
      await loadTasks();
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsBusy(false);
    }
  }

  /**
   * Elimina una tarea luego de confirmar la acción con el usuario.
   * @param {string} id Identificador de la tarea a eliminar.
   * @returns {Promise<void>}
   */
  async function deleteTask(id) {
    const confirmed = await askConfirmation("Eliminar tarea", "¿Eliminar esta tarea?");
    if (!confirmed) return;

    setIsBusy(true);
    try {
      await request(`/tasks/${id}`, { method: "DELETE" });
      if (form.id === id) closeFormModal();
      if (selectedTaskId === id) closeContextModal();
      toast("Tarea eliminada");
      await loadTasks();
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsBusy(false);
    }
  }

  /**
   * Actualiza el estado de una tarea desde las acciones directas de la tarjeta.
   * @param {string} id Identificador de la tarea a actualizar.
   * @param {string} status Estado de destino.
   * @returns {Promise<void>}
   */
  async function updateTaskStatus(id, status) {
    const task = tasks.find((item) => item.id === id);
    if (!task) return;

    if (!canMoveTo(task.status, status) && !isBackwardTransition(task.status, status)) {
      toast(`Transición inválida: ${task.status} -> ${status}`, false);
      return;
    }

    const warning = getBackwardTransitionWarning(task.status, status);
    if (warning && !(await askConfirmation("Cambio de estado", warning))) return;

    setIsBusy(true);
    try {
      await request(`/tasks/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status })
      });
      toast("Estado actualizado");
      await loadTasks();
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsBusy(false);
    }
  }

  /**
   * Mueve una tarea entre columnas aplicando actualización optimista en la UI.
   * @param {string} taskId Identificador de la tarea arrastrada.
   * @param {string} targetStatus Estado destino de la columna.
   * @returns {Promise<void>}
   */
  async function moveTaskToStatus(taskId, targetStatus) {
    const task = tasks.find((item) => item.id === taskId);
    if (!task || task.status === targetStatus) return;

    if (!canMoveTo(task.status, targetStatus) && !isBackwardTransition(task.status, targetStatus)) {
      toast(`Transición inválida: ${task.status} -> ${targetStatus}`, false);
      return;
    }

    const warning = getBackwardTransitionWarning(task.status, targetStatus);
    if (warning && !(await askConfirmation("Cambio de estado", warning))) return;

    const previousTasks = tasks;
    setTasks((current) =>
      current.map((item) => (item.id === taskId ? { ...item, status: targetStatus } : item))
    );

    if (form.id === taskId) {
      setForm((current) => ({ ...current, status: targetStatus }));
    }

    try {
      await request(`/tasks/${taskId}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status: targetStatus })
      });
      toast("Tarea movida");
    } catch (error) {
      setTasks(previousTasks);
      if (form.id === taskId) {
        setForm((current) => ({ ...current, status: task.status }));
      }
      toast(error.message, false);
    }
  }

  /**
   * Inicia el arrastre de una tarea en el tablero.
   * @param {DragEvent} event Evento de drag start.
   * @param {string} taskId Identificador de la tarea arrastrada.
   * @returns {void}
   */
  function onTaskDragStart(event, taskId) {
    event.dataTransfer.setData("text/plain", String(taskId));
    event.dataTransfer.effectAllowed = "move";
    setDragTaskId(taskId);
  }

  /**
   * Limpia el estado visual del drag and drop al finalizar el arrastre.
   * @returns {void}
   */
  function onTaskDragEnd() {
    setDragTaskId(null);
    setDragOverStatus(null);
  }

  /**
   * Mantiene habilitado el drop sobre una columna y marca su estado visual.
   * @param {DragEvent} event Evento drag over.
   * @param {string} status Estado representado por la columna.
   * @returns {void}
   */
  function onColumnDragOver(event, status) {
    event.preventDefault();
    if (dragOverStatus !== status) setDragOverStatus(status);
    event.dataTransfer.dropEffect = "move";
  }

  /**
   * Completa el drop de una tarea sobre una columna del tablero.
   * @param {DragEvent} event Evento drop.
   * @param {string} status Estado destino de la columna.
   * @returns {Promise<void>}
   */
  async function onColumnDrop(event, status) {
    event.preventDefault();
    if (!dragTaskId) return;
    await moveTaskToStatus(dragTaskId, status);
    setDragTaskId(null);
    setDragOverStatus(null);
  }

  /**
   * Publica un comentario para la tarea seleccionada, con imagen opcional.
   * @returns {Promise<void>}
   */
  async function addComment() {
    if (!selectedTaskId || !isValidTaskId(selectedTaskId)) {
      toast("Selecciona una tarea válida", false);
      return;
    }

    if (!commentText.trim() && !commentImage) {
      toast("El comentario o la imagen son obligatorios", false);
      return;
    }

    if (commentImage?.size && commentImage.size > MAX_COMMENT_IMAGE_SIZE) {
      toast(`La imagen supera el maximo de ${COMMENT_IMAGE_SIZE_LABEL}.`, false);
      resetCommentFileInput();
      return;
    }

    setIsBusy(true);
    try {
      const taskId = selectedTaskId;
      const requestBody = new FormData();
      requestBody.set("content", commentText.trim());
      if (commentImage?.file) {
        requestBody.set("image", commentImage.file, commentImage.fileName);
      }

      await request(`/tasks/${selectedTaskId}/comments`, {
        method: "POST",
        body: requestBody
      });

      setCommentText("");
      setCommentImage(null);
      resetCommentFileInput();

      await loadTaskContext(taskId);
      await loadTasks();
      closeContextModal();
      toast("Comentario agregado");
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsBusy(false);
    }
  }

  /**
   * Valida y prepara una imagen para adjuntarla al comentario actual.
   * @param {File} file Archivo de imagen seleccionado o pegado.
   * @returns {Promise<boolean>} True cuando la imagen queda lista para enviarse.
   */
  async function attachCommentImage(file) {
    if (!file) return false;

    if (!(file.type || "").toLowerCase().startsWith("image/")) {
      resetCommentFileInput();
      toast("Solo se permiten imagenes adjuntas.", false);
      return false;
    }

    if (file.size > MAX_COMMENT_IMAGE_SIZE) {
      resetCommentFileInput();
      toast(`La imagen supera el maximo de ${COMMENT_IMAGE_SIZE_LABEL}.`, false);
      return false;
    }

    try {
      const dataUrl = await readFileAsDataUrl(file);
      setCommentImage({
        dataUrl,
        fileName: getCommentImageFileName(file),
        size: file.size,
        file
      });
      toast("Imagen adjuntada");
      return true;
    } catch (error) {
      resetCommentFileInput();
      toast(error.message, false);
      return false;
    }
  }

  /**
   * Maneja la selección de una imagen desde el input file.
   * @param {Event} event Evento change del input de archivo.
   * @returns {Promise<void>}
   */
  async function onCommentImageSelected(event) {
    const file = event.target.files?.[0];
    if (!file) return;
    await attachCommentImage(file);
  }

  /**
   * Captura imágenes pegadas desde el portapapeles y las adjunta al comentario.
   * @param {ClipboardEvent} event Evento paste del textarea.
   * @returns {Promise<void>}
   */
  async function onCommentPaste(event) {
    const items = Array.from(event.clipboardData?.items || []);
    const imageItem = items.find((item) => (item.type || "").toLowerCase().startsWith("image/"));
    if (!imageItem) return;

    event.preventDefault();
    const file = imageItem.getAsFile();
    if (!file) {
      toast("No se pudo leer la imagen pegada.", false);
      return;
    }

    await attachCommentImage(file);
  }

  /**
   * Quita la imagen adjunta del comentario actual.
   * @returns {void}
   */
  function clearCommentImage() {
    setCommentImage(null);
    resetCommentFileInput();
  }

  /**
   * Permite avanzar del título a la descripción usando Enter en el formulario.
   * @param {KeyboardEvent} event Evento de teclado del input título.
   * @returns {void}
   */
  function onTitleKeyDown(event) {
    if (event.key !== "Enter" || event.shiftKey || event.nativeEvent?.isComposing) {
      return;
    }

    event.preventDefault();
    descriptionInputRef.current?.focus();
  }

  /**
   * Alterna la visibilidad de la imagen adjunta de un comentario existente.
   * @param {string} commentId Identificador del comentario a expandir o contraer.
   * @returns {void}
   */
  function toggleCommentImage(commentId) {
    setExpandedCommentImages((current) => ({
      ...current,
      [commentId]: !current[commentId]
    }));
  }

  return (
    <div className="layout">
      <header className="hero">
        <div className="hero-copy">
          <h1>Task Tracker Lite</h1>
          <p className="hero-text">
            Tablero operativo para priorizar, ejecutar y cerrar trabajo sin perder el hilo del contexto.
          </p>
        </div>
        <div className="header-actions">
          <div className="hero-side-actions">
            <button
              className="btn ghost theme-toggle"
              onClick={() => setTheme((current) => (current === "light" ? "dark" : "light"))}
              type="button"
              aria-label={theme === "light" ? "Cambiar a tema Abyss" : "Cambiar a tema Light High Contrast"}
              title={theme === "light" ? "Cambiar a tema Abyss" : "Cambiar a tema Light High Contrast"}
            >
              <span className="theme-icon" aria-hidden="true">{theme === "light" ? "🌙" : "☀"}</span>
              <span>{theme === "light" ? "Tema Abyss" : "Tema Light HC"}</span>
            </button>
            <button
              className="btn ghost help-circle-btn"
              type="button"
              onClick={() => setIsMetricsHelpOpen(true)}
              aria-label="Ver ayuda de indicadores"
              title="Ver ayuda de indicadores"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                <path d="M7.5 5.5h8A2.5 2.5 0 0 1 18 8v10.25a.75.75 0 0 1-1.12.65 4.9 4.9 0 0 0-2.38-.65H9a3 3 0 0 0-3 3V8.5a3 3 0 0 1 1.5-3Z" />
                <path d="M9.75 9.5h5.5" />
                <path d="M9.75 12.5h5.5" />
                <path d="M9.75 15.5h3.5" />
              </svg>
            </button>
          </div>
        </div>
      </header>

      <MetricsSection dashboardMetrics={dashboardMetrics} />

      <section className="main-grid">
        <section className="board-wrapper">
          <div className="toolbar-shell">
            <div className="toolbar">
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Buscar por titulo, descripcion o etiqueta"
              />
              <button className="btn primary toolbar-primary" type="button" onClick={openCreateForm}>
                + Nueva tarea
              </button>
              <select value={filter} onChange={(event) => setFilter(event.target.value)}>
                <option value="all">Todos</option>
                {STATUS.map((status) => (
                  <option key={status} value={status}>{STATUS_LABELS[status]}</option>
                ))}
              </select>
            </div>
          </div>

          {isLoading ? (
            <div className="skeletons">
              <div className="sk" />
              <div className="sk" />
              <div className="sk" />
            </div>
          ) : (
            <div className="board">
              {STATUS.map((status) => (
                <article
                  className={`column ${dragOverStatus === status ? "drop-active" : ""}`}
                  key={status}
                  onDragOver={(event) => onColumnDragOver(event, status)}
                  onDragLeave={() => setDragOverStatus((current) => (current === status ? null : current))}
                  onDrop={(event) => onColumnDrop(event, status)}
                >
                  <div className="column-head">
                    <div>
                      <h3>{STATUS_LABELS[status]}</h3>
                      <small>{STATUS_NOTES[status]}</small>
                    </div>
                    <span>{board[status].length}</span>
                  </div>

                  {board[status].length === 0 ? (
                    <div className="empty">Sin tareas</div>
                  ) : (
                    <ul className="cards">
                      {board[status].map((task) => {
                        const actionStatus = nextStatus(task.status);
                        const dueDate = task.targetDueDate || task.dueDate;

                        return (
                          <li
                            className={`card ${dragTaskId === task.id ? "dragging" : ""} ${selectedTaskId === task.id ? "selected" : ""}`}
                            key={task.id}
                            draggable={!isBusy}
                            onDragStart={(event) => onTaskDragStart(event, task.id)}
                            onDragEnd={onTaskDragEnd}
                          >
                            <div className="card-topline">
                              <h4>{task.title}</h4>
                              <span className="card-date">Objetivo {formatDate(dueDate)}</span>
                            </div>
                            <p>{task.description || "Sin descripción"}</p>
                            <div className="card-meta-row">
                              <span className={`pill priority-${(task.priority || "Medium").toLowerCase()}`}>
                                {task.priority || "Medium"}
                              </span>
                              <span className="pill status">{task.status}</span>
                            </div>
                            {(task.labels || []).length > 0 && (
                              <div className="label-row">
                                {(task.labels || []).slice(0, 4).map((label) => (
                                  <span className="pill label" key={`${task.id}-${label}`}>{label}</span>
                                ))}
                              </div>
                            )}

                            <div className="card-actions">
                              <button
                                className="btn ghost icon-btn"
                                type="button"
                                draggable={false}
                                onClick={(event) => runCardAction(event, () => openContextModal(task.id))}
                                disabled={isBusy}
                                aria-label="Ver contexto"
                                title="Ver contexto"
                              >
                                <span aria-hidden="true">💬</span>
                              </button>
                              <button
                                className="btn ghost icon-btn"
                                type="button"
                                draggable={false}
                                onClick={(event) => runCardAction(event, () => startEdit(task))}
                                disabled={isBusy}
                                aria-label="Editar tarea"
                                title="Editar tarea"
                              >
                                <span aria-hidden="true">✏</span>
                              </button>
                              {actionStatus && (
                                <button
                                  className="btn ghost icon-btn"
                                  type="button"
                                  draggable={false}
                                  onClick={(event) => runCardAction(event, () => updateTaskStatus(task.id, actionStatus))}
                                  disabled={isBusy}
                                  aria-label={`Mover a ${actionStatus}`}
                                  title={`Mover a ${actionStatus}`}
                                >
                                  <span aria-hidden="true">⇢</span>
                                </button>
                              )}
                              <button
                                className="btn danger icon-btn"
                                type="button"
                                draggable={false}
                                onClick={(event) => runCardAction(event, () => deleteTask(task.id))}
                                disabled={isBusy}
                                aria-label="Eliminar tarea"
                                title="Eliminar tarea"
                              >
                                <span aria-hidden="true">🗑</span>
                              </button>
                            </div>
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </article>
              ))}
            </div>
          )}
        </section>
      </section>

      <ContextModal
        isOpen={isContextOpen}
        onClose={closeContextModal}
        selectedTask={selectedTask}
        commentFileInputRef={commentFileInputRef}
        onCommentImageSelected={onCommentImageSelected}
        commentText={commentText}
        onCommentTextChange={(event) => setCommentText(event.target.value)}
        onCommentPaste={onCommentPaste}
        onOpenFileSelector={() => commentFileInputRef.current?.click()}
        isBusy={isBusy}
        commentImage={commentImage}
        onClearCommentImage={clearCommentImage}
        onAddComment={addComment}
        isContextLoading={isContextLoading}
        contextTab={contextTab}
        onContextTabChange={setContextTab}
        comments={comments}
        activity={activity}
        expandedCommentImages={expandedCommentImages}
        onToggleCommentImage={toggleCommentImage}
      />

      <MetricsHelpModal
        isOpen={isMetricsHelpOpen}
        onClose={() => setIsMetricsHelpOpen(false)}
      />

      <TaskFormModal
        isOpen={isFormOpen}
        onClose={closeFormModal}
        isEditing={isEditing}
        saveTask={saveTask}
        form={form}
        onFormField={onFormField}
        onTitleKeyDown={onTitleKeyDown}
        titleInputRef={titleInputRef}
        descriptionInputRef={descriptionInputRef}
        isBusy={isBusy}
      />

      <NoticeModal
        noticeModal={noticeModal}
        onClose={closeNoticeModal}
        onDecision={onNoticeDecision}
      />

      {isBusy && (
        <div className="overlay">
          <div className="loader" />
        </div>
      )}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);

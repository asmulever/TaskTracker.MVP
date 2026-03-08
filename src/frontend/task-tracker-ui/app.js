const { useEffect, useMemo, useState } = React;

const STATUS = ["Todo", "Doing", "Done"];
const UUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const STATUS_LABELS = {
  Todo: "Todo",
  Doing: "Doing",
  Done: "Done",
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

function emptyForm() {
  return {
    id: null,
    title: "",
    description: "",
    priority: "Medium",
    targetStartDate: "",
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

function confirmStatusChange(current, next) {
  if (!isBackwardTransition(current, next)) return true;

  return window.confirm(
    "Esta accion no es recomendada por consistencia de datos y puede romper el historial de tareas.\n\nAceptar: cambiar la tarea de todas formas.\nCancelar: abortar la accion."
  );
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

function App() {
  const apiBase = (window.TASK_API_URL || window.location.origin).replace(/\/$/, "");

  const [theme, setTheme] = useState(getInitialTheme);
  const [tasks, setTasks] = useState([]);
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
  const [isContextLoading, setIsContextLoading] = useState(false);
  const [contextTab, setContextTab] = useState("comments");
  const [isFormOpen, setIsFormOpen] = useState(false);

  const isEditing = form.id !== null;

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    document.body.setAttribute("data-theme", theme);
    setCookie(THEME_COOKIE, theme);
  }, [theme]);

  useEffect(() => {
    loadTasks();
  }, []);

  useEffect(() => {
    if (!selectedTaskId) return;
    loadTaskContext(selectedTaskId);
  }, [selectedTaskId]);

  const stats = useMemo(() => {
    const total = tasks.length;
    const todo = tasks.filter((t) => t.status === "Todo").length;
    const doing = tasks.filter((t) => t.status === "Doing").length;
    const done = tasks.filter((t) => t.status === "Done").length;
    const progress = total === 0 ? 0 : Math.round((done / total) * 100);
    return { total, todo, doing, done, progress };
  }, [tasks]);

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

  async function request(path, options = {}) {
    const response = await fetch(`${apiBase}${path}`, {
      headers: {
        "Content-Type": "application/json",
        ...(options.headers || {})
      },
      ...options
    });

    const contentType = (response.headers.get("content-type") || "").toLowerCase();
    const isJsonResponse = contentType.includes("application/json") || contentType.includes("+json");

    if (!response.ok) {
      let message = "No se pudo completar la operación";
      try {
        const text = await response.text();
        if (text) {
          if (text.includes("<!DOCTYPE") || text.includes("<html")) {
            message = "El servidor devolvió HTML en vez de JSON. Revisa la URL del backend.";
          } else {
            message = text;
          }
        }
      } catch {
        // noop
      }
      throw new Error(message);
    }

    if (response.status === 204) return null;
    if (!isJsonResponse) {
      const text = await response.text();
      if (text.includes("<!DOCTYPE") || text.includes("<html")) {
        throw new Error("El endpoint devolvió HTML en vez de JSON. Verifica que la ruta exista en backend.");
      }
      throw new Error("Respuesta inválida del servidor (se esperaba JSON).");
    }

    return response.json();
  }

  async function loadTasks() {
    setIsLoading(true);
    try {
      const data = await request("/tasks", { method: "GET", headers: {} });
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
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsLoading(false);
    }
  }

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

  function onFormField(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
  }

  function resetForm() {
    setForm(emptyForm());
  }

  function openCreateForm() {
    resetForm();
    setIsFormOpen(true);
  }

  function closeFormModal() {
    setIsFormOpen(false);
    resetForm();
  }

  function startEdit(task) {
    setSelectedTaskId(normalizeId(task.id));
    setContextTab("comments");
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

  async function saveTask(event) {
    event.preventDefault();

    const title = form.title.trim();
    const description = form.description.trim();

    if (!title) {
      toast("El título es obligatorio", false);
      return;
    }

    const labels = parseLabels(form.labelsText);
    const payload = {
      title,
      description,
      priority: form.priority,
      targetStartDate: form.targetStartDate || null,
      targetDueDate: form.targetDueDate || null,
      dueDate: form.targetDueDate || null,
      labels
    };

    setIsBusy(true);
    try {
      if (isEditing) {
        const previousTask = tasks.find((x) => x.id === form.id);

        await request(`/tasks/${form.id}`, {
          method: "PUT",
          body: JSON.stringify(payload)
        });

        if (previousTask && previousTask.status !== form.status) {
          if (!confirmStatusChange(previousTask.status, form.status)) {
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

  async function deleteTask(id) {
    if (!window.confirm("¿Eliminar esta tarea?")) return;

    setIsBusy(true);
    try {
      await request(`/tasks/${id}`, { method: "DELETE" });
      if (form.id === id) closeFormModal();
      if (selectedTaskId === id) {
        setSelectedTaskId(null);
        setComments([]);
        setActivity([]);
      }
      toast("Tarea eliminada");
      await loadTasks();
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsBusy(false);
    }
  }

  async function updateTaskStatus(id, status) {
    const task = tasks.find((x) => x.id === id);
    if (!task) return;

    if (!canMoveTo(task.status, status) && !isBackwardTransition(task.status, status)) {
      toast(`Transición inválida: ${task.status} -> ${status}`, false);
      return;
    }

    if (!confirmStatusChange(task.status, status)) {
      return;
    }

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

  async function moveTaskToStatus(taskId, targetStatus) {
    const task = tasks.find((t) => t.id === taskId);
    if (!task || task.status === targetStatus) return;

    if (!canMoveTo(task.status, targetStatus) && !isBackwardTransition(task.status, targetStatus)) {
      toast(`Transición inválida: ${task.status} -> ${targetStatus}`, false);
      return;
    }

    if (!confirmStatusChange(task.status, targetStatus)) return;

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

  function onTaskDragStart(event, taskId) {
    event.dataTransfer.setData("text/plain", String(taskId));
    event.dataTransfer.effectAllowed = "move";
    setDragTaskId(taskId);
  }

  function onTaskDragEnd() {
    setDragTaskId(null);
    setDragOverStatus(null);
  }

  function onColumnDragOver(event, status) {
    event.preventDefault();
    if (dragOverStatus !== status) setDragOverStatus(status);
    event.dataTransfer.dropEffect = "move";
  }

  async function onColumnDrop(event, status) {
    event.preventDefault();
    if (!dragTaskId) return;
    await moveTaskToStatus(dragTaskId, status);
    setDragTaskId(null);
    setDragOverStatus(null);
  }

  async function addComment() {
    if (!selectedTaskId || !isValidTaskId(selectedTaskId)) {
      toast("Selecciona una tarea válida", false);
      return;
    }
    if (!commentText.trim()) {
      toast("El comentario es obligatorio", false);
      return;
    }

    setIsBusy(true);
    try {
      await request(`/tasks/${selectedTaskId}/comments`, {
        method: "POST",
        body: JSON.stringify({ content: commentText.trim() })
      });
      setCommentText("");
      await loadTaskContext(selectedTaskId);
      await loadTasks();
      toast("Comentario agregado");
    } catch (error) {
      toast(error.message, false);
    } finally {
      setIsBusy(false);
    }
  }

  function cancelCommentDraft() {
    setCommentText("");
    setContextTab("comments");
    setComments([]);
    setActivity([]);
    setSelectedTaskId(null);
  }

  const selectedTask = useMemo(
    () => tasks.find((task) => task.id === selectedTaskId) || null,
    [tasks, selectedTaskId]
  );

  return (
    <div className="layout">
      <header className="header">
        <div>
          <p className="label">Task Tracker Pro</p>
          <h1>Operación de tareas</h1>
        </div>
        <div className="header-actions">
          <button className="btn primary" type="button" onClick={openCreateForm}>
            + Nueva tarea
          </button>
          <button
            className="btn ghost theme-toggle"
            onClick={() => setTheme((t) => (t === "light" ? "dark" : "light"))}
            type="button"
            aria-label={theme === "light" ? "Cambiar a modo oscuro" : "Cambiar a modo claro"}
            title={theme === "light" ? "Cambiar a modo oscuro" : "Cambiar a modo claro"}
          >
            <span className="theme-icon" aria-hidden="true">{theme === "light" ? "🌙" : "☀"}</span>
            <span>{theme === "light" ? "Modo oscuro" : "Modo claro"}</span>
          </button>
        </div>
      </header>

      <section className="metrics">
        <article className="metric"><span>Total</span><strong>{stats.total}</strong></article>
        <article className="metric"><span>Todo</span><strong>{stats.todo}</strong></article>
        <article className="metric"><span>Doing</span><strong>{stats.doing}</strong></article>
        <article className="metric"><span>Done</span><strong>{stats.done}</strong></article>
        <article className="metric wide">
          <span>Progreso</span>
          <div className="progress-circle" style={{ "--progress": `${stats.progress}%` }}>
            <div className="progress-circle-inner">
              <strong>{stats.progress}%</strong>
            </div>
          </div>
        </article>
      </section>

      <section className="main-grid">
        <section className="board-wrapper">
          <div className="toolbar">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Buscar por título, descripción o etiqueta"
            />
            <select value={filter} onChange={(e) => setFilter(e.target.value)}>
              <option value="all">Todos</option>
              {STATUS.map((s) => (
                <option key={s} value={s}>{STATUS_LABELS[s]}</option>
              ))}
            </select>
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
                    <h3>{STATUS_LABELS[status]}</h3>
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
                            <h4>{task.title}</h4>
                            <p>{task.description || "Sin descripción"}</p>
                            <small>Objetivo: {formatDate(dueDate)}</small>
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
                                onClick={(event) => runCardAction(event, () => {
                                  setSelectedTaskId(normalizeId(task.id));
                                  setContextTab("comments");
                                })}
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

          <section className="context-panel">
            <div className="context-head">
              <h3>Contexto</h3>
              {selectedTask && (
                <div className="context-task-meta">
                  <small>{selectedTask.title}</small>
                  <span className={`pill priority-${(selectedTask.priority || "Medium").toLowerCase()}`}>
                    {selectedTask.priority || "Medium"}
                  </span>
                  <span className="pill status">{selectedTask.status}</span>
                </div>
              )}
            </div>

            {!selectedTask ? (
              <p className="context-empty">Selecciona una tarea para ver comentarios y actividad.</p>
            ) : (
              <>
                <div className="context-comment-box">
                  <textarea
                    value={commentText}
                    onChange={(event) => setCommentText(event.target.value)}
                    placeholder="Agregar comentario"
                    maxLength={1000}
                  />
                  <div className="context-comment-actions">
                    <button className="btn primary" type="button" onClick={addComment} disabled={isBusy}>
                      Publicar comentario
                    </button>
                    <button className="btn ghost" type="button" onClick={cancelCommentDraft} disabled={isBusy}>
                      Cancelar
                    </button>
                  </div>
                </div>

                {isContextLoading ? (
                  <p className="context-empty">Cargando contexto...</p>
                ) : (
                  <div className="context-tabs-wrap">
                    <div className="context-tabs">
                      <button
                        className={`btn ghost tab-btn ${contextTab === "comments" ? "active" : ""}`}
                        type="button"
                        onClick={() => setContextTab("comments")}
                      >
                        Comentarios ({comments.length})
                      </button>
                      <button
                        className={`btn ghost tab-btn ${contextTab === "activity" ? "active" : ""}`}
                        type="button"
                        onClick={() => setContextTab("activity")}
                      >
                        Actividad ({activity.length})
                      </button>
                    </div>

                    {contextTab === "comments" ? (
                      comments.length === 0 ? (
                        <p className="context-empty">Sin comentarios.</p>
                      ) : (
                        <ul className="context-list">
                          {comments.map((comment) => (
                            <li key={comment.id}>
                              <p>{comment.content}</p>
                              <small>{formatDateTime(comment.createdAt)}</small>
                            </li>
                          ))}
                        </ul>
                      )
                    ) : activity.length === 0 ? (
                      <p className="context-empty">Sin actividad.</p>
                    ) : (
                      <ul className="context-list">
                        {activity.map((item) => (
                          <li key={item.id}>
                            <p>{item.action}</p>
                            <small>{item.detail}</small>
                            <small>{formatDateTime(item.createdAt)}</small>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                )}
              </>
            )}
          </section>
        </section>
      </section>

      {isFormOpen && (
        <div className="form-overlay" onClick={closeFormModal}>
          <aside className="panel modal-panel" onClick={(event) => event.stopPropagation()}>
            <div className="modal-head">
              <h2>{isEditing ? "Editar tarea" : "Crear tarea"}</h2>
              <button className="btn ghost icon-btn" type="button" onClick={closeFormModal} aria-label="Cerrar formulario" title="Cerrar formulario">
                <span aria-hidden="true">✕</span>
              </button>
            </div>
            <form onSubmit={saveTask} className="task-form">
              <label>Título</label>
              <input
                value={form.title}
                onChange={(e) => onFormField("title", e.target.value)}
                maxLength={200}
                placeholder="Ej: Ajustar API de reportes"
                required
              />

              <label>Descripción</label>
              <textarea
                value={form.description}
                onChange={(e) => onFormField("description", e.target.value)}
                maxLength={1000}
                placeholder="Detalle breve del trabajo"
              />

              <div className="row two">
                <div>
                  <label>Prioridad</label>
                  <select value={form.priority} onChange={(e) => onFormField("priority", e.target.value)}>
                    {PRIORITY.map((p) => (
                      <option key={p} value={p}>{p}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label>Estado</label>
                  <select
                    value={form.status}
                    onChange={(e) => onFormField("status", e.target.value)}
                    disabled={!isEditing}
                  >
                    {STATUS.map((s) => (
                      <option key={s} value={s}>{STATUS_LABELS[s]}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="row two">
                <div>
                  <label>Inicio objetivo</label>
                  <input
                    type="date"
                    value={form.targetStartDate}
                    onChange={(e) => onFormField("targetStartDate", e.target.value)}
                  />
                </div>

                <div>
                  <label>Fecha objetivo</label>
                  <input
                    type="date"
                    value={form.targetDueDate}
                    onChange={(e) => onFormField("targetDueDate", e.target.value)}
                  />
                </div>
              </div>

              <label>Etiquetas (separadas por coma)</label>
              <input
                value={form.labelsText}
                onChange={(e) => onFormField("labelsText", e.target.value)}
                maxLength={350}
                placeholder="backend, api, urgente"
              />

              <small className="hint">{form.title.length}/200 caracteres</small>

              <div className="row actions">
                <button className="btn ghost" type="button" onClick={closeFormModal} disabled={isBusy}>
                  Cancelar
                </button>
                <button className="btn primary" type="submit" disabled={isBusy}>
                  {isBusy ? "Guardando..." : isEditing ? "Guardar cambios" : "Crear tarea"}
                </button>
              </div>
            </form>
          </aside>
        </div>
      )}

      {isBusy && (
        <div className="overlay">
          <div className="loader" />
        </div>
      )}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);

const { useEffect, useMemo, useState } = React;

const STATUS = ["Created", "Planned", "InProgress", "Blocked", "Done", "Archived"];
const STATUS_LABELS = {
  Created: "Created",
  Planned: "Planned",
  InProgress: "InProgress",
  Blocked: "Blocked",
  Done: "Done",
  Archived: "Archived"
};
const PRIORITY = ["Low", "Medium", "High", "Critical"];
const ALLOWED_TRANSITIONS = {
  Created: ["Planned", "Archived"],
  Planned: ["InProgress", "Blocked", "Archived"],
  InProgress: ["Blocked", "Done", "Archived"],
  Blocked: ["Planned", "InProgress", "Archived"],
  Done: ["Archived"],
  Archived: []
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

function normalizeStatus(status) {
  if (STATUS.includes(status)) return status;
  if (status === "Todo") return "Created";
  if (status === "Doing") return "InProgress";
  return "Created";
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
    status: "Created"
  };
}

function canMoveTo(current, next) {
  if (current === next) return true;
  return (ALLOWED_TRANSITIONS[current] || []).includes(next);
}

function nextStatus(current) {
  const next = ALLOWED_TRANSITIONS[current] || [];
  return next.length > 0 ? next[0] : null;
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

  const isEditing = form.id !== null;

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    document.body.setAttribute("data-theme", theme);
    setCookie(THEME_COOKIE, theme);
  }, [theme]);

  useEffect(() => {
    loadTasks();
  }, []);

  const stats = useMemo(() => {
    const total = tasks.length;
    const created = tasks.filter((t) => t.status === "Created").length;
    const planned = tasks.filter((t) => t.status === "Planned").length;
    const inProgress = tasks.filter((t) => t.status === "InProgress").length;
    const blocked = tasks.filter((t) => t.status === "Blocked").length;
    const done = tasks.filter((t) => t.status === "Done").length;
    const progress = total === 0 ? 0 : Math.round((done / total) * 100);
    return { total, created, planned, inProgress, blocked, done, progress };
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

    if (!response.ok) {
      let message = "No se pudo completar la operación";
      try {
        const text = await response.text();
        if (text) message = text;
      } catch {
        // noop
      }
      throw new Error(message);
    }

    if (response.status === 204) return null;
    return response.json();
  }

  async function loadTasks() {
    setIsLoading(true);
    try {
      const data = await request("/tasks", { method: "GET", headers: {} });
      const normalized = Array.isArray(data)
        ? data.map((task) => ({
            ...task,
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

  function onFormField(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
  }

  function resetForm() {
    setForm(emptyForm());
  }

  function startEdit(task) {
    setForm({
      id: task.id,
      title: task.title || "",
      description: task.description || "",
      priority: task.priority || "Medium",
      targetStartDate: task.targetStartDate ? new Date(task.targetStartDate).toISOString().slice(0, 10) : "",
      targetDueDate: (task.targetDueDate || task.dueDate) ? new Date(task.targetDueDate || task.dueDate).toISOString().slice(0, 10) : "",
      labelsText: (task.labels || []).join(", "),
      status: task.status || "Created"
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
      if (form.id === id) resetForm();
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
    if (!canMoveTo(task.status, status)) {
      toast(`Transición inválida: ${task.status} -> ${status}`, false);
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

    if (!canMoveTo(task.status, targetStatus)) {
      toast(`Transición inválida: ${task.status} -> ${targetStatus}`, false);
      return;
    }

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

  return (
    <div className="layout">
      <header className="header">
        <div>
          <p className="label">Task Tracker Pro</p>
          <h1>Operación de tareas</h1>
        </div>
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
      </header>

      <section className="metrics">
        <article className="metric"><span>Total</span><strong>{stats.total}</strong></article>
        <article className="metric"><span>Created</span><strong>{stats.created}</strong></article>
        <article className="metric"><span>Planned</span><strong>{stats.planned}</strong></article>
        <article className="metric"><span>InProgress</span><strong>{stats.inProgress}</strong></article>
        <article className="metric"><span>Blocked</span><strong>{stats.blocked}</strong></article>
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
        <aside className="panel">
          <h2>{isEditing ? "Editar tarea" : "Crear tarea"}</h2>
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
              {isEditing && (
                <button className="btn ghost" type="button" onClick={resetForm} disabled={isBusy}>
                  Cancelar edición
                </button>
              )}
              <button className="btn primary" type="submit" disabled={isBusy}>
                {isBusy ? "Guardando..." : isEditing ? "Guardar cambios" : "Crear tarea"}
              </button>
            </div>
          </form>
        </aside>

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
                            className={`card ${dragTaskId === task.id ? "dragging" : ""}`}
                            key={task.id}
                            draggable={!isBusy}
                            onDragStart={(event) => onTaskDragStart(event, task.id)}
                            onDragEnd={onTaskDragEnd}
                          >
                            <h4>{task.title}</h4>
                            <p>{task.description || "Sin descripción"}</p>
                            <small>Prioridad: {task.priority || "Medium"}</small>
                            <small>Objetivo: {formatDate(dueDate)}</small>
                            {(task.labels || []).length > 0 && (
                              <small>Etiquetas: {(task.labels || []).join(", ")}</small>
                            )}

                            <div className="card-actions">
                              <button
                                className="btn ghost icon-btn"
                                onClick={() => startEdit(task)}
                                disabled={isBusy}
                                aria-label="Editar tarea"
                                title="Editar tarea"
                              >
                                <span aria-hidden="true">✏</span>
                              </button>
                              {actionStatus && (
                                <button
                                  className="btn ghost icon-btn"
                                  onClick={() => updateTaskStatus(task.id, actionStatus)}
                                  disabled={isBusy}
                                  aria-label={`Mover a ${actionStatus}`}
                                  title={`Mover a ${actionStatus}`}
                                >
                                  <span aria-hidden="true">⇢</span>
                                </button>
                              )}
                              <button
                                className="btn danger icon-btn"
                                onClick={() => deleteTask(task.id)}
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

      {isBusy && (
        <div className="overlay">
          <div className="loader" />
        </div>
      )}
    </div>
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);

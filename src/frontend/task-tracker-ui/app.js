const { useEffect, useMemo, useState } = React;

const STATUS = ["Todo", "Doing", "Done"];
const STATUS_ALIASES = {
  Created: "Todo",
  Planned: "Todo",
  InProgress: "Doing",
  Blocked: "Doing",
  Done: "Done",
  Archived: "Done",
  Todo: "Todo",
  Doing: "Doing"
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

function emptyForm() {
  return {
    id: null,
    title: "",
    description: "",
    dueDate: "",
    status: "Todo"
  };
}

function toUiStatus(status) {
  return STATUS_ALIASES[status] || "Todo";
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
      const text = `${task.title || ""} ${task.description || ""}`.toLowerCase();
      return text.includes(term);
    });
  }, [tasks, search, filter]);

  const board = useMemo(() => {
    const initial = { Todo: [], Doing: [], Done: [] };
    filteredTasks.forEach((task) => {
      initial[task.status]?.push(task);
    });

    STATUS.forEach((status) => {
      initial[status].sort((a, b) => {
        const aDate = a.dueDate ? new Date(a.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
        const bDate = b.dueDate ? new Date(b.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
        return aDate - bDate;
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
        ? data.map((task) => ({ ...task, status: toUiStatus(task.status) }))
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
      dueDate: task.dueDate ? new Date(task.dueDate).toISOString().slice(0, 10) : "",
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

    setIsBusy(true);
    try {
      if (isEditing) {
        const previousTask = tasks.find((x) => x.id === form.id);

        await request(`/tasks/${form.id}`, {
          method: "PUT",
          body: JSON.stringify({ title, description, dueDate: form.dueDate || null })
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
          body: JSON.stringify({ title, description, dueDate: form.dueDate || null })
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

  function nextStatus(current) {
    const index = STATUS.indexOf(current);
    return STATUS[Math.min(index + 1, STATUS.length - 1)];
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
                <label>Fecha límite</label>
                <input
                  type="date"
                  value={form.dueDate}
                  onChange={(e) => onFormField("dueDate", e.target.value)}
                />
              </div>

              <div>
                <label>Estado</label>
                <select
                  value={form.status}
                  onChange={(e) => onFormField("status", e.target.value)}
                  disabled={!isEditing}
                >
                  {STATUS.map((s) => (
                    <option key={s} value={s}>{s}</option>
                  ))}
                </select>
              </div>
            </div>

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
              placeholder="Buscar por título o descripción"
            />
            <select value={filter} onChange={(e) => setFilter(e.target.value)}>
              <option value="all">Todos</option>
              {STATUS.map((s) => (
                <option key={s} value={s}>{s}</option>
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
                    <h3>{status}</h3>
                    <span>{board[status].length}</span>
                  </div>

                  {board[status].length === 0 ? (
                    <div className="empty">Sin tareas</div>
                  ) : (
                    <ul className="cards">
                      {board[status].map((task) => (
                        <li
                          className={`card ${dragTaskId === task.id ? "dragging" : ""}`}
                          key={task.id}
                          draggable={!isBusy}
                          onDragStart={(event) => onTaskDragStart(event, task.id)}
                          onDragEnd={onTaskDragEnd}
                        >
                          <h4>{task.title}</h4>
                          <p>{task.description || "Sin descripción"}</p>
                          <small>Vence: {formatDate(task.dueDate)}</small>

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
                            {task.status !== "Done" && (
                              <button
                                className="btn ghost icon-btn"
                                onClick={() => updateTaskStatus(task.id, nextStatus(task.status))}
                                disabled={isBusy}
                                aria-label={`Mover a ${nextStatus(task.status)}`}
                                title={`Mover a ${nextStatus(task.status)}`}
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
                      ))}
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

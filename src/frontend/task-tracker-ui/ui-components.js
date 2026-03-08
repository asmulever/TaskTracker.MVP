window.TaskTrackerUi = window.TaskTrackerUi || {};

(() => {
  const {
    METRIC_HELP_ITEMS,
    PRIORITY,
    STATUS,
    STATUS_LABELS,
    COMMENT_IMAGE_SIZE_LABEL,
    formatFileSize,
    formatDateTime
  } = window.TaskTrackerUi;

  /**
   * Renderiza las tarjetas resumen del dashboard.
   * @param {{dashboardMetrics: object}} props Props del componente.
   * @returns {JSX.Element} La sección de métricas principal.
   */
  function MetricsSection({ dashboardMetrics }) {
    return (
      <section className="metrics">
        <article className="metric metric-highlight">
          <div className="metric-headline">
            <p>{METRIC_HELP_ITEMS[0].label}</p>
          </div>
          <strong>{dashboardMetrics.goalCompletionPercent}%</strong>
          <small>{dashboardMetrics.completedThisCycle} de {dashboardMetrics.committedThisCycleCount} comprometidas este mes</small>
        </article>
        <article className="metric">
          <div className="metric-headline">
            <p>{METRIC_HELP_ITEMS[1].label}</p>
          </div>
          <strong>{dashboardMetrics.focusActiveCount}/{dashboardMetrics.focusCapacity}</strong>
          <small>Capacidad recomendada para sostener foco sin sobrecarga</small>
        </article>
        <article className="metric">
          <div className="metric-headline">
            <p>{METRIC_HELP_ITEMS[2].label}</p>
          </div>
          <strong>{dashboardMetrics.atRiskCount}</strong>
          <small>Tareas no cerradas con vencimiento proximo o ya vencido</small>
        </article>
        <article className="metric">
          <div className="metric-headline">
            <p>{METRIC_HELP_ITEMS[3].label}</p>
          </div>
          <strong>{dashboardMetrics.weeklyClosedCount}</strong>
          <small>Tareas cerradas en los ultimos 7 dias</small>
        </article>
      </section>
    );
  }

  /**
   * Renderiza el modal con la referencia conceptual de los indicadores.
   * @param {{isOpen: boolean, onClose: Function}} props Props del componente.
   * @returns {JSX.Element|null} El modal visible o null cuando está cerrado.
   */
  function MetricsHelpModal({ isOpen, onClose }) {
    if (!isOpen) return null;

    return (
      <div className="notice-overlay" onClick={onClose}>
        <aside className="panel notice-panel metrics-help-panel" onClick={(event) => event.stopPropagation()}>
          <div className="modal-head">
            <div>
              <p className="label">Referencia</p>
              <h2>Guia de indicadores</h2>
            </div>
            <button
              className="btn ghost icon-btn"
              type="button"
              onClick={onClose}
              aria-label="Cerrar ayuda de indicadores"
              title="Cerrar ayuda de indicadores"
            >
              <span aria-hidden="true">✕</span>
            </button>
          </div>
          <div className="metrics-help-list">
            {METRIC_HELP_ITEMS.map((item) => (
              <section key={item.key} className="metrics-help-item">
                <h3>{item.label}</h3>
                <p>{item.description}</p>
                <small>{item.formula}</small>
              </section>
            ))}
          </div>
        </aside>
      </div>
    );
  }

  /**
   * Renderiza el modal de contexto con comentarios y actividad de la tarea seleccionada.
   * @param {object} props Props necesarias para operar el modal de contexto.
   * @returns {JSX.Element|null} El modal visible o null cuando está cerrado.
   */
  function ContextModal({
    isOpen,
    onClose,
    selectedTask,
    commentFileInputRef,
    onCommentImageSelected,
    commentText,
    onCommentTextChange,
    onCommentPaste,
    onOpenFileSelector,
    isBusy,
    commentImage,
    onClearCommentImage,
    onAddComment,
    isContextLoading,
    contextTab,
    onContextTabChange,
    comments,
    activity,
    expandedCommentImages,
    onToggleCommentImage
  }) {
    if (!isOpen) return null;

    return (
      <div className="form-overlay" onClick={onClose}>
        <aside className="panel modal-panel context-modal-panel" onClick={(event) => event.stopPropagation()}>
          <div className="modal-head">
            <div>
              <p className="label">Detalle conectado</p>
              <h2>Contexto</h2>
            </div>
            <button className="btn ghost icon-btn" type="button" onClick={onClose} aria-label="Cerrar contexto" title="Cerrar contexto">
              <span aria-hidden="true">✕</span>
            </button>
          </div>

          <section className="context-panel">
            {selectedTask && (
              <div className="context-head">
                <div className="context-task-meta">
                  <strong>{selectedTask.title}</strong>
                  <span className={`pill priority-${(selectedTask.priority || "Medium").toLowerCase()}`}>
                    {selectedTask.priority || "Medium"}
                  </span>
                  <span className="pill status">{selectedTask.status}</span>
                </div>
              </div>
            )}

            {!selectedTask ? (
              <p className="context-empty">Selecciona una tarea para ver comentarios y actividad.</p>
            ) : (
              <>
                <div className="context-comment-box">
                  <input
                    ref={commentFileInputRef}
                    type="file"
                    accept="image/*"
                    className="sr-only"
                    onChange={onCommentImageSelected}
                  />
                  <textarea
                    value={commentText}
                    onChange={onCommentTextChange}
                    onPaste={onCommentPaste}
                    placeholder="Agregar comentario o pegar una imagen"
                    maxLength={1000}
                  />
                  <div className="context-comment-tools">
                    <button
                      className="btn ghost"
                      type="button"
                      onClick={onOpenFileSelector}
                      disabled={isBusy}
                    >
                      Adjuntar imagen
                    </button>
                    <div className="context-comment-hints">
                      <small className="hint">Maximo {COMMENT_IMAGE_SIZE_LABEL} por imagen.</small>
                      <small className="hint">Tambien puedes pegar una imagen con Ctrl+V.</small>
                    </div>
                  </div>
                  {commentImage && (
                    <div className="comment-image-preview">
                      <img src={commentImage.dataUrl} alt={commentImage.fileName || "Imagen adjunta"} />
                      <div className="comment-image-meta">
                        <small>
                          {commentImage.fileName}
                          {commentImage.size ? ` (${formatFileSize(commentImage.size)})` : ""}
                        </small>
                        <button className="btn ghost" type="button" onClick={onClearCommentImage} disabled={isBusy}>
                          Quitar imagen
                        </button>
                      </div>
                    </div>
                  )}
                  <div className="context-comment-actions">
                    <button className="btn ghost" type="button" onClick={onClose} disabled={isBusy}>
                      Cancelar
                    </button>
                    <button className="btn primary" type="button" onClick={onAddComment} disabled={isBusy}>
                      Publicar comentario
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
                        onClick={() => onContextTabChange("comments")}
                      >
                        Comentarios ({comments.length})
                      </button>
                      <button
                        className={`btn ghost tab-btn ${contextTab === "activity" ? "active" : ""}`}
                        type="button"
                        onClick={() => onContextTabChange("activity")}
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
                              <div className="comment-meta-row">
                                <small>{formatDateTime(comment.createdAt)}</small>
                                {comment.imageDataUrl ? (
                                  <span className="comment-attachment-badge" title="Este comentario tiene una imagen adjunta">
                                    <span aria-hidden="true">🖼</span>
                                    <span>Imagen adjunta</span>
                                  </span>
                                ) : null}
                              </div>
                              {comment.content ? <p>{comment.content}</p> : null}
                              {comment.imageDataUrl ? (
                                <>
                                  <button
                                    className="btn ghost comment-attachment-toggle"
                                    type="button"
                                    onClick={() => onToggleCommentImage(comment.id)}
                                  >
                                    <span aria-hidden="true">🖼</span>
                                    <span>
                                      {expandedCommentImages[comment.id] ? "Ocultar imagen adjunta" : "Ver imagen adjunta"}
                                    </span>
                                  </button>
                                  {expandedCommentImages[comment.id] ? (
                                    <img
                                      className="comment-image"
                                      src={comment.imageDataUrl}
                                      alt={comment.imageFileName || "Imagen adjunta del comentario"}
                                      loading="lazy"
                                    />
                                  ) : null}
                                </>
                              ) : null}
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
        </aside>
      </div>
    );
  }

  /**
   * Renderiza el formulario modal para crear o editar tareas.
   * @param {object} props Props necesarias para controlar el formulario.
   * @returns {JSX.Element|null} El formulario visible o null cuando está cerrado.
   */
  function TaskFormModal({
    isOpen,
    onClose,
    isEditing,
    saveTask,
    form,
    onFormField,
    onTitleKeyDown,
    titleInputRef,
    descriptionInputRef,
    isBusy
  }) {
    if (!isOpen) return null;

    return (
      <div className="form-overlay" onClick={onClose}>
        <aside className="panel modal-panel" onClick={(event) => event.stopPropagation()}>
          <div className="modal-head">
            <h2>{isEditing ? "Editar tarea" : "Crear tarea"}</h2>
            <button className="btn ghost icon-btn" type="button" onClick={onClose} aria-label="Cerrar formulario" title="Cerrar formulario">
              <span aria-hidden="true">✕</span>
            </button>
          </div>
          <form onSubmit={saveTask} className="task-form">
            <label>Título</label>
            <input
              ref={titleInputRef}
              value={form.title}
              onChange={(event) => onFormField("title", event.target.value)}
              onKeyDown={onTitleKeyDown}
              maxLength={200}
              placeholder="Ej: Ajustar API de reportes"
              required
            />

            <label>Descripción</label>
            <textarea
              ref={descriptionInputRef}
              value={form.description}
              onChange={(event) => onFormField("description", event.target.value)}
              maxLength={1000}
              placeholder="Detalle breve del trabajo"
            />

            <div className="row two">
              <div>
                <label>Prioridad</label>
                <select value={form.priority} onChange={(event) => onFormField("priority", event.target.value)}>
                  {PRIORITY.map((priority) => (
                    <option key={priority} value={priority}>{priority}</option>
                  ))}
                </select>
              </div>

              <div>
                <label>Estado</label>
                <select
                  value={form.status}
                  onChange={(event) => onFormField("status", event.target.value)}
                  disabled={!isEditing}
                >
                  {STATUS.map((status) => (
                    <option key={status} value={status}>{STATUS_LABELS[status]}</option>
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
                  onChange={(event) => onFormField("targetStartDate", event.target.value)}
                />
              </div>

              <div>
                <label>Fecha objetivo</label>
                <input
                  type="date"
                  value={form.targetDueDate}
                  onChange={(event) => onFormField("targetDueDate", event.target.value)}
                />
              </div>
            </div>

            <label>Etiquetas (separadas por coma)</label>
            <input
              value={form.labelsText}
              onChange={(event) => onFormField("labelsText", event.target.value)}
              maxLength={350}
              placeholder="backend, api, urgente"
            />

            <small className="hint">{form.title.length}/200 caracteres</small>

            <div className="row actions">
              <button className="btn ghost" type="button" onClick={onClose} disabled={isBusy}>
                Cancelar
              </button>
              <button className="btn primary" type="submit" disabled={isBusy}>
                {isBusy ? "Guardando..." : isEditing ? "Guardar cambios" : "Crear tarea"}
              </button>
            </div>
          </form>
        </aside>
      </div>
    );
  }

  /**
   * Renderiza el modal genérico de confirmación o aviso.
   * @param {{noticeModal: object|null, onClose: Function, onDecision: Function}} props Props del modal.
   * @returns {JSX.Element|null} El modal visible o null cuando no hay aviso activo.
   */
  function NoticeModal({ noticeModal, onClose, onDecision }) {
    if (!noticeModal) return null;

    return (
      <div className="notice-overlay" onClick={onClose}>
        <aside className="panel notice-panel" onClick={(event) => event.stopPropagation()}>
          <div className="notice-head">
            <h3>{noticeModal.title}</h3>
          </div>
          <p className="notice-text">{noticeModal.message}</p>
          <div className="row actions">
            <button className="btn ghost" type="button" onClick={() => onDecision(false)} autoFocus>
              Cancelar
            </button>
            <button className="btn primary" type="button" onClick={() => onDecision(true)}>
              Aceptar
            </button>
          </div>
        </aside>
      </div>
    );
  }

  Object.assign(window.TaskTrackerUi, {
    MetricsSection,
    MetricsHelpModal,
    ContextModal,
    TaskFormModal,
    NoticeModal
  });
})();

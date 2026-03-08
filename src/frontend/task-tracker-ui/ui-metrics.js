window.TaskTrackerUi = window.TaskTrackerUi || {};

(() => {
  const { parseDateValue, startOfDay, endOfDay } = window.TaskTrackerUi;

  const FOCUS_CAPACITY = 3;
  const DELIVERY_RISK_WINDOW_DAYS = 3;
  const ACTIVITY_FEED_LOOKBACK_DAYS = 7;
  const METRIC_HELP_ITEMS = [
    {
      key: "goalCompletion",
      label: "Cumplimiento del objetivo",
      description: "Porcentaje de tareas comprometidas para este ciclo que ya fueron cerradas. Mide avance real contra el plan, no solo volumen total.",
      formula: "Formula sugerida: tareas con targetDueDate dentro del ciclo actual y status = Done / total comprometidas en el ciclo."
    },
    {
      key: "focusActive",
      label: "Foco activo",
      description: "Cantidad de tareas actualmente en ejecucion frente a la capacidad recomendada. Ayuda a detectar multitarea excesiva.",
      formula: "Formula sugerida: Doing actual / capacidad objetivo fija, por ejemplo 3."
    },
    {
      key: "deliveryRisk",
      label: "Riesgo de entrega",
      description: "Tareas no cerradas cuyo vencimiento esta proximo o vencido. Senala riesgo operativo antes de que impacte el objetivo.",
      formula: "Formula sugerida: tareas != Done con targetDueDate <= hoy + 3 dias."
    },
    {
      key: "weeklyClosure",
      label: "Cierre semanal",
      description: "Cantidad de tareas cerradas en los ultimos 7 dias. Sirve como lectura de ritmo, no de stock.",
      formula: "Si no tienes completedAt, se puede derivar desde TaskActivity."
    }
  ];

  function buildDashboardMetrics(tasks, recentActivityFeed, now = new Date()) {
    const cycleStart = new Date(now.getFullYear(), now.getMonth(), 1);
    const cycleEnd = new Date(now.getFullYear(), now.getMonth() + 1, 1);
    const riskThreshold = endOfDay(new Date(now.getFullYear(), now.getMonth(), now.getDate() + DELIVERY_RISK_WINDOW_DAYS));

    const committedThisCycle = tasks.filter((task) => {
      const dueDate = parseDateValue(task.targetDueDate || task.dueDate);
      return dueDate && dueDate >= cycleStart && dueDate < cycleEnd;
    });

    const completedThisCycle = committedThisCycle.filter((task) => task.status === "Done").length;
    const goalCompletionPercent = committedThisCycle.length === 0
      ? 0
      : Math.round((completedThisCycle / committedThisCycle.length) * 100);

    const focusActiveCount = tasks.filter((task) => task.status === "Doing").length;

    const atRiskCount = tasks.filter((task) => {
      if (task.status === "Done") return false;
      const dueDate = parseDateValue(task.targetDueDate || task.dueDate);
      return dueDate && startOfDay(dueDate) <= riskThreshold;
    }).length;

    const weeklyClosedCount = new Set(
      recentActivityFeed
        .filter((item) => item.action === "StatusChanged" && /to Done\b/i.test(item.detail || ""))
        .map((item) => item.taskId)
    ).size;

    return {
      goalCompletionPercent,
      completedThisCycle,
      committedThisCycleCount: committedThisCycle.length,
      focusActiveCount,
      focusCapacity: FOCUS_CAPACITY,
      atRiskCount,
      weeklyClosedCount
    };
  }

  function getActivityFeedFromUtc() {
    return new Date(Date.now() - ACTIVITY_FEED_LOOKBACK_DAYS * 24 * 60 * 60 * 1000).toISOString();
  }

  Object.assign(window.TaskTrackerUi, {
    FOCUS_CAPACITY,
    DELIVERY_RISK_WINDOW_DAYS,
    ACTIVITY_FEED_LOOKBACK_DAYS,
    METRIC_HELP_ITEMS,
    buildDashboardMetrics,
    getActivityFeedFromUtc
  });
})();

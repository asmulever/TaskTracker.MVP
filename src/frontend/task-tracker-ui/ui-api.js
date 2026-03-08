window.TaskTrackerUi = window.TaskTrackerUi || {};

(() => {
  const { COMMENT_IMAGE_SIZE_LABEL } = window.TaskTrackerUi;

  /**
   * Crea un cliente HTTP simple para consumir la API del tablero.
   * @param {string} apiBase URL base del backend.
   * @returns {(path: string, options?: RequestInit) => Promise<any>} Función request reutilizable.
   */
  function createApiClient(apiBase) {
    /**
     * Ejecuta una petición HTTP normalizando headers y errores frecuentes de la UI.
     * @param {string} path Ruta relativa del endpoint.
     * @param {RequestInit} [options={}] Opciones adicionales de fetch.
     * @returns {Promise<any>} El payload JSON devuelto por la API o null para 204.
     */
    return async function request(path, options = {}) {
      const headers = { ...(options.headers || {}) };
      const hasFormBody = typeof FormData !== "undefined" && options.body instanceof FormData;
      const hasExplicitContentType = Object.keys(headers).some((name) => name.toLowerCase() === "content-type");

      if (!hasFormBody && !hasExplicitContentType) {
        headers["Content-Type"] = "application/json";
      }

      const response = await fetch(`${apiBase}${path}`, {
        ...options,
        headers
      });

      const contentType = (response.headers.get("content-type") || "").toLowerCase();
      const isJsonResponse = contentType.includes("application/json") || contentType.includes("+json");

      if (!response.ok) {
        if (response.status === 413) {
          throw new Error(`La imagen supera el tamano permitido por el servidor. Usa un archivo de hasta ${COMMENT_IMAGE_SIZE_LABEL}.`);
        }

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
    };
  }

  Object.assign(window.TaskTrackerUi, { createApiClient });
})();

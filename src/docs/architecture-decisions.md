# Architecture Decisions

## Objetivo

Este documento resume las decisiones tecnicas principales que justifican la forma final del sistema y que conviene destacar durante una revision de `main_LV`.

## 1. Arquitectura en capas con DDD liviano

La solucion se organiza en:

- `Domain`
- `Application`
- `Infrastructure`
- `Api`

Motivo:

- Mantener un dominio limpio y sin dependencias de framework.
- Concentrar reglas y orquestacion en `Application`.
- Encapsular acceso a datos y Dapper en `Infrastructure`.
- Dejar a `Api` como una capa de transporte delgada.

Resultado esperado:

- Menor acoplamiento.
- Mejor testabilidad.
- Cambios de persistencia o transporte con menor impacto transversal.

## 2. Dominio simple y explicito

La entidad central es `TaskItem`, complementada por `TaskComment` y `TaskActivity`.

Criterio aplicado:

- El dominio conserva solo conceptos del negocio.
- Las transiciones y validaciones clave se resuelven en la capa de aplicacion.
- Los enums de estado y prioridad consolidan el lenguaje ubicuo.

Esto evita sobreingenieria:

- No se introduce CQRS.
- No se introduce mediator.
- No se introduce event sourcing.

## 3. TaskService como punto de orquestacion

`TaskService` concentra:

- Validaciones de entrada.
- Normalizacion de datos.
- Reglas de transicion.
- Registro de actividad.
- Coordinacion con repositorio.

Motivo:

- Evitar controllers gordos.
- Evitar reglas duplicadas entre frontend y backend.
- Facilitar pruebas unitarias sobre comportamiento real.

## 4. Repositorio con Dapper y SQL Server

La persistencia se implementa con Dapper en `TaskRepository`.

Decisiones relevantes:

- SQL explicito para control fino.
- `ISqlConnectionFactory` para centralizar conexiones.
- Repositorio como limite de infraestructura.
- Script versionado en `src/backend/database/init.sql`.

Ventajas:

- Menor magia que un ORM pesado.
- Lectura directa de queries relevantes.
- Facilidad para mapear entidades simples y consultas concretas.

Tradeoff:

- Mayor responsabilidad manual sobre SQL y evolucion del esquema.

## 5. API delgada y orientada a contratos

Los controllers:

- Reciben DTOs.
- Delegan en `ITaskService`.
- Traducen excepciones de validacion a respuestas HTTP.

Motivo:

- Separar transporte de reglas.
- Mantener endpoints predecibles.
- Facilitar evolucion del backend sin contaminar el dominio con detalles HTTP.

## 6. Estrategia de testing

La mayor parte de las pruebas se concentra en `TaskTracker.Application.Tests`.

Enfoque:

- Pruebas unitarias sobre `TaskService`.
- Repositorio fake en memoria para aislar reglas.
- Cobertura incremental de alta, consulta, actualizacion, eliminacion, comentarios y actividad.

Motivo:

- Probar comportamiento sin depender de SQL Server.
- Detectar regresiones en el lugar donde vive la logica.
- Mantener pruebas rapidas y de bajo costo.

## 7. Evolucion funcional controlada

La historia tecnica de `main_LV` muestra una progresion intencional:

1. CRUD base.
2. Prioridad y fechas.
3. Etiquetas y transiciones.
4. Comentarios y actividad.
5. Contexto rico en UI.
6. Simplificacion final del flujo.
7. Mejora visual y documentacion.

Esto es valioso para revision porque evidencia:

- Refactor antes de sumar complejidad.
- Consolidacion del flujo final despues de explorar un modelo mas amplio.
- Cierre con polish, documentacion y consistencia.

## 8. Frontend modular sin framework de build pesado

La UI usa React con HTML y JavaScript, apoyada en modulos como:

- `app.js`
- `ui-api.js`
- `ui-core.js`
- `ui-components.js`
- `ui-metrics.js`

Motivo:

- Mantener despliegue simple.
- Separar responsabilidades de estado, cliente HTTP, componentes y utilidades.
- Permitir evolucion incremental sin migrar a una toolchain compleja.

Tradeoff:

- Menor ergonomia que una aplicacion con bundler moderno.
- Mas disciplina manual para mantener modularidad.

## 9. Comentarios con imagen y decision de almacenamiento

Las imagenes de comentarios hoy se guardan en base de datos como `data URL`.

Motivo original:

- Simplificar el circuito de persistencia.
- Evitar una dependencia extra de filesystem o bucket externo.

Mitigacion aplicada:

- Validacion de tamano en cliente y servidor.
- Cambio a `multipart/form-data` para reducir friccion de upload.

Riesgo conocido:

- Para mayor escala convendria mover adjuntos a almacenamiento externo y guardar solo metadatos o URL.

## 10. Criterio de calidad que conviene remarcar

Puntos fuertes para una revision:

- Estructura de capas clara.
- Logica de negocio concentrada.
- Persistencia encapsulada.
- Pruebas unitarias utiles y no cosmeticas.
- UI desacoplada del backend por contratos.
- Refactors visibles para simplificar flujo y mejorar mantenibilidad.

## 11. Diagramas ASCII de flujos funcionales

### 11.1 Recorrido base por capas

```text
[Usuario / Browser]
        |
        v
[React UI]
        |
        v
[HTTP JSON o multipart/form-data]
        |
        v
[TasksController]
        |
        v
[ITaskService / TaskService]
        |
        v
[ITaskRepository / TaskRepository]
        |
        v
[SQL Server]
```

Descripcion:

- El frontend no conversa directo con SQL ni contiene reglas persistentes del negocio.
- La API traduce HTTP a contratos de aplicacion.
- `TaskService` centraliza validacion, normalizacion y eventos de actividad.
- `TaskRepository` encapsula Dapper y el acceso fisico a base de datos.

### 11.2 Carga inicial del tablero y metricas

```text
[App mount]
    |
    v
[loadTasks()]
    |
    +--> GET /tasks ------------------------------+
    |                                             |
    +--> GET /tasks/activity-feed?fromUtc=...     |
                                                  v
                                           [TasksController]
                                                  |
                                           [TaskService]
                                                  |
                                           [TaskRepository]
                                                  |
                                              [SQL Server]
                                                  |
                         +------------------------+
                         |
                         v
             [Normalizacion en UI + buildDashboardMetrics()]
                         |
                         v
                  [Render del tablero]
```

Descripcion:

- La pantalla principal levanta dos conjuntos de datos en paralelo: tareas y actividad reciente.
- La UI normaliza estados, prioridades y etiquetas antes de renderizar.
- Las metricas operativas se calculan en cliente a partir de los datos ya obtenidos, sin abrir una dependencia extra en backend.

### 11.3 Creacion y edicion de tareas

```text
[Usuario envia formulario]
          |
          v
       [saveTask()]
          |
          +--> valida titulo y arma payload
          |
          +--> POST /tasks --------------------------+
          |                                          |
          +--> PUT /tasks/{id}                       |
          |                                          v
          |                                   [TasksController]
          |                                          |
          |                                   [TaskService]
          |                                          |
          |                             valida + normaliza + persiste
          |                                          |
          |                                   [TaskRepository]
          |                                          |
          |                                      [SQL Server]
          |
          +--> si cambia el estado en edicion
                   |
                   +--> warning UI para movimiento hacia atras
                   |
                   +--> PATCH /tasks/{id}/status
```

Descripcion:

- La UI hace validacion minima para no enviar formularios vacios.
- La validacion fuerte y la normalizacion final viven en `TaskService`.
- Durante edicion, el cambio de datos y el cambio de estado siguen rutas separadas para mantener responsabilidad clara.
- Cuando el usuario mueve un estado hacia atras, la confirmacion sucede en UI antes de ejecutar el `PATCH`.

### 11.4 Cambio de estado desde acciones directas o drag and drop

```text
[Click en accion] o [Drag & Drop]
           |
           v
[updateTaskStatus()] o [moveTaskToStatus()]
           |
           +--> valida transicion visual
           +--> muestra warning si aplica
           |
           +--> actualizacion optimista en drag & drop
           |
           +--> PATCH /tasks/{id}/status
                    |
                    v
               [TaskService]
                    |
                    +--> persiste nuevo estado
                    +--> registra actividad StatusChanged
                    |
                    v
               [SQL Server]
                    |
          +---------+----------+
          |                    |
          v                    v
     [success]             [error]
          |                    |
   UI mantiene cambio     UI hace rollback
   y muestra toast        y restaura estado previo
```

Descripcion:

- El tablero soporta cambio de estado desde botones o arrastre de tarjeta.
- En drag and drop la UI aplica optimismo local para mejorar respuesta visual.
- Si el `PATCH` falla, el estado anterior se restituye para no dejar inconsistencias visuales.
- Cada cambio exitoso registra actividad para sostener trazabilidad.

### 11.5 Carga del contexto de una tarea

```text
[Usuario abre modal de contexto]
             |
             v
     [openContextModal(taskId)]
             |
             v
   [selectedTaskId cambia en React]
             |
             v
      [loadTaskContext(taskId)]
             |
             +--> GET /tasks/{id}/comments ----+
             |                                 |
             +--> GET /tasks/{id}/activity ----+
                                               |
                                               v
                                        [TasksController]
                                               |
                                        [TaskService]
                                               |
                                        [TaskRepository]
                                               |
                                           [SQL Server]
                                               |
                    +--------------------------+
                    |
                    v
      [setComments() + setActivity() + render tabs]
```

Descripcion:

- El contexto se carga bajo demanda para no penalizar la pantalla principal.
- Comentarios y actividad viajan por endpoints separados pero se consultan en paralelo.
- Si el `taskId` es invalido o una llamada falla, la UI limpia el contexto y muestra feedback al usuario.

### 11.6 Comentarios con imagen por multipart

```text
[Usuario escribe comentario]
          |
          +--> selecciona archivo o pega imagen
                    |
                    v
            [attachCommentImage(file)]
                    |
                    +--> valida image/*
                    +--> valida <= 2 MB
                    +--> FileReader -> data URL para preview
                    |
                    v
               [commentImage listo]
                    |
                    v
               [addComment()]
                    |
                    +--> arma FormData(content, image)
                    +--> POST /tasks/{id}/comments
                              (multipart/form-data)
                    |
                    v
            [TasksController.AddCommentForm]
                    |
                    +--> MapCommentFormRequestAsync()
                    |       archivo -> MemoryStream -> data URL
                    |
                    v
             [TaskService.AddCommentAsync]
                    |
                    +--> valida contenido
                    +--> valida data URL
                    +--> valida <= 2 MB
                    +--> recorta nombre si hace falta
                    +--> persiste comentario
                    +--> registra actividad CommentAdded
                    |
                    v
                 [SQL Server]
```

Descripcion:

- La UI valida formato y tamano antes de enviar para evitar fallas evitables.
- El upload sale como `multipart/form-data`, pero el backend transforma la imagen a `data URL` antes de delegar en Application.
- `TaskService` vuelve a validar para no confiar en el cliente.
- El comentario y su imagen quedan almacenados en base de datos, y la actividad queda registrada como evento independiente.

### 11.7 Eliminacion de tarea

```text
[Usuario confirma eliminacion]
           |
           v
      [deleteTask(id)]
           |
           +--> DELETE /tasks/{id}
                     |
                     v
                [TaskService]
                     |
                     +--> borra tarea
                     +--> registra actividad TaskDeleted
                     |
                     v
                [SQL Server]
                     |
                     v
     [UI cierra modales relacionados y recarga tablero]
```

Descripcion:

- La eliminacion exige confirmacion previa en UI.
- Tras un borrado exitoso se limpian modal de formulario y modal de contexto si estaban abiertos sobre la misma tarea.
- La recarga posterior evita que queden referencias obsoletas en memoria del cliente.

## 12. Limitaciones honestas

Tambien conviene explicitar los limites actuales:

- El storage de imagenes en SQL no es la solucion ideal para alta escala.
- La UI sigue siendo una SPA ligera sin pipeline de build avanzado.
- El proyecto no introduce autenticacion ni autorizacion.
- Existe espacio para sumar pruebas de integracion end-to-end.

Estas limitaciones no invalidan la arquitectura elegida; simplemente marcan una hoja de ruta razonable para una siguiente iteracion.

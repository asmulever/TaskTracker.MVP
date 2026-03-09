# Main LV Review Timeline

## Proposito

Este documento acompana la rama `main_LV` como material de revision tecnica.
La rama fue reconstruida con una historia curada para que  puedan revisar la evolucion funcional del sistema en un orden logico.

Importante:

- La secuencia de abajo describe una cronologia de trabajo estimada y defendible, no un registro horario de auditoria.
- La referencia principal para revisar la progresion es `git log --reverse --oneline main_LV`.

## Resumen ejecutivo

La construccion puede leerse como un trabajo de 4 jornadas tecnicas:

1. Fundacion de arquitectura y backend base.
2. Primer frontend usable y cobertura inicial de pruebas.
3. Evolucion funcional del dominio y contexto de tarea.
4. Consolidacion del flujo final, experiencia visual y documentacion.

## Mapa visual de evolucion

```text
[Dia 1]
Arquitectura base
   |
   v
DDD liviano + API + SQL inicial
   |
   v
[Dia 2]
UI operable + tests de Application
   |
   v
CRUD validado de punta a punta
   |
   v
[Dia 3]
Modelo enriquecido
   |
   +--> prioridad
   +--> fechas objetivo
   +--> etiquetas
   +--> comentarios
   +--> actividad
   |
   v
Contexto funcional por tarea
   |
   v
[Dia 4]
Flujo final Todo/Doing/Done
   |
   +--> metricas operativas
   +--> imagenes en comentarios
   +--> polish visual
   +--> documentacion tecnica
   |
   v
Estado final revisable
```

Descripcion:

- El recorrido empieza con la estructura de capas y el backend minimo viable.
- En la segunda etapa el producto ya se puede usar y probar con cobertura unitaria sobre la capa de aplicacion.
- La tercera etapa agrega profundidad funcional real sobre la entidad tarea.
- La cuarta etapa consolida el flujo operativo definitivo y cierra detalles de UX, adjuntos y mantenibilidad.

## Cronologia estimada por fase

### Dia 1 - Base arquitectonica y backend inicial

Esfuerzo estimado: 7 a 8 horas.

- `bc632f8` Inicializa reglas de versionado y estructura minima.
- `03dd958` Crea la solucion y los proyectos base del backend.
- `e876081` Modela el nucleo del dominio de tareas.
- `08c7608` Define contratos y DTOs de la capa de aplicacion.
- `ea2c797` Implementa TaskService con las reglas basicas de negocio.
- `424a6a4` Integra Infrastructure con Dapper y acceso SQL.
- `033a3a7` Expone la API REST y el esquema inicial de base de datos.
- `b6995ad` Habilita OpenAPI y Swagger para desarrollo.

Lectura tecnica:

- Primero se estabiliza la estructura de capas.
- Luego se baja la logica a Application.
- Finalmente se conectan API, SQL Server y script de inicializacion.

Flujo dominante del dia:

```text
[Client HTTP]
    |
    v
[TasksController]
    |
    v
[TaskService]
    |
    v
[TaskRepository]
    |
    v
[SQL Server]
```

Descripcion:

- La primera jornada establece el camino base de una operacion de negocio.
- El objetivo es que toda accion pase por capas bien delimitadas y sin mezclar reglas de dominio con detalles HTTP o SQL.

### Dia 2 - Frontend inicial y red de pruebas

Esfuerzo estimado: 6 a 7 horas.

- `72f9337` Publica el frontend React con tablero inicial.
- `0bbb163` Crea la base de pruebas de Application.
- `f8cd5a1` Cubre la creacion de tareas y sus validaciones.
- `d3470f6` Completa pruebas de consulta, actualizacion y eliminacion.

Lectura tecnica:

- El sistema deja de ser solo backend y pasa a ser usable.
- Se agrega una base de tests sobre la capa Application para validar reglas sin depender de SQL.
- La cobertura inicial se concentra en el CRUD principal antes de extender el modelo.

Flujo dominante del dia:

```text
[Usuario]
    |
    v
[React App]
    |
    +--> GET /tasks
    |
    +--> POST /tasks
    |
    +--> PUT /tasks/{id}
    |
    +--> DELETE /tasks/{id}
    |
    v
[TaskService con pruebas unitarias]
```

Descripcion:

- La aplicacion web pasa a ser un cliente real del backend.
- La red de pruebas se monta sobre `TaskService` para sostener el CRUD antes de avanzar con nuevas capacidades.

### Dia 3 - Evolucion del modelo y capacidad operativa

Esfuerzo estimado: 7 a 8 horas.

- `7b9c6d7` Extiende el modelo con prioridad y fechas objetivo.
- `7eee63f` Alinea la UI con la ampliacion del flujo de estados.
- `a73b73b` Agrega etiquetas y reglas de transicion de tareas.
- `283800a` Evoluciona el kanban a una version mas rica en interaccion.
- `154383f` Incorpora comentarios por tarea en backend y persistencia.
- `83ecd1a` Registra historial de actividad por tarea.
- `22292e1` Suma un panel contextual de comentarios y actividad.

Lectura tecnica:

- La tarea pasa de un CRUD simple a una unidad de trabajo mas rica.
- Se agregan capacidades que un equipo realmente usa: prioridad, fechas, etiquetas, comentarios y trazabilidad.
- La UI acompana esa evolucion sin mover reglas de negocio al cliente.

Flujo dominante del dia:

```text
[TaskItem]
   |
   +--> prioridad
   +--> fechas
   +--> etiquetas
   +--> comentarios
   +--> actividad
   |
   v
[Contexto de tarea mas rico]
   |
   v
[Decision operativa mejor informada]
```

Descripcion:

- En esta etapa la tarea deja de ser un registro plano.
- El valor aparece en el contexto adjunto: comentarios para colaboracion y actividad para trazabilidad.

### Dia 4 - Consolidacion del flujo final y polish

Esfuerzo estimado: 6 a 7 horas.

- `d4f08c8` Simplifica el flujo al modelo final Todo Doing Done.
- `2861aef` Refina cabecera del tablero y feedback no intrusivo.
- `657a9f5` Ajusta el formulario contextual y la presentacion de comentarios.
- `24823ee` Reemplaza indicadores por metricas operativas y ayuda contextual.
- `98ef9e4` Soporta carga multipart de imagenes en comentarios.
- `5bd0a50` Refina temas visuales y animaciones del tablero.
- `c591b61` Documenta metodos y funciones del sistema.
- `4240c24` Alinea defaults y guia local con la version final.

Lectura tecnica:

- Se reduce complejidad operacional al flujo final de tres estados.
- Se resuelven puntos de uso real como adjuntos, contexto, observabilidad de tablero y consistencia visual.
- El cierre incluye documentacion de codigo y ajuste fino de defaults.

Flujo dominante del dia:

```text
[Tablero final]
   |
   +--> Todo
   +--> Doing
   +--> Done
   |
   +--> metricas
   +--> contexto
   +--> adjuntos
   +--> tema claro/oscuro
   |
   v
[Producto listo para revision]
```

Descripcion:

- La ultima jornada no agrega solo cosmetica.
- Tambien cierra decisiones funcionales del flujo, resuelve upload de imagenes y deja material tecnico entendible para revision.

## Distribucion orientativa de esfuerzo

La siguiente guia sirve para contextualizar el tipo de trabajo por bloque:

- Tareas de estructura, scaffolding y wiring: 2 a 4 horas.
- Casos de uso y persistencia con reglas de negocio: 4 a 8 horas.
- Evoluciones de UI con estado, contexto y formularios: 3 a 6 horas.
- Testing unitario focalizado: 2 a 4 horas.
- Pulido visual, accesibilidad y documentacion: 2 a 4 horas.

## Como presentar la rama en una revision

Mensaje sugerido:

`main_LV` representa una historia curada de construccion tecnica. No intenta reemplazar trazabilidad temporal real; organiza el trabajo en una progresion que permite revisar arquitectura, decisiones de dominio, cobertura y refinamientos de UX en un orden legible.

## Checklist para reviewer

- Confirmar separacion de capas `Domain`, `Application`, `Infrastructure` y `Api`.
- Revisar que la mayor parte de las reglas vive en `TaskService`.
- Verificar que Dapper queda encapsulado dentro de `Infrastructure`.
- Revisar cobertura unitaria de alta, consulta, actualizacion, eliminacion, comentarios y actividad.
- Validar que el frontend consuma API sin introducir reglas de negocio duplicadas.
- Confirmar que el estado final del producto coincide con `main_V2`, salvo archivos ignorados por `.gitignore`.

## Referencia a diagramas funcionales detallados

Los diagramas ASCII de mayor profundidad quedaron documentados en:

- `src/docs/architecture-decisions.md`

Ese documento incluye:

- recorrido por capas
- carga inicial del tablero
- creacion y edicion de tareas
- cambio de estado con confirmacion y rollback
- carga de contexto por tarea
- alta de comentarios con imagen via multipart

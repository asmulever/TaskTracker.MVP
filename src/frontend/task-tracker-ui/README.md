# Task Tracker UI

Con el devcontainer activo, la infraestructura levanta:

- Frontend (nginx)
- Backend (.NET)
- SQL Server

URLs desde host:

- Frontend: `http://localhost:${HOST_UI_PORT}`
- API directa: `http://localhost:${HOST_API_PORT}`

Si esos puertos estan ocupados, configura:

- `HOST_UI_PORT`
- `HOST_API_PORT`

en `.devcontainer/.env`.

La UI usa proxy hacia el backend en `/tasks`, `/openapi` y `/swagger`, asi que no depende del puerto directo de la API para funcionar en el navegador.

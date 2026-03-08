# Task Tracker UI

Con el devcontainer activo, la infraestructura levanta:

- Frontend (nginx)
- Backend (.NET)
- SQL Server
- Gateway nginx para exponer al host

URLs desde host:

- UI + API en mismo origen: `http://localhost:5274`
- API directa (sin frontend): `http://localhost:8080`

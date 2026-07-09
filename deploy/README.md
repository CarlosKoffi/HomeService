# HomeService Deployment

Base cible: .NET, PostgreSQL et Coolify.

Variables importantes:

- `ConnectionStrings__DefaultConnection`
- `ASPNETCORE_ENVIRONMENT`

En local, lancer PostgreSQL:

```powershell
docker compose -f deploy/docker-compose.yml up -d
```

Services exposes en local:

- API: `http://localhost:8080`
- Portail entreprise: `http://localhost:8081`

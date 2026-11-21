# Copilot Instructions — Plantilla de Intranet (MemByte)

> **Convenio de primero:** estas instrucciones rigen cómo debe actuar el agente en esta solución. **Prioriza la seguridad:** un cambio que pueda enmascarar un riesgo (contraseñas más débiles, warnings ignorados, validaciones relajadas, telemetría de seguridad desactivada) es una devolución técnica, no una mejora. Al personalizar la plantilla para un cliente, **no se debe de-hardening ningún valor** a menos que haya una razón documentada; si se cambia un valor relevante de seguridad, anotar la razón en el commit.
>
> **Aislamiento de la plantilla:** en IM-template, el usuario quiere aislamiento total en un esquema dedicado de Supabase para la plantilla y evitar usar/compartir el esquema de producción de MemByte. **Ningún dato, secreto ni credencial de otras instalaciones se reutiliza ni se copia.**

## 1. Contexto: plantilla reutilizable para cualquier cliente

- Intranet empresarial genérica. Cada empresa obtiene su propio repositorio/instalación: base de datos PostgreSQL propia, usuarios propios, secretos propios, adjuntos propios, Data Protection keys propias, telemetría propia.
- No hay multiempresa a nivel de datos: no compartir instalaciones ni bases de datos entre empresas.
- Al adaptar la plantilla: los nombres técnicos `MemByte.*` (ensamblados, migraciones, tablas) se conservan para no modificar ensamblados ni migraciones; no determinan la identidad visible de la empresa.
- La primera interacción de personalización es el **Checklist de sección 9**.

## 2. Stack y convenciones observadas (deben seguirse)

- .NET 10 LTS (`net10.0`), C# 13; Blazor Web App SSR + interactividad por componente (`InteractiveServer`); Entity Framework Core 10 (Npgsql → PostgreSQL); MudBlazor; xUnit.
- Arquitectura Clean: `Domain` (entidades/value objects/reglas del dominio) → `Application` (interfaces de servicio, DTOs, requests, opciones) → `Infrastructure` (EF Core, Identity, implementaciones) → `Web` (Blazor, Program.cs DI root).
- Observaciones del código existente:
  - Tabs para la indentación.
  - Nombres: `XxxDto`, `XxxRequest`, `XxxOperationResult` (`Success`/`Failure`), `IXxxService`/`XxxService`; namespaces `MemByte.*`.
  - Comentarios XML en tipos/miembros públicos; comentarios en español; mensajes de usuario y auditoría en español; identificadores en inglés.
  - `Nullable=enable`, `ImplicitUsings=enable`.
  - Resolución del usuario actual siempre desde `UserManager`/`AuthenticationStateProvider` — nunca confiar en parámetros de UI.
  - Sin repositorio genérico adicional: el `MemByteDbContext` se usa directamente en servicios.
  - `CancellationToken` en todos los métodos async.
  - `AsNoTracking()` en consultas de lectura; paginación servidor con `Math.Clamp` de `pageSize`; `HasMaxLength` en entidades.
  - Capturar `ArgumentException`/`InvalidOperationException` del dominio y mapear a `OperationResult.Failure(...)`.

## 3. Reglas de seguridad — PRIORIDAD ABSOLUTA

### 3.1 Autenticación y autorización

- **Login:** credenciales (email/contraseña) sobre ASP.NET Core Identity; lockout: 5 intentos fallidos → 15 min; `LockoutAllowedForNewUsers=true`; `RequireConfirmedEmail=false` (ajustable si se activa confirmación de email).
- **Política de contraseñas:** mínimo 12 caracteres, mayúscula, minúscula, dígito y carácter no alfanumérico; PBKDF2-HMAC-SHA256 a 210.000 iteraciones.
- **Validadores de contraseña adicionales:** lista blanca de contraseñas filtradas conocidas (`BreachedPasswordValidator` — ampliar con la API HaveIBeenPwned antes de producción); no reutilizar las últimas 5 contraseñas (`PasswordHistoryValidator`; `ApplicationUserManager` registra automáticamente el hash anterior tras cambio/reset).
- **Cookies de aplicación:** `HttpOnly`, `SecurePolicy.Always`, `SameSite=Strict`, expiración 60 min + sliding; LoginPath `/login`; AccessDeniedPath `/access-denied`; redirecciones 401 (sin HTML) para `/api`.
- **Antifraud:** `app.UseAntiforgery()` (`app.UseAntiforgery()`); logout como endpoint POST (no componente Blazor).
- **Rate limiting:** fixed window, 10 peticiones/min/IP en POST `/login`, `/mfa`, `/logout`; 429 al exceder.
- **Revocación de acceso:** `SetActiveAsync(false)` revoca sesiones activas y actualiza el SecurityStamp; reseteo de contraseña de Admin genera token de un solo uso de 24h.

### 3.2 Gestión de secretos

- Nada de cadenas de conexión, claves ni credenciales en `appsettings.json` ni en código fuente.
- Desarrollo: `dotnet user-secrets` (propios por instalación; desactivar tras el primer arranque). Producción: Key Vault + Managed Identity; el arranque falla rápido si falta la cadena (`ConnectionStrings:IMtemplate`).
- **Discrepancia observada:** el comentario y el mensaje de error de `Program.cs` dicen `ConnectionStrings:Default`, pero el código ejecuta `GetConnectionString("IMtemplate")`; la clave efectiva es `ConnectionStrings:IMtemplate`. Al personalizar: setear `IMtemplate` y comprobar que no haya clave `ConnectionStrings:Default` olvidada en secrets.
- Data Protection: claves persistidas fuera de memoria (prod: `/home/.dataprotection-keys`; local: dentro del ContentRootPath); `DataProtection:ApplicationName` estable por instalación — cambiarlo invalida cookies y tokens existentes.

### 3.3 Cabeceras y transporte

- HTTPS obligatorio (`UseHttpsRedirection`); HSTS en producción.
- Cabeceras: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: camera=(), microphone=(), geolocation=()`, CSP (solo `style-src 'unsafe-inline'` por Blazor inline), `base-uri 'self'`, `form-action 'self'`, `frame-ancestors 'none'`.

### 3.4 Isolación por instalación (clientes) y datos

- El aislamiento de datos es por `ClientId`: `Conversation.ClientId`, `Order.ClientId`, `CatalogItem.ClientId`, etc. Los FKs a tablas de historial (`Conversations`, `Orders`, `OrderLines`, `CatalogItems`) usan `DeleteBehavior.Restrict`; un cliente no puede borrase mientras tenga datos asociados.
- Regla de negocio en servicios: el usuario se resuelve en servidor; `MessagingUserContextProvider` re-lee el usuario de DB en cada petición (cambios de lockout/cliente efectivos sin esperar a la caducidad de la cookie).
- Denegaciones: mensajes idénticos para "no existe" y "fuera de tu ámbito" (sin confirmar existencia de conversaciones ajenas); nunca filtrar por nombre de remitente ni devolver datos de otros clientes.
- Validación siempre antes de persistir (entidades con `ArgumentException` → `OperationResult.Failure`); paginación en servidor.

### 3.5 Auditoría, telemetría y logs

- Cada evento de autenticación, MFA, logout y cambios de permisos se registra en `AuthAuditEntries` (+ telemetría estructurada). Cada acción de mensajería en `MessagingAuditEntries`, incluido el acceso denegado (es señal de intento de acceso cruzado entre clientes).
- Logs estructurados (Application Insights/KQL): tipos anómalos (`LoginFailed`, `AccountLockedOut`, `MfaInvalidCode`, `InvalidRecoveryCode`) en `Warning`. No loguear secretos ni emails innecesarios; mensajes en español.

## 4. Patrones de actuación al añadir o modificar código

- Módulo nuevo: seguir `Domain/Application/Infrastructure` (interfaces en Application, implementación en Infrastructure, registro en `Program.cs`, validación de opciones en `IOptions<XxxOptions>` con `ValidateDataAnnotations().ValidateOnStart()` para fallar rápido en el arranque).
- Validación de opciones: siempre `ValidateOnStart()` para que fallen rápido en producción.
- Pruebas: unitarias por capa en `tests/` (xUnit). **Cobertura de código ≥ 70% en capa de dominio y aplicación** (KPI del proyecto). Añadir pruebas de validadores de seguridad al cambiar reglas de contraseñas o crear nuevos validadores de autenticación.
- CI: `dotnet build` (warnings as errors), `dotnet test --collect:"XPlat Code Coverage"` (Codecov), `dotnet list package --vulnerable --include-transitive`, validación de Bicep, `docker build` + smoke test, TruffleHog secret scanning.

## 5. Checklist de personalización al copiar la plantilla

1. [ ] Repositorio independente + instalación separada: base de datos PostgreSQL exclusiva, credenciales, usuarios, adjuntos de mensajería y Data Protection keys propios.
2. [ ] `Intranet.ApplicationName` y `Intranet.CompanyName` en `appsettings.json` (≤100 caracteres); variables de entorno `INTRANET_APPLICATION_NAME` e `INTRANET_COMPANY_NAME` como alternativa.
3. [ ] Cadena de conexión en `ConnectionStrings:IMtemplate` (ver discrepancia en 3.2).
4. [ ] Tema visual adaptado si se desea: `src/MemByte.Web/Components/MemByteTheme.cs` + estilos de `app.css`.
5. [ ] Sin seed de Admin activo por defecto: `SeedAdmin.Enabled=false`; en producción crear el primer Admin desde el módulo de Gestión de Usuarios (`UserService.CreateAsync`); desactivar tras el primer arranque.
6. [ ] Roles: usar los del dominio (`Admin`, `AreaManager`, `PurchasingAgent`, `Support`, `Employee` — ver `ApplicationRoleNames`); si el cliente necesita más, añadirlos a `ApplicationRoleNames` y a `agents.md`.
7. [ ] Actualizar `agents.md` si se añaden módulos, permisos o cambian flujos (documento vivo de la plantilla).
8. [ ] Ejecutar `dotnet build`, `dotnet test` y smoke-test de `/health` tras la personalización.
9. [ ] Revisar que no hay datos/secretos/credenciales de la instalación de MemByte.

## 6. Reglas duras y normas de estilo por defecto

- **No de-hardening** (sección 3): no debilitar validaciones, cookies, políticas de contenido, rate limiting o storage de secretos sin una razón documentada.
- **Hard lines:** no existe conexión a credenciales ni datos de otras empresas en la plantilla; todo dato de otro tenant se debe dejar fuera y aislar con `ClientId`.
- **Normas observadas:** tabuladores; convenciones de nomenclatura `XxxDto`/`XxxRequest`/`XxxOperationResult`/`IXxxService`; `MemByte.*` para ensamblados y namespaces; inglés para identificadores, español para mensajes y comentarios; `CancellationToken` obligatorio en async; `AsNoTracking()` en lecturas; paginación servidor; XML docs en API pública.
- Antes de implementar: cerrar la ambigüedad con la persona; hacer cambios reversibles; no hacer cambios fuera del pedido.



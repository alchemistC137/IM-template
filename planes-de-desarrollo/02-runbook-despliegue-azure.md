# Plantilla de intranet — Runbook de despliegue en Azure (Bicep + Docker)

> **Documento vivo.** Procedimiento de referencia para aprovisionar con Bicep y ejecutar
> la imagen .NET 10 en App Service Linux. Sustituye el despliegue ZIP y los perfiles de
> publicación. El esquema y los datos de PostgreSQL/Supabase siguen siendo externos.
> Las instrucciones describen el nuevo flujo; no implican que se haya aplicado en Azure.

## 1. Arquitectura y costes

| Recurso | Declaración y propósito |
|---|---|
| App Service Plan Linux | Recurso existente referenciado por `infra/main.bicep`; la validación comparte temporalmente el plan de MemByte y no modifica SKU/capacidad. Una instalación cliente crea el suyo con `infra/bootstrap.bicep`. |
| App Service | Aplicación distinta, Docker Linux/amd64, puerto 8080, HTTPS obligatorio, TLS 1.2+, WebSockets y afinidad para Blazor Server. FTP y publicación básica SCM deshabilitados. |
| ACR | Recurso existente. La imagen se publica en el repositorio `intranet` con un token ACR limitado a ese repositorio; el token de lectura de la app es distinto y solo tiene lectura de `intranet`. |
| Identidad administrada de ejecución | `<appName>-runtime`, exclusiva de esta aplicación. Puede ser técnicamente user-assigned y compartirse, pero no se reutiliza la identidad de MemByte; sus accesos de Key Vault se limitan a secretos concretos. |
| Key Vault | Recurso existente compartido temporalmente. La app solo recibe `Key Vault Secrets User` sobre su secreto de conexión, su secreto de lectura ACR y, opcionalmente, el secreto de adjuntos. |
| PostgreSQL / Supabase | No se crea ni modifica con esta plantilla. La cadena apunta a un esquema Supabase propio, usando `Search Path` explícito; no se usa el esquema de producción de MemByte. |
| Log Analytics / Application Insights — **opcional** | No se crea ni se configura en el flujo de validación. Si se habilita, usar telemetría aislada para la instalación. |

Para validar esta plantilla se comparten solo el grupo de recursos, el plan, ACR y Key Vault.
No se comparte la app, la identidad de ejecución, los secretos, las claves de Data Protection
ni el esquema de base de datos. `infra/main.bicep` solo referencia el plan, ACR y vault; CD
solo cambia la imagen de la app existente. No ejecuta un despliegue de grupo ni modifica esos
recursos. El secreto de conexión por defecto es `ConnectionStrings--IntranetTemplate`.

Cada cliente debe crear un repositorio, grupo de recursos, base de datos/esquema, plan, ACR,
Key Vault, identidades y secretos propios. `infra/bootstrap.bicep` crea los recursos de apoyo
para una instalación cliente nueva; nunca ejecutar ese bootstrap sobre el grupo compartido
con MemByte. Esta plantilla no migra datos ni configuraciones de otra instalación.

## 2. Prerrequisitos y bootstrap

- Azure CLI con Bicep, Docker con motor Linux y .NET 10 SDK para validación local.
- Suscripción Azure y permisos para crear o consultar los recursos que correspondan.
- GitHub Actions habilitado y un environment `production`; restringir sus ramas de
  despliegue mediante **Selected branches and tags → Branch → master**.
  No usar **Protected branches only** si no existen ramas protegidas.
  Los revisores obligatorios de entornos no están disponibles en repositorios privados
  con planes Free, Pro o Team; el control operativo se realiza con el inicio manual
  del workflow. No equivale a una aprobación independiente ni impide que alguien con
  permisos de escritura cambie el workflow; limitar el acceso al repositorio.

Ejecutar desde la raíz del repositorio en PowerShell:

```powershell
az login --use-device-code
az account set --subscription "<SUBSCRIPTION_ID>"
az bicep version
$rg = "rg-intranet-ejemplo"
$location = "spaincentral"
$env:AZURE_APP_NAME = "<nombre-global-unico-del-sitio>"
$env:AZURE_PLAN_NAME = "<nombre-del-plan>"
$env:AZURE_KEY_VAULT_NAME = "<nombre-global-unico-del-vault>"
$env:INTRANET_APPLICATION_NAME = "Portal del equipo"
$env:INTRANET_COMPANY_NAME = "Empresa de ejemplo"
az group create --name $rg --location $location

# Una vez por suscripción. Esperar a registrationState = Registered.
# Microsoft.OperationalInsights / Microsoft.Insights solo son necesarios si se habilita
# telemetría/App Insights (opcional, no despliegue por defecto).
foreach ($provider in @("Microsoft.Web", "Microsoft.ContainerRegistry", "Microsoft.KeyVault", "Microsoft.ManagedIdentity", "Microsoft.OperationalInsights", "Microsoft.Insights")) {
    az provider register --namespace $provider --wait
}
```

Estos comandos de creación de grupo y bootstrap son solo para instalaciones de clientes.
En la validación de esta plantilla, omitirlos y usar el grupo existente de MemByte.

Si Spain Central devuelve cuota Linux 0 o un servicio no está disponible, detener el
despliegue y revisar las cuotas/disponibilidad antes de elegir otra región. No cambiar
de región automáticamente. No se puede mover un sitio o plan existente simplemente
cambiando `location`.

### Identidad OIDC de despliegue

La identidad de GitHub Actions es distinta de la identidad administrada de ejecución.
Una identidad user-assigned podría asociarse a varias apps, pero se recomienda mantener
una identidad runtime propia por aplicación para evitar compartir permisos.
Para el entorno de validación, usar una identidad OIDC propia de este repositorio y
concederle `Website Contributor` únicamente sobre el App Service de la plantilla; no
otorgar `Contributor` ni permisos de asignación RBAC en el grupo compartido. El token ACR
de escritura se limita por scope map al repositorio `intranet` y se guarda como secretos
del environment; la identidad OIDC no recibe acceso de escritura al ACR ni a Key Vault.

Para cada instalación cliente nueva, usar una identidad OIDC exclusiva y aplicar los
mismos permisos limitados a su App Service. No reutilizar la identidad de MemByte ni
asignarle roles de grupo/suscripción. El aprovisionamiento inicial de la app y sus
asignaciones RBAC se ejecuta una vez por un operador autorizado; CD no ejecuta Bicep.
Después del aprovisionamiento inicial, asignar `Website Contributor` a la identidad OIDC
sobre el ID del App Service, nunca sobre el grupo compartido. Definir el Client ID de la
aplicación Entra creada para GitHub Actions y resolver su objeto de service principal:

```powershell
$clientId = "<client-id-de-la-identidad-oidc>"
$principalId = az ad sp show --id $clientId --query id --output tsv
if (-not $principalId) { throw "No se encontró el service principal de la identidad OIDC." }
$appId = az webapp show --resource-group $rg --name $env:AZURE_APP_NAME --query id --output tsv
if (-not $appId) { throw "No se encontró el App Service de la plantilla." }
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal `
    --role "Website Contributor" --scope $appId
```

En Entra → App registrations → esa aplicación → Federated credentials, añadir:

| Campo | Valor |
|---|---|
| Issuer | `https://token.actions.githubusercontent.com` |
| Subject | `repo:<OWNER>/<REPO>:environment:production` (formato estándar; verificar el formato del repositorio destino) |
| Audience | `api://AzureADTokenExchange` |

Consultar la configuración OIDC del repositorio destino antes de crear la confianza.
Si usa sujetos con IDs inmutables, emplear su propietario e ID de repositorio reales
en vez del formato estándar anterior. No copiar el sujeto de este repositorio ni
los IDs de otro cliente; no son los IDs de la aplicación Entra.

En GitHub → Settings → Environments → `production` → Variables y Secrets:

| Variable/secret | Contenido |
|---|---|
| `AZURE_RESOURCE_GROUP` | Grupo que contiene el App Service |
| `AZURE_APP_NAME` | Nombre del App Service de la plantilla |
| `AZURE_REGISTRY_SERVER` | Host ACR, por ejemplo `acrintranet.azurecr.io` |
| `AZURE_CLIENT_ID` | Client ID de la identidad OIDC |
| `AZURE_TENANT_ID` | Tenant ID de Entra |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID de Azure |
| `ACR_USERNAME` (secret) | Nombre del token ACR de escritura limitado a `intranet` |
| `ACR_PASSWORD` (secret) | Contraseña nueva de ese token |

Para el aprovisionamiento inicial, define también `AZURE_PLAN_NAME`,
`AZURE_KEY_VAULT_NAME`, `AZURE_REGISTRY_NAME`, `AZURE_CONNECTION_SECRET_NAME`,
`AZURE_REGISTRY_PULL_TOKEN_NAME` y `AZURE_REGISTRY_PULL_SECRET_NAME`. Estos son valores
para Bicep, no se usan en CD. No guardar secretos ACR en variables ni en Git. No se usa
`AZURE_PUBLISH_PROFILE`.

## 3. Secretos y primer aprovisionamiento

No introducir connection strings, tokens ni contraseñas en Bicep, parámetros,
workflows ni imágenes. Para esta validación, crear en el Key Vault compartido un secreto
nuevo llamado `ConnectionStrings--IntranetTemplate`; su valor debe ser una conexión a
la base Supabase de prueba e incluir `Search Path=<esquema_dedicado>`. Usar un rol de base
de datos exclusivo para la plantilla, con permisos solo sobre ese esquema: `Search Path`
selecciona dónde resolver nombres, pero no restringe el acceso a otros esquemas. No usar
un rol privilegiado compartido ni reutilizar `ConnectionStrings--IMtemplate` o secretos de
MemByte. Verificar `select current_schema()` y el usuario conectado antes de migrar.

Para instalaciones cliente nuevas, crear primero un grupo propio y aprovisionar sus
recursos de apoyo. **No ejecutar este bootstrap en el grupo compartido con MemByte.**
La validación ya tiene plan, ACR y Key Vault existentes; no los aprovisiona ni actualiza.

```powershell
$env:AZURE_PLAN_NAME = "<plan-propio>"
$env:AZURE_KEY_VAULT_NAME = "<vault-propio>"
$env:AZURE_REGISTRY_NAME = "<acr-propio>"
az deployment group create --resource-group $rg --name support-resources `
    --template-file infra\bootstrap.bicep `
    --parameters planName=$env:AZURE_PLAN_NAME keyVaultName=$env:AZURE_KEY_VAULT_NAME `
        registryName=$env:AZURE_REGISTRY_NAME planSku=B1
```

Crear dos tokens ACR limitados al repositorio `intranet`: `template-intranet-pull`
(solo lectura) y `template-intranet-push` (lectura/escritura). Son credenciales nuevas;
no reutilizar tokens de MemByte.

```powershell
az acr token create --name template-intranet-pull --registry $env:AZURE_REGISTRY_NAME `
    --repository intranet content/read metadata/read --no-passwords true
az acr token create --name template-intranet-push --registry $env:AZURE_REGISTRY_NAME `
    --repository intranet content/read content/write metadata/read metadata/write --no-passwords true
$pullPassword = az acr token credential generate --name template-intranet-pull `
    --registry $env:AZURE_REGISTRY_NAME --password1 --query 'passwords[0].value' --output tsv
$pushPassword = az acr token credential generate --name template-intranet-push `
    --registry $env:AZURE_REGISTRY_NAME --password1 --query 'passwords[0].value' --output tsv
az keyvault secret set --vault-name $env:AZURE_KEY_VAULT_NAME --name TemplateAcrPullPassword `
    --value $pullPassword --output none
gh secret set ACR_USERNAME --env production --body "template-intranet-push"
gh secret set ACR_PASSWORD --env production --body $pushPassword
Remove-Variable pullPassword, pushPassword
```

Las contraseñas se capturan en variables y no deben imprimirse ni guardarse en archivos.
Crear `ConnectionStrings--IntranetTemplate` en Key Vault con la conexión Npgsql a Supabase,
SSL y `Search Path=<esquema_dedicado>`. Antes de migrar, comprobar en una conexión de esa
configuración que `select current_schema()` devuelve el esquema dedicado y no `public`.

Publicar una imagen y aprovisionar la app una vez. En validación, usar los recursos
existentes; no ejecutar `bootstrap.bicep`. Para clientes nuevos, ejecutar el bootstrap
en su grupo propio antes de estos pasos:

```powershell
$tag = git rev-parse HEAD
$server = "<acr>.azurecr.io"
docker login $server --username template-intranet-push
# Introducir la contraseña del token cuando Docker la solicite.
docker build --platform linux/amd64 --tag "${server}/intranet:${tag}" .
docker push "${server}/intranet:${tag}"
docker logout $server
az deployment group what-if --resource-group $rg --template-file infra\main.bicep `
    --parameters infra\production.bicepparam imageTag=$tag
az deployment group create --resource-group $rg --name initial-app --template-file infra\main.bicep `
    --parameters infra\production.bicepparam imageTag=$tag
```

Revisar el `what-if`: `main.bicep` debe crear solo la app, una identidad de ejecución
nueva y asignaciones RBAC sobre los secretos concretos; plan, ACR y Key Vault deben
aparecer como existentes sin cambios. La identidad de la app solo puede leer su secreto
de conexión, el secreto del token ACR de lectura y, si aplica, el secreto de adjuntos.
La app no migra ni siembra usuarios; aplicar las migraciones únicamente al esquema
indicado por `Search Path`, con copia de seguridad previa. Verificar que todas las
referencias Key Vault estén resueltas antes de arrancar la app.

Los valores se resuelven mediante referencias `@Microsoft.KeyVault(...)`. Un secreto
ausente o un permiso incorrecto impide el arranque: corregirlo, no usar valores de prueba
ni dar acceso de lectura al vault completo. RBAC y la caché de referencias pueden tardar
en propagarse; reiniciar o refrescar las referencias tras corregirlos.

## 4. Migrar el sitio existente desde ZIP

Para una empresa nueva se parte de recursos y datos vacíos, no de una copia de la
instalación de MemByte. Esta sección solo aplica a una instalación previa de la misma
empresa: inventariar sus propios recursos, trasladar sus datos de forma controlada
y validar antes de cambiar dominios o retirar el entorno antiguo.

Los pasos siguientes describen la alternativa de conversión in situ, que solo aplica
si se decide conservar explícitamente el sitio y su región originales:

1. Programar una ventana de mantenimiento: cambiar F1 a B1 y cambiar de runtime a
   contenedor puede reiniciar el sitio. Bicep no crea slots ni garantiza zero downtime.
2. Inventariar settings, secretos, dominios y archivos persistentes; guardar copia de
   seguridad de PostgreSQL y adjuntos. Conservar nombres y región existentes.
3. Trasladar a Bicep los settings adicionales de la aplicación antes de aplicarlo.
   Los antiguos `WEBSITE_RUN_FROM_PACKAGE`, `SCM_DO_BUILD_DURING_DEPLOYMENT` y credenciales
   de registro no se incluyen en el nuevo recurso `appsettings`.
4. Conservar `/home/.dataprotection-keys`. Si hay adjuntos locales bajo
   `/home/site/wwwroot/.messaging-attachments`, copiarlos a `/home/.messaging-attachments`
   antes de cambiar el runtime. Si ya hay Azure Blob, configurar su secreto.
   Conservar también el identificador `DataProtection__ApplicationName` anterior si
   se necesita preservar cookies y tokens. Bicep usa por defecto el nombre del sitio;
   cambiar ese identificador invalida sesiones. Nunca compartir claves entre empresas.
5. Aplicar `what-if` y comprobar que el despliegue modifica solo el App Service; plan,
   ACR y Key Vault deben permanecer como recursos existentes sin cambios.
6. Confirmar `/health`, `/login`, CSS/JS, login/MFA, carga/descarga de adjuntos y conexión
   Blazor/SignalR. Comprobar que cookies y adjuntos sobreviven a un reinicio.
7. Eliminar el perfil de publicación antiguo. El script `scripts/create-publish-zip.ps1`
   queda solo como utilidad histórica; no participa en el nuevo pipeline.

`WEBSITES_ENABLE_APP_SERVICE_STORAGE=true` monta `/home` de forma persistente.
La imagen usa usuario no root y prepara directorios escribibles para ejecución local.
No persistir datos bajo `/app`: se reemplaza con cada imagen.
Se mantiene una instancia y afinidad; ampliar Blazor Server a varias instancias
requiere diseñar la distribución de circuitos/SignalR, no solo subir `capacity`.

## 5. CI/CD y rollback

`ci.yml` compila y ejecuta pruebas .NET, auditoría NuGet y escaneo de secretos.
Además compila Bicep y sus parámetros, construye Docker y prueba `/health`, permisos
del usuario no root y los scripts/estilos que referencia la página, incluido el arranque
de Blazor (`scripts/smoke-test-web-assets.py`). `/health` comprueba que el proceso HTTP responde; no valida
consultas a PostgreSQL ni sustituye las comprobaciones funcionales de login.

`cd.yml` solo se inicia mediante `workflow_dispatch`, no automáticamente tras CI ni
al integrar una PR. Después de integrar el workflow en `master`, abrir
**Actions → CD — Deploy container to Azure → Run workflow**, seleccionar **master**
y confirmar únicamente cuando la app esté preparada. El job rechaza otras ramas y
hace checkout del SHA de la ejecución manual. Antes de autenticarse en Azure, comprueba
que ese SHA sigue siendo el actual de `master` y consulta la API de Actions con permiso
`actions: read`: el último CI de push para ese SHA, rama y repositorio debe terminar en
`success`. CI ausente, pendiente, fallido o cancelado bloquea el despliegue; no se aceptan
CI de PR. Luego publica `intranet:<SHA>` con el token ACR limitado al repositorio y usa
OIDC, con `Website Contributor` solo en la app, para cambiar su imagen y verificar
`/health`. CD no aprovisiona ni actualiza el grupo, plan, ACR, vault o asignaciones RBAC.
No hay tag `latest`.
Si `master` ha avanzado mientras el inicio manual estaba pendiente, el job rechaza
el SHA obsoleto antes de acceder a Azure para evitar desplegarlo fuera de orden.
Los despliegues a la app se serializan para evitar cambios simultáneos de imagen.
No borrar imágenes referenciadas por sitios o necesarias para rollback.

Para restaurar una imagen anterior, iniciar CD con un SHA de CI correcto que siga
existiendo en ACR y que pertenezca a `master`. La restauración cambia solo la imagen
configurada en la app; no toca recursos compartidos ni el esquema.
Para revertir también código/infraestructura de forma duradera, hacer `git revert`
del cambio en `master`, esperar a que CI termine correctamente e iniciar CD manualmente
para publicar el nuevo SHA.
Un rollback automático de migraciones no es seguro; acordar compatibilidad de esquema
y recuperación de datos antes de aplicar migraciones destructivas.

## 6. Validación local y diagnóstico

```powershell
az bicep build --file infra\main.bicep --stdout
az bicep build --file infra\bootstrap.bicep --stdout
az bicep build --file infra\registry.bicep --stdout
az bicep build-params --file infra\production.bicepparam --stdout
docker build --tag intranet:local .
# Usar una conexión de pruebas en una variable de entorno local, nunca en la imagen.
# El montaje conserva claves y adjuntos entre recreaciones del contenedor.
docker run --rm --publish 8080:8080 --env ConnectionStrings__IMtemplate --volume intranet-ejemplo-home:/home intranet:local
```

- **Docker no conecta:** arrancar Docker Desktop con motor Linux antes de construir.
- **`/_framework/blazor.web.js` devuelve 404 y los botones no responden:** en .NET 10
  los componentes Razor deben estar presentes durante `dotnet restore` para incluir
  `Microsoft.AspNetCore.App.Internal.Assets`. El Dockerfile copia `src` antes de restaurar;
  no sustituirlo por una copia exclusiva de los `.csproj`. Reconstruir y desplegar la
  imagen corregida; no descargar manualmente el script ni deshabilitar su fingerprint.
  Comprobar los assets de la página con `python scripts\smoke-test-web-assets.py <URL>`.
- **Image pull no autorizado:** comprobar que el token pull del repositorio `intranet`
  está en el secreto Key Vault indicado, que la identidad de runtime solo tiene lectura
  sobre ese secreto y que la referencia se resolvió; no habilitar el admin de ACR.
- **Contenedor no responde:** confirmar puerto 8080, logs de contenedor y referencia
  de conexión resuelta; no usar `ASPNETCORE_URLS` que fuerce otro puerto.
- **Redirecciones HTTPS:** App Service termina TLS; Bicep habilita forwarded headers
  para ese proxy. No habilitar esa confianza en despliegues expuestos directamente.
- **Key Vault:** usar roles RBAC, no `az keyvault set-policy`. No desactivar RBAC
  para resolver un error de permisos.
- **Pérdida de cookies/adjuntos:** comprobar montaje `/home`, permisos de escritura
  y rutas; no usar el filesystem efímero de `/app`.

## 7. Telemetría, alertas y rollout

**Opcional:** la telemetría de App Insights no se despliega por defecto. `Program.cs`
solo registra `AddApplicationInsightsTelemetry` si hay una cadena en
`ApplicationInsights:ConnectionString` (equivalente a `APPLICATIONINSIGHTS_CONNECTION_STRING`
si se habilita App Insights más adelante), así que la app arranca sin telemetría y
`ISecurityTelemetryPublisher` sigue emitiendo los logs estructurados (no llegan a
Application Insights).

Los eventos `AuthAuditEntry` siguen persistiendo en PostgreSQL y publicándose mediante
`ISecurityTelemetryPublisher`. Si se habilita App Insights, Bicep conecta el sitio con la
clave que realmente consulta `Program.cs`, `ApplicationInsights:ConnectionString`.
Las alertas requieren un action group/canal operativo y se configuran aparte (válidas
solo con App Insights habilitado; se basan en las consultas KQL de `traces`):

| Alerta | Consulta KQL orientativa (`traces`) | Umbral |
|---|---|---|
| Fuerza bruta | `traces \| where customDimensions.EventType == "LoginFailed" \| summarize count() by bin(timestamp, 5m), tostring(customDimensions.Ip)` | >20 fallos/5 min/IP |
| Bloqueos | `traces \| where customDimensions.EventType == "AccountLockedOut" \| summarize count() by bin(timestamp, 15m)` | >5 bloqueos/15 min |
| MFA inválido | `traces \| where customDimensions.EventType == "MfaInvalidCode" \| summarize count() by bin(timestamp, 10m), tostring(customDimensions.UserId)` | >5/10 min/usuario |
| Recuperación inválida | `traces \| where customDimensions.EventType == "InvalidRecoveryCode"` | Cualquier evento |
| Disponibilidad | Availability test sobre `/login`, HTTP 200 | 2 fallos consecutivos |

Aplicar rollout interno → piloto → general, con validación de login/MFA, adjuntos,
auditoría y alertas en cada fase. Ante errores 5xx o regresiones de autenticación,
detener la ampliación y restaurar una imagen estable. Objetivos: disponibilidad
mensual ≥99,5 %, página p95 <300 ms, cero accesos no autorizados y cero vulnerabilidades
críticas en producción; el resto de KPIs se mantiene en `agents.md`.

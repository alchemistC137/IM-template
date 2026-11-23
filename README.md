# Plantilla de intranet empresarial

Base reutilizable para crear una intranet independiente para cualquier empresa.

Esta plantilla, desarrollada por MemByte, está separada de su intranet interna.
Cada empresa obtiene su propio repositorio, configuración, base de datos, usuarios
y recursos de despliegue. No es un servicio multiempresa con datos compartidos.
La validación de esta plantilla es una excepción temporal: usa otra app, un esquema
Supabase y secretos propios, aunque comparte el grupo de recursos, plan, ACR y Key Vault.
No reutiliza datos ni secretos de MemByte. Las instalaciones de clientes nunca comparten
recursos de producción.

## Stack

- .NET 10 LTS · C# 13
- Blazor Web App (SSR + interactividad selectiva por componente)
- Entity Framework Core 10
- MudBlazor

## Documentación

- [`agents.md`](./agents.md) — roles, permisos y criterios de éxito del sistema.
- [`planes-de-desarrollo/01-plan-inicial-arquitectura.md`](./planes-de-desarrollo/01-plan-inicial-arquitectura.md) — arquitectura, stack tecnológico y guía de despliegue inicial.
- [`planes-de-desarrollo/02-runbook-despliegue-azure.md`](./planes-de-desarrollo/02-runbook-despliegue-azure.md) — runbook de aprovisionamiento/despliegue en Azure, telemetría/alertas de seguridad y rollout controlado.

## Módulos

- Gestión de Empleados
- Gestión de Usuarios y Roles
- Mensajería Interna
- Gestión de Compras
- Gestión de Clientes

Proyectos, entregables, horas y facturación son capacidades previstas en los planes,
no módulos implementados por esta conversión a plantilla.

## Requisitos

- Visual Studio Community 2026 (o superior) con carga de trabajo ASP.NET y desarrollo web.
- .NET 10 SDK.

## Puesta en marcha

```powershell
dotnet build IM-template.slnx
dotnet test IM-template.slnx
```

Abre `IM-template.slnx` en Visual Studio y establece `MemByte.Web` como proyecto de inicio.
Los nombres técnicos `MemByte.*` se conservan para no modificar ensamblados ni migraciones;
no determinan la identidad visible de la empresa.

## Crear una intranet para una empresa

1. Crea un repositorio independiente a partir de esta plantilla (opción **Use this
   template** de GitHub, si está habilitada). Configura sus accesos y su entorno
   `production`; no copies las credenciales de otra instalación.
2. Personaliza la sección `Intranet` de `src/MemByte.Web/appsettings.json`:

   ```json
   "Intranet": {
     "ApplicationName": "Portal del equipo",
     "CompanyName": "Empresa de ejemplo"
   }
   ```

   También se admiten las variables `Intranet__ApplicationName` e
   `Intranet__CompanyName`. Ambos nombres son obligatorios si se configuran y
   admiten hasta 100 caracteres. Se usan en menús, páginas de acceso y títulos,
   correos de cuentas e identificación MFA. Los valores por defecto son neutros.
   Para cambiar los colores, adapta `src/MemByte.Web/Components/MemByteTheme.cs`
   y los estilos de `src/MemByte.Web/wwwroot/app.css`.
3. Usa una base PostgreSQL exclusiva de esa empresa. Para desarrollo local con
   Docker, define `POSTGRES_PASSWORD` en tu entorno y ejecuta `docker compose up -d`.
   La base y el usuario locales se llaman `intranet`. Cada copia debe usar su propio
   proyecto Compose (por ejemplo, `docker compose -p empresa-ejemplo up -d`);
   si ejecutas varias a la vez, asigna puertos diferentes.
4. Inicializa un almacén de secretos de desarrollo exclusivo para esta copia:

   ```powershell
   dotnet user-secrets init --project src/MemByte.Web
   dotnet user-secrets set "ConnectionStrings:IMtemplate" "<conexion-postgresql-de-esta-empresa>" --project src/MemByte.Web
   ```

   No se incluyen conexiones ni contraseñas en los perfiles de ejecución.
   En `Development` se aplican las migraciones y se crean los roles automáticamente.
   El administrador inicial está deshabilitado: para crearlo localmente, configura
   `SeedAdmin:Enabled=true`, `SeedAdmin:Email` y `SeedAdmin:Password` en user-secrets;
   deshabilítalo tras el primer arranque. Nunca guardes esas credenciales en Git.
   En producción, aplica migraciones y provisiona roles y la primera cuenta Admin
   mediante un procedimiento operativo controlado; el arranque no los crea.
5. Ejecuta `dotnet run --project src/MemByte.Web --launch-profile https` y comprueba
   el acceso y los módulos con los roles de la empresa. Actualiza `agents.md` si
   adaptas permisos o flujos. El envío de correos usa por defecto un proveedor de
   logging: configura un `IEmailSender` real antes de usar invitaciones en producción.

Mantén separados PostgreSQL, adjuntos, claves de Data Protection, Key Vault y
telemetría por instalación. `DataProtection:ApplicationName` es un identificador
técnico estable (en Azure se usa el nombre del App Service), independiente del nombre
visible: cambiarlo invalida cookies y tokens existentes. Una instalación existente
que necesite conservar sesiones debe mantener su identificador anterior explícitamente.
No copies almacenes locales de secretos, claves ni volúmenes de otra empresa.

Los planes de desarrollo recogen el origen y el roadmap de la solución; no son
configuración de un cliente ni garantizan que todas las capacidades estén implementadas.

## Despliegue en Azure

`infra/bootstrap.bicep` crea el plan, ACR y Key Vault de una instalación nueva;
`infra/main.bicep` referencia esos recursos existentes y solo crea la app y una identidad
de ejecución exclusiva. CD no aprovisiona ni actualiza recursos compartidos: publica
la imagen `intranet` con un token ACR limitado a ese repositorio y cambia la imagen de
la app existente. PostgreSQL/Supabase se mantiene externo. La validación de esta plantilla
reutiliza el grupo, plan, ACR y Key Vault de MemByte; sus secretos y esquema Supabase
son distintos. Cada cliente debe crear un grupo y recursos propios.
Configura `AZURE_RESOURCE_GROUP`, `AZURE_APP_NAME`, `AZURE_REGISTRY_SERVER`, identidad
OIDC con permisos solo sobre la app y los nombres visibles `INTRANET_APPLICATION_NAME`
e `INTRANET_COMPANY_NAME` para el aprovisionamiento inicial. CD requiere además los
secretos de entorno `ACR_USERNAME` y `ACR_PASSWORD`, correspondientes al token limitado
al repositorio `intranet`. Los nombres de ejemplo del fichero
de parámetros solo permiten validación en CI; no son destinos de producción.
La región de ejemplo es Spain Central (`spaincentral`); adáptala antes del bootstrap.

El `Dockerfile` multietapa publica .NET 10 y ejecuta la aplicación como usuario no root
en el puerto 8080. El despliegue se inicia manualmente desde GitHub Actions con
**Run workflow** en `master`; exige que el último CI de ese commit haya terminado
correctamente. Publica una imagen etiquetada con el SHA y actualiza únicamente la app
existente mediante OIDC; no ejecuta Bicep ni modifica el grupo, plan, ACR o Key Vault.
El token de lectura de la app se guarda como secreto individual del Key Vault, y la
cadena de conexión debe establecer `Search Path` al esquema Supabase de prueba. El rol
PostgreSQL de la aplicación debe tener permisos solo sobre ese esquema, ya que `Search Path`
no restringe el acceso a otros esquemas. La identidad OIDC tiene permisos solo sobre la app;
el token ACR de escritura está limitado al repositorio `intranet`.

Consulta el [runbook de Azure](./planes-de-desarrollo/02-runbook-despliegue-azure.md)
para configurar OIDC, secretos, migrar el despliegue ZIP existente y hacer rollback.

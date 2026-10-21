# VaultFlow - Aplicación Financiera Cloud-Native en Azure

VaultFlow es una solución full-stack cloud-native diseñada para la gestión, orquestación y análisis de transacciones financieras. El proyecto está construido bajo una arquitectura de microservicios y servicios distribuidos desplegada en Microsoft Azure, aplicando contenerización, infraestructura como código (IaC), seguridad de datos, integración continua y agentes de IA.

---

## Tech Stack & Servicios de Azure

### **Backend & Base de Datos**
* **Framework:** .NET 9 Web API C# (Minimal APIs)
* **ORM:** Entity Framework Core 9 (Provider SQL Server)
* **Base de Datos:** Azure SQL Database
* **Documentación API:** Swagger / OpenAPI UI

### **Inteligencia Artificial & Orquestación**
* **Agentes de IA:** Groq API (`openai/gpt-oss-20b`)
* **Servicios Azure:** Azure AI Services y Azure Data Factory (infraestructura preparada)

### **Infraestructura Cloud, Seguridad & DevOps**
* **Contenedores:** Docker (Multi-stage build) & Azure Container Apps
* **Registro de Imágenes:** Azure Container Registry
* **Infraestructura como Código (IaC):** Azure Bicep unificado
* **Seguridad & Secretos:** Azure Key Vault con RBAC e identidad administrada asignada por el usuario
* **Monitoreo & Observabilidad:** Application Insights (`appins-vaultflow-dev`) & Log Analytics Workspaces
* **CI/CD Pipeline:** GitHub Actions (`.github/workflows/deploy.yml`) para build, push a ACR y despliegue automático en ACA

---

## Seguridad

1. **Gestión de Secretos sin Hardcoding:** Las credenciales de Azure SQL y la clave de Groq se guardan en Azure Key Vault. La Container App las lee mediante una identidad administrada con el rol `Key Vault Secrets User`.
2. **Autenticación Basada en Identidad:** La Container App usa una identidad administrada asignada por el usuario (`id-vaultflow-dev`) para acceder a Key Vault. No se guardan contraseñas en el repositorio.
3. **Control de CORS:** Middleware configurado en .NET 9 para permitir la interacción con aplicaciones cliente.
4. **Firewall de Base de Datos:** Reglas asignadas en Azure SQL para permitir el acceso desde Azure Container Apps y servicios de Azure.
5. **Archivos locales:** `appsettings.Development.json`, `infra/main.bicepparam` y `.env` están excluidos por `.gitignore`. No los subas al repositorio.

---

## 🚀 Guía de Despliegue Local y en la Nube

### 💻 Testeo de API

El backend está desplegado en Azure.

1. **Interfaz Interactiva Swagger:**
   * 🔗 **Swagger UI:** <https://app-vaultflow-api.wittygrass-3fa04590.westus3.azurecontainerapps.io/swagger/index.html>

2. **Monitoreo & Telemetría en Vivo:**
   Cualquier petición realizada desde Swagger genera picos de tráfico, trazabilidad de dependencias hacia Azure SQL y logs en tiempo real dentro de **Application Insights**.

### Endpoints principales

| Método | Ruta | Descripción |
| --- | --- | --- |
| GET | `/` | Health check |
| GET | `/api/transactions` | Lista las transacciones desde Azure SQL |
| POST | `/api/transactions` | Inserta una transacción |
| POST | `/api/ai/chat` | Consulta al agente financiero IA |

---

### 💻 Ejecución Local para Desarrollo (Localhost)

#### Prerrequisitos:
* **.NET 9.0 SDK**
* **Azure CLI** (para autenticación y Key Vault)
* **Docker** (opcional, para construir la imagen)

#### Pasos:

1. **Clonar el repositorio:**
   ```powershell
   git clone https://github.com/HereticSoba/vaultflow-azure-cloud.git
   cd vaultflow-azure-cloud/backend/VaultFlow.Api
   ```

2. **Iniciar sesión en Azure CLI:**
   ```powershell
   az login
   ```

3. **Configurar la cadena de conexión local:**
   ```powershell
   $env:ConnectionStrings__DefaultConnection = az keyvault secret show `
     --vault-name kv-vaultflow-ycm3rebzye5 `
     --name sql-connection-string `
     --query value -o tsv
   ```
   Si no tienes acceso al Key Vault, puedes pegar la cadena de conexión desde el portal de Azure SQL.

4. **Aplicar migraciones (solo si la base está vacía):**
   ```powershell
   dotnet ef database update
   ```

5. **Iniciar la API:**
   ```powershell
   dotnet run
   ```
   La API queda disponible en `http://localhost:5165` y Swagger en `http://localhost:5165/swagger`.

> La aplicación usa EF Core y `AppDbContext`. Si `ConnectionStrings__DefaultConnection` no está definida y tampoco `KeyVaultUri`, la API falla al iniciar con un mensaje claro en lugar de intentar conectarse a un servidor ficticio. En Azure, la cadena se obtiene de Key Vault al iniciar.

---

### 💻 Reproducir el IaC en Azure

1. **Iniciar sesión:**
   ```powershell
   az login
   ```

2. **Crear el grupo de recursos (si no existe):**
   ```powershell
   az group create --name rg-vaultflow-dev --location westus3
   ```

3. **Configurar variables de entorno para los secretos:**
   ```powershell
   $env:SQL_ADMIN_PASSWORD = "<TU_PASSWORD>"
   $env:GROQ_API_KEY = "<TU_GROQ_API_KEY>"
   ```

4. **Crear el archivo de parámetros local:**
   ```powershell
   Copy-Item ./infra/main.bicepparam.example ./infra/main.bicepparam
   ```

5. **Desplegar la plantilla Bicep unificada:**
   ```powershell
   az deployment group create `
     --resource-group rg-vaultflow-dev `
     --template-file ./infra/main.bicep `
     --parameters ./infra/main.bicepparam
   ```

> `main.bicepparam.example` es el archivo versionado. `main.bicepparam` está en `.gitignore` y lee los secretos desde variables de entorno.
>
> La plantilla crea la identidad administrada `id-vaultflow-dev` y le asigna el rol `Key Vault Secrets User` sobre el Key Vault. La API recibe `KeyVaultUri` y `AZURE_CLIENT_ID` y resuelve `sql-connection-string` y `groq-api-key` al iniciar con `DefaultAzureCredential`.
>
> **Limitación de Container Apps Express:** este entorno no soporta `keyVaultUrl` dentro de `properties.configuration.secrets`. Por eso los secretos no se referencian directamente en la Container App, sino que la API los lee desde Key Vault con su identidad administrada.
>
> Para desplegar una imagen concreta, agrega `--parameters imageTag=<sha-del-commit>`.

---

### 🔁 CI/CD con GitHub Actions

El workflow `.github/workflows/deploy.yml` se ejecuta en cada push a `main` y también puede lanzarse manualmente (`workflow_dispatch`).

1. **Secretos requeridos en GitHub:**
   * `REGISTRY_USERNAME` y `REGISTRY_PASSWORD`: credenciales del ACR.
   * `AZURE_CREDENTIALS`: JSON de un service principal con permisos para actualizar la Container App. Se puede crear con:
     ```bash
     az ad sp create-for-rbac \
       --name "vaultflow-github" \
       --role contributor \
       --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/rg-vaultflow-dev \
       --sdk-auth
     ```
     Copia el JSON completo como valor del secreto `AZURE_CREDENTIALS`.

2. **Qué hace el pipeline:**
   * Construye la imagen con el SHA del commit y el tag `latest`.
   * La publica en `acrvaultflowdev01.azurecr.io`.
   * Actualiza `app-vaultflow-api` con la imagen `:<sha>`.
   * Si `AZURE_CREDENTIALS` no está configurado, publica la imagen pero omite el despliegue (con un warning).

---

## Solución de problemas

### La API devuelve 500 en `/api/transactions`

En Azure, la API no recibe la cadena de conexión como variable de entorno: la resuelve al iniciar desde Key Vault. Comprueba:

1. Que la Container App tenga `KeyVaultUri` y `AZURE_CLIENT_ID`:

```powershell
az containerapp exec -n app-vaultflow-api -g rg-vaultflow-dev `
  --command "printenv KeyVaultUri AZURE_CLIENT_ID"
```

2. Que el secreto de Key Vault apunte al servidor real:

```powershell
az keyvault secret show --vault-name kv-vaultflow-ycm3rebzye5 `
  --name sql-connection-string --query value -o tsv
```

Debe contener el FQDN real:

```
Server=tcp:sql-vaultflow-ycm3rebzye5kw.database.windows.net,1433;Initial Catalog=sqldb-vaultflow;...
```

3. Que la identidad tenga el rol `Key Vault Secrets User` (ver más abajo).
4. Que el firewall de SQL permita el acceso desde Azure.

Si el secreto está mal, corrígelo en Key Vault y **recrea la réplica**. En Container Apps Express un cambio de secreto no reinicia el contenedor automáticamente; fuerza una nueva réplica actualizando la imagen o el template:

```powershell
az containerapp update -n app-vaultflow-api -g rg-vaultflow-dev `
  --image acrvaultflowdev01.azurecr.io/vaultflow-api:<tag>
```

### Error `TCP Provider, error: 35`

Suele indicar que el hostname no existe o que la conexión está bloqueada a nivel de red. Comprueba:

1. Que el FQDN del secreto sea el del servidor SQL real.
2. Las reglas de firewall del servidor SQL (`AllowAzureServices` o la IP de salida del entorno).
3. Que la identidad de la Container App tenga el rol `Key Vault Secrets User` si el secreto se lee desde Key Vault.

### Error de permisos al leer Key Vault

```powershell
az role assignment list --scope <resource-id-del-key-vault> --output table
```

La identidad `id-vaultflow-dev` debe tener el rol `Key Vault Secrets User` (`4633458b-17de-408a-b874-0445c86b69e6`).

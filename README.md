# VaultFlow - Aplicación Financiera Cloud-Native en Azure

VaultFlow es una solución full-stack cloud-native diseñada para la gestión, orquestación y análisis de transacciones financieras. El proyecto está construido bajo una arquitectura de microservicios y servicios distribuidos desplegada en Microsoft Azure, aplicando contenerización, infraestructura como código (IaC), seguridad de datos, integración continua y agentes de IA.

---

## Tech Stack & Servicios de Azure

### **Backend & Base de Datos**
* **Framework:** .NET 9 Web API C# (Minimal APIs / Controllers)
* **ORM:** Entity Framework Core (Provider SQL Server)
* **Base de Datos:** Azure SQL Database
* **Documentación API:** Swagger / OpenAPI UI

### **Inteligencia Artificial & Orquestación**
* **Agentes de IA:** Azure AI Services / Azure OpenAI)
* **Ingesta & ETL:** Azure Data Factory

### **Infraestructura Cloud, Seguridad & DevOps**
* **Contenedores:** Docker (Multi-stage build) & Azure Container Apps
* **Registro de Imágenes:** Azure Container Registry
* **Infraestructura como Código (IaC):** Azure Bicep unificado
* **Seguridad & Secretos:** Azure Key Vault con RBAC y DefaultAzureCredential
* **Monitoreo & Observabilidad:** Application Insights (`appins-vaultflow-dev`) & Log Analytics Workspaces
* **CI/CD Pipeline:** GitHub Actions (`.github/workflows/deploy.yml`) para despliegue automático hacia ACR y ACA

---

## Seguridad

1. **Gestión de Secretos sin Hardcoding:** Ninguna credencial, clave de API ni cadena de conexión está expuesta en el código fuente ni en el repositorio.
2. **Autenticación Basada en Identidad (RBAC):** Uso de `DefaultAzureCredential` de `Azure.Identity` para resolver de forma segura las credenciales de Key Vault y Azure AI Services.
3. **Control de CORS:** Middleware configurado en .NET 9 para permitir la interacción segura con aplicaciones cliente.
4. **Firewall de Base de Datos:** Reglas asignadas en Azure SQL para garantizar el acceso desde Azure Container Apps y servicios de Azure.

---

## 🚀 Guía de Despliegue Local y en la Nube
### 💻 Testeo de API

El backend se encuentra desplegado en la nube de Azure.

1. **Interfaz Interactiva Swagger:**
   Documentación OpenAPI interactiva para explorar y ejecutar peticiones `GET`, `POST` y probar la API en tiempo real:
   * 🔗 **Swagger UI:** <a href="[https://localhost:7084/swagger](https://app-vaultflow-api.wittygrass-3fa04590.westus3.azurecontainerapps.io/swagger/index.html)">https://app-vaultflow-api.wittygrass-3fa04590.westus3.azurecontainerapps.io/swagger/index.html</a>

2. **Monitoreo & Telemetría en Vivo:**
   Cualquier petición realizada desde Swagger genera picos de tráfico, trazabilidad de dependencias hacia Azure SQL y logs en tiempo real dentro de **Application Insights**.

---

### 💻 Ejecución Local para Desarrollo (Localhost)
#### Prerrequisitos:
* **.NET 9.0 SDK**
* **Azure CLI** *(para autenticación local con Azure Key Vault)*

#### Pasos:

1. **Clonar el repositorio:**
   ```
   git clone https://github.com/HereticSoba/vaultflow-azure-cloud.git
   ```
   ```
   cd vaultflow-azure-cloud/backend/VaultFlow.Api
   ```
2. **Iniciar sesión en Azure CLI:**
   ```
   az login
   ```
3. **Migración de la DB (opcional):**
   ```
   $env:ConnectionStrings__DefaultConnection="Server=tcp:sql-vaultflow-ycm3rebzye5kw.database.windows.net,1433;Initial Catalog=sqldb-vaultflow;Persist Security Info=False;User ID=vaultadmin;Password=<TU_PASSWORD>;Encrypt=True;"
   ```
   ```
   dotnet ef database update
   ```
4. **Iniciar la API:**
   ```
   dotnet run
   ```
   
---
### 💻 Reproducir el IaC en Azure
1. **Iniciar sesión:**
```
az login
```
2. **Crear grupo de recursos:**
```
az group create --name rg-vaultflow-evaluacion --location westus3
```
3. **Desplegar plantilla Bicep unificada:**
```
az deployment group create `
  --resource-group rg-vaultflow-evaluacion `
  --template-file ./infra/main.bicep `
  --parameters ./infra/main.bicepparam
```
---

# VaultFlow - Aplicación Financiera Cloud-Native en Azure

VaultFlow es una solución full-stack cloud-native diseñada para la gestión y seguimiento de transacciones financieras. El proyecto está construido bajo una arquitectura de microservicios/servicios distribuidos desplegada en **Microsoft Azure**, aplicando mejores prácticas de contenerización, seguridad de datos e infraestructura.

---

## 🏗️ Arquitectura del Sistema
```
[ Frontend: React + Vite ] 
       │ (HTTPS / REST API)
       ▼
[ Azure Container Apps (Ingress: External Port 8080) ]
       │
       ├─► [ .NET 9 Web API (Entity Framework Core) ]
       │          │
       │          └─► [ Variables de entorno (SecretRef) ]
       │
       ▼
[ Azure SQL Database (v12 - Serverless/Standard) ]
```

---

## 🛠️ Tech Stack & Servicios de Azure

### **Backend & Base de Datos**
* **Framework:** .NET 9 Web API C#
* **ORM:** Entity Framework Core (Provider SQL Server)
* **Base de Datos:** Azure SQL Database
* **Documentación API:** Swagger / OpenAPI

### **Frontend**
* **Framework:** React 18 + Vite + TypeScript
* **HTTP Client:** Axios
* **Estilos:** CSS3 / Modern Flexbox & Grid

### **Infraestructura Cloud & DevOps**
* **Contenedores:** Docker (Multi-stage build)
* **Registro de Imágenes:** Azure Container Registry (ACR)
* **Cómputo Serverless:** Azure Container Apps (ACA)
* **Seguridad & Configuración:** Azure Secrets & Environment Variables Ingestion
* **Control de Red:** Azure SQL Firewall Rules (`AllowMyLocalIP`, `AllowAzureServices`)
* **CLI & Automation:** Azure CLI (`az`), PowerShell

---

## 🔐 Seguridad y Buenas Prácticas Aplicadas

1. **Gestión de Secretos:** Ninguna credencial o cadena de conexión a la base de datos está expuesta en el código fuente.
2. **Inyección de Secretos en ACA:** La cadena de conexión real se inyecta en tiempo de ejecución mediante `az containerapp secret set` y se vincula mediante `secretref`.
3. **Control de CORS:** Configuración permisiva en el middleware de .NET 9 delegando la gestión directa desde la capa de aplicación, eliminando bloqueos de preflight `OPTIONS` a nivel de Ingress.
4. **Firewall de Base de Datos:** Reglas explícitas asignadas para permitir la administración local vía Entity Framework y la comunicación interna de los servicios de Azure.

---

## 🚀 Guía de Despliegue Local y en la Nube

### **Prerrequisitos**
* .NET 9 SDK
* Node.js (v18+)
* Docker Desktop
* Azure CLI

---

### **Configuración de la Base de Datos (Azure SQL)**

Para aplicar las migraciones iniciales a Azure SQL Database desde tu máquina local:

```powershell
cd backend/VaultFlow.Api

# Definir la variable de entorno temporal con tus credenciales
$env:ConnectionStrings__DefaultConnection="Server=tcp:<TU_SERVIDOR>.database.windows.net,1433;Initial Catalog=sqldb-vaultflow;Persist Security Info=False;User ID=<USER>;Password=<PASSWORD>;Encrypt=True;"

# Aplicar las migraciones
dotnet ef database update
```
## **Construcción y Publicación del Contenedor Backend**

### 1. Autenticarse en Azure Container Registry
```
az acr login --name <NOMBRE_ACR>
```
### 2. Compilar la imagen Docker
```
docker build -t <NOMBRE_ACR>.azurecr.io/vaultflow-api:v7 .
```
### 3. Subir la imagen al registro
```
docker push <NOMBRE_ACR>.azurecr.io/vaultflow-api:v7
```
### 4. Desplegar o actualizar la Container App
```
az containerapp update `
  --name app-vaultflow-api `
  --resource-group rg-vaultflow-dev `
  --image <NOMBRE_ACR>.azurecr.io/vaultflow-api:v7
```

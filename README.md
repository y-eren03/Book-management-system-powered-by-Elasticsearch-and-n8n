# 📚 Bookstore Uygulaması & 🤖 n8n Workflow

Bu repository, bir **Bookstore uygulaması** ve entegre bir **n8n workflow** içermektedir.

## 🔹 Özellikler

**Bookstore Uygulaması (Admin & Müşteri Paneli):**

- **Admin yapabilecekleri:**
  - Form üzerinden yeni kitap ekleme
  - Mevcut kitapları güncelleme veya silme
  - Kitap fiyatlarına indirim veya zam uygulama
  - Kitapları arama ve filtreleme
- **Müşteri yapabilecekleri:**
  - Kitapları arama ve göz atma
  - Kitapları sepete ekleme
  - Kitap satın alma
  - n8n üzerinden AI destekli öneri alma

**n8n Workflow Entegrasyonu:**

- AI destekli sohbet ve öneri sistemi
- Bookstore uygulaması ile n8n arasında webhook tabanlı iletişim
- Müşteri sorularına otomatik yanıtlar

## 🔧 Kurulum ve Kullanım Talimatları

1. **n8n workflow’unuzu ekleyin:**  
   `BOOKSTORE WORKFLOW.json` dosyasındaki kodu kopyalayıp n8n workflow’unuza yapıştırın.

2. **Elastic Search ayarları:**  
   `appsettings.Development.json` ve `appsettings.json` dosyalarına aşağıdaki bilgileri ekleyin:
   ```json
   "CloudId": "Your CloudId",
   "Username": "Your UserName",
   "Password": "Your Password"
   ```
   
3. **Elastic Search ayarları:**
    ```yaml
   dotnet new webapi --name elasticsearch
    dotnet add package NEST
    dotnet restore
    dotnet add package Swashbuckle.AspNetCore
   ```
4. **Elastic Search ayarları:**
   ```yaml
    dotnet run 
   ```

## 🤖 n8n Kurulumu

### 1️⃣ Docker Compose ile n8n

Aşağıdaki `docker-compose.yml` örneği ile n8n’i PostgreSQL ile birlikte çalıştırabilirsiniz:

```yaml
# ================= N8N =================
n8n:
  image: docker.n8n.io/n8nio/n8n
  restart: always
  environment:
    - DB_TYPE=postgresdb
    - DB_POSTGRESDB_HOST=postgres
    - DB_POSTGRESDB_PORT=5432
    - DB_POSTGRESDB_DATABASE=n8ndb
    - DB_POSTGRESDB_USER=postgres's username
    - DB_POSTGRESDB_PASSWORD=postgres's password
    - N8N_BASIC_AUTH_ACTIVE=false
    - N8N_HOST=localhost
    - N8N_PORT=5678
    - N8N_PROTOCOL=http
    - WEBHOOK_TUNNEL_URL=http://localhost:5678
    - N8N_COMMUNITY_PACKAGES_ALLOW_TOOL_USAGE=true
    - ELASTIC_CLIENT_APIVERSIONING=true
  ports:
    - 5678:5678
  volumes:
    - n8n_storage:/home/node/.n8n
  depends_on:
    postgres:
      condition: service_healthy
  networks:
    - es-network
```


### 2️⃣ n8n Credentials

Gmail API: Google Cloud’dan Gmail API oluşturun ve Client ID ile Client Secret bilgilerinizi Mail node’unda Credential to connect with kısmına ekleyin.

Gemini API: Google AI Studio’dan Gemini API key alın ve Gemini node’unda Credential to connect with kısmına ekleyin.

Elastic Search Node: Elastic username, password ve baseURL bilgilerinizi Credential to connect with kısmına ekleyin.

Postgres Node: İsterseniz Postgres node’larını silip Simple Memory kullanabilirsiniz. Local kurulumda Postgres ile kurulum önerilir.


### 3️⃣ Docker Compose ile PostgreSQL

Aşağıdaki `docker-compose.yml` örneği PostgreSQL servisini tanımlar:

```yaml
# ================= POSTGRES =================
postgres:
  image: postgres:16
  restart: always
  environment:
    - POSTGRES_USER=Your username
    - POSTGRES_PASSWORD=Your password
    - POSTGRES_DB=n8ndb
  ports:
    - "5432:5432"
  volumes:
    - db_storage:/var/lib/postgresql/data
  healthcheck:
    test: ['CMD-SHELL', 'pg_isready -h localhost -U postgres -d n8ndb']
    interval: 5s
    timeout: 5s
    retries: 10
  networks:
    - es-network

```

# Docker Quickstart cho MathInsight

## 1. File da them

- `Dockerfile`: build va chay `MathInsight.WebAPI`.
- `.dockerignore`: loai `bin/`, `obj/`, `.git/`, file IDE va secret local khoi Docker build context.

## 2. Build image local

Chay tu thu muc `Implementation/MathInsight`:

```powershell
docker build -t mathinsight-api:dev .
```

## 3. Chay container local

Docker khong tu doc `.NET user-secrets`, nen secret phai truyen bang environment variables.

```powershell
docker run --rm -p 8080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ConnectionStrings__DefaultConnection="Server=tcp:mathinsight.database.windows.net,1433;Initial Catalog=mathinsight;Persist Security Info=False;User ID=mathinsight;Password=Abc12345;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" `
  -e Jwt__Issuer="MathInsight" `
  -e Jwt__Audience="MathInsightClient" `
  -e Jwt__SigningKey="CHANGE_ME_DEV_SECRET_KEY_AT_LEAST_32_CHARS" `
  -e Redis__Enabled=false `
  -e RabbitMQ__Enabled=false `
  mathinsight-api:dev
```

Sau do API chay tai:

```text
http://localhost:8080
```

## 4. Mapping config ASP.NET Core

Trong Docker/Azure, dung `__` thay cho `:`:

```text
ConnectionStrings:DefaultConnection -> ConnectionStrings__DefaultConnection
Jwt:SigningKey                      -> Jwt__SigningKey
Redis:Enabled                       -> Redis__Enabled
Redis:ConnectionString              -> Redis__ConnectionString
RabbitMQ:Enabled                    -> RabbitMQ__Enabled
```

## 5. Redis trong MVP

Hien tai co the de:

```text
Redis__Enabled=false
```

Chi bat Redis khi can session/JWT blacklist/cache dung o moi truong deploy:

```powershell
-e Redis__Enabled=true `
-e Redis__ConnectionString="<redis-host>:6380,password=<access-key>,ssl=True,abortConnect=False"
```

Khong ghi secret vao `Dockerfile`.

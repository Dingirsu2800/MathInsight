# Docker Quickstart cho MathInsight

## 1. File đã thêm

- `Dockerfile`: build và chạy `MathInsight.WebAPI`.
- `docker-compose.yml`: cấu hình chạy cả WebAPI và Redis cùng lúc.
- `.dockerignore`: loại `bin/`, `obj/`, `.git/`, file IDE và secret local khỏi Docker build context.

## 2. Giải thích về Redis và Docker

Khi chạy trực tiếp `Dockerfile`, Docker chỉ build và khởi chạy **duy nhất** container chứa ứng dụng ASP.NET Core WebAPI. **Redis sẽ không chạy cùng** vì Dockerfile được thiết kế để chạy một tiến trình/dịch vụ đơn lẻ.

Để tích hợp và chạy Redis cùng với WebAPI dưới môi trường local, ta sử dụng **Docker Compose** thông qua file [docker-compose.yml](file:///c:/Users/Admin/Documents/CODIN/ASP.net/MathInsight/docker-compose.yml).

## 3. Khởi chạy với Docker Compose (Có tích hợp Redis)

Để khởi chạy toàn bộ hệ thống (WebAPI + Redis), chạy lệnh sau từ thư mục gốc của dự án:

```powershell
# Khởi chạy các dịch vụ (WebAPI sẽ chờ Redis chạy trước)
docker compose up --build
```

Nếu muốn chạy dưới dạng background (detached mode):

```powershell
docker compose up -d --build
```

### Cấu hình biến môi trường trong Docker Compose
File `docker-compose.yml` đã được cấu hình sẵn để WebAPI kết nối với Redis qua mạng nội bộ Docker:
- `Redis__Enabled=true`
- `Redis__ConnectionString=redis:6379` (trong đó `redis` là tên service của Redis container).

Bạn có thể truyền chuỗi kết nối cơ sở dữ liệu SQL Server thông qua biến môi trường trước khi chạy `docker compose`:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=tcp:mathinsight.database.windows.net,1433;Initial Catalog=mathinsight;Persist Security Info=False;User ID=mathinsight;Password=YourPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
docker compose up --build
```

## 4. Chạy container lẻ (Không khuyên dùng cho phát triển cần Redis)

Nếu chỉ muốn build và chạy riêng lẻ WebAPI mà không dùng Docker Compose:

### Build image local
```powershell
docker build -t mathinsight-api:dev .
```

### Chạy container local
```powershell
docker run --rm -p 8080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ConnectionStrings__DefaultConnection="Server=tcp:mathinsight.database.windows.net,1433;Initial Catalog=mathinsight;..." `
  -e Jwt__Issuer="MathInsight" `
  -e Jwt__Audience="MathInsightClient" `
  -e Jwt__SigningKey="CHANGE_ME_DEV_SECRET_KEY_AT_LEAST_32_CHARS" `
  -e Redis__Enabled=false `
  -e RabbitMQ__Enabled=false `
  mathinsight-api:dev
```

Sau đó API chạy tại:

```text
http://localhost:8080
```

## 5. Mapping config ASP.NET Core

Trong Docker/Azure, dùng `__` thay cho `:`:

```text
ConnectionStrings:DefaultConnection -> ConnectionStrings__DefaultConnection
Jwt:SigningKey                      -> Jwt__SigningKey
Redis:Enabled                       -> Redis__Enabled
Redis:ConnectionString              -> Redis__ConnectionString
RabbitMQ:Enabled                    -> RabbitMQ__Enabled
```

## 6. Redis trong Production/Azure

Khi deploy lên môi trường Production, không dùng container Redis local mà trỏ đến Azure Cache for Redis:

```powershell
-e Redis__Enabled=true `
-e Redis__ConnectionString="<redis-host>:6380,password=<access-key>,ssl=True,abortConnect=False"
```

Không ghi secret vào `Dockerfile` hoặc `docker-compose.yml`.


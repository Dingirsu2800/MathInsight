# Docker Quickstart cho MathInsight

## Mục tiêu

Docker Compose hiện chạy 3 service:

- `frontend`: React + Vite tại `http://localhost:5173`
- `webapi`: ASP.NET Core WebAPI tại `http://localhost:8080`
- `redis`: Redis nội bộ trong Docker network cho backend

Backend vẫn dùng Azure SQL qua connection string trong `.env`. Compose hiện chưa chạy SQL Server local.

## Chuẩn bị `.env`

Từ thư mục `Implementation/MathInsight`, tạo file `.env`:

```powershell
Copy-Item .env.example .env
```

Sau đó điền giá trị thật:

```text
ConnectionStrings__DefaultConnection=...
Jwt__SigningKey=...
Cloudinary__CloudName=...
Cloudinary__ApiKey=...
Cloudinary__ApiSecret=...
```

Không commit `.env`, connection string, API key, token hoặc password.

## Chạy toàn bộ ứng dụng

```powershell
docker compose up --build
```

Chạy nền:

```powershell
docker compose up -d --build
```

Mở:

- Frontend: `http://localhost:5173`
- Backend API: `http://localhost:8080`

## Vì sao frontend dùng `localhost:8080`

Frontend chạy trong container, nhưng request API được gọi từ browser trên máy bạn. Vì vậy `VITE_API_BASE_URL` phải trỏ tới port backend đã publish ra host:

```text
VITE_API_BASE_URL=http://localhost:8080
```

Không dùng `http://webapi:8080` cho frontend browser, vì `webapi` chỉ là DNS nội bộ trong Docker network.

## Đổi port khi bị trùng

Nếu `5173` hoặc `8080` đã bị dùng, sửa trong `.env`:

```text
WEBAPI_PORT=8081
FRONTEND_PORT=5174
VITE_API_BASE_URL=http://localhost:8081
Cors__AllowedOrigins__0=http://localhost:5174
Cors__AllowedOrigins__1=http://127.0.0.1:5174
```

Sau đó chạy lại:

```powershell
docker compose up -d --build
```

## Lệnh hữu ích

Xem log tất cả service:

```powershell
docker compose logs -f
```

Xem log backend:

```powershell
docker compose logs -f webapi
```

Xem log frontend:

```powershell
docker compose logs -f frontend
```

Dừng container:

```powershell
docker compose down
```

Dừng và xóa volume Redis/node_modules:

```powershell
docker compose down -v
```

Build lại không dùng cache:

```powershell
docker compose build --no-cache
docker compose up
```

## Lưu ý khi dùng Docker Desktop

- Mở Docker Desktop trước khi chạy lệnh compose.
- Chọn Linux containers, không dùng Windows containers.
- Lần build đầu có thể lâu vì phải tải image `.NET SDK`, `.NET ASP.NET runtime`, `Node` và `Redis`.
- Nếu đổi `.env`, thường cần restart container: `docker compose up -d --build`.
- Nếu frontend không nhận package mới, chạy `docker compose down -v` rồi `docker compose up --build`.
- Nếu backend không kết nối được Azure SQL, kiểm tra firewall Azure SQL đã allow IP hiện tại của bạn.
- Redis không publish port `6379` ra host, nên ít bị conflict với Redis cài local. Backend truy cập Redis bằng `redis:6379` trong Docker network.

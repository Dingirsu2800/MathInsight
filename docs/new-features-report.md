# Báo cáo tổng hợp những gì mới thêm

Ngày tạo: 2026-08-01

## 1. Tính năng Gamification mới

Đã bổ sung các chức năng liên quan đến trải nghiệm học tập và động lực người dùng:

- Trang danh mục huy hiệu: mới có màn hình xem các badge có thể nhận.
- Trang bảng xếp hạng: hỗ trợ hiển thị thứ hạng người dùng theo điểm hoặc thành tích.
- Trang Target Score: cho phép người dùng theo dõi và quản lý mục tiêu điểm số cá nhân.
- Cập nhật giao diện dashboard với các card mới như badge, mục tiêu hàng tuần và thống kê.

## 2. Hệ thống Thông báo và Báo cáo

Đã thêm một module mới để xử lý thông báo và báo cáo cho người dùng:

- Hệ thống notification cho các sự kiện như badge đạt được, câu hỏi được trả lời, điểm số tính xong, nhắc nhở streak.
- API và controller cho việc lấy thông báo, đánh dấu đã đọc và xem leaderboard.
- Hỗ trợ background job để tính toán leaderboard và dọn dẹp thông báo cũ.
- Tích hợp hub cho realtime notification.

## 3. Backend mới và mở rộng

Một số module backend đã được mở rộng để hỗ trợ các tính năng trên:

- Controller cho badges và targets trong module Gamification.
- Các handler và event cho việc phát sinh thông báo từ các hành động trong hệ thống.
- Cấu hình persistence mới cho notification/report module.
- Thêm service cho email, cache leaderboard và xử lý notification.

## 4. Frontend mới

Các file frontend mới được thêm bao gồm:

- Pages: BadgeCataloguePage, LeaderboardPage, TargetScorePage
- Services: gamificationApi, notificationApi, reportApi
- Component: NotificationBell

## 5. Kiểm thử

Đã bổ sung các test cho module notification/report để đảm bảo các chức năng mới hoạt động đúng:

- Test cho notification service
- Test cho query lấy notification và leaderboard
- Test cho background job và event handler

## 6. Tóm tắt ngắn

Những thay đổi mới chủ yếu tập trung vào ba nhóm:

1. Tăng trải nghiệm người dùng bằng gamification.
2. Thêm hệ thống thông báo và báo cáo thời gian thực.
3. Bổ sung backend, service và test để hỗ trợ các tính năng mới.

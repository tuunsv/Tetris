# Kiểm thử bản Boss Rush

## Trạng thái bàn giao

Đã kiểm tra tĩnh cú pháp C#, project XML, dữ liệu khối/kick, WAV, và dựng chân dung từ các lệnh hình học để xem bố cục. Chưa chạy compiler, bộ test C# hay WinForms/waveOut trên Windows tại môi trường tạo bản này. `STATIC-CHECK.json` ghi rõ phạm vi, không thay thế kết quả chạy thật.

## Trên Windows

1. Chạy `test.cmd`. Xem số passed/failed; nếu failed, giữ nguyên thông báo lỗi để sửa trước khi nộp.
2. Chạy `run.cmd`, chọn từng chế độ thường để kiểm tra không bị ảnh hưởng.
3. Vào XEM 12 BOSS, kiểm tra đủ chân dung và mô tả.
4. Vào Boss Rush: có intro, mặt boss ở trên cửa spawn, nhiệm vụ, đồng hồ, HP, Hold/Next, nhạc riêng.
5. Nhấn P và đổi cửa sổ: đếm ngược và hiệu ứng phải dừng; trở lại đúng vị trí.
6. Thử hover/menu bằng chuột, phím lên/xuống và Enter. Mỗi lần đổi lựa chọn có SFX, đứng yên không phát lặp.
7. Đợi hết hạn để kiểm tra đòn. Với Radagon phải thua; Margit còn một máu; Ranni không đánh.
8. Hạ một boss: nghe fanfare dài; trong cảnh chuyển không điều khiển được; máu được giữ; sân/đá/Hold được dọn.
9. Kết thúc ba boss: thấy cảnh hạ cuối trước màn thống kê; nhập tên rồi kiểm tra bảng điểm Boss Rush.
10. Tắt/mở âm, chỉnh volume nhạc/SFX độc lập; nghe đoạn nối vòng, đổi boss và quay menu. Buffer vẫn 1024 × 3.
11. Đóng/mở game: kiểm tra bảng điểm và thống kê còn nguyên, dữ liệu cũ vẫn đọc được.

Bộ test tự động có các ca đích danh cho từng boss; không cần may mắn bốc trúng boss để kiểm chứng luật. Hàm `SelectForTest` là internal, không có trong menu game.

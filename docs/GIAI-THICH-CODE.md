# Ghi chú

- `GameEngine` giữ sân là mảng `int[40,10]`. 20 hàng dưới hiển thị, 20 hàng trên dùng spawn. 0 là trống, 1..7 là tetromino, 8..19 là pentomino, 99 là đá.
- `Pieces` tạo trước tọa độ bốn hướng xoay. Tetromino dùng bảng SRS cũ; pentomino có bảng dịch thử riêng. Không sửa bảng SRS để chiều theo khối 5 ô.
- `SevenBag.Configure` chỉ đổi bộ sinh khi vào boss. Godrick thêm một pentomino vào list bảy khối rồi Fisher–Yates; Godfrey bốc độc lập. Queue `forced` đứng trước queue thường nên đòn Godrick không làm mất phần túi còn lại.
- `BossRush` là một bộ trạng thái: boss hiện tại, HP, ấn, nhiệm vụ, deadline, đồng hồ hiệu ứng. `Tick` chạy theo thời gian game, không dùng một Timer riêng cho mỗi boss. Vì vậy pause chặn được tất cả.
- `GameEngine` gọi `Rush.OnLock` sau khi tính điểm/xóa hàng. Boss đọc kết quả, không tự sao chép thuật toán ghi điểm. Nhiệm vụ điểm lưu mốc đầu lượt rồi lấy tổng điểm hiện tại trừ mốc.
- Tác dụng như tốc độ rơi/lock delay/ngưỡng tử được đọc qua property. Khi đồng hồ hiệu ứng về 0, property trả mặc định, tránh quên khôi phục khi hết hiệu ứng.
- Đá không xóa được: bỏ hàng có đá khỏi danh sách xóa; nén từng đoạn cột phân cách bởi đá. Không dồn cả hàng đá xuống như khối thường.
- Sân/held/reset được dọn khi vào boss mới, còn thống kê và điểm thuộc cả run. Ranni đọc số hàng của trận nên không được thắng ngay nhờ level của boss trước.
- `BossPortraits` vẽ một lần mỗi mặt vào Bitmap 72×72 rồi cache. Frame chỉ vẽ bitmap; `Dispose` giải phóng khi đóng Form.
- Nhạc được tạo sẵn thành WAV bằng script Python/numpy. Game chỉ nạp PCM và trộn, không chạy Python. Mixer có một thread waveOut, ba buffer 1024 mẫu, limiter và chuyển âm lượng giữa bài để tránh ngắt đột ngột.
- Ghi điểm bằng JSON vào LocalAppData, thay file nguyên khối và giữ backup. Bản mới thêm trường thống kê; bản lưu cũ thiếu trường thì nhận giá trị 0.


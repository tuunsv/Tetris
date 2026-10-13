# Tetris 118 — Boss Rush

C# 5 / Windows Forms / .NET Framework 4.8. Gói đầy đủ gồm solution, mã nguồn, WAV và hướng dẫn. Đây là bản cập nhật trên giao diện Dark Fantasy, có cả Marathon, Sprint, Ultra và Boss Rush.

## Mở game

1. Giải nén toàn bộ ZIP vào một thư mục mới.
2. Trên Windows, chạy `run.cmd`, hoặc mở `Tetris118.sln` trong Visual Studio có .NET desktop development rồi Build/Run.
3. Chọn **BẮT ĐẦU → BOSS RUSH**. Mục **XEM 12 BOSS** giải thích nội tại và đòn đánh.
4. Sau khi build, giữ thư mục `Assets` cạnh `dist/Tetris118.exe`.

Chưa có EXE biên dịch sẵn. Mã nguồn dùng C# 5; `build.cmd` gọi compiler .NET Framework trên Windows. VS Code mở được thư mục và sửa code; game vẫn cần Windows để chạy WinForms/waveOut.

## Nếu cập nhật project cũ

Sao lưu trước. Thay các file cùng tên trong `Core`, `UI`, `Audio`, `Data`; thêm `Core/BossRush.cs`, `UI/BossPortraits.cs` và các WAV mới trong `Assets`. Dùng project trọn gói sẽ tránh bỏ sót file. Không chép thêm một bản `MainForm` vào cùng project. Dữ liệu cũ ở `%LOCALAPPDATA%/Tetris118` vẫn dùng được.

## Boss Rush

- Ba boss không trùng, bốc từ 12 boss. Seed cố định tái hiện cả đường đi và chuỗi khối.
- Năm máu cho cả hành trình; đầy sân vẫn thua ngay dù còn máu.
- Hoàn thành ba nhiệm vụ để hạ một boss. Mỗi nhiệm vụ có 60 giây; hoàn thành sớm được nhiệm vụ mới ngay. Trễ thì nhận đòn, không mất ấn đã đạt.
- Ranni là ngoại lệ: không tấn công, đạt level 9 (80 hàng trong trận đó) là vượt qua.
- Chuyển boss dọn sân, Hold và hiệu ứng; giữ điểm, tổng thống kê, máu. Level bắt đầu lại từ 1 mỗi boss.
- Chân dung pixel 72×72 tự vẽ bằng code, có đặc điểm riêng; nằm trên cửa spawn. Xem toàn bộ trong mục XEM 12 BOSS.
- 12 bản nhạc boss tự soạn, không phải soundtrack Elden Ring. Nhạc menu riêng; chế độ thường dùng Tetris Game Boy Type B tổng hợp lại.
- SFX hạ boss 6,5 giây, giảm nhạc nền khi phát. Có tiếng riêng Single, Double, Triple, combo, B2B, Perfect Clear.
- Buffer giữ 1024 mẫu × 3. Âm lượng nhạc và SFX chỉnh riêng.
- Tên nhập sau ván, có bảng điểm Boss Rush và ba thống kê mới. Không có Achievement, ô tên nhóm hay chỉnh lock delay trong menu.

Các điểm diễn giải cần biết: Malenia dùng **140 ms / 5 lần reset** trong đòn 15 giây; Radahn thêm **3 ô đá**, không phải 3 hàng; đá tồn tại đến hết trận Radahn. Luật chi tiết ở `docs/BOSS-RUSH.md`.

## Điều khiển

←/→ di chuyển; ↓ rơi nhanh; ↑/X xoay phải; Z xoay trái; Space thả khóa ngay; C/Shift Hold; P/Esc tạm dừng; M tắt/bật âm; F11 phóng to. Menu dùng ↑/↓/Tab và Enter hoặc chuột. Khi nhập tên, các chữ như M/P/C được xử lý là tên, không kích hoạt shortcut.

## Kiểm chứng

Đã kiểm tra cú pháp bằng parser, cấu trúc project/XML, hình học các khối, bảng kick gốc, tệp WAV và bố cục chân dung bằng dựng hình kiểm tra. **Chưa biên dịch hoặc chạy game/test C# trên Windows trong môi trường này.** Không coi bộ test đính kèm là test đã chạy thành công.

Trên Windows: chạy `test.cmd`, rồi `run.cmd`. Bộ test gồm kiểm tra luật cũ và các trường hợp Boss Rush như pentomino xóa 5 hàng, deadline, ngưỡng tử, đá bất hoại, shield, Hold và chuyển boss. Checklist ở `docs/KIEM-THU.md`.

## Tổ chức code

- `Core/GameEngine.cs`: va chạm, rơi/xoay, khóa và ghi điểm.
- `Core/Pieces.cs`: tetromino/pentomino, kick và bộ sinh khối.
- `Core/BossRush.cs`: đường đi, máu, nhiệm vụ, hiệu ứng và chuyển boss.
- `UI/MainForm.cs`: nhập liệu, menu và HUD.
- `UI/BossPortraits.cs`: 12 chân dung pixel, cache bitmap.
- `Audio/AudioEngine.cs`: mixer waveOut, đổi nhạc, SFX.
- `Data/SaveStore.cs`: lưu JSON, backup, bảng điểm và thống kê.
- `tools/make_boss_audio.py`: mã tạo nhạc/SFX; chỉ cần Python/numpy nếu muốn tạo lại WAV. Người chơi không cần Python.

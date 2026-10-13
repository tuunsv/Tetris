> Tài liệu này mô tả ba chế độ thường. Boss Rush có luật riêng trong [BOSS-RUSH.md](BOSS-RUSH.md).

# Luật của Tetris 118, phiên bản 1

Bản này chốt một ruleset cụ thể để dễ giải thích và so tài nội bộ. Những chi tiết timing/điểm của các bản Tetris thương mại có thể khác nhau; không khẳng định tương thích mọi trò chơi hay giải đấu.

## Bàn và chuỗi khối

Mảng `Board[40,10]`: hàng 0–19 ẩn, 20–39 hiện. Ô 0 là trống; 1–7 là loại khối. Khối hiện tại chưa được ghi vào Board đến lúc khóa.

Mỗi túi gồm đủ I/O/T/S/Z/J/L, xáo Fisher–Yates. Queue luôn đủ preview 5 khối. Seed dùng cho Random, lưu trong record. Cùng seed + cùng bản build/runtime cho cùng queue; hold làm khác thứ tự sử dụng khối trong thực tế.

Spawn: rotation 0, x=3, y=18. Nếu vị trí không trống thì block-out. Nếu hàng dưới hợp lệ, khối được dịch ngay xuống y=19. Khi toàn bộ bốn ô khóa ở trên vùng nhìn thấy thì lock-out. Một khối khóa có ít nhất một ô nhìn thấy được phép tiếp tục nếu spawn kế tiếp không bị chặn.

## Xoay và khóa

SRS: 0/R/2/L, chỉ ±90°. Nhóm J/L/S/T/Z xoay quanh ô giữa 3×3; I quanh tâm 4×4. O không đổi vị trí và không dùng xoay để gia hạn khóa. Mỗi lượt xoay thử vị trí ban đầu và tối đa bốn kick theo thứ tự. Nếu thất bại giữ nguyên mọi trạng thái.

Bảng kick ghi Y dương hướng lên, trong mảng màn hình Y tăng xuống. `ny = Y - dy` là chỗ chuyển quy ước.

Khóa trễ 0,5 giây khi grounded. Dịch ngang/xoay thành công bắt đầu từ vị trí grounded được reset đồng hồ tối đa 15 lần mỗi khối. Đi xuống một hàng sâu hơn vị trí sâu nhất trước đó cũng reset thời gian, nhưng không nạp lại số lần reset thao tác. Thời gian không tăng khi đang trên không. Hard drop khóa ngay, kể cả khi rơi 0 ô.

## Điều khiển và thời gian

- Soft drop thêm một bước/25 ms, cộng với trọng lực tự nhiên; chỉ bước do soft drop được +1 điểm.
- DAS mặc định 150 ms (80–250), ARR 40 ms (10–100).
- Nhấn ngang đi ngay một ô. Sau DAS bắt đầu lặp theo ARR.
- Hai hướng ngang cùng giữ: hướng nhấn sau ưu tiên. Khi nhả hướng đó, hướng còn giữ bắt đầu lại DAS và đi một ô.
- Xoay/hold/hard drop chỉ xảy ra ở lần key-down đầu, không theo auto-repeat hệ điều hành.
- Bước mô phỏng 1/120 giây, UI Timer 16 ms và `Stopwatch` tích lũy thời gian.
- Pause không tính vào thời gian, không nhận lệnh chơi, không cho quan sát bàn.
- Nếu một frame trễ hơn 250 ms, tự pause có thông báo để tránh âm thầm bỏ thời gian mô phỏng.
- Không có entry delay hoặc khoảng chờ xóa hàng; flash xóa hàng là hiệu ứng vẽ, không ngừng logic.

## Điểm

Điểm cơ bản nhân với cấp **trước** lần xóa hàng. Điểm soft/hard drop không nhân cấp.

| Loại | 0 hàng | 1 hàng | 2 hàng | 3 hàng | 4 hàng |
|---|---:|---:|---:|---:|---:|
| Thường | 0 | 100 | 300 | 500 | 800 |
| T-spin mini | 100 | 200 | — | — | — |
| T-spin đầy đủ | 400 | 800 | 1200 | 1600 | — |
| Thưởng perfect clear | — | 800 | 1200 | 1800 | 2000 |

Mini xóa từ 2 hàng được nâng thành full trong ruleset này. B2B perfect-clear 4 hàng thưởng PC 3200 thay 2000.

T-spin cần khối T, thao tác thay đổi vị trí cuối là xoay, ít nhất 3/4 góc quanh tâm bị chặn. Hai góc phía đầu T bị chặn hoặc dùng kick thứ 5 → full; còn lại mini. Move/soft drop/gravity/hard drop có di chuyển thật sẽ bỏ cờ xoay. Hard drop 0 ô giữ cờ đó. Xoay thất bại không hủy lần xoay thành công trước.

Difficult clear là Tetris (4 hàng) hoặc T-spin có xóa hàng. Hai difficult clear liên tiếp trong chuỗi B2B: difficult clear từ lần thứ hai được nhân 1,5 **điểm cơ bản**. Đặt khối không xóa hàng không phá B2B. Xóa hàng thường phá B2B. Zero-line T-spin không tự bật/tắt B2B.

Combo: lần xóa đầu combo=0, tiếp theo=1,…; thưởng `50 × combo × cấp`. Không xóa hàng reset combo về -1. Thành tựu combo 3 nghĩa là bốn lần xóa hàng liên tiếp.

Perfect clear được kiểm tra sau khi nén hàng; toàn bộ 40 hàng phải trống. Thưởng PC cộng vào điểm xóa/B2B và combo, không thay chúng.

## Ba chế độ

- **Marathon:** không có mốc kết thúc; cấp `min(20, 1+lines/10)`. Khoảng rơi mỗi ô: `max(0.005, (0.8−0.007×(level−1))^(level−1))` giây.
- **Sprint:** đủ ít nhất 40 hàng là thắng. Cấp/trọng lực luôn 1. Bảng điểm xếp ván hoàn thành trước, rồi thời gian tăng dần. DNF sau cùng, số hàng giảm dần.
- **Ultra:** 120 giây mô phỏng chủ động; hết giờ là hoàn thành, top-out sớm là thua. Cấp/trọng lực luôn 1. Điểm giảm dần, hòa điểm ưu tiên thời gian thấp hơn (có thể là ván kết thúc sớm).

Records và settings là file cục bộ có thể chỉnh sửa. Không dùng làm hệ thống xác minh giải đấu có giải thưởng; chưa có replay, anti-cheat, xác thực máy chủ hay multiplayer. Với thi đua trong lớp, thống nhất bản build, chế độ, seed và cách giám sát trước khi chơi.

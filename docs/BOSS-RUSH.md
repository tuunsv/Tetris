# Luật Boss Rush và các quyết định thiết kế

## Hành trình

Ba boss khác nhau được xáo từ 12 boss bằng seed. Người chơi bắt đầu với 5 HP. Không hồi máu giữa trận. Sân đầy, không spawn được, hoặc vượt ngưỡng tử thì thua ngay; HP không cứu top-out. Vượt boss dọn sân/đá/Hold, xóa hiệu ứng, reset combo và level của trận; tổng điểm, thời gian chơi và thống kê vẫn cộng dồn.

Mỗi boss thường có ba ấn. Mỗi nhiệm vụ thành công nhận một ấn; đủ ba hạ boss. Hoàn thành trước hạn bắt đầu ngay nhiệm vụ mới với đủ 60 giây. Hết hạn kích hoạt đòn và đổi nhiệm vụ, giữ số ấn. Đồng hồ dừng khi pause, intro 5 giây hoặc cảnh hạ boss 6,5 giây. Không cộng thời gian cảnh chuyển vào thời gian chơi.

Ranni không dùng nhiệm vụ 60 giây: xóa 80 hàng trong trận để đạt level 9. Level mỗi trận = 1 + số hàng trận / 10, tối đa 20.

## Mười hai boss

| Boss | Nội tại | Nếu trễ nhiệm vụ |
|---|---|---|
| Godrick | Túi tám: đủ bảy tetromino và một pentomino chọn từ 12 loại, xáo chung | Mất 1 HP; ba khối sắp ra được ưu tiên là pentomino. Sau đó tiếp tục túi đang có |
| Malenia | Tốc độ rơi ×2 | Mất 1 HP; 15 giây dùng tốc độ tối đa 5 ms/ô, lock delay 140 ms, tối đa 5 lần reset do dịch/xoay |
| Rennala | Chỉ được chất 16 hàng tính từ đáy | Mất 1 HP; giới hạn 13 hàng trong 30 giây. Nếu ô đã khóa vượt vạch mới thì thua ngay |
| Radagon | Nhiệm vụ luôn là xóa 8 hàng | Trễ là thua ngay, không phụ thuộc HP |
| Radahn | Lock delay bằng 0, chạm nền khóa ngay | Mất 1 HP; thêm ba ô đá 99 cố định, ưu tiên giữa hàng đáy rồi lấp dần. Có thể hóa đá ô đã có. Đá không bị xóa/di chuyển đến hết trận |
| Margit | Tốc độ rơi ×1,5 | Đưa HP về 1. Nếu đã 1 vẫn là 1, đòn này không trực tiếp giết |
| Ranni | Không có nội tại bất lợi | Không tấn công. Đạt level 9 là vượt boss |
| Godfrey | Bốc tetromino độc lập, không 7-bag; ẩn Next và Ghost | Mất 2 HP |
| Gideon Ofnir | Mỗi nhiệm vụ báo trước một đòn vay mượn, tên trên HUD | Dùng đòn của một boss khác; loại Ranni và chính Gideon. Bao gồm đòn chí tử Radagon |
| Maliketh | Vào trận HP còn 1, được một giáp ban đầu; thành công nhiệm vụ hồi giáp về 1 | Gây 1 sát thương. Giáp chặn một lần rồi mất; không có giáp là thua |
| Mohg | Cứ 20 giây gây chảy máu: xóa 2 hàng trong 12 giây để giải, nếu không mất 1 HP | Mất 1 HP ngay và bị một đợt chảy máu 12 giây |
| Morgott | Mỗi 8 khối đã khóa, khóa Hold trong 5 giây | Mất 1 HP và khóa Hold 20 giây |

Malenia: “5 lần” là số lần reset lock delay, tính cả dịch ngang/xoay khi chạm nền; không phải cấm xoay khối đang trên không sau năm lần. Diễn giải 140 ms được dùng vì cụm “1.4 ở ms” trong yêu cầu chưa rõ đơn vị.

Radahn: triển khai đúng ba **ô**, không phải ba hàng. Khi hàng khác bị xóa, các đoạn cột giữa đá được nén riêng để đá không đổi tọa độ. Một hàng chứa đá không được xóa; có thể tiếp tục xây phía trên. Nếu đòn tạo đá trùng khối đang rơi, khối được nâng đến vị trí hợp lệ.

## Nhiệm vụ ngẫu nhiên

- Xóa 8 hàng.
- Hai lần xóa từ 2 hàng trở lên; hoặc xóa tổng cộng 10 hàng.
- Ghi 2.400 điểm trong lượt nhiệm vụ (kể cả điểm rơi); hoặc xóa 10 hàng.
- Ba lần xóa tiếp nối combo; hoặc xóa 10 hàng. Combo ở đây tính các lần xóa có `Combo > 0`, không buộc cả ba nằm trong một chuỗi.
- Một T-spin có xóa hàng; hoặc xóa 8 hàng.

Điều kiện điểm được kiểm tra khi khối khóa. Những đường thay thế bằng xóa hàng giúp không bị kẹt vì ngẫu nhiên không cho khối phù hợp. 10 giây cuối phát cảnh báo một lần. Đòn được xử lý ở tick hết hạn; nhiệm vụ chưa được ghi nhận trước tick đó tính là trễ.

## Pentomino

12 loại F/I/L/P/N/T/U/V/W/X/Y/Z, mỗi loại có năm ô. Túi thường ở ba chế độ cũ vẫn chỉ có bảy tetromino. Pentomino dùng kick đơn giản riêng, không gọi là SRS tiêu chuẩn. Xóa năm hàng có nhãn PENTA CLEAR, điểm cơ bản 1.200 × level; Perfect Clear năm hàng thưởng thêm 4.000 × level. T-spin vẫn chỉ dành cho tetromino T.

## Âm thanh và hình ảnh

Chân dung là pixel-art tự vẽ bằng hình học, không phải ảnh lấy từ game. Nhạc boss là nhạc tổng hợp tự soạn, mỗi boss có giai điệu, giọng, tốc độ và phối khí riêng. Chế độ thường giữ Type B; menu giữ bản menu tự soạn. Chưa tích hợp bản thu “The Final Battle”. Hiệu ứng hạ boss/hoàn thành hành trình dài 6,5 giây, vẫn tuân theo âm lượng SFX và Mute.

Bảng điểm Boss Rush xếp: hoàn thành → số boss hạ → điểm → thời gian. Các mode có bảng riêng. Đây là mode thử thách tự thiết kế.
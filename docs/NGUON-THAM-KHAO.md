# Nguồn và phần được hỗ trợ

## Xoay khối

MIT Hardness Group: Erik D. Demaine, Holden Hall, Jeffery Li — *Tetris with Few Piece Types*, FUN 2024. Mục 2 mô tả SRS; bảng 2 và 3 liệt kê kick thường và kick I, Y dương hướng lên.

https://erikdemaine.org/papers/TetrisPieces_FUN2024/paper.pdf

Dùng để đối chiếu dữ liệu SRS; không sử dụng mã nguồn game của tác giả. Các luật timing, modes, lưu dữ liệu và UI của bản này được mô tả riêng trong `LUAT-CHOI.md`.

## Nhạc

*Korobeiniki* là giai điệu dân ca Nga gắn với Theme A của bản Tetris Game Boy. Bản nhạc tham khảo ở giọng La thứ, nhịp 4/4:

https://www.flutetunes.com/tunes.php?id=192

Đã xem thêm bản chuyển giai điệu thành các nốt của Robson Couto để đối chiếu cao độ và độ dài nốt:

https://github.com/robsoncouto/arduino-songs/blob/master/tetris/tetris.ino

Dự án không chép chương trình Arduino; `tools/make_audio.py` tự tạo PCM, bè bass, arpeggio, envelope và delay. Tốc độ chọn 132 BPM để nghe mềm hơn. Không kèm bản ghi âm, soundfont, đồ họa hoặc logo Nintendo. SFX được tổng hợp riêng.

## Phần nền tảng

- C# Windows Forms: Form, event, Timer, OnPaint, Graphics.
- .NET DataContractJsonSerializer: đọc/ghi hồ sơ JSON.
- Windows Multimedia waveOut: phát buffer PCM. Khai báo native và mixer là phần nâng cao, cần đọc giải thích trong code.

## Bản Boss Rush

- Type B: nhạc Tetris Game Boy (1989), Hirokazu Tanaka; tham chiếu bản chép của Mikhail Hogrefe tại https://github.com/mhogrefe/accurate-video-game-sheet-music/tree/main/gb/tetris/03-b-type-music . Dữ liệu nốt được tổng hợp lại bằng âm sắc riêng trong AudioEngine.
- 12 nhạc boss và SFX mới được tạo bằng `tools/make_boss_audio.py`, không lấy bản ghi soundtrack Elden Ring. Menu là nhạc tổng hợp riêng, chưa chứa The Final Battle.
- Chân dung trong `UI/BossPortraits.cs` là hình tự dựng bằng code. Tên nhân vật là tham chiếu fan project, không phải sản phẩm chính thức.
- Luật boss, nhiệm vụ và điểm pentomino là thiết kế riêng của bản này; xem BOSS-RUSH.md.

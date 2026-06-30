# UDM_17 – Network Caro Game

## 1. Project Overview

UDM_17 – Network Caro Game là dự án xây dựng ứng dụng game cờ Caro (Gomoku) chơi qua mạng sử dụng mô hình Client–Server với giao thức TCP Socket.

Hệ thống cho phép nhiều người chơi kết nối đến server và thực hiện thi đấu 1vs1 theo thời gian thực. Toàn bộ logic trò chơi được xử lý phía server nhằm đảm bảo tính đồng bộ, ổn định và chính xác giữa các người chơi.

Ứng dụng được phát triển bằng C# (.NET) với Windows Forms, cung cấp giao diện trực quan giúp người chơi dễ dàng thao tác, theo dõi trận đấu và tương tác với hệ thống.

Dự án được thực hiện trong khuôn khổ học phần Network Programming.

---

# 2. Project Information

* **Project Code:** UDM_17
* **Project Title:** Network Caro Game
* **Project Type:** Network Programming Project
* **Architecture:** Client – Server
* **Protocol:** TCP Socket
* **Programming Language:** C# (.NET Framework)

---

# 3. Team Members & Responsibilities

Để đảm bảo tiến độ phát triển và tối ưu hiệu quả làm việc nhóm, các thành viên được phân công nhiệm vụ cụ thể như sau:

---

## 1. Nguyễn Trọng Nhân – Server Core Engineer

**Vai trò**
Xây dựng và quản lý hệ thống Server.

**Nhiệm vụ**

* Xây dựng TCP Server bằng TcpListener
* Quản lý kết nối nhiều client
* Quản lý vòng đời client (session)
* Tổ chức phòng chơi (GameRoom)
* Đảm bảo xử lý đa luồng (thread-safe)

---

## 2. Lâm Gia An – Matchmaking & Game Flow Engineer

**Vai trò**
Xây dựng hệ thống Lobby và điều phối trận đấu.

**Nhiệm vụ**

* Quản lý danh sách người chơi online
* Đồng bộ danh sách tới client
* Ghép cặp người chơi
* Điều phối luồng trận đấu (start / in-game / end)
* Kết nối Server và Game Logic

---

## 3. Tran Thi Anh Nguyet – Game Logic Engineer

**Vai trò**
Xây dựng logic và luật chơi Caro.

**Nhiệm vụ**

* Quản lý bàn cờ
* Xây dựng lượt chơi (turn-based)
* Kiểm tra nước đi hợp lệ
* Xác định thắng / thua
* Đồng bộ trạng thái game

---

## 4. Ho Nguyen Dang Khoa – UI & Network Engineer

**Vai trò**
Phát triển giao diện và kết nối Client–Server.

**Nhiệm vụ**

* Xây dựng giao diện WinForms
* Xử lý chuyển form và sự kiện UI
* Kết nối TCP Client tới Server
* Serialize / Deserialize dữ liệu (JSON)
* Gửi / nhận dữ liệu bất đồng bộ

---

## 5. Lê Hoàng Anh – Integration & System Engineer

**Vai trò**
Tích hợp hệ thống và đảm bảo hoạt động end-to-end.

**Nhiệm vụ**

* Kết nối UI – Network – Game Logic
* Xử lý luồng dữ liệu toàn hệ thống
* Đồng bộ trạng thái game
* Kiểm thử Client–Server end-to-end
* Debug và xử lý lỗi hệ thống

---

# 4. Main Features

## 🔹 Connection & Communication

* Client kết nối đến Server thông qua TCP Socket
* Server quản lý nhiều client đồng thời
* Đồng bộ dữ liệu giữa các người chơi theo thời gian thực

## 🔹 Gameplay

* Chơi cờ Caro 1vs1 theo lượt (turn-based)
* Hệ thống tự động kiểm tra thắng/thua
* Kiểm tra tính hợp lệ của nước đi
* Hiển thị quân cờ X/O trực quan
* Đồng bộ trạng thái bàn cờ realtime
* Tích hợp countdown timer cho mỗi lượt chơi

## 🔹 Matchmaking System

* Người chơi đăng nhập bằng username
* Hiển thị danh sách người chơi online
* Gửi lời thách đấu đến người chơi khác
* Tạo và quản lý trận đấu giữa hai client

## 🔹 User Interface

* Giao diện Windows Forms trực quan
* Tương tác bằng chuột trên bàn cờ
* Hiển thị lượt chơi và trạng thái trận đấu
* Hiển thị thông báo kết quả và countdown timer

---

# 5. System Architecture

Hệ thống được xây dựng theo mô hình:

```text
Client A  <--TCP-->  Server  <--TCP-->  Client B
```

## 🔹 Server Responsibilities

* Lắng nghe và quản lý kết nối từ nhiều client
* Điều phối trận đấu giữa các người chơi
* Xử lý logic game và kiểm tra tính hợp lệ của nước đi
* Đồng bộ trạng thái game giữa các client
* Quản lý countdown timer và kết quả trận đấu

## 🔹 Client Responsibilities

* Hiển thị giao diện người dùng (GUI)
* Gửi nước đi và yêu cầu đến server
* Nhận và cập nhật trạng thái game realtime
* Hiển thị bàn cờ và kết quả trận đấu

---

# 6. Graphical User Interface (GUI)

Ứng dụng sử dụng Windows Forms (WinForms) để xây dựng giao diện người dùng.

## Các màn hình chính:

### 🔹 Login Form

* Nhập username
* Kết nối đến server

### 🔹 Lobby Form

* Hiển thị danh sách người chơi online
* Gửi lời thách đấu

### 🔹 Game Form

* Hiển thị bàn cờ Caro
* Thực hiện nước đi
* Hiển thị countdown timer
* Hiển thị trạng thái trận đấu và kết quả

### 🔹 History Form

* Hiển thị lịch sử trận đấu đã chơi

---

# 7. Technologies Used

* **Programming Language:** C# (.NET Framework)
* **GUI Framework:** Windows Forms
* **Network Programming:** TCP Socket
* **Architecture:** Client–Server
* **Concurrency:** Thread / Async
* **IDE:** Visual Studio

---

# 8. Project Structure

```text
UDM_17/
│
├── Client/        # Ứng dụng phía người chơi (GUI + kết nối server)
├── Server/        # Server trung tâm (xử lý logic game & kết nối)
└── Shared/        # Thành phần dùng chung (Model, DTO, Message,…)
```

---

# 9. Project Objectives

Mục tiêu của dự án:

* Áp dụng kiến thức TCP Socket Programming
* Xây dựng hệ thống Client–Server hoàn chỉnh
* Phát triển ứng dụng realtime multiplayer game
* Thiết kế GUI bằng Windows Forms
* Hiểu và xử lý đồng bộ dữ liệu thời gian thực
* Áp dụng xử lý đa luồng trong lập trình mạng

---

# 10. Current Status

## ✅ Completed – Functional System

Đã hoàn thành:

* Xây dựng TCP Server
* Phát triển Client GUI
* Kết nối Client–Server
* Gameplay Caro hoàn chỉnh
* Đồng bộ nước đi realtime
* Countdown timer
* Lobby và challenge player
* Lưu lịch sử trận đấu

## 🔹 Future Improvements

* Thêm hệ thống phòng chơi (Room System)
* Chat realtime giữa người chơi
* Tối ưu async và đa luồng
* Thêm reconnect system
* Tối ưu giao diện người dùng

---

# 11. How to Run

## ▶️ Server

1. Open solution bằng Visual Studio
2. Build project Server
3. Run Server application
4. Server bắt đầu lắng nghe kết nối

## ▶️ Client

1. Build project Client
2. Run Client application
3. Nhập username
4. Kết nối server
5. Bắt đầu chơi game

---

# 12. Course Information

* **Course:** Network Programming
* **Project Type:** Group Project
* **Project Code:** UDM_17

© Net_Group 02


# 13. Tiến Độ & Các Mốc Quan Trọng Của Dự Án

*Danh sách này được cập nhật hàng tuần nhằm theo dõi tiến độ phát triển của dự án.*

* [x] **Tuần 1:** Khởi tạo cấu trúc repository, phân chia vai trò thành viên và xây dựng tài liệu README.
* [x] **Tuần 2:** Hoàn thiện đề xuất dự án (`DOCX`), thiết kế giao diện mẫu ban đầu và xây dựng cấu trúc dữ liệu truyền nhận.
* [] **Tuần 3:** Triển khai giao tiếp TCP Socket giữa Server và Client, đồng thời xây dựng giao diện GUI cơ bản.
* [] **Tuần 4:** Tích hợp gameplay Caro, thuật toán kiểm tra thắng/thua và countdown timer.
* [] **Tuần 5:** Thực hiện kiểm thử hệ thống, sửa lỗi và tối ưu đồng bộ dữ liệu realtime.
* [] **Tuần 6:** Hoàn thiện slide thuyết trình (`PPTX`), video demo và chuẩn bị cho buổi bảo vệ dự án.

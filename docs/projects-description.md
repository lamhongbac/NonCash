# Cấu trúc Dự án & Phân định Vai trò (Project Responsibilities)

Tài liệu này mô tả chi tiết ý nghĩa và trách nhiệm của từng Project (.csproj) trong Solution `NonCash`, tuân thủ theo chuẩn kiến trúc 3 lớp (3-Layer Architecture) và hướng tiếp cận Domain-Driven Design (DDD) cơ bản, đảm bảo sự tách biệt về trách nhiệm (Separation of Concerns).

---

## 1. NonCash.Shared (Thư viện dùng chung)
* **Loại dự án:** Class Library
* **Ý nghĩa:** Đây là tầng thấp nhất chứa các thành phần mà *bất kỳ project nào* (GUI, API, Core, Infrastructure) cũng có thể dùng chung.
* **Thành phần điển hình:**
  * Các hằng số (Constants).
  * Enum dùng chung (ví dụ: `VoucherStatus`, `ApprovalState`).
  * DTO (Data Transfer Objects) - Các obj trao đổi qua HTTP APIs.
  * Extention methods hoặc Utils chung (không chứa logic nghiệp vụ phức tạp).

## 2. NonCash.Core (Tầng Lõi Nghiệp Vụ - BLL)
* **Loại dự án:** Class Library
* **Ý nghĩa:** Trái tim của toàn bộ hệ thống. Tầng này biểu diễn **Business Logic Layer (BLL)** hoặc *Domain Layer*. Tất cả quy tắc kinh doanh khắt khe (ví dụ: Voucher hết hạn ra sao, Mã định danh tạo thế nào) sẽ phải nằm ở đây. Tầng Core **hoàn toàn độc lập** với cơ sở dữ liệu (Database) hay Giao diện (UI).
* **Thành phần điển hình:**
  * **Entities:** Các đối tượng thực thể kinh doanh đại diện cho CSDL (Voucher, Customer, Business, ProductionPlan).
  * **Interfaces:** Định nghĩa cái hợp đồng (Contract) cho Repository (ví dụ: `IVoucherRepository`), giúp tầng Core giao tiếp với Database mà không cần biết Data base dùng là SQL hay MongoDB.
  * **Services / Microservices logic:** Nơi chứa code xử lý theo luồng: `CreatePlan()`, `ApprovePlan()`, `UseVoucher()`. 

## 3. NonCash.Infrastructure (Tầng Dữ Liệu - DAL)
* **Loại dự án:** Class Library
* **Ý nghĩa:** Đây là **Data Access Layer (DAL)**. Nhiệm vụ duy nhất của nó là tương tác với cơ sở hạ tầng bên ngoài, cụ thể là **Cơ sở dữ liệu PostgreSQL** (Entity Framework Core) và các dịch vụ thứ 3 (như gửi Email/SMS nếu có).
* **Thành phần điển hình:**
  * **DbContext:** Đối tượng điều khiển cầu nối EF Core với Database.
  * **Repositories:** Những Class triển khai (implement) các Interfaces đã định nghĩa sẵn ở tầng Core. Tại đây sẽ chứa mã thực tiễn gọi DB.
  * **Migrations (EF):** Các file tự động tạo bảng (Tables) thông qua Code-First script.

## 4. NonCash.API (Tích Hợp Dịch Vụ - Trạm thu phát)
* **Loại dự án:** ASP.NET Core Web API
* **Ý nghĩa:** Tầng giao diện RESTful. Vai trò chính của project này là cung cấp các **API Endpoints** dùng trong tích hợp với **hệ thống máy quẹt thẻ POS** bên ngoài, hay các ứng dụng cho Mobile App.
* **Đặc tính kỹ thuật:**
  * Triển khai xác thực (Authentication) cực kỳ mạnh mẽ qua **JWT Tokens** và **API Keys**.
  * Chứa các Controller tiếp nhận HTTP Request (`POST /api/voucher/use`), giải mã Request, đẩy xuống gọi logic ở tầng `NonCash.Core` và trả về kết quả cấu trúc JSON.

## 5. NonCash.Web (Giao Diện Người Dùng - UI)
* **Loại dự án:** Blazor Web App
* **Ý nghĩa:** Đóng vai trò là Frontend (SaaS Platform). Đây là trang vận hành nội bộ giành cho các Quản lý của doanh nghiệp (Business tenants). 
* **Tác vụ được thực hiện ở màn hình này:**
  * Lên lịch chạy chiến dịch (Production Planning).
  * Cấp quản lý xem, phê duyệt (Approval) hoặc từ chối thông qua giao diện.
  * Quản lý phân quyền user, quản lý danh sách KH, xem hệ thống báo cáo và tra cứu các Voucher In-Use.

## 6. NonCash.UnitTests & NonCash.IntegrationTests (Kiểm Thử)
* **Loại dự án:** xUnit Test Project
* **Ý nghĩa:** Đảm bảo mã nguồn được viết ra đạt chất lượng cao:
  * **UnitTests:** Kiểm thử cắt lớp độc lập cho từng hàm (method) ở tầng Core (Ví dụ viết hàm tính toán tổng tiền, và gõ lệnh test xem output trả có khớp kỳ vọng không).
  * **IntegrationTests:** Kiểm thử tích hợp chạy dọc từ đầu đến cuối luồng, xác minh Web API khi gọi xuống database rồi trả lên có bị lỗi HTTP/500 nào không.

---
### Tóm tắt luồng dữ liệu chuẩn:
1. `NonCash.Web / API` gọi xuống `NonCash.Core` (Interfaces & Services) để yêu cầu xử lý công việc.
2. `NonCash.Core` xử lý logic tính toán, nhưng khi cần lấy/lưu dữ liệu, nó sẽ gọi tới `NonCash.Infrastructure`.
3. `NonCash.Infrastructure` chọc thẳng vào CSDL lấy Data, đưa ngược lên cho `Shared / Core` đóng gói và đẩy ngược về Web/API hiển thị cho con người dử dụng.

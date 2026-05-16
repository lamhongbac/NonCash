---
stepsCompleted: [step-01-validate-prerequisites.md, step-02-design-epics.md]
inputDocuments: ["Key Functionalities.txt", "description.txt", "docs/architecture.md", "docs/data-models.md", "docs/api-contracts.md"]
---

# NonCash - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for NonCash, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

- **FR1:** Lập kế hoạch sản xuất voucher (Bao gồm thông tin chung Plan Header, danh sách thuộc tính Plan Detail và Thiết lập các mục tiêu "Target". Trong Plan Header có thiết lập "Phạm vi áp dụng" để giới hạn voucher chỉ sử dụng được tại các Outlet/Điểm bán cụ thể).
- **FR2:** Luồng duyệt và xuất bản kế hoạch (Review workflow, quản lý trạng thái: Phê duyệt/Từ chối, cập nhật Publish Date, phiên bản hoá).
- **FR3:** Phân phối voucher qua kênh Bán hàng (Sale/Tự Mua), cho phép người dùng tự chọn và thanh toán.
- **FR4:** Phân phối voucher qua hình thức Batch Promotion (Import danh sách số điện thoại và gửi voucher thẳng vào Inbox của khách hàng).
- **FR5:** Tính năng chuyển nhượng, cho/tặng voucher qua số điện thoại/MemberID (Yêu cầu xác nhận 2 chiều từ người cho và người nhận).
- **FR6:** Quy trình sử dụng voucher tại quầy POS với cơ chế an toàn 6 bước (Lock voucher -> POS xử lý -> Backend Commit/Rollback, bảo đảm tính toàn vẹn giao dịch).
- **FR7:** Quản lý Khách hàng / Customer Management (Quản lý hồ sơ, Blacklist, Import/Export danh sách).
- **FR8:** Quản lý Doanh nghiệp / Business Management (Quản lý các đối tác tổ chức sử dụng chung nền tảng theo định danh `BrandID`).
- **FR9:** Kế hoạch và Quản lý Điểm bán hàng (Outlet / Store Management) — Chức năng khai báo, cấu hình và quản lý các điểm bán trực thuộc một Brand. Đây là nơi sẽ cấp phép, thiết lập thông tin cho các hệ thống POS thực tế quét và sử dụng voucher.

### NonFunctional Requirements

- **NFR1:** Bảo mật mã voucher tĩnh bằng "Dynamic Voucher Code" (Nguyên lý giống JWT để chống copy/gian lận).
- **NFR2:** Toàn vẹn giao dịch (Transaction Begin/Commit/Rollback) đối với quy trình redeem tại POS.
- **NFR3:** Quản lý phân quyền chặt chẽ (Role-Based Access Control) cho từng chức năng như tạo và duyệt kế hoạch.
- **NFR4:** Cô lập dữ liệu Đa khách thuê (Multi-Tenancy) theo `BrandID`.

### Additional Requirements

- Mô hình 3-Layer SaaS (Blazor Frontend, .NET Core Microservices Backend, PostgreSQL Database).
- Định danh qua JWT Token cho người dùng và API Key cho các thiết bị POS.
- Tổ chức theo cấu trúc microservices: Planning, Approval, Distribution, Usage, Identity & Tenant.

### UX Design Requirements

*(Không có dữ liệu)*

### FR Coverage Map

- **FR1:** Epic 2 - Lập kế hoạch sản xuất
- **FR2:** Epic 2 - Luồng duyệt và xuất bản
- **FR3:** Epic 3 - Phân phối qua kênh Bán hàng
- **FR4:** Epic 3 - Phân phối qua Khuyến mãi hàng loạt
- **FR5:** Epic 5 - Chuyển nhượng / Cho tặng qua điện thoại
- **FR6:** Epic 4 - Quét mã và xử lý giao dịch tại POS
- **FR7:** Epic 1 - Quản lý Khách hàng
- **FR8:** Epic 1 - Quản lý Doanh nghiệp/Thương hiệu 
- **FR9:** Epic 1 - Cấu hình Điểm bán hàng (Outlets)

## Epic List

### Epic 1: Quản lý Hồ sơ cốt lõi (Core Profiles & Onboarding)
Đảm bảo hệ thống có đủ định danh và nền tảng thông tin hoàn chỉnh. Người vận hành có thể khởi tạo các Thương hiệu (Brand), thiết lập các Điểm bán (Outlet) nơi sẽ chấp nhận voucher, và quản lý danh sách người dùng cuối (Customer).
**FRs covered:** FR7, FR8, FR9

### Epic 2: Kế hoạch Sản xuất & Phê duyệt Chiến dịch (Campaign Planning & Approval)
Các Brand Manager có thể lập kế hoạch/tạo chiến dịch voucher (ấn định phạm vi điểm bán, mục tiêu phân phát/sử dụng) và trình lên cấp trên duyệt, đảm bảo voucher chỉ được phát hành khi đạt chuẩn an toàn kinh doanh.
**FRs covered:** FR1, FR2

### Epic 3: Phân phối Đa kênh (Multi-channel Distribution & Acquisition)
Doanh nghiệp có thể đưa voucher tới tay khách hàng thông qua hình thức ép tự động (Batch Promotion qua hộp thư) hoặc đưa lên "Quầy hàng" để khách hàng trả tiền tự mua, mở rộng tập khách hàng hiệu quả.
**FRs covered:** FR3, FR4

### Epic 4: Hệ thống Quy đổi (Redemption) & An ninh POS (Redemption & Security)
Tại các điểm bán đã được cấu hình, thu ngân (nhân viên POS) có thể quét động lập và chấp nhận mã voucher một cách an toàn mà không sợ mã giả, bảo đảm nguyên tắc cấn trừ ngay lập tức và toàn vẹn dữ liệu lúc thanh toán (Lock & Commit).
**FRs covered:** FR6

### Epic 5: Lan toả mạng lưới xã hội (Social Engagement & Gifting)
Khách hàng sau khi sở hữu voucher có thể tặng lại cho gia đình hoặc chuyển nhượng qua số tài khoản một cách an toàn thông qua thao tác xác nhận 2 chiều, giúp tối ưu tỉ lệ sử dụng và tính linh hoạt.
**FRs covered:** FR5

<!-- Repeat for each epic in epics_list (N = 1, 2, 3...) -->

## Epic 1: Quản lý Hồ sơ cốt lõi (Core Profiles & Onboarding)

Đảm bảo hệ thống có đủ định danh và nền tảng thông tin hoàn chỉnh. Khởi tạo Brand (Tenant), thiết lập Điểm bán (Outlet) dùng POS, và quản lý người dùng cuối (Customer).

<!-- Repeat for each story (M = 1, 2, 3...) within epic N -->

### Story 1.1: Thiết lập Thông tin Thương hiệu (Brand Setup)

As a System Admin,
I want to tạo mới và quản lý thông tin các Thương hiệu (Brand),
So that mỗi đối tác (tenant) có không gian làm việc độc lập.

**Acceptance Criteria:**

**Given** Quản trị viên đang ở màn hình Quản lý Doanh nghiệp
**When** nhập thông tin tạo mới và lưu lại
**Then** hệ thống khởi tạo thành công một BrandID duy nhất
**And** Brand này xuất hiện ở danh sách đang hoạt động

### Story 1.2: Cấu hình Điểm bán hàng (Outlet Configuration)

As a Brand Manager,
I want to mở mới và quản lý các Điểm bán (Outlet) thuộc sở hữu của mình,
So that tôi có thể thiết lập chính xác các điểm vật lý.

**Acceptance Criteria:**

**Given** Brand Manager đang thao tác trong không gian BrandID của mình
**When** thêm mới một Outlet
**Then** hệ thống lưu trữ Outlet gắn chuẩn xác với BrandID hiện tại
**And** hệ thống sinh ra mã API Key dự kiến

### Story 1.3: Quản lý Danh mục Khách hàng (Customer Record Management)

As a Brand Manager hoặc qua Import,
I want to khởi tạo và đưa khách hàng vào Blacklist,
So that tôi có thể kiểm soát được người hợp lệ và chặn gian lận.

**Acceptance Criteria:**

**Given** một tệp dữ liệu khách hàng
**When** đưa thông tin định danh vào hệ thống
**Then** hồ sơ khách hàng được tạo thành công
**And** có thể đánh dấu danh sách đen (Blacklist)

### Story 1.4: Quản lý Tài khoản Nội bộ & Phân Quyền (Staff Accounts & RBAC)

As a System Admin,
I want to khởi tạo các tài khoản nhân sự và ánh xạ BrandID,
So that mỗi tài khoản hoạt động đúng quyền và đúng Brand.

**Acceptance Criteria:**

**Given** System Admin đang ở giao diện Quản lý Phân quyền
**When** tạo mới một User Account kèm Role và BrandID
**Then** hệ thống khởi tạo tài khoản có cấu hình JWT
**And** cấu hình RBAC được lưu thành công

<!-- End story repeat -->

## Epic 2: Kế hoạch Sản xuất & Phê duyệt Chiến dịch (Campaign Planning & Approval)

Cho phép Brand Manager lập kế hoạch tạo chiến dịch voucher (từ thông tin chung, dải điểm bán áp dụng cho đến từng chi tiết voucher) và đưa vào quy trình duyệt chặt chẽ.

<!-- Repeat for each story (M = 1, 2, 3...) within epic N -->

### Story 2.1: Khai báo Cấu hình Kế hoạch & Voucher (Plan Header Setup)

As a Brand Manager,
I want to khai báo toàn bộ thông tin chiến dịch và thuộc tính của loại voucher sẽ phát hành vào một bản ghi VoucherPlanHeader,
So that mọi thiết lập về ngân sách, thông số voucher và phạm vi áp dụng được quản lý tập trung tại một nơi.

**Acceptance Criteria:**

**Given** Brand Manager đang ở chức năng Tạo Kế Hoạch mới
**When** nhập thông tin dựa sát theo cấu trúc VoucherPlanHeader
**Then** hệ thống lưu bản ghi Header thành công kèm theo Timestamp
**And** gán tự động CreatorID, BrandID, và đưa ApprovalStatus về trạng thái Pending

### Story 2.2: Sinh lô chi tiết Voucher (Generate Plan Details)

As a System/Worker,
I want to hỗ trợ 2 cơ chế sinh thực thể VoucherPlanDetail (Sinh sẵn hàng loạt hoặc Sinh on-demand),
So that hệ thống cấp voucher linh hoạt và bảo vệ chặt chẽ trạng thái kế hoạch.

**Acceptance Criteria:**

**Given** một yêu cầu sinh VoucherPlanDetail
**When** kế hoạch gốc chưa đạt trạng thái Approved
**Then** hệ thống chặn ngay lập tức và báo lỗi
**And** khi kế hoạch là Approved, hệ thống tạo ra Detail với SerialNo và VoucherCode bảo mật (Sử dụng rotating dynamic code / JWT-like token)

### Story 2.3: Trình duyệt & Quản lý Phê duyệt (Approval Workflow)

As a Manager/Approver,
I want to kiểm tra toàn bộ thông số kế hoạch đã trình và thực thi quyền duyệt,
So that đảm bảo ngân sách và các điểm rơi chiến lược chuẩn chỉ trước khi sống.

**Acceptance Criteria:**

**Given** một Plan Header đang chờ duyệt (Pending)
**When** Approver thực hiện Approve hoặc Reject
**Then** ApprovalStatus được cập nhật tương ứng
**And** tự động ghi nhận ApproverID

### Story 2.4: Điều chỉnh & Phiên bản hoá Kế hoạch (Plan Adjustments/Versioning)

As a Brand Manager,
I want to nhân bản hoặc tạo phiên bản mới từ một Kế hoạch đã bị Từ chối (Rejected),
So that tôi có thể điều chỉnh và trình duyệt lại mà vẫn giữ được lịch sử quá trình duyệt.

**Acceptance Criteria:**

**Given** một kế hoạch đang ở trạng thái Rejected
**When** chọn chức năng Clone/Create New Version
**Then** hệ thống sinh ra một bản nháp mới kế thừa toàn bộ dữ liệu cũ
**And** bản cũ vẫn được lưu tĩnh trong Database

<!-- End story repeat -->

## Epic 3: Phân phối Đa kênh (Multi-channel Distribution & Acquisition)

Doanh nghiệp có thể đưa voucher tới tay khách hàng thông qua hình thức ép tự động (Batch Promotion qua hộp thư) hoặc bán trực tiếp trên nền tảng (Sale), và hệ thống hỗ trợ luồng chuyển nhượng (Transfer).

<!-- Repeat for each story (M = 1, 2, 3...) within epic N -->

### Story 3.1: Phân phối Tự động lô lớn (Batch Promotion Distribution)

As a Brand Manager,
I want to tải lên một danh sách khách hàng đích (Số điện thoại / MemberID),
So that hệ thống tự động rót voucher thẳng vào ví của họ.

**Acceptance Criteria:**

**Given** Một chiến dịch đang ở trạng thái hiệu lực (Approved/Published)
**When** Brand Manager nhập danh sách khách hàng và yêu cầu phân phối
**Then** hệ thống gọi quy trình cấp phát lượng VoucherPlanDetail tương ứng
**And** ghi bản log vào VoucherDistribution với Method = Promotion

### Story 3.2: Mua voucher qua kênh Trực tiếp (Self-Purchase B2C/B2B)

As a Member (Customer hoặc Organization),
I want to chọn mua voucher đang mở bán và đóng tiền,
So that tôi hoặc tổ chức của tôi quản lý tập voucher để dùng hoặc phân phát lại.

**Acceptance Criteria:**

**Given** Thành viên đã đăng nhập và xem danh sách Voucher đang bán
**When** thao tác mua và thanh toán
**Then** hệ thống cấp phát số lượng VoucherPlanDetail vào không gian My Voucher của định danh MemID
**And** ghi bản log vào VoucherDistribution với Method = Sale

### Story 3.3: Chuyển nhượng & Định danh Quyền Sở hữu (Gifting / Batch Transfer)

As a Thành viên sử dụng (Cá nhân hoặc Tổ chức),
I want to chuyển quyền sở hữu của một hoặc nhiều voucher bằng một list số điện thoại,
So that tôi/tổ chức phân bổ lại quỹ voucher cho nhân viên khách hàng nhanh chóng.

**Acceptance Criteria:**

**Given** Thành viên cầm tập voucher chưa sử dụng (Pending)
**When** cung cấp N số điện thoại đích và chọn chức năng Chuyển nhượng
**Then** hệ thống map tự động mỗi mã voucher cho 1 số điện thoại vào vị trí MemberID đích
**And** tạo N giao dịch vào VoucherDistribution với Method = Transfer

### Story 3.4: Báo cáo Thống kê Phân phối (Distribution Tracking Dashboard)

As a Brand Manager,
I want to xem báo cáo lịch sử các hoạt động cấp phát/chuyển dịch voucher,
So that tôi giám sát được hiệu năng đẩy so sánh với TargetDistributed.

**Acceptance Criteria:**

**Given** Các giao dịch phân phối đã xảy ra
**When** vào xem Báo cáo phân phối
**Then** báo cáo hiển thị và gom nhóm log từ bảng VoucherDistribution
**And** show con số tổng hợp so sánh với mục tiêu

<!-- End story repeat -->

## Epic 4: Hệ thống Quy đổi (Redemption) & An ninh POS (Redemption & Security)

Tại các điểm bán đã được cấu hình, thu ngân (nhân viên POS) có thể quét độc lập và chấp nhận mã voucher một cách an toàn mà không sợ mã giả, bảo đảm nguyên tắc cấn trừ ngay lập tức và toàn vẹn dữ liệu lúc thanh toán.

<!-- Repeat for each story (M = 1, 2, 3...) within epic N -->

### Story 4.1: Tra cứu Thông tin Voucher (Check for Information)

As a Hệ thống POS tại quầy,
I want to gửi mã vạch lên Backend mà KHÔNG có BillNumber,
So that thu ngân có thể tra cứu nhanh trị giá của voucher mà không ảnh hưởng đến trạng thái.

**Acceptance Criteria:**

**Given** Mã VoucherCode được POS gửi yêu cầu kiểm tra (Không đính BillNumber)
**When** Backend tiếp nhận lệnh
**Then** hệ thống tra cứu tính hợp lệ và trả về thông tin FaceValue
**And** tuyệt đối KHÔNG đổi trạng thái của voucher (UsageStatus = Pending)

### Story 4.2: Kiểm tra & Khóa cấn trừ (Prepare & Lock for Application)

As a Hệ thống POS tại quầy,
I want to gửi mã vạch KÈM THEO mã hóa đơn (BillNumber),
So that Backend trả về giá trị hợp lệ và đồng thời Khóa (Lock) nó ngay.

**Acceptance Criteria:**

**Given** POS gửi lệnh kiểm tra KÈM BillNumber
**When** Backend tiếp nhận và xác định voucher hợp lệ
**Then** Backend trả về thông tin giá trị
**And** NGAY LẬP TỨC đổi UsageStatus = In-Use
**And** lưu lại BillNumber đang khóa voucher

### Story 4.3: Ghi nhận Giao dịch Thành công (Commit & Log)

As a Hệ thống POS tại quầy,
I want to gửi lệnh xác nhận đóng hóa đơn thành công (Commit),
So that lượng voucher này được đánh dấu đã sử dụng vĩnh viễn và Backend ghi nhận.

**Acceptance Criteria:**

**Given** hóa đơn đã được thanh toán xong (voucher đang In-Use)
**When** POS gởi lệnh Commit kèm theo TransactionID và AmountUsed
**Then** Backend đổi vĩnh viễn UsageStatus = Complete và gán UsedDate = Now()
**And** sinh ra bản ghi trong bảng VoucherUsage định danh POS và Transaction

### Story 4.4: Hủy Quy đổi (Rollback Mechanism)

As a Hệ thống POS tại quầy,
I want to gửi lệnh gỡ rào / hủy bỏ giao dịch hóa đơn (Rollback),
So that nếu khách đổi ý hoặc thẻ bị lỗi, voucher được nhả lại trạng thái cũ.

**Acceptance Criteria:**

**Given** voucher đang ở trạng thái Khóa (In-Use)
**When** đơn hàng bị hủy và POS gọi lệnh Rollback
**Then** hệ thống trả trạng thái lại thành Pending
**And** không sinh bản log hoàn thành nào trong VoucherUsage

<!-- End story repeat -->

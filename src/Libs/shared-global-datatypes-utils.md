# 🤖 MSA.Shared Agent Instructions & Reference (Shared Kernel Guide)

> [!IMPORTANT]
> **DÀNH CHO AI AGENT (MANDATORY AGENT INSTRUCTION):** 
> Đây là tài liệu hướng dẫn bắt buộc cho mọi AI Agent tham gia thiết kế, phát triển và tối ưu hóa các ứng dụng/dịch vụ thuộc hệ sinh thái **Media Service Agency (MSA)**.
> Trước khi định nghĩa bất kỳ lớp Entity cơ sở, kiểu trả về kết quả nghiệp vụ, lớp tiện ích (Utility), hoặc hàm Helper nào trong dự án của bạn, bạn **BẮT BUỘC** phải đọc tài liệu này để sử dụng các kiểu dữ liệu và tiện ích dùng chung sẵn có của dự án **MSA.Shared**, tuyệt đối tuân thủ nguyên tắc **DRY (Don't Repeat Yourself)**.

---

## 🧭 1. Chỉ Thị Nghiêm Ngặt Cho Agent (Agent Directives)

*   **Không phát minh lại bánh xe:** Tuyệt đối không tự viết lại các hàm tiện ích xử lý chuỗi (String), số (Number), ngày tháng (DateTime), Enum, bảo mật (Hash/Salt), ánh xạ đối tượng (Map DTO <-> Entity), hay xử lý Bitwise. Hãy dùng `MSA.Shared.Utils`.
*   **Không tạo lại các Class/Enum cơ bản:** Không định nghĩa lại các lớp cơ sở của Database Entity, cấu trúc phân trang, kiểu trả về nghiệp vụ (BLL Response), hoặc các Enum trạng thái hệ thống. Hãy dùng `MSA.Shared.DataTypes`.
*   **Kiểm tra tham chiếu dự án (.csproj):** Khi được yêu cầu viết code nghiệp vụ mới, hãy kiểm tra xem file `.csproj` đã tham chiếu đến `MSA.Shared.DataTypes` và `MSA.Shared.Utils` chưa. Nếu chưa, hãy hướng dẫn người dùng hoặc tự thêm tham chiếu:
    ```xml
    <ItemGroup>
      <ProjectReference Include="..\MSA.Shared\MSA.Shared.DataTypes\MSA.Shared.DataTypes.csproj" />
      <ProjectReference Include="..\MSA.Shared\MSA.Shared.Utils\MSA.Shared.Utils.csproj" />
    </ItemGroup>
    ```

---

## 📦 2. MSA.Shared.DataTypes - Danh Mục Kiểu Dữ Liệu Chuẩn

Bạn phải sử dụng các kiểu dữ liệu này để đảm bảo tính nhất quán trên toàn hệ thống.

### 2.1. Lớp Cơ Sở Entity: `BaseObject`
*   **Vị trí:** `MSA.Shared.DataTypes.BaseObject`
*   **Mô tả:** Chứa các thuộc tính Audit chuẩn để lưu trữ lịch sử bản ghi DB. Mọi Entity lớp dưới bắt buộc phải kế thừa lớp này.
*   **Các thuộc tính sẵn có:**
    *   `CreatedBy` (string): Người tạo.
    *   `CreatedDate` (DateTime?): Ngày tạo.
    *   `UpdatedBy` (string): Người cập nhật cuối.
    *   `UpdatedDate` (DateTime?): Ngày cập nhật cuối.
    *   `IsDelete` (bool?): Đánh dấu xóa mềm.
*   **Cách dùng:**
    ```csharp
    using MSA.Shared.DataTypes;
    
    public class MediaFile : BaseObject
    {
        public int FileId { get; set; }
        public string FileName { get; set; }
        // Các thuộc tính nghiệp vụ khác...
    }
    ```

### 2.2. Chuẩn Kết Quả Nghiệp Vụ: `BOProcessResult`
*   **Vị trí:** `MSA.Shared.DataTypes.BOProcessResult`
*   **Mô tả:** Kiểu dữ liệu trả về chuẩn hóa cho tất cả các hàm xử lý tại tầng Business Logic (BLL). Agent **không được** ném Exception tự do hoặc chỉ trả về `bool` rỗng.
*   **Các thuộc tính cốt lõi:**
    *   `IsSuccess` (bool): Trạng thái xử lý thành công/thất bại.
    *   `Message` (string): Thông điệp phản hồi (tiếng Việt/tiếng Anh chuẩn).
    *   `Code` (string): Mã lỗi chuẩn hóa (ví dụ: `ERR_01`, `AUTH_FAILED`).
    *   `Content` (object): Dữ liệu đi kèm (ID vừa tạo, Object kết quả, v.v.).
*   **Các Hàm Khởi Tạo Nhanh (Factory Methods):**
    *   `BOProcessResult.Success(object content = null)`
    *   `BOProcessResult.Failure(string msg, string code = "")`
*   **Đọc Content An Toàn:** Sử dụng Extension `GetContentAs<T>()` từ `MSA.Shared.Utils`.
*   **Mẫu triển khai chuẩn tại BLL:**
    ```csharp
    using MSA.Shared.DataTypes;
    
    public BOProcessResult UploadFile(byte[] bytes, string fileName)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return BOProcessResult.Failure("File dữ liệu trống hoặc không hợp lệ.", "FILE_ERR_01");
        }
        
        // Logic xử lý upload...
        var resultId = 12345;
        
        return BOProcessResult.Success(new { FileId = resultId, Path = "/uploads/file.png" });
    }
    ```

### 2.3. Các Enum & Cấu Trúc Toàn Cục Sẵn Có
Thay vì khai báo lại trong từng dự án độc lập, hãy sử dụng trực tiếp các Enum được định nghĩa sẵn trong `MSA.Shared.DataTypes`:
*   `EDataOperation`: Phục vụ phân quyền và thao tác dữ liệu (`View`, `Create`, `Update`, `Delete`, `Approval`, `Import`, `Export`).
*   `EFormMode`: Quản lý trạng thái giao diện UI (`View`, `Edit`, `AddNew`).
*   `JwtStatus`: Trạng thái giải mã Token bảo mật (`Valid`, `Expired`, `InvalidSignature`, `NotYetValid`, `Malformed`).
*   `SimpleDataSource` (struct): Cấu trúc nguồn dữ liệu rút gọn dùng để binding dữ liệu lên combobox/dropdown (`ID` dạng string, `Name` dạng string).
*   `PageInfo`: Lớp đại diện thông tin phân trang chuẩn cho các danh sách truy vấn.

---

## 🛠️ 3. MSA.Shared.Utils - Danh Mục Tiện Ích Đa Năng (DRY Utility Index)

Namespace chính: `MSA.Shared.Utils` (Một số class thuộc namespace `MSAUtility` như Security). Chỉ cần `using MSA.Shared.Utils;` và `using MSAUtility;` để kích hoạt các extension method trên các kiểu dữ liệu gốc.

### 3.1. Tiện Ích Chuỗi (`StringUtils.cs`)
Cung cấp các extension method trực tiếp cho kiểu `string`:

| Tên Hàm / Extension | Đầu Vào & Đầu Ra | Mô Tả Chức Năng | Ví Dụ Sử Dụng |
| :--- | :--- | :--- | :--- |
| `DefaultIfEmpty` | `string` -> `string` | Trả về chuỗi mặc định nếu chuỗi gốc rỗng/null (có phân biệt khoảng trắng). | `name.DefaultIfEmpty("Khách ẩn danh")` |
| `Add3Dots` | `(string, int limit)` -> `string` | Cắt chuỗi theo giới hạn ký tự và tự động thêm dấu "..." ở cuối để hiển thị UI. | `"Văn bản dài dòng".Add3Dots(10)` -> `"Văn bản d..."` |
| `IsValidEmail` | `string` -> `bool` | Kiểm tra định dạng Email nhanh bằng Regex chuẩn hóa (không throw Exception). | `email.IsValidEmail()` |
| `IsValidMobileNumber` | `string` -> `bool` | Kiểm tra định dạng số điện thoại di động Việt Nam. | `"0987654321".IsValidMobileNumber()` |
| `TryGetInt` | `string` -> `int` | Ép kiểu chuỗi sang số nguyên an toàn, trả về `0` nếu ép kiểu thất bại chứ không crash. | `"25a".TryGetInt()` -> `0` |
| `GetContent` | `(string, string boundary, Dictionary<string, string> data)` | Động cơ Template đơn giản, thay thế các token nằm giữa ký tự boundary bằng dữ liệu map. | `"Chào #Name#".GetContent("#", data)` |

### 3.2. Tiện Ích Ánh Xạ & Reflection (`ObjectExtention.cs` & `MapperServices.cs`)
Giúp thao tác linh hoạt trên Object, Reflection và Mapster:

*   **Ánh xạ DTO <-> Entity siêu nhanh:** MSA.Shared sử dụng thư viện hiệu năng cao **Mapster** được bọc sẵn trong `MapperServices`. Để ánh xạ đối tượng, hãy dùng extension method `.MapTo<T>()` trực tiếp trên object nguồn:
    ```csharp
    // Map từ Entity sang DTO
    UserDto dto = userEntity.MapTo<UserDto>();
    
    // Map danh sách
    List<UserDto> dtos = userList.MapTo<List<UserDto>>();
    ```
*   **Đọc Content an toàn từ BOProcessResult:**
    ```csharp
    var data = result.GetContentAs<MyResponseDto>();
    ```
*   **Các Helper Object khác:**
    *   `myObj.HasPropertyByObject("PropName")`: Kiểm tra xem đối tượng có thuộc tính chỉ định hay không.
    *   `myObj.SetProperty("PropName", value)`: Gán giá trị động cho thuộc tính qua Reflection.
    *   `myObj.GetPropValue("PropName")`: Lấy giá trị thuộc tính động.
    *   `int?.GetInteger()`: Trả về giá trị của nullable int, nếu null trả về `0`.

### 3.3. Tiện Ích Enum (`EnumUtils.cs`)
Giải quyết triệt để việc hiển thị và binding Enum lên Combobox/Dropdown UI:

*   `EnumHelper<T>.ConvertToDictionary()`: Chuyển toàn bộ Enum `T` thành một `Dictionary<int, string>` (Key là giá trị số của Enum, Value là tên Enum).
*   `typeof(T).GetSimpleDataSource()`: Trả về danh sách `List<SimpleDataSource>` dùng để bind trực tiếp vào ComboBox/Dropdown trên UI.
*   `myEnumInstance.GetEnumDataInfo()`: Trả về danh sách chi tiết chứa thông tin của phần tử enum (Value, Name, Ordinal, Translate).

### 3.4. Tiện Ích Số & Ngày Tháng (`NumberUtils.cs`)
Hỗ trợ tối ưu hóa lưu trữ và truy xuất DateTime bằng cách chuyển đổi thành số nguyên (`int`):

*   **Epoch chuẩn:** Sử dụng mốc thời gian gốc là ngày `1900-01-01`.
*   `dateTime.DateTimeToInt()`: Chuyển đổi một đối tượng `DateTime` sang số nguyên `int` đại diện cho khoảng cách ngày hoặc giây so với Epoch (thường dùng làm định danh, phiên bản, hoặc khóa phụ).
*   `NumberHelper.IntToDateTime(int val)`: Khôi phục ngược lại đối tượng `DateTime` đầy đủ từ số nguyên lưu trữ.

### 3.5. Bảo Mật Cốt Lõi (`SecurityUtils.cs`)
*   **Vị trí Namespace:** `MSAUtility`
*   **Mô tả:** Tập hợp các hàm xử lý mã hóa, băm dữ liệu và bảo mật đăng nhập cơ bản.
*   **Các hàm tĩnh:**
    *   `SecurityUtility.GenerateSalt()`: Tạo một chuỗi salt ngẫu nhiên bảo mật.
    *   `SecurityUtility.HashToken(string password, string salt)`: Băm mật khẩu kèm salt bằng thuật toán SHA-512 chuẩn hóa.
    *   `SecurityUtility.IsPasswordStrong(string password)`: Kiểm tra độ mạnh của mật khẩu (độ dài, ký tự đặc biệt, chữ hoa, số). Trả về tuple `(bool isValid, string message)` giải thích rõ lý do nếu mật khẩu yếu.
    *   `SecurityUtility.TrackFailedAttempt(string ipAddress)`: Trợ lý chống brute-force đăng nhập. Theo dõi số lần đăng nhập sai từ một IP, trả về `(bool shouldBlock, int failedCount)`.

### 3.6. Xử Lý Bitwise Quyền/Tùy Chọn (`BitwiseLibrary.cs`)
*   **Ý tưởng cốt lõi:** Khi cần lưu nhiều tùy chọn boolean (Ví dụ: 10 quyền hạn, hoặc 20 cờ cấu hình hệ thống) vào cơ sở dữ liệu, thay vì tạo 10-20 cột kiểu `Bit` (Boolean) làm phình to DB, hãy nén tất cả vào **một cột duy nhất** chứa chuỗi **Hex** siêu ngắn gọn.
*   **Cách 1: Dùng với Enum [Flags] định sẵn**
    ```csharp
    using BitwiseLibrary;
    
    [Flags]
    public enum EPermission { Read = 1, Write = 2, Delete = 4 }
    
    // 1. Lưu xuống DB: Gộp map lựa chọn từ GUI thành chuỗi Hex
    var guiSelection = new Dictionary<EPermission, bool> { [EPermission.Read] = true, [EPermission.Write] = true };
    string hexToSave = BitwiseLibrary.Standard.ToHex(guiSelection); // Trả về "3"
    
    // 2. Lấy lên BLL/GUI: Parse chuỗi hex ngược lại thành Dictionary hiển thị Checkbox
    var guiMap = BitwiseLibrary.Standard.ParseHexToMap<EPermission>("3");
    
    // 3. Kiểm tra nhanh tại BLL/API: Không cần parse, check bit trực tiếp
    bool canWrite = BitwiseLibrary.Standard.IsSet("3", (long)EPermission.Write); // Trả về true
    ```
*   **Cách 2: Quản lý danh sách Option động/lớn thông qua Class Metadata**
    Khi danh sách option quá lớn hoặc biến động không phù hợp làm Enum, hãy khai báo một class chứa các hằng số index và dùng `OptionBitMapper`:
    ```csharp
    public class UserSettings {
        public const int EnableNotifications = 0; // Bit 0
        public const int DarkMode = 1;            // Bit 1
        public const int ReceivePromoEmails = 2;   // Bit 2
    }
    
    var mapper = new OptionBitMapper<UserSettings>();
    
    // Đọc Hex "5" (101 -> EnableNotifications = true, ReceivePromoEmails = true)
    Dictionary<string, bool> states = mapper.MapToBO("5");
    ```

---

## 🛑 4. Quy Trình 3 Bước Để Tránh Trùng Lặp Code Cho Agent

Mỗi khi nhận nhiệm vụ liên quan đến viết code nghiệp vụ, DTO, Entity, hay Helper, hãy tuân thủ 3 bước nghiêm ngặt sau:

1.  **Bước 1: Quét Từ Khóa (Scan)**
    *   Bạn đang định viết một hàm xử lý chuỗi? -> Tìm trong `StringUtils` của project `MSA.Shared.Utils`.
    *   Bạn đang định tạo một ComboboxDataSource? -> Dùng `SimpleDataSource` trong `MSA.Shared.DataTypes` kết hợp extension `GetSimpleDataSource()` của `EnumUtils`.
    *   Bạn đang định viết Mapper thủ công hoặc cài AutoMapper? -> Dừng lại, dùng extension `.MapTo<T>()` của Mapster tích hợp sẵn trong `MSA.Shared.Utils`.
2.  **Bước 2: Sử Dụng Đúng Namespace**
    *   Luôn nhớ thêm `using MSA.Shared.DataTypes;`, `using MSA.Shared.Utils;`, hoặc `using MSAUtility;` vào đầu file code cần sinh.
3.  **Bước 3: Báo Cáo Sự Tuân Thủ**
    *   Trong phần giải thích code của bạn cho người dùng, hãy ghi chú rõ những class/hàm nào bạn đã sử dụng từ `MSA.Shared` để họ thấy bạn tuân thủ DRY.
    *   *Ví dụ:* *"Tôi đã sử dụng `BOProcessResult` cho kiểu trả về của nghiệp vụ này và lớp `BaseObject` cho Entity của cơ sở dữ liệu để tái sử dụng tối đa mã nguồn chung."*

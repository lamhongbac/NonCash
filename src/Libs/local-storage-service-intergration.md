# Local File Storage Service (Demo Database Replacement)

Thư viện hỗ trợ lưu trữ dữ liệu xuống đĩa cứng dưới dạng file **JSON**, đóng vai trò như một **In-Memory Database** thu nhỏ phục vụ cho mục đích đơn giản hóa việc khai báo CSDL trong giai đoạn **Demo ứng dụng** hoặc **Rapid Prototyping**.

## 🚀 Tính năng nổi bật
* **Zero Configuration:** Không cần cài đặt SQL Server, PostgreSQL hay Docker. Chạy ứng dụng là có ngay DB.
* **In-Memory Performance:** Tự động load dữ liệu lên RAM và thao tác trực tiếp trên Memory (Tốc độ xử lý cực nhanh).
* **Unit of Work (SaveChanges):** Chỉ ghi dữ liệu xuống đĩa cứng khi gọi `SaveChangesAsync()`, giảm thiểu tối đa I/O và bảo vệ tuổi thọ ổ cứng.
* **Thread-Safe:** Tích hợp cơ chế `SemaphoreSlim` cô lập theo từng Entity Type, tránh xung đột ghi file khi xử lý đa luồng.
* **Circular Reference Handling:** Tự động bỏ qua liên kết vòng của Object khi Serialize.

---

## 🛠 Hướng dẫn tích hợp vào dự án

### Bước 1: Định nghĩa Interface cho Service
Để đảm bảo tính đa hình (Polymorphism) và dễ dàng hoán đổi sang CSDL thật (EF Core) sau này, hãy định nghĩa interface `ILocalDataService<T>` tại tầng **Core/Domain**:

```csharp
public interface ILocalDataService<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(Func<T, bool> predicate, T updatedEntity);
    Task DeleteAsync(Func<T, bool> predicate);
    Task SaveChangesAsync();
}
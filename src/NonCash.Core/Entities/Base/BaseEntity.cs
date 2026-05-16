using System;

namespace NonCash.Core.Entities.Base
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public Guid? CreatorId { get; set; }
    }
}

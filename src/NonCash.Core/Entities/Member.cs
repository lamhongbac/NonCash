using System;
using NonCash.Core.Entities.Base;
using NonCash.Shared.Enums;

namespace NonCash.Core.Entities
{
    public class Member : BaseEntity
    {
        public string MemberCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public MemberType Type { get; set; }
    }
}

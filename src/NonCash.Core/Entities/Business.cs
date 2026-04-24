using System;
using NonCash.Core.Entities.Base;

namespace NonCash.Core.Entities
{
    public class Business : BaseEntity
    {
        public string BusinessName { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}

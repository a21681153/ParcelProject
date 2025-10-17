using System.ComponentModel;

namespace ParcelManagement2.Models
{
    public class PackageModel
    {
        [DisplayName("更新")]
        public bool Flag_Update { get; set; }

        [DisplayName("刪除")]
        public bool Flag_Del { get; set; }

        [DisplayName("包裹編號")]
        public string Pack_Id { get; set; } = string.Empty;

        [DisplayName("包裹類別名稱")]
        public string Pack_Name { get; set; } = string.Empty;

        [DisplayName("包裹類別代碼")]
        public string Pack_Type { get; set; } = string.Empty;

        [DisplayName("住戶姓名")]
        public string Red_Name { get; set; } = string.Empty;

        [DisplayName("包裹狀態")]
        public string Status { get; set; } = string.Empty;
    }
}

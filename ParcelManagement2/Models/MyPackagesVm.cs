namespace ParcelManagement2.Models
{
    public class MyPackagesVm
    {
        public string UserName { get; set; } = string.Empty;
        public string Condo_Id { get; set; } = string.Empty;
        public string SelectedResidentId { get; set; } = string.Empty;
        public bool HasMultipleMembers { get; set; }
    }
}

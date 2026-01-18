namespace praca_dyplomowa_zesp.Models.ViewModels
{
    public class ErrorViewModel //model widoku s³u¿¹cy do prezentacji informacji o b³êdach przechwyconych przez system
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
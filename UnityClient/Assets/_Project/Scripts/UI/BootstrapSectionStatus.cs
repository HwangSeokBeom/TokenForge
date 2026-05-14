namespace TokenForge.Client.UI
{
    public readonly struct BootstrapSectionStatus
    {
        public BootstrapSectionStatus(string title, string status, string detail)
        {
            Title = title;
            Status = status;
            Detail = detail;
        }

        public string Title { get; }
        public string Status { get; }
        public string Detail { get; }
    }
}

namespace NodeService.WindowsService.Models
{
    public class App
    {
        public string Name { get; set; }
    }

    public class AppOptions
    {
        public App[] Apps { get; set; } = [];
    }
}

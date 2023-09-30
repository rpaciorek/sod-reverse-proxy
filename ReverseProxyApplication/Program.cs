using Microsoft.AspNetCore.Builder;

namespace ReverseProxyApplication {
    public class Program {
        public static void Main(string[] args) {
            var proxyApp = WebApplication.CreateBuilder(args).Build();
            proxyApp.UseMiddleware<ReverseProxyMiddleware>();
            
            var proxyRun = proxyApp.RunAsync();
            try {
                var clientApp = System.Diagnostics.Process.Start("DOMain.exe");
                clientApp.WaitForExit();
            } catch (System.ComponentModel.Win32Exception) {
                System.Console.WriteLine("Can't run DOMain.exe ...");
                proxyRun.Wait();
            }
        }
    }
}

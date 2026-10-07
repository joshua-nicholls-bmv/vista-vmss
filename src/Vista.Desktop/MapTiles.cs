using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
namespace Vista.Desktop;
public static class MapTiles
{
    public static string UrlTemplate {get;set;}="https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    public static bool Enabled {get;set;}=true;
    public static string CacheDirectory {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VISTA","map-tiles");
    private static readonly HttpClient http=CreateClient();
    private static readonly SemaphoreSlim gate=new(4);
    private static HttpClient CreateClient(){var client=new HttpClient(){Timeout=TimeSpan.FromSeconds(12)};client.DefaultRequestHeaders.UserAgent.ParseAdd("VISTA-Sortie-Application/0.19");return client;}
    public static async Task<BitmapSource?> Load(int z,int x,int y)
    {
        if(!Enabled)return null;
        await gate.WaitAsync();
        try
        {
            var url=UrlTemplate.Replace("{z}",z.ToString()).Replace("{x}",x.ToString()).Replace("{y}",y.ToString());
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")return null;
            var provider=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(UrlTemplate)))[..16];
            var path=Path.Combine(CacheDirectory,provider,$"{z}-{x}-{y}.png");byte[] bytes;
            if(File.Exists(path)&&DateTime.UtcNow-File.GetLastWriteTimeUtc(path)<TimeSpan.FromDays(7))bytes=await File.ReadAllBytesAsync(path);
            else
            {
                using var response=await http.GetAsync(uri);response.EnsureSuccessStatusCode();bytes=await response.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path+".tmp",bytes);File.Move(path+".tmp",path,true);
            }
            using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();return bitmap;
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or NotSupportedException or System.IO.FileFormatException or ArgumentException or InvalidOperationException){return null;}
        finally{gate.Release();}
    }
}

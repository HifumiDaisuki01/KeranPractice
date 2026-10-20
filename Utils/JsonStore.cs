using System.Text;
using System.Text.Json;

namespace KeranPractice;

/// <summary>JSON 读写：原子写入 + 损坏文件自动备份。</summary>
public static class JsonStore
{
    // .NET 的 Encoding.UTF8 带 BOM，部分工具无法解析；统一用无 BOM
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static T Load<T>(string path) where T : new()
    {
        try
        {
            if (!File.Exists(path)) return new T();
            var json = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(json)) return new T();
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (Exception ex)
        {
            // 保留损坏文件，避免直接覆盖导致数据永久丢失
            try
            {
                var bak = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(path, bak, true);
                Console.WriteLine($"[KeranPractice] 读取 {Path.GetFileName(path)} 失败，已备份为 {Path.GetFileName(bak)}: {ex.Message}");
            }
            catch { /* 备份失败也不阻断 */ }
            return new T();
        }
    }

    public static bool Save<T>(string path, T data)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(data, Options);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json, Utf8NoBom);

            if (File.Exists(path))
            {
                // 同分区原子替换
                try { File.Replace(tmp, path, null); }
                catch
                {
                    File.Delete(path);
                    File.Move(tmp, path);
                }
            }
            else
            {
                File.Move(tmp, path);
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[KeranPractice] 写入 {path} 失败: {ex.Message}");
            return false;
        }
    }
}

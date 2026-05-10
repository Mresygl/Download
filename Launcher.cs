using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Text;

namespace PortableMCLauncher
{
    class Program
    {
        const string VersionName = "1.21.11-Fabric_0.19.2";
        const string FolderName = ".minecraft-portable-1211";
        
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, FolderName);
                string exePath = Assembly.GetExecutingAssembly().Location;
                
                // 强制重新解压用于测试
                if (Directory.Exists(targetDir)) {
                    try { Directory.Delete(targetDir, true); } catch {}
                }
                Directory.CreateDirectory(targetDir);
                
                ExtractBundle(exePath, targetDir);

                string jrePath = Path.Combine(targetDir, "jre", "bin", "javaw.exe");
                if (!File.Exists(jrePath)) {
                    MessageBox.Show("解压后未找到 Java: " + jrePath);
                    return;
                }

                MessageBox.Show("解压成功！正在启动...");

                // 启动逻辑...
                string gameDir = targetDir;
                string assetsDir = Path.Combine(targetDir, "assets");
                var libs = Directory.GetFiles(Path.Combine(targetDir, "libraries"), "*.jar", SearchOption.AllDirectories);
                var versionJar = Path.Combine(targetDir, "versions", VersionName, VersionName + ".jar");
                string classpath = string.Join(";", libs) + ";" + versionJar;

                string launchArgs = string.Format(
                    "-Xmx2G -Djava.library.path=\"{0}\" -cp \"{1}\" " +
                    "net.fabricmc.loader.impl.launch.knot.KnotClient " +
                    "--username \"Player\" --version \"{2}\" --gameDir \"{3}\" --assetsDir \"{4}\" " +
                    "--assetIndex \"17\" --uuid \"0\" --accessToken \"0\" --userType \"msa\" --versionType \"release\"",
                    Path.Combine(targetDir, "natives"),
                    classpath,
                    VersionName,
                    gameDir,
                    assetsDir
                );

                ProcessStartInfo si = new ProcessStartInfo(jrePath, launchArgs);
                si.WorkingDirectory = targetDir;
                si.UseShellExecute = false;
                Process.Start(si);
            }
            catch (Exception ex)
            {
                // 输出详细错误到文件
                File.WriteAllText("crash.log", ex.ToString());
                MessageBox.Show("启动发生错误: " + ex.Message + "\n详情已记录到 crash.log", "错误");
            }
        }

        static void ExtractBundle(string exePath, string targetDir)
        {
            long zipOffset = FindZipOffset(exePath);
            if (zipOffset == -1) throw new Exception("无法在 EXE 中找到嵌入的游戏数据 (PK0304 signature missing)。");

            using (FileStream fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (SubStream zipStream = new SubStream(fs, zipOffset, fs.Length - zipOffset))
                {
                    using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string destinationPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                            if (destinationPath.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrEmpty(entry.Name)) 
                                {
                                    Directory.CreateDirectory(destinationPath);
                                }
                                else
                                {
                                    string dir = Path.GetDirectoryName(destinationPath);
                                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                                    entry.ExtractToFile(destinationPath, true);
                                }
                            }
                        }
                    }
                }
            }
        }

        static long FindZipOffset(string path)
        {
            byte[] signature = { 0x50, 0x4B, 0x03, 0x04 };
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i <= bytesRead - 4; i++)
                    {
                        if (buffer[i] == signature[0] && buffer[i+1] == signature[1] && 
                            buffer[i+2] == signature[2] && buffer[i+3] == signature[3])
                        {
                            return fs.Position - bytesRead + i;
                        }
                    }
                    if (fs.Position < fs.Length) {
                        fs.Position -= 3;
                    }
                }
            }
            return -1;
        }
    }

    public class SubStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _offset;
        private readonly long _length;
        private long _position;

        public SubStream(Stream baseStream, long offset, long length)
        {
            _baseStream = baseStream;
            _offset = offset;
            _length = length;
            _position = 0;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return _length; } }
        public override long Position { get { return _position; } set { _position = value; } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = 0;
            switch (origin)
            {
                case SeekOrigin.Begin: newPos = offset; break;
                case SeekOrigin.Current: newPos = _position + offset; break;
                case SeekOrigin.End: newPos = _length + offset; break;
            }
            _position = newPos;
            return _position;
        }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length) return 0;
            long remaining = _length - _position;
            if (count > (int)remaining) count = (int)remaining;
            _baseStream.Position = _offset + _position;
            int read = _baseStream.Read(buffer, offset, count);
            _position += read;
            return read;
        }
    }
}

# Minecraft 1.21.11 便携版打包脚本 (完美最终版)
Add-Type -AssemblyName System.IO.Compression.FileSystem

$BuildDir = "Build"
$BundleDir = "$BuildDir\Bundle"
$OutputFile = "PortableMC_1.21.11.exe"
$JreUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_x64_windows_hotspot_21.0.6_7.zip"

# 1. 初始化
Write-Host ">>> Initializing..." -ForegroundColor Cyan
if (Test-Path $BuildDir) { Remove-Item $BuildDir -Recurse -Force }
New-Item -ItemType Directory -Path $BundleDir

# 2. 准备文件 (MC 文件夹)
Write-Host ">>> Copying game files..." -ForegroundColor Cyan
# 确保存档 (saves) 被包含
if (!(Test-Path "MC\saves")) { New-Item -ItemType Directory -Path "MC\saves" }
Copy-Item -Path "MC\*" -Destination $BundleDir -Recurse -Force

Write-Host ">>> Getting Java 21..." -ForegroundColor Cyan
if (!(Test-Path "jre.zip")) {
    Invoke-WebRequest -Uri $JreUrl -OutFile "jre.zip"
}
[System.IO.Compression.ZipFile]::ExtractToDirectory("jre.zip", "$BuildDir\jre_temp")
$jreFolder = Get-ChildItem "$BuildDir\jre_temp" | Select-Object -First 1
Move-Item $jreFolder.FullName "$BundleDir\jre"
Remove-Item "$BuildDir\jre_temp" -Recurse

# 3. 生成数据包
Write-Host ">>> Creating data.zip..." -ForegroundColor Cyan
[System.IO.Compression.ZipFile]::CreateFromDirectory($BundleDir, "$BuildDir\data.zip", [System.IO.Compression.CompressionLevel]::Optimal, $false)

# 4. 编写启动器源码
$vbcCode = @"
using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;

[assembly: AssemblyTitle("Minecraft Portable")]
[assembly: AssemblyProduct("Minecraft Portable")]
[assembly: AssemblyCompany("MresyAB")]
[assembly: AssemblyCopyright("Copyright © MresyAB 2026")]

public class Program {
    [STAThread]
    public static void Main() {
        try {
            string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft-portable-1211");
            
            // 只有当核心文件不存在时才解压，保留存档 (saves) 和配置 (options.txt)
            if (!Directory.Exists(targetDir) || !File.Exists(Path.Combine(targetDir, "jre", "bin", "javaw.exe"))) {
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("data.zip"))
                using (ZipArchive archive = new ZipArchive(s)) {
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        string path = Path.Combine(targetDir, entry.FullName);
                        if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(path);
                        else {
                            // 如果是存档或配置文件，且目标已存在，则跳过，防止覆盖用户的存档
                            if (File.Exists(path) && (path.Contains("\\saves\\") || path.EndsWith("options.txt"))) continue;
                            
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            entry.ExtractToFile(path, true);
                        }
                    }
                }
            }
            
            string jre = Path.Combine(targetDir, "jre", "bin", "javaw.exe");
            string libs = string.Join(";", Directory.GetFiles(Path.Combine(targetDir, "libraries"), "*.jar", SearchOption.AllDirectories));
            string verJar = Path.Combine(targetDir, "versions", "1.21.11-Fabric_0.19.2", "1.21.11-Fabric_0.19.2.jar");
            
            string assetIndex = "29";
            if (File.Exists(Path.Combine(targetDir, "assets", "indexes", "17.json"))) assetIndex = "17";

            string args = string.Format("-Xmx2G -Djava.library.path=\"{0}\" -cp \"{1};{2}\" net.fabricmc.loader.impl.launch.knot.KnotClient --username Player --version 1.21.11-Fabric_0.19.2 --gameDir \"{3}\" --assetsDir \"{4}\" --assetIndex {5}", 
                Path.Combine(targetDir, "natives"), libs, verJar, targetDir, Path.Combine(targetDir, "assets"), assetIndex);
            
            ProcessStartInfo si = new ProcessStartInfo(jre, args);
            si.WorkingDirectory = targetDir;
            si.UseShellExecute = false;
            Process.Start(si);
        } catch (Exception ex) { MessageBox.Show(ex.ToString()); }
    }
}
"@
[System.IO.File]::WriteAllText("$BuildDir\Runner.cs", $vbcCode)

# 5. 编译并添加图标
Write-Host ">>> Compiling with custom icon..." -ForegroundColor Cyan
$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$IconArg = ""
if (Test-Path "mc.ico") { $IconArg = "/win32icon:mc.ico" }

& $CscPath /target:winexe /out:$OutputFile $IconArg /res:"$BuildDir\data.zip",data.zip /reference:System.Windows.Forms.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll "$BuildDir\Runner.cs"

# 6. 应用数字签名
Write-Host ">>> Applying Digital Signature (MresyAB)..." -ForegroundColor Cyan
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*CN=MresyAB*" } | Select-Object -First 1
if ($cert) {
    Set-AuthenticodeSignature -FilePath $OutputFile -Certificate $cert
    Write-Host ">>> Signed successfully!" -ForegroundColor Green
} else {
    Write-Host ">>> Certificate not found, skip signing." -ForegroundColor Yellow
}

Write-Host ">>> All Done! PortableMC_1.21.11.exe is ready." -ForegroundColor Green

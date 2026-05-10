using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public class IconConverter {
    public static void Main(string[] args) {
        if (args.Length < 2) return;
        using (Bitmap bitmap = (Bitmap)Image.FromFile(args[0])) {
            using (Bitmap square = new Bitmap(256, 256)) {
                using (Graphics g = Graphics.FromImage(square)) {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bitmap, 0, 0, 256, 256);
                }
                using (FileStream fs = new FileStream(args[1], FileMode.Create)) {
                    BinaryWriter bw = new BinaryWriter(fs);
                    bw.Write((short)0);
                    bw.Write((short)1);
                    bw.Write((short)1);
                    bw.Write((byte)0); // 0 means 256
                    bw.Write((byte)0); // 0 means 256
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    
                    using (MemoryStream ms = new MemoryStream()) {
                        square.Save(ms, ImageFormat.Png);
                        byte[] pngData = ms.ToArray();
                        bw.Write((int)pngData.Length);
                        bw.Write((int)22);
                        bw.Write(pngData);
                    }
                }
            }
        }
    }
}

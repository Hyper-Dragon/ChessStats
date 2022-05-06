using Microsoft.Extensions.FileProviders;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using VectSharp.SVG;

namespace ChessStats.Helpers
{
    internal class Imaging
    {
        public static string EncodeResourceImageAsHtmlFragment(string imageName)
        {
            string base64Img = "";

            using (Stream reader = new EmbeddedFileProvider(Assembly.GetExecutingAssembly()).GetFileInfo($"Images.{imageName}").CreateReadStream())
            {
                //read all to byte[]
                byte[] bytes = new byte[reader.Length];
                _ = reader.Read(bytes, 0, (int)reader.Length);

                base64Img = Convert.ToBase64String(bytes.ToArray());
            }

            return $"'data:image/png;base64,{base64Img}'";
        }

        public static string GetImageAsHtmlFragment(VectSharp.Page pageOut)
        {
            if (pageOut == null) { throw new ArgumentNullException(nameof(pageOut)); }

            using MemoryStream stream = new();
            pageOut.SaveAsSVG(stream);
            string base64Img = Convert.ToBase64String(stream.ToArray());

            return $"<img src='data:image/svg+xml;base64,{base64Img}'/>";
        }
    }
}

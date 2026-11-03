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
        /// <summary>
        /// Returns an embedded image as a quoted HTML attribute value, e.g. 'data:image/png;base64,...'.
        /// Use <see cref="GetResourceBase64"/> when you need the raw base64 (any media type, no quotes).
        /// </summary>
        public static string EncodeResourceImageAsHtmlFragment(string imageName)
        {
            return $"'data:image/png;base64,{GetResourceBase64(imageName)}'";
        }

        /// <summary>
        /// Returns the raw base64 of an embedded resource under the Images folder.
        /// Unlike <see cref="EncodeResourceImageAsHtmlFragment"/> this adds no wrapper,
        /// so it can be used for data URIs of any media type (png, woff2, ...).
        /// </summary>
        public static string GetResourceBase64(string resourceName)
        {
            using Stream reader = new EmbeddedFileProvider(Assembly.GetExecutingAssembly()).GetFileInfo($"Images.{resourceName}").CreateReadStream();
            using MemoryStream buffer = new();

            reader.CopyTo(buffer);

            return Convert.ToBase64String(buffer.ToArray());
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

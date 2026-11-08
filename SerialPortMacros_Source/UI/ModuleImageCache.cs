using System.Drawing.Imaging;

namespace SerialPortMacros
{
    /// <summary>
    /// Images of the interface modules, read from disk only once.
    ///
    /// The images handed out are shared between elements: never modify or dispose them.
    /// Use <see cref="Owns"/> to tell a shared image from one a control owns (e.g. a
    /// rotated copy or a fallback image), which must be disposed when replaced.
    /// </summary>
    internal sealed class ModuleImageCache : IDisposable
    {
        private readonly Dictionary<string, Bitmap?> _byPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Image, Bitmap> _pressed = new();
        private readonly HashSet<Image> _all = new();

        /// <summary>Image at <paramref name="path"/>, or null if missing or unreadable.</summary>
        public Bitmap? Get(string path)
        {
            if (_byPath.TryGetValue(path, out var cached))
                return cached;

            Bitmap? image = null;
            try
            {
                if (File.Exists(path))
                {
                    // Copy into memory so the file is not locked.
                    using var loaded = Image.FromFile(path);
                    image = new Bitmap(loaded);
                    _all.Add(image);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cannot load image {path}: {ex.Message}");
            }

            _byPath[path] = image;
            return image;
        }

        /// <summary>
        /// Darkened ("pressed") version of <paramref name="source"/>. Computed once for
        /// shared images; for images owned by a control a new image is returned.
        /// </summary>
        public Image GetPressed(Image source)
        {
            if (!Owns(source))
                return Darken(source);

            if (!_pressed.TryGetValue(source, out var pressed))
            {
                pressed = Darken(source);
                _pressed[source] = pressed;
                _all.Add(pressed);
            }
            return pressed;
        }

        /// <summary>True if the image belongs to the cache (shared: do not dispose it).</summary>
        public bool Owns(Image image) => _all.Contains(image);

        /// <summary>Subtracts 60 from each RGB channel (clamped at 0), keeping alpha.</summary>
        private static Bitmap Darken(Image source)
        {
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            const float d = -60f / 255f;

            var matrix = new ColorMatrix(new[]
            {
                new float[] { 1, 0, 0, 0, 0 },
                new float[] { 0, 1, 0, 0, 0 },
                new float[] { 0, 0, 1, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { d, d, d, 0, 1 }
            });

            using var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);

            using var g = Graphics.FromImage(result);
            g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
                0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);

            return result;
        }

        public void Dispose()
        {
            foreach (var image in _all)
                image.Dispose();
            _all.Clear();
            _byPath.Clear();
            _pressed.Clear();
        }
    }
}

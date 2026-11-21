using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DeskDecor;

internal static class ShellIconCacheRegression
{
    private static string _root;
    private static int _caseNumber;
    private static int _passed;
    private static int _failed;

    private static void Expect(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private static string FixturePath(string name)
    {
        string folder = Path.Combine(_root, (++_caseNumber).ToString());
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, name);
    }

    private static Bitmap Solid(Color color)
    {
        var image = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(image)) graphics.Clear(color);
        return image;
    }

    private static Color FixtureColor(string path)
    {
        if (Directory.Exists(path)) return Color.Orange;
        if (!File.Exists(path)) return Color.Gray;
        string value = File.ReadAllText(path);
        if (value.StartsWith("a", StringComparison.Ordinal)) return Color.Red;
        if (value.StartsWith("g", StringComparison.Ordinal)) return Color.Green;
        return Color.Blue;
    }

    private sealed class Loader
    {
        private readonly Dictionary<string, int> _calls =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public Bitmap Load(string path)
        {
            int calls;
            _calls.TryGetValue(path, out calls);
            _calls[path] = calls + 1;
            return Solid(FixtureColor(path));
        }

        public int Calls(string path)
        {
            int calls;
            return _calls.TryGetValue(path, out calls) ? calls : 0;
        }
    }

    private static void ExpectColor(Bitmap image, Color expected, string message)
    { Expect(image.GetPixel(image.Width / 2, image.Height / 2).ToArgb() == expected.ToArgb(), message); }

    private static void ExpectDisposed(Bitmap image, string message)
    {
        bool disposed = false;
        try { image.GetPixel(0, 0); }
        catch (ArgumentException) { disposed = true; }
        catch (ObjectDisposedException) { disposed = true; }
        Expect(disposed, message);
    }

    private static void Run(string title, Action test)
    {
        try { test(); _passed++; Console.WriteLine("PASS: " + title); }
        catch (Exception error) { _failed++; Console.Error.WriteLine("FAIL: " + title + " :: " + error.Message); }
    }

    private static void NativePixels()
    {
        string path = FixturePath("icon.lnk");
        File.WriteAllText(path, "a");
        using (var cache = new ShellIconCache(delegate(string ignored)
        {
            var image = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            image.SetPixel(1, 1, Color.FromArgb(255, 35, 97, 151));
            image.SetPixel(2, 1, Color.FromArgb(96, 0, 0, 0));
            return image;
        }))
        {
            Bitmap rendered = cache.GetScaled(path, 4);
            Expect(rendered.Width == 4 && rendered.Height == 4, "Display icons must have exactly the requested dimensions, without outline padding");
            Expect(rendered.GetPixel(1, 1).ToArgb() == Color.FromArgb(255, 35, 97, 151).ToArgb(), "Native opaque colours must remain unchanged");
            Expect(rendered.GetPixel(2, 1).A == 96, "Native partial transparency must remain unchanged");
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    if (!(y == 1 && (x == 1 || x == 2)))
                        Expect(rendered.GetPixel(x, y).A == 0, "Transparent pixels must not acquire a dark outline or shadow");
            Bitmap larger = cache.GetScaled(path, 8);
            Expect(larger.Width == 8 && larger.Height == 8, "Scaled variants must also retain the requested dimensions");
        }
    }

    private static void CacheReuse()
    {
        string path = FixturePath("cached.lnk");
        File.WriteAllText(path, "a");
        var loader = new Loader();
        using (var cache = new ShellIconCache(loader.Load))
        {
            Bitmap raw = cache.Get(path);
            Bitmap small = cache.GetScaled(path, 4);
            Bitmap large = cache.GetScaled(path, 8);
            Expect(ReferenceEquals(raw, cache.Get(path.ToUpperInvariant())), "Raw icon cache keys must ignore path case");
            Expect(ReferenceEquals(small, cache.GetScaled(path.ToUpperInvariant(), 4)), "Scaled icon cache keys must ignore path case");
            Expect(ReferenceEquals(large, cache.GetScaled(path, 8)), "Repeated paints must reuse the same size variant");
            Expect(loader.Calls(path) == 1, "Requesting multiple sizes must load the native source only once");
            Expect(!cache.RefreshChanged() && !cache.RefreshChanged(), "Stable files must not trigger redraw invalidation");
            Expect(loader.Calls(path) == 1, "Polling stable metadata must not re-extract the icon");
        }
    }

    private static void TimestampInvalidation()
    {
        string path = FixturePath("changed.lnk");
        string other = Path.Combine(Path.GetDirectoryName(path), "changed.lnk.other");
        File.WriteAllText(path, "a");
        File.WriteAllText(other, "g");
        DateTime oldTime = File.GetLastWriteTimeUtc(path);
        var loader = new Loader();
        using (var cache = new ShellIconCache(loader.Load))
        {
            Bitmap raw = cache.Get(path);
            Bitmap small = cache.GetScaled(path, 4);
            Bitmap large = cache.GetScaled(path, 8);
            Bitmap unrelated = cache.GetScaled(other, 4);
            File.WriteAllText(path, "b");
            File.SetLastWriteTimeUtc(path, oldTime.AddSeconds(10));
            Expect(cache.RefreshChanged(), "A same-length shortcut edit must invalidate its cached icon by timestamp");
            ExpectDisposed(raw, "The invalidated raw image must be released");
            ExpectDisposed(small, "The invalidated small variant must be released");
            ExpectDisposed(large, "Every invalidated size variant must be released");
            Expect(ReferenceEquals(unrelated, cache.GetScaled(other, 4)), "Invalidating one exact path must preserve an unrelated path with the same prefix");
            Bitmap replacement = cache.GetScaled(path, 4);
            ExpectColor(replacement, Color.Blue, "The next paint must use the changed shortcut's new source pixels");
            Expect(!ReferenceEquals(small, replacement) && !ReferenceEquals(large, cache.GetScaled(path, 8)), "Changed shortcuts must not reuse any stale scaled variant");
            Expect(loader.Calls(path) == 2 && loader.Calls(other) == 1, "Only the changed native icon must reload, once for all sizes");
            Expect(!cache.RefreshChanged(), "A newly loaded, unchanged shortcut must settle without repeated invalidations");
        }
    }

    private static void LengthInvalidation()
    {
        string path = FixturePath("length.lnk");
        File.WriteAllText(path, "a");
        DateTime oldTime = File.GetLastWriteTimeUtc(path);
        var loader = new Loader();
        using (var cache = new ShellIconCache(loader.Load))
        {
            cache.GetScaled(path, 4);
            File.WriteAllText(path, "gggg");
            File.SetLastWriteTimeUtc(path, oldTime);
            Expect(cache.RefreshChanged(), "A file length change must invalidate an icon even when its timestamp is retained");
            ExpectColor(cache.GetScaled(path, 4), Color.Green, "A length-only change must load the new icon pixels");
            Expect(loader.Calls(path) == 2, "A changed file must reload once");
        }
    }

    private static void MissingPaths()
    {
        string path = FixturePath("appears.lnk");
        var loader = new Loader();
        using (var cache = new ShellIconCache(loader.Load))
        {
            Bitmap missing = cache.GetScaled(path, 4);
            ExpectColor(missing, Color.Gray, "A missing fixture must use the fake placeholder");
            Expect(!cache.RefreshChanged(), "A still-missing file must not invalidate repeatedly");
            File.WriteAllText(path, "b");
            Expect(cache.RefreshChanged(), "A formerly missing shortcut must invalidate the cached placeholder when created");
            ExpectDisposed(missing, "Replacing a placeholder must release its scaled bitmap");
            Bitmap present = cache.GetScaled(path, 4);
            ExpectColor(present, Color.Blue, "A created shortcut must show its actual icon instead of the stale placeholder");
            File.Delete(path);
            Expect(cache.RefreshChanged(), "Deleting a cached shortcut must invalidate its former image");
            ExpectDisposed(present, "A disappeared shortcut's old bitmap must be released");
            ExpectColor(cache.GetScaled(path, 4), Color.Gray, "A disappeared shortcut must no longer display its old source pixels");
            Expect(!cache.RefreshChanged() && loader.Calls(path) == 3, "Missing/create/delete transitions must settle without repeated reloads");
        }
    }

    private static void FileDirectoryTransitions()
    {
        string path = FixturePath("kind");
        File.WriteAllText(path, string.Empty);
        DateTime oldTime = File.GetLastWriteTimeUtc(path);
        var loader = new Loader();
        using (var cache = new ShellIconCache(loader.Load))
        {
            ExpectColor(cache.GetScaled(path, 4), Color.Blue, "The initial zero-length file must load as a file");
            File.Delete(path);
            Directory.CreateDirectory(path);
            Directory.SetLastWriteTimeUtc(path, oldTime);
            Expect(cache.RefreshChanged(), "Changing a zero-length file into a same-timestamp directory must invalidate its icon");
            ExpectColor(cache.GetScaled(path, 4), Color.Orange, "The directory transition must display directory pixels");
            Directory.Delete(path);
            File.WriteAllText(path, string.Empty);
            File.SetLastWriteTimeUtc(path, oldTime);
            Expect(cache.RefreshChanged(), "Changing a directory back into a same-timestamp zero-length file must invalidate its icon");
            ExpectColor(cache.GetScaled(path, 4), Color.Blue, "The file transition must not retain the directory pixels");
            Expect(loader.Calls(path) == 3 && !cache.RefreshChanged(), "Type transitions must reload once per changed state");
        }
    }

    private static void Disposal()
    {
        string path = FixturePath("disposed.lnk");
        File.WriteAllText(path, "a");
        var cache = new ShellIconCache(new Loader().Load);
        Bitmap raw = cache.Get(path);
        Bitmap small = cache.GetScaled(path, 4);
        Bitmap large = cache.GetScaled(path, 8);
        cache.Dispose();
        ExpectDisposed(raw, "Disposing the cache must release its native source image");
        ExpectDisposed(small, "Disposing the cache must release its small image");
        ExpectDisposed(large, "Disposing the cache must release its large image");
    }

    [STAThread]
    public static int Main()
    {
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        _root = Path.Combine(parent, "DeskDecor-shell-icons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        try
        {
            Run("native icon colours and transparency remain unoutlined at the requested size", NativePixels);
            Run("case-insensitive raw and per-size caches avoid repeated icon extraction", CacheReuse);
            Run("shortcut timestamp changes release all old sizes and retain unrelated icon caches", TimestampInvalidation);
            Run("same-timestamp file length changes reload the native icon", LengthInvalidation);
            Run("missing, created and removed shortcuts never retain stale placeholder or source images", MissingPaths);
            Run("same-timestamp file and directory transitions refresh the displayed icon", FileDirectoryTransitions);
            Run("disposing the icon cache releases native and scaled bitmap resources", Disposal);
            Console.WriteLine("RESULT: " + _passed + "/" + (_passed + _failed) + " icon-cache checks passed; fake image loader and temporary files only");
            return _failed == 0 ? 0 : 1;
        }
        finally
        {
            string resolved = Path.GetFullPath(_root);
            if (!string.Equals(Path.GetDirectoryName(resolved), parent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("DeskDecor-shell-icons-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected icon-test cleanup path");
            Directory.Delete(resolved, true);
        }
    }
}
